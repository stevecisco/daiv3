namespace Daiv3.FoundryLocal.Management;

using Daiv3.Infrastructure.Shared.Hardware;
using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.Logging;

public sealed class FoundryLocalManagementService : IAsyncDisposable
{
    private readonly ILogger _logger;
    private readonly ServiceCatalogClient _serviceCatalogClient = new();
    private readonly SemaphoreSlim _modelLifecycleLock = new(1, 1);
    private bool _initialized;
    private bool _ownsManager;
    private FoundryLocalManager? _manager;
    private IDisposable? _loadedModelManager;
    private string? _loadedModelId;

    public FoundryLocalManagementService(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InitializeAsync(FoundryLocalOptions options, CancellationToken ct = default)
    {
        if (_initialized)
        {
            return;
        }

        if (FoundryLocalManager.IsInitialized)
        {
            _initialized = true;
            _ownsManager = false;
            _manager = FoundryLocalManager.Instance;
            return;
        }

        if (string.IsNullOrWhiteSpace(options.AppName))
        {
            throw new ArgumentException("AppName must be set.", nameof(options));
        }

        var cacheDir = options.ModelCacheDir;
        if (string.IsNullOrWhiteSpace(cacheDir))
        {
            cacheDir = await _serviceCatalogClient.GetCacheDirectoryAsync(ct).ConfigureAwait(false);
        }

        var config = new Configuration
        {
            AppName = options.AppName,
            LogLevel = options.LogLevel,
            AppDataDir = options.AppDataDir,
            ModelCacheDir = cacheDir
        };

        await FoundryLocalManager.CreateAsync(config, _logger, ct).ConfigureAwait(false);
        _manager = FoundryLocalManager.Instance;
        _ownsManager = true;

        if (options.EnsureExecutionProviders)
        {
            await FoundryLocalManager.Instance.EnsureEpsDownloadedAsync(ct).ConfigureAwait(false);
        }

        _initialized = true;
    }

    public async Task<IReadOnlyList<ModelCatalogEntry>> ListAvailableModelsAsync(CancellationToken ct = default)
    {
        EnsureInitialized();

        var models = await _serviceCatalogClient.ListModelsAsync(ct).ConfigureAwait(false);
        return models
            .GroupBy(m => m.Alias, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ModelCatalogEntry(
                group.Key,
                group.Select(m => m.DisplayName).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)),
                group
                    .OrderBy(m => m.ModelId, StringComparer.OrdinalIgnoreCase)
                    .Select(MapServiceVariant)
                    .ToList()))
            .ToList();
    }

    public async Task<IReadOnlyList<ModelVariantEntry>> ListCachedModelsAsync(CancellationToken ct = default)
    {
        EnsureInitialized();

        var cachedIds = await _serviceCatalogClient.GetCachedModelIdsAsync(ct).ConfigureAwait(false);
        var allModels = await _serviceCatalogClient.ListModelsAsync(ct).ConfigureAwait(false);

        // Build a set of cached model names for quick lookup
        var cachedSet = new HashSet<string>(cachedIds, StringComparer.OrdinalIgnoreCase);

        // Match catalog models by constructing their cached name format
        var matches = new List<ModelVariantEntry>();
        foreach (var model in allModels)
        {
            var cachedName = ConstructCachedModelName(model);
            if (!string.IsNullOrEmpty(cachedName) && cachedSet.Contains(cachedName))
            {
                matches.Add(MapServiceVariant(model));
            }
        }

        matches = matches
            .OrderBy(m => m.Alias, StringComparer.OrdinalIgnoreCase)
            .ThenBy(m => m.Version)
            .ToList();

        return matches;
    }

    private static string? ConstructCachedModelName(ServiceModelInfo model)
    {
        // The cached directory name matches the model ID without the version suffix
        // e.g., "Phi-4-mini-instruct-generic-cpu:5" -> "Phi-4-mini-instruct-generic-cpu"
        var modelId = model.ModelId;

        if (string.IsNullOrEmpty(modelId))
        {
            return null;
        }

        // Strip version suffix (":1", ":2", etc.)
        var colonIndex = modelId.IndexOf(':');
        var cachedName = colonIndex > 0 ? modelId.Substring(0, colonIndex) : modelId;

        return cachedName.ToLowerInvariant();
    }

    public async Task<ModelVariantEntry> DownloadModelAsync(
        string aliasOrId,
        int? version = null,
        DeviceType? device = null,
        IProgress<float>? progress = null,
        CancellationToken ct = default)
    {
        EnsureInitialized();

        var serviceModel = await ResolveServiceModelAsync(aliasOrId, version, device, ct).ConfigureAwait(false);
        await _serviceCatalogClient.DownloadModelAsync(serviceModel, progress, ct).ConfigureAwait(false);
        return MapServiceVariant(serviceModel);
    }

    public async Task<int> DeleteCachedModelsAsync(
        string aliasOrId,
        int? version = null,
        DeviceType? device = null,
        CancellationToken ct = default)
    {
        EnsureInitialized();

        var cachedIds = await _serviceCatalogClient.GetCachedModelIdsAsync(ct).ConfigureAwait(false);
        var cachedSet = new HashSet<string>(cachedIds, StringComparer.OrdinalIgnoreCase);
        var allModels = await _serviceCatalogClient.ListModelsAsync(ct).ConfigureAwait(false);

        var candidates = allModels
            .Where(m => LooksLikeModelId(aliasOrId)
                ? m.ModelId.Equals(aliasOrId, StringComparison.OrdinalIgnoreCase)
                : m.Alias.Equals(aliasOrId, StringComparison.OrdinalIgnoreCase))
            .Where(m => version == null || ParseVersion(m.ModelId) == version.Value)
            .Where(m => device == null || ParseDeviceType(m.Runtime?.DeviceType) == device.Value)
            .ToList();

        if (candidates.Count == 0)
        {
            return 0;
        }

        int deletedCount = 0;
        foreach (var model in candidates)
        {
            var cachedName = ConstructCachedModelName(model);
            if (!string.IsNullOrEmpty(cachedName) && cachedSet.Contains(cachedName))
            {
                if (await _serviceCatalogClient.DeleteModelAsync(cachedName, ct).ConfigureAwait(false))
                {
                    deletedCount++;
                }
            }
        }

        return deletedCount;
    }

    public async Task<bool> IsModelAvailableAsync(string aliasOrId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(aliasOrId);
        EnsureInitialized();

        var models = await _serviceCatalogClient.ListModelsAsync(ct).ConfigureAwait(false);
        return models.Any(m =>
            m.ModelId.Equals(aliasOrId, StringComparison.OrdinalIgnoreCase) ||
            m.Alias.Equals(aliasOrId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task LoadModelAsync(string aliasOrId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(aliasOrId);
        EnsureInitialized();

        await _modelLifecycleLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!string.IsNullOrWhiteSpace(_loadedModelId) &&
                _loadedModelId.Equals(aliasOrId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _loadedModelManager?.Dispose();
            _loadedModelManager = null;

            var managerType = typeof(FoundryLocalManager);
            var startMethod = managerType.GetMethod("StartModelAsync", new[] { typeof(string) });
            if (startMethod != null)
            {
                var startTask = (Task?)startMethod.Invoke(null, [aliasOrId]);
                if (startTask == null)
                {
                    throw new InvalidOperationException("Foundry Local SDK returned null task from StartModelAsync.");
                }

                await startTask.ConfigureAwait(false);

                var resultProperty = startTask.GetType().GetProperty("Result");
                if (resultProperty?.GetValue(startTask) is IDisposable disposable)
                {
                    _loadedModelManager = disposable;
                }
            }

            _loadedModelId = aliasOrId;
            _logger.LogInformation("Foundry Local model loaded: {ModelId}", _loadedModelId);
        }
        finally
        {
            _modelLifecycleLock.Release();
        }
    }

    public async Task UnloadModelAsync(CancellationToken ct = default)
    {
        EnsureInitialized();

        await _modelLifecycleLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var modelId = _loadedModelId;

            var managerType = typeof(FoundryLocalManager);
            var stopWithModel = managerType.GetMethod("StopModelAsync", new[] { typeof(string) });
            if (stopWithModel != null && !string.IsNullOrWhiteSpace(modelId))
            {
                var stopTask = (Task?)stopWithModel.Invoke(null, [modelId]);
                if (stopTask != null)
                {
                    await stopTask.ConfigureAwait(false);
                }
            }
            else
            {
                var stopWithoutArgs = managerType.GetMethod("StopModelAsync", Type.EmptyTypes);
                if (stopWithoutArgs != null)
                {
                    var stopTask = (Task?)stopWithoutArgs.Invoke(null, null);
                    if (stopTask != null)
                    {
                        await stopTask.ConfigureAwait(false);
                    }
                }
            }

            _loadedModelManager?.Dispose();
            _loadedModelManager = null;
            _loadedModelId = null;

            _logger.LogInformation("Foundry Local model unloaded: {ModelId}", modelId);
        }
        finally
        {
            _modelLifecycleLock.Release();
        }
    }

    public Task<string?> GetLoadedModelAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_loadedModelId);
    }

    public async ValueTask DisposeAsync()
    {
        _loadedModelManager?.Dispose();
        _loadedModelManager = null;
        _loadedModelId = null;

        if (_ownsManager && _manager != null)
        {
#pragma warning disable IDISP007 // Don't dispose injected - we only dispose if we own it
            _manager.Dispose();
#pragma warning restore IDISP007
            _manager = null;
        }

        _initialized = false;
        _ownsManager = false;
        _modelLifecycleLock.Dispose();
        await _serviceCatalogClient.DisposeAsync().ConfigureAwait(false);
    }

    private static ModelVariantEntry MapVariant(ModelVariant variant, bool cached)
    {
        var runtime = variant.Info.Runtime;
        var deviceType = runtime?.DeviceType ?? DeviceType.Invalid;
        var executionProvider = runtime?.ExecutionProvider ?? "unknown";

        return new ModelVariantEntry(
            variant.Id,
            variant.Alias,
            variant.Version,
            deviceType,
            executionProvider,
            cached,
            variant.Info.FileSizeMb);
    }

    private static ModelVariantEntry MapServiceVariant(ServiceModelInfo model)
    {
        var runtime = model.Runtime;
        var deviceType = ParseDeviceType(runtime?.DeviceType);
        var executionProvider = runtime?.ExecutionProvider ?? "unknown";
        var version = ParseVersion(model.ModelId);

        return new ModelVariantEntry(
            model.ModelId,
            model.Alias,
            version,
            deviceType,
            executionProvider,
            false,
            model.FileSizeMb == null ? null : (int?)Math.Min(model.FileSizeMb.Value, int.MaxValue));
    }

    private static DeviceType ParseDeviceType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DeviceType.Invalid;
        }

        return value.Trim().ToUpperInvariant() switch
        {
            "CPU" => DeviceType.CPU,
            "GPU" => DeviceType.GPU,
            "NPU" => DeviceType.NPU,
            _ => DeviceType.Invalid
        };
    }

    private static int ParseVersion(string modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return 0;
        }

        var parts = modelId.Split(':');
        if (parts.Length == 0)
        {
            return 0;
        }

        var last = parts[^1];
        return int.TryParse(last, out var version) ? version : 0;
    }

    private async Task<ServiceModelInfo> ResolveServiceModelAsync(
        string aliasOrId,
        int? version,
        DeviceType? device,
        CancellationToken ct)
    {
        var models = await _serviceCatalogClient.ListModelsAsync(ct).ConfigureAwait(false);

        IEnumerable<ServiceModelInfo> candidates = models;
        if (LooksLikeModelId(aliasOrId))
        {
            candidates = candidates.Where(m => m.ModelId.Equals(aliasOrId, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            candidates = candidates.Where(m => m.Alias.Equals(aliasOrId, StringComparison.OrdinalIgnoreCase));
        }

        if (version != null)
        {
            candidates = candidates.Where(m => ParseVersion(m.ModelId) == version.Value);
        }

        if (device != null)
        {
            candidates = candidates.Where(m => ParseDeviceType(m.Runtime?.DeviceType) == device.Value);
        }

        var candidateList = candidates.ToList();
        if (candidateList.Count == 0)
        {
            throw new InvalidOperationException($"Model '{aliasOrId}' not found in service catalog.");
        }

        // Service catalog is pre-ordered by priority: NPU > specific-GPU > generic-GPU > CPU
        // Just take the first match
        return candidateList[0];
    }

    private static bool LooksLikeModelId(string aliasOrId)
    {
        return aliasOrId.Contains(':', StringComparison.Ordinal);
    }

    private void EnsureInitialized()
    {
        if (!_initialized || !FoundryLocalManager.IsInitialized)
        {
            throw new InvalidOperationException("Foundry Local is not initialized. Call InitializeAsync first.");
        }
    }

    /// <summary>
    /// Gets the current hardware detection override settings from environment variables.
    /// </summary>
    public HardwareOverrideSettings GetHardwareOverrides()
    {
        var config = HardwareDetectionConfig.ReadFromEnvironment();
        return new HardwareOverrideSettings
        {
            ForceCpuOnly = config.ForceCpuOnly,
            DisableNpu = config.DisableNpu,
            DisableGpu = config.DisableGpu
        };
    }

    /// <summary>
    /// Sets hardware detection override settings in environment variables.
    /// </summary>
    /// <param name="settings">Override settings to apply.</param>
    public void SetHardwareOverrides(HardwareOverrideSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var config = new HardwareDetectionConfig
        {
            ForceCpuOnly = settings.ForceCpuOnly,
            DisableNpu = settings.DisableNpu,
            DisableGpu = settings.DisableGpu
        };

        config.WriteToEnvironment();
        _logger.LogInformation("Hardware overrides updated: {Settings}", settings);
    }

    /// <summary>
    /// Clears all hardware detection override settings.
    /// </summary>
    public void ClearHardwareOverrides()
    {
        HardwareDetectionConfig.ClearEnvironment();
        _logger.LogInformation("Hardware overrides cleared; will auto-detect hardware");
    }
}
