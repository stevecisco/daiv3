using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Daiv3.App.Maui.Models;
using Daiv3.ModelExecution.Interfaces;
using Daiv3.ModelExecution.Models;
using Daiv3.Orchestration;
using Daiv3.Orchestration.Interfaces;

namespace Daiv3.App.Maui.Services;

/// <summary>
/// Implementation of IDashboardService for real-time dashboard telemetry.
/// Implements CT-REQ-003: The system SHALL provide a real-time transparency dashboard.
/// Implements CT-REQ-006: Agent activity, iterations, token usage, and system resource metrics.
/// Implements CT-REQ-007: Online token usage and budget status display.
/// Supports CT-NFR-001: Async/await patterns with debouncing to prevent UI blocking.
/// </summary>
public sealed class DashboardService : IDashboardService, IDisposable
{
    private readonly ILogger<DashboardService> _logger;
    private readonly IModelQueue? _modelQueue;
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly AgentExecutionMetricsCollector? _metricsCollector;
    private readonly ISystemMetricsService? _systemMetrics;
    private readonly IOnlineProviderRouter? _onlineProviderRouter;
    private readonly DashboardConfiguration _configuration;

    private CancellationTokenSource? _monitoringCts;
    private Task? _monitoringTask;
    private DashboardData? _cachedData;
    private DateTimeOffset _lastUpdateTime = DateTimeOffset.MinValue;
    private readonly object _cacheLock = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="DashboardService"/> class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <param name="modelQueue">Optional model queue service for queue status. If null, queue data uses defaults.</param>
    /// <param name="configuration">Optional custom configuration.</param>
    /// <param name="scopeFactory">Optional service scope factory for resolving scoped services (e.g. IAgentManager).</param>
    /// <param name="metricsCollector">Optional singleton agent execution metrics collector.</param>
    /// <param name="systemMetrics">Optional system metrics service for real CPU/memory/disk data.</param>
    /// <param name="onlineProviderRouter">Optional online provider router for token usage tracking (CT-REQ-007).</param>
    public DashboardService(
        ILogger<DashboardService> logger,
        IModelQueue? modelQueue = null,
        DashboardConfiguration? configuration = null,
        IServiceScopeFactory? scopeFactory = null,
        AgentExecutionMetricsCollector? metricsCollector = null,
        ISystemMetricsService? systemMetrics = null,
        IOnlineProviderRouter? onlineProviderRouter = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _modelQueue = modelQueue;
        _configuration = configuration ?? new DashboardConfiguration();
        _configuration.Validate();
        _scopeFactory = scopeFactory;
        _metricsCollector = metricsCollector;
        _systemMetrics = systemMetrics;
        _onlineProviderRouter = onlineProviderRouter;

        _logger.LogInformation(
            "DashboardService initialized: RefreshMs={RefreshMs}, AgentManager={HasAgent}, SystemMetrics={HasMetrics}, OnlineRouter={HasOnline}",
            _configuration.RefreshIntervalMs,
            _scopeFactory != null,
            _systemMetrics != null,
            _onlineProviderRouter != null);
    }

    /// <inheritdoc />
    public bool IsMonitoring { get; private set; }

    /// <inheritdoc />
    public DashboardConfiguration Configuration => _configuration;

    /// <inheritdoc />
    public event EventHandler<DashboardDataUpdatedEventArgs>? DataUpdated;

