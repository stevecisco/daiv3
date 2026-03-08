using Daiv3.Orchestration.Configuration;
using Daiv3.Orchestration.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Daiv3.Orchestration;

/// <summary>
/// Watches skill configuration files and progressively reloads effective skills at runtime.
/// </summary>
public sealed class SkillFileWatcherHostedService : IHostedService, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SkillFileWatcherHostedService> _logger;
    private readonly OrchestrationOptions _options;
    private readonly object _gate = new();
    private readonly HashSet<string> _lastLoaded = new(StringComparer.OrdinalIgnoreCase);

    private FileSystemWatcher? _watcher;
    private Timer? _debounceTimer;

    public SkillFileWatcherHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<SkillFileWatcherHostedService> logger,
        IOptions<OrchestrationOptions> options)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.EnableModuleAutoDiscovery || !_options.EnableSkillFileWatcher)
        {
            _logger.LogInformation("Skill file watcher disabled");
            return;
        }

        var rootPath = _options.SkillConfigAutoLoadPath;
        if (!Directory.Exists(rootPath))
        {
            _logger.LogDebug("Skill watcher path does not exist: {Path}", rootPath);
            return;
        }

        _watcher?.Dispose();
        _watcher = new FileSystemWatcher(rootPath)
        {
            IncludeSubdirectories = _options.ModuleAutoLoadRecursive,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime
        };

        _watcher.Created += OnChanged;
        _watcher.Changed += OnChanged;
        _watcher.Deleted += OnChanged;
        _watcher.Renamed += OnRenamed;
        _watcher.EnableRaisingEvents = true;

        await ReloadSkillsAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Skill file watcher active for {Path}", rootPath);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
        }

        _debounceTimer?.Dispose();
        _debounceTimer = null;

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_watcher != null)
        {
            _watcher.Created -= OnChanged;
            _watcher.Changed -= OnChanged;
            _watcher.Deleted -= OnChanged;
            _watcher.Renamed -= OnRenamed;
            _watcher.Dispose();
            _watcher = null;
        }

        _debounceTimer?.Dispose();
        _debounceTimer = null;
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        if (!IsSupportedSkillFile(e.FullPath))
        {
            return;
        }

        ScheduleReload();
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        if (!IsSupportedSkillFile(e.FullPath) && !IsSupportedSkillFile(e.OldFullPath))
        {
            return;
        }

        ScheduleReload();
    }

    private void ScheduleReload()
    {
        lock (_gate)
        {
            _debounceTimer?.Dispose();
            _debounceTimer = new Timer(
                callback: async _ => await ReloadSkillsAsync(CancellationToken.None).ConfigureAwait(false),
                state: null,
                dueTime: TimeSpan.FromMilliseconds(450),
                period: Timeout.InfiniteTimeSpan);
        }
    }

    private async Task ReloadSkillsAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var loader = scope.ServiceProvider.GetRequiredService<SkillConfigFileLoader>();
            var registry = scope.ServiceProvider.GetRequiredService<ISkillRegistry>();

            var batch = await loader.LoadSkillBatchAsync(_options.SkillConfigAutoLoadPath, _options.ModuleAutoLoadRecursive, ct).ConfigureAwait(false);

            var loadedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var config in batch.Skills)
            {
                var validation = loader.ValidateConfiguration(config);
                if (!validation.IsValid)
                {
                    continue;
                }

                var metadata = loader.ToSkillMetadata(config);
                registry.RegisterSkill(new ConfiguredSkill(metadata, config.Config), metadata.Source);
                loadedNames.Add(metadata.Name);
            }

            foreach (var staleSkill in _lastLoaded.Where(name => !loadedNames.Contains(name)).ToList())
            {
                registry.UnregisterSkill(staleSkill);
            }

            _lastLoaded.Clear();
            foreach (var loaded in loadedNames)
            {
                _lastLoaded.Add(loaded);
            }

            _logger.LogInformation("Skill watcher reloaded {Count} effective skill(s)", loadedNames.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Skill watcher reload failed");
        }
    }

    private static bool IsSupportedSkillFile(string path)
    {
        var ext = Path.GetExtension(path);
        return ext.Equals(".json", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".yaml", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".yml", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".md", StringComparison.OrdinalIgnoreCase);
    }
}
