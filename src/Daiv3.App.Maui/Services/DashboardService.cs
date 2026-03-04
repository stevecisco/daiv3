using Microsoft.Extensions.Logging;
using Daiv3.App.Maui.Models;

namespace Daiv3.App.Maui.Services;

/// <summary>
/// Implementation of IDashboardService for real-time dashboard telemetry.
/// Implements CT-REQ-003: The system SHALL provide a real-time transparency dashboard.
/// Supports CT-NFR-001: Async/await patterns with debouncing to prevent UI blocking.
/// </summary>
public class DashboardService : IDashboardService, IDisposable
{
    private readonly ILogger<DashboardService> _logger;
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
    /// <param name="configuration">Optional custom configuration.</param>
    public DashboardService(
        ILogger<DashboardService> logger,
        DashboardConfiguration? configuration = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? new DashboardConfiguration();
        _configuration.Validate();
        _logger.LogInformation("DashboardService initialized with refresh interval {RefreshMs}ms",
            _configuration.RefreshIntervalMs);
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
            // Use timeout for data collection
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_configuration.DataCollectionTimeoutMs);

            var data = await CollectDashboardDataAsync(timeoutCts.Token).ConfigureAwait(false);

            // Update cache
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

        // Validate interval
        if (refreshIntervalMs < DashboardConfiguration.MinRefreshIntervalMs ||
            refreshIntervalMs > DashboardConfiguration.MaxRefreshIntervalMs)
        {
            throw new ArgumentOutOfRangeException(
                nameof(refreshIntervalMs),
                $"Interval must be between {DashboardConfiguration.MinRefreshIntervalMs} and {DashboardConfiguration.MaxRefreshIntervalMs}ms");
        }

        _monitoringCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        IsMonitoring = true;

        _monitoringTask = MonitoringLoopAsync(refreshIntervalMs, _monitoringCts.Token);
        
        _logger.LogInformation("Dashboard monitoring started with {IntervalMs}ms refresh", refreshIntervalMs);
        
        // Don't await - let it run in background
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopMonitoringAsync()
    {
        ThrowIfDisposed();

        if (!IsMonitoring || _monitoringCts == null)
        {
            return;
        }

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
    /// Background loop that periodically collects dashboard data and raises update events.
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

                    // Raise event on main thread
                    // Note: This will be marshaled by MAUI when DataUpdated is called from UI context
                    DataUpdated?.Invoke(this, new DashboardDataUpdatedEventArgs(data, data.CollectedAt));

                    if (_configuration.EnableLogging)
                    {
                        _logger.LogDebug("Dashboard data updated at {Timestamp}", data.CollectedAt);
                    }

                    // Wait for next refresh interval
                    await Task.Delay(refreshIntervalMs, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Cancellation requested
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
    /// Collects dashboard data from all available sources.
    /// This is the main data aggregation method.
    /// </summary>
    private async Task<DashboardData> CollectDashboardDataAsync(CancellationToken cancellationToken)
    {
        var data = new DashboardData
        {
            CollectedAt = DateTimeOffset.UtcNow
        };

        // Collect hardware status (always available)
        data.Hardware = CollectHardwareStatus();

        // Collect queue status (placeholder - will integrate with actual queue service)
        data.Queue = await CollectQueueStatusAsync(cancellationToken).ConfigureAwait(false);

        // Collect indexing status (placeholder - will integrate with actual indexing service)
        data.Indexing = await CollectIndexingStatusAsync(cancellationToken).ConfigureAwait(false);

        // Collect agent status (placeholder - will integrate with actual agent service)
        data.Agent = await CollectAgentStatusAsync(cancellationToken).ConfigureAwait(false);

        // Collect system resources (placeholder - will integrate with performance counters)
        data.SystemResources = CollectSystemResourceMetrics();

        return data;
    }

    private HardwareStatus CollectHardwareStatus()
    {
        return new HardwareStatus
        {
            OverallStatus = "System Ready",
            NpuStatus = "Detection pending (integration forthcoming)",
            GpuStatus = "Detection pending (integration forthcoming)",
            CpuStatus = "Available",
            PlatformInfo = "Windows 11"
        };
    }

    private async Task<QueueStatus> CollectQueueStatusAsync(CancellationToken cancellationToken)
    {
        // Placeholder implementation
        // Will integrate with IModelQueue service when available
        await Task.CompletedTask;
        
        return new QueueStatus
        {
            PendingCount = 0,
            CompletedCount = 0,
            CurrentModel = null,
            TopItems = []
        };
    }

    private async Task<IndexingStatus> CollectIndexingStatusAsync(CancellationToken cancellationToken)
    {
        // Placeholder implementation
        // Will integrate with IKnowledgeFileOrchestrationService when available
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

    private async Task<AgentStatus> CollectAgentStatusAsync(CancellationToken cancellationToken)
    {
        // Placeholder implementation
        // Will integrate with agent orchestration services when available
        await Task.CompletedTask;
        
        return new AgentStatus
        {
            ActiveAgentCount = 0,
            TotalIterations = 0,
            TotalTokensUsed = 0,
            Activities = []
        };
    }

    private SystemResourceMetrics CollectSystemResourceMetrics()
    {
        // Placeholder implementation
        // Will integrate with Windows Performance Counters for real metrics
        return new SystemResourceMetrics
        {
            CpuUtilizationPercent = 0,
            MemoryAvailablePercent = 100,
            GpuUtilizationPercent = null,
            NpuUtilizationPercent = null,
            AvailableDiskBytes = 0,
            TotalDiskBytes = 1,
            ProcessMemoryBytes = 0
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
        // Note: Don't dispose _monitoringTask - it will be cancelled by CTS cancellation
        // Disposing a task that hasn't completed will throw InvalidOperationException

        _disposed = true;
    }
}