    /// <inheritdoc />
    public async Task<DashboardData> GetDashboardDataAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_configuration.DataCollectionTimeoutMs);

            var data = await CollectDashboardDataAsync(timeoutCts.Token).ConfigureAwait(false);

            lock (_cacheLock)
            {
                _cachedData = data;
                _lastUpdateTime = DateTimeOffset.UtcNow;
            }

            return data;
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning("Dashboard data collection timed out: {Message}", ex.Message);
            return CreateErrorData("Dashboard data collection timed out");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting dashboard data");
            return CreateErrorData($"Error collecting dashboard data: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task StartMonitoringAsync(int refreshIntervalMs = 3000, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (IsMonitoring)
        {
            _logger.LogInformation("Dashboard monitoring already active");
            return;
        }

        if (refreshIntervalMs < DashboardConfiguration.MinRefreshIntervalMs ||
            refreshIntervalMs > DashboardConfiguration.MaxRefreshIntervalMs)
        {
            throw new ArgumentOutOfRangeException(
                nameof(refreshIntervalMs),
                $"Interval must be between {DashboardConfiguration.MinRefreshIntervalMs} and {DashboardConfiguration.MaxRefreshIntervalMs}ms");
        }

        _monitoringCts?.Dispose();
        _monitoringCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        IsMonitoring = true;

        _monitoringTask = MonitoringLoopAsync(refreshIntervalMs, _monitoringCts.Token);

        _logger.LogInformation("Dashboard monitoring started with {IntervalMs}ms refresh", refreshIntervalMs);
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopMonitoringAsync()
    {
        ThrowIfDisposed();

        if (!IsMonitoring || _monitoringCts == null)
            return;

        _logger.LogInformation("Stopping dashboard monitoring");
        _monitoringCts.Cancel();
        IsMonitoring = false;

        if (_monitoringTask != null)
        {
            try
            {
                await _monitoringTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected when monitoring is cancelled
            }
        }

        _monitoringCts?.Dispose();
        _monitoringCts = null;
    }

    /// <summary>
    /// Background monitoring loop that periodically collects dashboard data and raises update events.
    /// </summary>
    private async Task MonitoringLoopAsync(int refreshIntervalMs, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var data = await GetDashboardDataAsync(cancellationToken).ConfigureAwait(false);
                    DataUpdated?.Invoke(this, new DashboardDataUpdatedEventArgs(data, data.CollectedAt));

                    if (_configuration.EnableLogging)
                        _logger.LogDebug("Dashboard data updated at {Timestamp}", data.CollectedAt);

                    await Task.Delay(refreshIntervalMs, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    if (_configuration.ContinueOnError)
                    {
                        _logger.LogWarning(ex, "Error in dashboard monitoring loop, continuing");
                        await Task.Delay(refreshIntervalMs, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        _logger.LogError(ex, "Error in dashboard monitoring loop, stopping");
                        throw;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Dashboard monitoring cancelled");
        }
        finally
        {
            IsMonitoring = false;
        }
    }

    /// <summary>
    /// Collects all dashboard data from available sources.
    /// </summary>
    private async Task<DashboardData> CollectDashboardDataAsync(CancellationToken cancellationToken)
    {
        var data = new DashboardData
        {
            CollectedAt = DateTimeOffset.UtcNow
        };

        data.Hardware = CollectHardwareStatus();
        data.Queue = await CollectQueueStatusAsync(cancellationToken).ConfigureAwait(false);
        data.OnlineUsage = await CollectOnlineProviderUsageAsync(cancellationToken).ConfigureAwait(false);
        data.Indexing = await CollectIndexingStatusAsync(cancellationToken).ConfigureAwait(false);
        data.Agent = await CollectAgentStatusAsync(cancellationToken).ConfigureAwait(false);
        data.SystemResources = CollectSystemResourceMetrics();
        data.Alerts = ComputeResourceAlerts(data.SystemResources);

        return data;
    }

    private static HardwareStatus CollectHardwareStatus()
    {
        return new HardwareStatus
        {
            OverallStatus = "System Ready",
            NpuStatus = "Detection pending (HW-NFR-002)",
            GpuStatus = "Detection pending (HW-NFR-002)",
            CpuStatus = "Available",
            PlatformInfo = "Windows 11"
        };
    }

    private async Task<Models.QueueStatus> CollectQueueStatusAsync(CancellationToken cancellationToken)
    {
        if (_modelQueue == null)
        {
            return new Models.QueueStatus
            {
                PendingCount = 0,
                CompletedCount = 0,
                CurrentModel = null,
                TopItems = [],
                AllPendingItems = [],
                ImmediateCount = 0,
                NormalCount = 0,
                BackgroundCount = 0
            };
        }

        try
        {
            var mxQueueStatus = await _modelQueue.GetQueueStatusAsync().ConfigureAwait(false);
            var mxMetrics = await _modelQueue.GetMetricsAsync().ConfigureAwait(false);

            var queueStatus = new Models.QueueStatus
            {
                PendingCount = (mxQueueStatus?.ImmediateCount ?? 0) +
                               (mxQueueStatus?.NormalCount ?? 0) +
                               (mxQueueStatus?.BackgroundCount ?? 0),
                CompletedCount = (int)(mxMetrics?.TotalCompleted ?? 0),
                CurrentModel = mxQueueStatus?.CurrentModelId,
                LastModelSwitchTime = mxQueueStatus?.LastModelSwitch,
                ImmediateCount = mxQueueStatus?.ImmediateCount ?? 0,
                NormalCount = mxQueueStatus?.NormalCount ?? 0,
                BackgroundCount = mxQueueStatus?.BackgroundCount ?? 0
            };

            if (mxMetrics != null)
            {
                if (mxMetrics.AverageExecutionDurationMs > 0)
                    queueStatus.AverageTaskDurationSeconds = mxMetrics.AverageExecutionDurationMs / 1000.0;

                if (mxMetrics.AverageQueueWaitMs > 0)
                    queueStatus.EstimatedWaitSeconds = mxMetrics.AverageQueueWaitMs / 1000.0;

                queueStatus.ThroughputPerMinute = mxMetrics.TotalCompleted;

                queueStatus.ModelUtilizationPercent = mxMetrics.InFlightExecutions > 0
                    ? (int)Math.Min(100, (mxMetrics.InFlightExecutions / 4.0) * 100.0)
                    : 0;
            }

            queueStatus.TopItems = [];
            queueStatus.AllPendingItems = [];

            return queueStatus;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error collecting queue status, returning defaults");
            return new Models.QueueStatus
            {
                PendingCount = 0,
                CompletedCount = 0,
                CurrentModel = null,
                TopItems = [],
                AllPendingItems = []
            };
        }
    }

    private async Task<IndexingStatus> CollectIndexingStatusAsync(CancellationToken cancellationToken)
    {
        // Placeholder - integrates with IKnowledgeFileOrchestrationService
        await Task.CompletedTask;

        return new IndexingStatus
        {
            IsIndexing = false,
            FilesIndexed = 0,
            FilesInProgress = 0,
            FilesWithErrors = 0,
            ProgressPercentage = 0,
            LastScanTime = null,
            TotalDocuments = 0
        };
    }

    /// <summary>
    /// Collects agent activity from active executions via IAgentManager.
    /// Implements CT-REQ-006: Agent Activity Display and Agent Metrics per Agent.
    /// Uses IServiceScopeFactory to resolve the scoped IAgentManager from this singleton service.
    /// </summary>
    private async Task<AgentStatus> CollectAgentStatusAsync(CancellationToken cancellationToken)
    {
        if (_scopeFactory == null)
        {
            return new AgentStatus { ActiveAgentCount = 0, TotalIterations = 0, TotalTokensUsed = 0, Activities = [] };
        }

        try
        {
            var activities = new List<IndividualAgentActivity>();

            using var scope = _scopeFactory.CreateScope();
            var agentManager = scope.ServiceProvider.GetService<IAgentManager>();

            if (agentManager == null)
                return new AgentStatus { Activities = [] };

            var activeExecutions = agentManager.GetActiveExecutions();

            foreach (var executionControl in activeExecutions)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Resolve agent details
                Agent? agent = null;
                try
                {
                    agent = await agentManager.GetAgentAsync(executionControl.AgentId, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Could not resolve agent {AgentId}", executionControl.AgentId);
                }

                // Get execution metrics snapshot from the singleton collector
                AgentExecutionMetricsSnapshot? snapshot = null;
                if (_metricsCollector != null)
                    snapshot = _metricsCollector.GetMetricsSnapshot(executionControl.ExecutionId);

                var state = executionControl.IsStopped ? "Stopped"
                          : executionControl.IsPaused ? "Paused"
                          : "Running";

                var shortId = executionControl.AgentId.ToString("N")[..8];
                activities.Add(new IndividualAgentActivity
                {
                    AgentId = executionControl.AgentId.ToString(),
                    AgentName = agent?.Name ?? $"Agent-{shortId}",
                    CurrentTask = null, // Will be populated when execution carries task goal (future)
                    State = state,
                    StatusDetail = state,
                    IterationCount = snapshot?.TotalIterations ?? 0,
                    TokensUsed = snapshot?.TotalTokensConsumed ?? 0,
                    StartTime = snapshot?.StartedAt,
                    ElapsedTime = snapshot?.TotalDuration ?? TimeSpan.Zero,
                    LastExecutedAt = snapshot?.StartedAt
                });
            }

            return new AgentStatus
            {
                ActiveAgentCount = activities.Count,
                TotalIterations = activities.Sum(a => a.IterationCount),
                TotalTokensUsed = activities.Sum(a => a.TokensUsed),
                Activities = activities
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error collecting agent status");
            return new AgentStatus { Activities = [] };
        }
    }

    /// <summary>
    /// Collects system resource metrics.
    /// Implements CT-REQ-006: System Resource Metrics (CPU, Memory, Disk).
    /// </summary>
    private SystemResourceMetrics CollectSystemResourceMetrics()
    {
        if (_systemMetrics == null)
        {
            return new SystemResourceMetrics
            {
                CpuCoreCount = Environment.ProcessorCount,
                ActiveExecutionProvider = "CPU"
            };
        }

        try
        {
            var cpuPercent = _systemMetrics.GetCpuUtilizationPercent();
            var (memUsed, memTotal) = _systemMetrics.GetSystemMemory();
            var (diskAvail, diskTotal) = _systemMetrics.GetDiskInfo();
            var processMem = _systemMetrics.GetProcessMemoryBytes();

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var knowledgeSize = _systemMetrics.GetDirectorySize(Path.Combine(localAppData, "Daiv3", "knowledge"));
            var modelSize = _systemMetrics.GetDirectorySize(Path.Combine(localAppData, "Daiv3", "models"));

            var memAvailPercent = memTotal > 0
                ? ((memTotal - memUsed) * 100.0 / memTotal)
                : 100.0;

            return new SystemResourceMetrics
            {
                CpuUtilizationPercent = cpuPercent,
                MemoryAvailablePercent = memAvailPercent,
                MemoryUsedBytes = memUsed,
                MemoryTotalBytes = memTotal,
                GpuUtilizationPercent = null,   // Pending HW-NFR-002
                NpuUtilizationPercent = null,   // Pending HW-NFR-002
                AvailableDiskBytes = diskAvail,
                TotalDiskBytes = diskTotal,
                ProcessMemoryBytes = processMem,
                ActiveExecutionProvider = "CPU",  // Pending HW-NFR-002
                CpuCoreCount = Environment.ProcessorCount,
                KnowledgeBaseSizeBytes = knowledgeSize,
                ModelCacheSizeBytes = modelSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error collecting system resource metrics");
            return new SystemResourceMetrics
            {
                CpuCoreCount = Environment.ProcessorCount,
                ActiveExecutionProvider = "CPU"
            };
        }
    }

    /// <summary>
    /// Computes resource alert state from collected metrics.
    /// Implements CT-REQ-006: Resource Alerts (CPU >85%, Memory >80%, Disk &lt;1GB).
    /// </summary>
    private static ResourceAlerts ComputeResourceAlerts(SystemResourceMetrics resources)
    {
        var alerts = new ResourceAlerts();

        if (resources.CpuUtilizationPercent > 85)
        {
            alerts.HasHighCpuAlert = true;
            alerts.AlertMessages.Add($"High CPU: {resources.CpuUtilizationPercent:F0}% (threshold: 85%)");
        }

        if (resources.MemoryTotalBytes > 0 && resources.MemoryUtilizationPercent > 80)
        {
            alerts.HasHighMemoryAlert = true;
            alerts.AlertMessages.Add($"High Memory: {resources.MemoryUtilizationPercent:F0}% used (threshold: 80%)");
        }

        const long oneGb = 1024L * 1024 * 1024;
        if (resources.AvailableDiskBytes > 0 && resources.AvailableDiskBytes < oneGb)
        {
            alerts.HasLowDiskAlert = true;
            var freeMb = resources.AvailableDiskBytes / (1024.0 * 1024);
            alerts.AlertMessages.Add($"Low Disk: {freeMb:F0} MB free (threshold: 1 GB)");
        }

        return alerts;
    }

    /// <summary>
    /// Collects online provider token usage and budget status.
    /// Implements CT-REQ-007: The dashboard SHALL display online token usage and budget status.
    /// </summary>
    private async Task<OnlineProviderUsage> CollectOnlineProviderUsageAsync(CancellationToken cancellationToken)
    {
        if (_onlineProviderRouter == null)
        {
            return new OnlineProviderUsage
            {
                HasOnlineProviders = false,
                HasActiveUsage = false,
                Providers = []
            };
        }

        try
        {
            var providerNames = await _onlineProviderRouter.ListProvidersAsync().ConfigureAwait(false);

            if (providerNames.Count == 0)
            {
                return new OnlineProviderUsage
                {
                    HasOnlineProviders = false,
                    HasActiveUsage = false,
                    Providers = []
                };
            }

            var providerSummaries = new List<ProviderUsageSummary>();
            var hasActiveUsage = false;

            foreach (var providerName in providerNames)
            {
                try
                {
                    var usage = await _onlineProviderRouter.GetTokenUsageAsync(providerName, cancellationToken)
                        .ConfigureAwait(false);

                    var summary = new ProviderUsageSummary
                    {
                        ProviderName = usage.ProviderName,
                        DisplayName = GetProviderDisplayName(usage.ProviderName),
                        DailyInputTokens = usage.DailyInputTokens,
                        DailyOutputTokens = usage.DailyOutputTokens,
                        MonthlyInputTokens = usage.MonthlyInputTokens,
                        MonthlyOutputTokens = usage.MonthlyOutputTokens,
                        DailyInputLimit = usage.DailyInputLimit,
                        DailyOutputLimit = usage.DailyOutputLimit,
                        MonthlyInputLimit = usage.MonthlyInputLimit,
                        MonthlyOutputLimit = usage.MonthlyOutputLimit,
                        ResetDate = usage.ResetDate
                    };

                    providerSummaries.Add(summary);

                    if (summary.DailyTotalTokens > 0 || summary.MonthlyTotalTokens > 0)
                    {
                        hasActiveUsage = true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error collecting token usage for provider {Provider}", providerName);
                }
            }

            return new OnlineProviderUsage
            {
                HasOnlineProviders = true,
                HasActiveUsage = hasActiveUsage,
                Providers = providerSummaries
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error collecting online provider usage");
            return new OnlineProviderUsage
            {
                HasOnlineProviders = false,
                HasActiveUsage = false,
                Providers = []
            };
        }
    }

    /// <summary>
    /// Converts provider names to friendly display names.
    /// </summary>
    private static string GetProviderDisplayName(string providerName)
    {
        return providerName?.ToLowerInvariant() switch
        {
            "openai" => "OpenAI",
            "azure-openai" => "Azure OpenAI",
            "anthropic" => "Claude (Anthropic)",
            _ => providerName ?? "Unknown"
        };
    }

    private DashboardData CreateErrorData(string errorMessage)
    {
        return new DashboardData
        {
            CollectedAt = DateTimeOffset.UtcNow,
            Hardware = new HardwareStatus { OverallStatus = "Error" },
            CollectionError = errorMessage
        };
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(GetType().Name);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        _monitoringCts?.Cancel();
        _monitoringCts?.Dispose();

        _disposed = true;
    }
}
