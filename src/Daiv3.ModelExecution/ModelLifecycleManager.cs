using System.Diagnostics;
using Daiv3.FoundryLocal.Management;
using Daiv3.ModelExecution.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Daiv3.ModelExecution;

/// <summary>
/// Enforces the constraint that only one Foundry Local model is loaded at a time.
/// </summary>
/// <remarks>
/// This is a core architectural constraint driven by the Foundry Local SDK limitation.
/// The ModelLifecycleManager ensures:
/// 1. Only one model is in memory at any time
/// 2. Clear error messages when constraint is violated
/// 3. Observable metrics for monitoring model switching
/// 4. Thread-safe operations via SemaphoreSlim locking
/// </remarks>
public class ModelLifecycleManager : IModelLifecycleManager
{
    private readonly ILogger<ModelLifecycleManager> _logger;
    private readonly FoundryLocalManagementService? _foundryManagementService;
    private readonly ModelLifecycleOptions _options;
    private readonly SemaphoreSlim _lockSlim = new(1, 1);

    private string? _currentModelId;
    private DateTimeOffset? _lastModelSwitch;
    private int _totalLoads;
    private int _successfulLoads;
    private int _failedLoads;
    private int _constraintViolations;
    private readonly List<long> _loadTimes = new();

    public ModelLifecycleManager(ILogger<ModelLifecycleManager> logger)
        : this(logger, null, null)
    {
    }

    public ModelLifecycleManager(
        ILogger<ModelLifecycleManager> logger,
        FoundryLocalManagementService? foundryManagementService,
        IOptions<ModelLifecycleOptions>? options = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _foundryManagementService = foundryManagementService;
        _options = options?.Value ?? new ModelLifecycleOptions();
    }

    public async Task LoadModelAsync(string modelId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);

        await _lockSlim.WaitAsync(ct);
        long? loadTime = null;

