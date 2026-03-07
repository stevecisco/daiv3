using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Daiv3.App.Maui.Models;
using Daiv3.ModelExecution.Interfaces;
using Daiv3.ModelExecution.Models;
using Daiv3.Orchestration;
using Daiv3.Orchestration.Interfaces;
using Daiv3.Scheduler;

namespace Daiv3.App.Maui.Services;

/// <summary>
/// Implementation of IDashboardService for real-time dashboard telemetry.
/// Implements CT-REQ-003: The system SHALL provide a real-time transparency dashboard.
/// Implements CT-REQ-006: Agent activity, iterations, token usage, and system resource metrics.
/// Implements CT-REQ-007: Online token usage and budget status display.
/// Implements CT-REQ-008: Scheduled jobs status and execution results display.
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
    private readonly IScheduler? _scheduler;
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
    /// <param name="scheduler">Optional scheduler for job status tracking (CT-REQ-008).</param>
    public DashboardService(
        ILogger<DashboardService> logger,
        IModelQueue? modelQueue = null,
        DashboardConfiguration? configuration = null,
        IServiceScopeFactory? scopeFactory = null,
        AgentExecutionMetricsCollector? metricsCollector = null,
        ISystemMetricsService? systemMetrics = null,
        IOnlineProviderRouter? onlineProviderRouter = null,
        IScheduler? scheduler = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _modelQueue = modelQueue;
        _configuration = configuration ?? new DashboardConfiguration();
        _configuration.Validate();
        _scopeFactory = scopeFactory;
        _metricsCollector = metricsCollector;
        _systemMetrics = systemMetrics;
        _onlineProviderRouter = onlineProviderRouter;
        _scheduler = scheduler;

        _logger.LogInformation(
            "DashboardService initialized: RefreshMs={RefreshMs}, AgentManager={HasAgent}, SystemMetrics={HasMetrics}, OnlineRouter={HasOnline}, Scheduler={HasScheduler}",
            _configuration.RefreshIntervalMs,
            _scopeFactory != null,
            _systemMetrics != null,
            _onlineProviderRouter != null,
            _scheduler != null);
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
        data.ScheduledJobs = await CollectScheduledJobsStatusAsync(cancellationToken).ConfigureAwait(false);
        data.BackgroundTasks = await CollectBackgroundTasksAsync(cancellationToken).ConfigureAwait(false);
        data.TimeTracking = await CollectTimeTrackingStatusAsync(cancellationToken).ConfigureAwait(false);
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

    /// <summary>
    /// Collects scheduled jobs status and execution history for the dashboard.
    /// Implements CT-REQ-008: The dashboard SHALL display scheduled jobs and results.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Scheduled jobs status with all job metadata.</returns>
    private async Task<ScheduledJobsStatus> CollectScheduledJobsStatusAsync(CancellationToken cancellationToken)
    {
        if (_scheduler == null)
        {
            return new ScheduledJobsStatus
            {
                HasScheduledJobs = false,
                TotalJobs = 0,
                Jobs = []
            };
        }

        try
        {
            var allJobs = await _scheduler.GetAllJobsAsync(cancellationToken).ConfigureAwait(false);

            if (allJobs.Count == 0)
            {
                return new ScheduledJobsStatus
                {
                    HasScheduledJobs = false,
                    TotalJobs = 0,
                    Jobs = []
                };
            }

            var jobSummaries = new List<ScheduledJobSummary>();
            var statusCounts = new Dictionary<ScheduledJobStatus, int>
            {
                [ScheduledJobStatus.Pending] = 0,
                [ScheduledJobStatus.Running] = 0,
                [ScheduledJobStatus.Completed] = 0,
                [ScheduledJobStatus.Failed] = 0,
                [ScheduledJobStatus.Cancelled] = 0,
                [ScheduledJobStatus.Scheduled] = 0,
                [ScheduledJobStatus.Paused] = 0
            };

            foreach (var jobMetadata in allJobs)
            {
                try
                {
                    var summary = new ScheduledJobSummary
                    {
                        JobId = jobMetadata.JobId,
                        JobName = jobMetadata.JobName,
                        Status = jobMetadata.Status.ToString(),
                        ScheduleType = jobMetadata.ScheduleType.ToString(),
                        NextRunTime = jobMetadata.ScheduledAtUtc,
                        LastStartTime = jobMetadata.LastStartedAtUtc,
                        LastCompletionTime = jobMetadata.LastCompletedAtUtc,
                        LastExecutionDuration = jobMetadata.LastExecutionDuration,
                        ExecutionCount = jobMetadata.ExecutionCount,
                        IntervalSeconds = jobMetadata.IntervalSeconds,
                        CronExpression = jobMetadata.CronExpression,
                        EventType = jobMetadata.EventType,
                        LastErrorMessage = jobMetadata.LastErrorMessage
                    };

                    jobSummaries.Add(summary);

                    // Count by status
                    if (statusCounts.ContainsKey(jobMetadata.Status))
                    {
                        statusCounts[jobMetadata.Status]++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error converting job metadata for job {JobId}", jobMetadata.JobId);
                }
            }

            return new ScheduledJobsStatus
            {
                HasScheduledJobs = true,
                TotalJobs = allJobs.Count,
                ScheduledCount = statusCounts[ScheduledJobStatus.Scheduled],
                RunningCount = statusCounts[ScheduledJobStatus.Running],
                PendingCount = statusCounts[ScheduledJobStatus.Pending],
                CompletedCount = statusCounts[ScheduledJobStatus.Completed],
                FailedCount = statusCounts[ScheduledJobStatus.Failed],
                PausedCount = statusCounts[ScheduledJobStatus.Paused],
                CancelledCount = statusCounts[ScheduledJobStatus.Cancelled],
                Jobs = jobSummaries
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error collecting scheduled jobs status");
            return new ScheduledJobsStatus
            {
                HasScheduledJobs = false,
                TotalJobs = 0,
                Jobs = []
            };
        }
    }

    /// <summary>
    /// Collects background tasks status and lifecycle for the service inspector.
    /// Implements CT-REQ-012: The system SHALL provide a Background Service Inspector.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Background tasks status with all task metadata.</returns>
    private async Task<BackgroundTasksStatus> CollectBackgroundTasksAsync(CancellationToken cancellationToken)
    {
       // For Phase 1 implementation, return placeholder data
        // Future: Query IScheduler for running scheduled jobs, ITaskOrchestrator for active tasks,
        // and IModelQueue for pending/in-flight model executions

        await Task.CompletedTask; // Async method placeholder

        var tasks = new List<BackgroundTaskInfo>();

        // Collect scheduler jobs (if available)
        if (_scheduler != null)
        {
            try
            {
                var runningJobs = await _scheduler.GetAllJobsAsync(cancellationToken).ConfigureAwait(false);
                
                foreach (var job in runningJobs.Where(j => j.Status == ScheduledJobStatus.Running))
                {
                    var taskStatus = job.Status switch
                    {
                        ScheduledJobStatus.Pending => Models.TaskStatus.Queued,
                        ScheduledJobStatus.Running => Models.TaskStatus.Running,
                        ScheduledJobStatus.Paused => Models.TaskStatus.Paused,
                        ScheduledJobStatus.Failed => Models.TaskStatus.Failed,
                        ScheduledJobStatus.Completed => Models.TaskStatus.Completed,
                        ScheduledJobStatus.Cancelled => Models.TaskStatus.Failed, // Map cancelled to failed
                        _ => Models.TaskStatus.Queued
                    };

                    var elapsedTime = job.LastStartedAtUtc.HasValue
                        ? DateTime.UtcNow - job.LastStartedAtUtc.Value
                        : TimeSpan.Zero;

                    tasks.Add(new BackgroundTaskInfo
                    {
                        TaskId = job.JobId,
                        Name = job.JobName,
                        Description = $"{job.ScheduleType} job",
                        Status = taskStatus,
                        StartTime = job.LastStartedAtUtc ?? DateTime.UtcNow,
                        ElapsedTime = elapsedTime,
                        ProgressPercent = null, // Jobs don't report progress currently
                        CurrentOperation = job.Status == ScheduledJobStatus.Running ? "Executing" : job.Status.ToString(),
                        AgentName = "Scheduler",
                        Priority = "Normal",
                        Metrics = new TaskMetrics
                        {
                            CpuPercent = 0, // Not tracked per-job yet
                            MemoryBytes = 0, // Not tracked per-job yet
                            ThreadCount = 0,
                            EstimatedRemaining = null,
                            TokensUsed = 0
                        },
                        ErrorMessage = job.LastErrorMessage
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error collecting background tasks from scheduler");
            }
        }

        // Collect active agent executions (if available) via AgentExecutionMetricsCollector
        if (_metricsCollector != null && _scopeFactory != null)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var agentManager = scope.ServiceProvider.GetService<IAgentManager>();

                if (agentManager != null)
                {
                    var activeExecutions = agentManager.GetActiveExecutions();

                    foreach (var executionControl in activeExecutions)
                    {
                        var snapshot = _metricsCollector.GetMetricsSnapshot(executionControl.ExecutionId);

                        if (snapshot != null)
                        {
                            var taskStatus = executionControl.IsStopped ? Models.TaskStatus.Completed
                                           : executionControl.IsPaused ? Models.TaskStatus.Paused
                                           : Models.TaskStatus.Running;

                            var shortId = executionControl.AgentId.ToString("N")[..8];

                            tasks.Add(new BackgroundTaskInfo
                            {
                                TaskId = executionControl.ExecutionId.ToString(),
                                Name = $"Agent-{shortId}",
                                Description = "Agent execution",
                                Status = taskStatus,
                                StartTime = snapshot.StartedAt.DateTime,
                                ElapsedTime = snapshot.TotalDuration,
                                ProgressPercent = null,
                                CurrentOperation = $"Iteration {snapshot.TotalIterations}",
                                AgentName = "Orchestrator",
                                Priority = "Normal",
                                Metrics = new TaskMetrics
                                {
                                    CpuPercent = 0, // Not tracked per-agent yet
                                    MemoryBytes = 0, // Not tracked per-agent yet
                                    ThreadCount = 0,
                                    EstimatedRemaining = null,
                                    TokensUsed = snapshot.TotalTokensConsumed
                                }
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error collecting background tasks from agent manager");
            }
        }

        // Collect model queue in-flight executions (if available)
        if (_modelQueue != null)
        {
            try
            {
                var metrics = await _modelQueue.GetMetricsAsync().ConfigureAwait(false);
                
                // Add summary task for in-flight model executions
                if (metrics?.InFlightExecutions > 0)
                {
                    tasks.Add(new BackgroundTaskInfo
                    {
                        TaskId = "model-queue-aggregate",
                        Name = "Model Executions",
                        Description = $"{metrics.InFlightExecutions} in-flight model execution(s)",
                        Status = Models.TaskStatus.Running,
                        StartTime = DateTime.UtcNow, // Placeholder, actual start time not tracked
                        ElapsedTime = TimeSpan.Zero,
                        ProgressPercent = null,
                        CurrentOperation = $"{metrics.InFlightExecutions} executing",
                        AgentName = "ModelQueue",
                        Priority = "Normal",
                        Metrics = new TaskMetrics
                        {
                            CpuPercent = 0,
                            MemoryBytes = 0,
                            ThreadCount = (int)metrics.InFlightExecutions,
                            EstimatedRemaining = null,
                            TokensUsed = 0
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error collecting background tasks from model queue");
            }
        }

        // Compute status counts
        var statusGroups = tasks.GroupBy(t => t.Status).ToDictionary(g => g.Key, g => g.Count());

        return new BackgroundTasksStatus
        {
            HasTasks = tasks.Count > 0,
            TotalTasks = tasks.Count,
            RunningCount = statusGroups.GetValueOrDefault(Models.TaskStatus.Running, 0),
            QueuedCount = statusGroups.GetValueOrDefault(Models.TaskStatus.Queued, 0),
            PausedCount = statusGroups.GetValueOrDefault(Models.TaskStatus.Paused, 0),
            BlockedCount = statusGroups.GetValueOrDefault(Models.TaskStatus.Blocked, 0),
            CancellingCount = statusGroups.GetValueOrDefault(Models.TaskStatus.Cancelling, 0),
            FailedCount = statusGroups.GetValueOrDefault(Models.TaskStatus.Failed, 0),
            CompletedCount = statusGroups.GetValueOrDefault(Models.TaskStatus.Completed, 0),
            Tasks = tasks
        };
    }

    /// <summary>
    /// Collects hierarchical time tracking data for project/task/agent rollups.
    /// Implements CT-REQ-013 Time Tracking Dashboard (Phase 6 MVP scope).
    /// </summary>
    private async Task<TimeTrackingStatus> CollectTimeTrackingStatusAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var entries = new List<TimeEntry>();

        if (_scheduler != null)
        {
            try
            {
                var allJobs = await _scheduler.GetAllJobsAsync(cancellationToken).ConfigureAwait(false);

                foreach (var job in allJobs)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var hasExecutionData = job.LastStartedAtUtc.HasValue || job.LastExecutionDuration.HasValue;
                    if (!hasExecutionData)
                        continue;

                    var startTimeUtc = job.LastStartedAtUtc ?? job.CreatedAtUtc;
                    var elapsed = ResolveJobElapsed(job, now);
                    if (elapsed <= TimeSpan.Zero)
                        continue;

                    var endTimeUtc = ResolveJobEndTime(job, startTimeUtc, elapsed, now);
                    var utilization = ResolveUtilizationForStatus(job.Status);
                    var billable = ComputeBillableTime(elapsed, utilization);
                    var (projectId, projectName) = ResolveProject(job);

                    entries.Add(new TimeEntry
                    {
                        TaskId = job.JobId,
                        TaskName = string.IsNullOrWhiteSpace(job.JobName) ? "Scheduled Job" : job.JobName,
                        ProjectId = projectId,
                        ProjectName = projectName,
                        AgentName = "Scheduler",
                        StartTime = new DateTimeOffset(startTimeUtc, TimeSpan.Zero),
                        EndTime = endTimeUtc,
                        ElapsedTime = elapsed,
                        BillableTime = billable,
                        EstimatedTime = job.LastExecutionDuration,
                        UtilizationPercent = utilization,
                        WorkType = job.ScheduleType.ToString(),
                        Status = job.Status.ToString()
                    });
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error collecting time tracking entries from scheduler");
            }
        }

        if (_scopeFactory != null && _metricsCollector != null)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var agentManager = scope.ServiceProvider.GetService<IAgentManager>();

                if (agentManager != null)
                {
                    var activeExecutions = agentManager.GetActiveExecutions();

                    foreach (var executionControl in activeExecutions)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var snapshot = _metricsCollector.GetMetricsSnapshot(executionControl.ExecutionId);
                        if (snapshot == null || snapshot.TotalDuration <= TimeSpan.Zero)
                            continue;

                        Agent? agent = null;
                        try
                        {
                            agent = await agentManager.GetAgentAsync(executionControl.AgentId, cancellationToken)
                                .ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "Could not resolve agent for execution {ExecutionId}", executionControl.ExecutionId);
                        }

                        var agentName = agent?.Name ?? $"Agent-{executionControl.AgentId.ToString("N")[..8]}";
                        var state = executionControl.IsStopped ? "Stopped"
                                  : executionControl.IsPaused ? "Paused"
                                  : "Running";

                        var utilization = state == "Running" ? 100 : state == "Paused" ? 60 : 80;
                        var billable = ComputeBillableTime(snapshot.TotalDuration, utilization);

                        entries.Add(new TimeEntry
                        {
                            TaskId = executionControl.ExecutionId.ToString(),
                            TaskName = "Agent execution",
                            ProjectId = "agent-workloads",
                            ProjectName = "Agent Workloads",
                            AgentName = agentName,
                            StartTime = snapshot.StartedAt,
                            EndTime = snapshot.StartedAt + snapshot.TotalDuration,
                            ElapsedTime = snapshot.TotalDuration,
                            BillableTime = billable,
                            EstimatedTime = null,
                            UtilizationPercent = utilization,
                            WorkType = "AgentExecution",
                            Status = state
                        });
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error collecting time tracking entries from agent manager");
            }
        }

        var projects = BuildProjectRollups(entries);
        var agents = BuildAgentRollups(entries);

        return new TimeTrackingStatus
        {
            HasEntries = entries.Count > 0,
            PeriodStartUtc = now.AddDays(-7),
            PeriodEndUtc = now,
            Entries = entries,
            Projects = projects,
            Agents = agents
        };
    }

    private static TimeSpan ResolveJobElapsed(ScheduledJobMetadata job, DateTimeOffset now)
    {
        if (job.Status == ScheduledJobStatus.Running && job.LastStartedAtUtc.HasValue)
        {
            var elapsed = now - new DateTimeOffset(job.LastStartedAtUtc.Value, TimeSpan.Zero);
            return elapsed > TimeSpan.Zero ? elapsed : TimeSpan.Zero;
        }

        return job.LastExecutionDuration ?? TimeSpan.Zero;
    }

    private static DateTimeOffset ResolveJobEndTime(ScheduledJobMetadata job, DateTime startTimeUtc, TimeSpan elapsed, DateTimeOffset now)
    {
        if (job.Status == ScheduledJobStatus.Running)
            return now;

        if (job.LastCompletedAtUtc.HasValue)
            return new DateTimeOffset(job.LastCompletedAtUtc.Value, TimeSpan.Zero);

        return new DateTimeOffset(startTimeUtc, TimeSpan.Zero) + elapsed;
    }

    private static double ResolveUtilizationForStatus(ScheduledJobStatus status)
    {
        return status switch
        {
            ScheduledJobStatus.Running => 100,
            ScheduledJobStatus.Completed => 95,
            ScheduledJobStatus.Scheduled => 45,
            ScheduledJobStatus.Pending => 60,
            ScheduledJobStatus.Paused => 40,
            ScheduledJobStatus.Cancelled => 20,
            ScheduledJobStatus.Failed => 35,
            _ => 50
        };
    }

    private static TimeSpan ComputeBillableTime(TimeSpan elapsed, double utilizationPercent)
    {
        var clamped = Math.Max(0, Math.Min(100, utilizationPercent));
        return TimeSpan.FromTicks((long)(elapsed.Ticks * (clamped / 100.0)));
    }

    private static (string ProjectId, string ProjectName) ResolveProject(ScheduledJobMetadata job)
    {
        var metadata = job.Metadata;
        if (metadata != null)
        {
            var projectId = TryGetMetadataValue(metadata, "projectId")
                ?? TryGetMetadataValue(metadata, "project_id")
                ?? TryGetMetadataValue(metadata, "ProjectId");

            var projectName = TryGetMetadataValue(metadata, "projectName")
                ?? TryGetMetadataValue(metadata, "project_name")
                ?? TryGetMetadataValue(metadata, "ProjectName");

            if (!string.IsNullOrWhiteSpace(projectId))
            {
                return (projectId, string.IsNullOrWhiteSpace(projectName) ? projectId : projectName);
            }

            if (!string.IsNullOrWhiteSpace(projectName))
            {
                return (projectName, projectName);
            }
        }

        if (!string.IsNullOrWhiteSpace(job.EventType))
        {
            return (job.EventType, job.EventType);
        }

        return ("unassigned", "Unassigned");
    }

    private static string? TryGetMetadataValue(IReadOnlyDictionary<string, object> metadata, string key)
    {
        if (!metadata.TryGetValue(key, out var value) || value == null)
            return null;

        return value.ToString();
    }

    private static List<ProjectTimeSummary> BuildProjectRollups(List<TimeEntry> entries)
    {
        if (entries.Count == 0)
            return [];

        return entries
            .GroupBy(e => new { e.ProjectId, e.ProjectName })
            .Select(projectGroup =>
            {
                var tasks = projectGroup
                    .GroupBy(e => new { e.TaskId, e.TaskName })
                    .Select(taskGroup =>
                    {
                        var taskEntries = taskGroup.ToList();
                        return new TaskTimeSummary
                        {
                            TaskId = taskGroup.Key.TaskId,
                            TaskName = taskGroup.Key.TaskName,
                            TotalElapsedTime = TimeSpan.FromTicks(taskEntries.Sum(e => e.ElapsedTime.Ticks)),
                            TotalBillableTime = TimeSpan.FromTicks(taskEntries.Sum(e => e.BillableTime.Ticks)),
                            EstimatedTime = taskEntries.FirstOrDefault(e => e.EstimatedTime.HasValue)?.EstimatedTime,
                            AgentBreakdown = taskEntries
                                .GroupBy(e => e.AgentName, StringComparer.OrdinalIgnoreCase)
                                .ToDictionary(
                                    g => g.Key,
                                    g => TimeSpan.FromTicks(g.Sum(x => x.ElapsedTime.Ticks)),
                                    StringComparer.OrdinalIgnoreCase)
                        };
                    })
                    .OrderByDescending(t => t.TotalElapsedTime)
                    .ToList();

                return new ProjectTimeSummary
                {
                    ProjectId = projectGroup.Key.ProjectId,
                    ProjectName = projectGroup.Key.ProjectName,
                    TotalElapsedTime = TimeSpan.FromTicks(projectGroup.Sum(e => e.ElapsedTime.Ticks)),
                    TotalBillableTime = TimeSpan.FromTicks(projectGroup.Sum(e => e.BillableTime.Ticks)),
                    TaskCount = tasks.Count,
                    OverrunTaskCount = tasks.Count(t => t.IsOverrun),
                    Tasks = tasks,
                    AgentBreakdown = projectGroup
                        .GroupBy(e => e.AgentName, StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(
                            g => g.Key,
                            g => TimeSpan.FromTicks(g.Sum(x => x.ElapsedTime.Ticks)),
                            StringComparer.OrdinalIgnoreCase)
                };
            })
            .OrderByDescending(p => p.TotalElapsedTime)
            .ToList();
    }

    private static List<AgentTimeSummary> BuildAgentRollups(List<TimeEntry> entries)
    {
        if (entries.Count == 0)
            return [];

        return entries
            .GroupBy(e => e.AgentName, StringComparer.OrdinalIgnoreCase)
            .Select(agentGroup =>
            {
                var agentEntries = agentGroup.ToList();
                var totalElapsedTicks = agentEntries.Sum(e => e.ElapsedTime.Ticks);
                var totalBillableTicks = agentEntries.Sum(e => e.BillableTime.Ticks);

                var projectDurations = agentEntries
                    .GroupBy(e => e.ProjectName, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(
                        g => g.Key,
                        g => TimeSpan.FromTicks(g.Sum(x => x.ElapsedTime.Ticks)),
                        StringComparer.OrdinalIgnoreCase);

                var projectPercentages = projectDurations.ToDictionary(
                    kvp => kvp.Key,
                    kvp => totalElapsedTicks == 0 ? 0 : kvp.Value.Ticks * 100.0 / totalElapsedTicks,
                    StringComparer.OrdinalIgnoreCase);

                return new AgentTimeSummary
                {
                    AgentName = agentGroup.Key,
                    TotalElapsedTime = TimeSpan.FromTicks(totalElapsedTicks),
                    TotalBillableTime = TimeSpan.FromTicks(totalBillableTicks),
                    UtilizationPercent = totalElapsedTicks == 0 ? 0 : totalBillableTicks * 100.0 / totalElapsedTicks,
                    ActiveProjectCount = projectDurations.Count,
                    AverageTaskDuration = TimeSpan.FromTicks((long)agentEntries.Average(e => e.ElapsedTime.Ticks)),
                    CurrentTask = agentEntries
                        .OrderByDescending(e => e.EndTime)
                        .Select(e => e.TaskName)
                        .FirstOrDefault(),
                    ProjectPercentages = projectPercentages
                };
            })
            .OrderByDescending(a => a.TotalElapsedTime)
            .ToList();
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