        try
        {
            _totalLoads++;
            var stopwatch = Stopwatch.StartNew();

            // Constraint: Only one model can be loaded at a time
            if (_currentModelId != null && _currentModelId != modelId)
            {
                _constraintViolations++;
                _logger.LogError(
                    "Attempted to load model {NewModelId} while {CurrentModelId} is already loaded. " +
                    "Only one model can be loaded at a time. Use SwitchModelAsync() instead.",
                    modelId, _currentModelId);

                throw new InvalidOperationException(
                    $"Cannot load model '{modelId}': model '{_currentModelId}' is already loaded. " +
                    "Only one model can be loaded at a time. Call SwitchModelAsync() to switch models.");
            }

            // Idempotent: If the same model is already loaded, no-op
            if (_currentModelId == modelId)
            {
                _logger.LogDebug("Model {ModelId} is already loaded (idempotent)", modelId);
                return;
            }

            _logger.LogInformation("Loading model: {ModelId}", modelId);

            await LoadWithTimeoutAsync(modelId, ct).ConfigureAwait(false);

            _currentModelId = modelId;
            _lastModelSwitch = DateTimeOffset.UtcNow;
            _successfulLoads++;

            stopwatch.Stop();
            loadTime = stopwatch.ElapsedMilliseconds;
            _loadTimes.Add(loadTime.Value);

            _logger.LogInformation(
                "Successfully loaded model {ModelId} in {LoadTimeMs}ms",
                modelId, loadTime);
        }
        catch (InvalidOperationException)
        {
            _failedLoads++;
            throw;
        }
        catch (Exception ex)
        {
            _failedLoads++;
            _logger.LogError(
                ex,
                "Failed to load model {ModelId}",
                modelId);
            throw;
        }
        finally
        {
            _lockSlim.Release();
        }
    }

    public async Task SwitchModelAsync(string newModelId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newModelId);

        await _lockSlim.WaitAsync(ct);
        long? switchTime = null;

        try
        {
            var stopwatch = Stopwatch.StartNew();

            // Idempotent: If the same model is already loaded, no-op
            if (_currentModelId == newModelId)
            {
                _logger.LogDebug("Model {ModelId} is already loaded (idempotent)", newModelId);
                return;
            }

            var previousModel = _currentModelId;
            _logger.LogInformation(
                "Switching model: {PreviousModel} → {NewModelId}",
                previousModel ?? "(none)", newModelId);

            // Unload the current model
            if (_currentModelId != null)
            {
                _logger.LogDebug("Unloading model: {ModelId}", _currentModelId);
                await UnloadWithTimeoutAsync(ct).ConfigureAwait(false);
            }

            // Load the new model
            _totalLoads++;
            _logger.LogDebug("Loading model: {NewModelId}", newModelId);
            await LoadWithTimeoutAsync(newModelId, ct).ConfigureAwait(false);

            _currentModelId = newModelId;
            _lastModelSwitch = DateTimeOffset.UtcNow;
            _successfulLoads++;

            stopwatch.Stop();
            switchTime = stopwatch.ElapsedMilliseconds;
            _loadTimes.Add(switchTime.Value);

            _logger.LogInformation(
                "Successfully switched model in {SwitchTimeMs}ms: {PreviousModel} → {NewModelId}",
                switchTime, previousModel ?? "(none)", newModelId);
        }
        catch (Exception ex)
        {
            _failedLoads++;
            _logger.LogError(
                ex,
                "Failed to switch to model {NewModelId}",
                newModelId);
            throw;
        }
        finally
        {
            _lockSlim.Release();
        }
    }

    public async Task UnloadModelAsync(CancellationToken ct = default)
    {
        await _lockSlim.WaitAsync(ct);
        try
        {
            if (_currentModelId == null)
            {
                _logger.LogDebug("No model currently loaded (unload is a no-op)");
                return;
            }

            var modelToUnload = _currentModelId;
            _logger.LogInformation("Unloading model: {ModelId}", modelToUnload);

            await UnloadWithTimeoutAsync(ct).ConfigureAwait(false);

            _currentModelId = null;
            _lastModelSwitch = DateTimeOffset.UtcNow;

            _logger.LogInformation("Successfully unloaded model: {ModelId}", modelToUnload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unload model");
            throw;
        }
        finally
        {
            _lockSlim.Release();
        }
    }

    private async Task LoadWithTimeoutAsync(string modelId, CancellationToken ct)
    {
        if (_foundryManagementService == null)
        {
            await Task.Delay(100, ct).ConfigureAwait(false);
            return;
        }

        await EnsureFoundryInitializedAsync(ct).ConfigureAwait(false);

        if (!await _foundryManagementService.IsModelAvailableAsync(modelId, ct).ConfigureAwait(false))
        {
            throw new InvalidOperationException($"Cannot load model '{modelId}': model is not available in Foundry Local catalog.");
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(_options.LoadTimeoutMs));
        await _foundryManagementService.LoadModelAsync(modelId, timeoutCts.Token).ConfigureAwait(false);
    }

    private async Task UnloadWithTimeoutAsync(CancellationToken ct)
    {
        if (_foundryManagementService == null)
        {
            await Task.Delay(50, ct).ConfigureAwait(false);
            return;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(_options.SwitchTimeoutMs));
        await _foundryManagementService.UnloadModelAsync(timeoutCts.Token).ConfigureAwait(false);
    }

    private async Task EnsureFoundryInitializedAsync(CancellationToken ct)
    {
        if (_foundryManagementService == null)
        {
            return;
        }

        await _foundryManagementService.InitializeAsync(
            new FoundryLocalOptions
            {
                AppName = "daiv3-model-execution",
                EnsureExecutionProviders = true
            },
            ct).ConfigureAwait(false);
    }

    public Task<string?> GetLoadedModelAsync()
    {
        return Task.FromResult(_currentModelId);
    }

    public Task<bool> IsModelLoadedAsync(string modelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        return Task.FromResult(_currentModelId == modelId);
    }

    public Task<DateTimeOffset?> GetLastModelSwitchAsync()
    {
        return Task.FromResult(_lastModelSwitch);
    }

    public Task<ModelLifecycleMetrics> GetMetricsAsync()
    {
        var metrics = new ModelLifecycleMetrics
        {
            TotalLoads = _totalLoads,
            SuccessfulLoads = _successfulLoads,
            FailedLoads = _failedLoads,
            ConstraintViolations = _constraintViolations,
            CurrentModelId = _currentModelId,
            LastModelSwitch = _lastModelSwitch,
            AverageLoadTimeMs = _loadTimes.Count > 0 ? _loadTimes.Average() : 0
        };

        return Task.FromResult(metrics);
    }
}
