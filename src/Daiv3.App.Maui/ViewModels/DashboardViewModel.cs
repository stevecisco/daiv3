using Microsoft.Extensions.Logging;
using Daiv3.App.Maui.Models;
using Daiv3.App.Maui.Services;

namespace Daiv3.App.Maui.ViewModels;

/// <summary>
/// ViewModel for the Status Dashboard.
/// Implements CT-REQ-003: Real-time transparency dashboard.
/// Implements CT-NFR-001: Async/await patterns with debouncing for UI responsiveness.
/// Displays system status, hardware info, model queue status, and agent activity with real-time updates.
/// </summary>
public sealed class DashboardViewModel : BaseViewModel, IAsyncDisposable
{
    private readonly ILogger<DashboardViewModel> _logger;
    private readonly IDashboardService _dashboardService;
    private CancellationTokenSource? _viewLifetimeCts;
    private string _hardwareStatus = "Detecting...";
    private string _npuStatus = "Unknown";
    private string _gpuStatus = "Unknown";
    private int _queuedTasks;
    private int _completedTasks;
    private string _currentActivity = "Initializing...";
    private string _lastUpdateTime = "Never";
    private bool _isMonitoring;
    private string? _currentModel;
    private List<QueueItemSummary> _topQueueItems = [];
    private double? _averageWaitTimeSeconds;
    private double? _throughputPerMinute;
    private int? _modelUtilizationPercent;
    private string? _selectedProjectFilter;

    public DashboardViewModel(
        ILogger<DashboardViewModel> logger,
        IDashboardService dashboardService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dashboardService = dashboardService ?? throw new ArgumentNullException(nameof(dashboardService));
        Title = "Dashboard";
        _logger.LogInformation("DashboardViewModel initialized");
    }

    /// <summary>
    /// Gets or sets the overall hardware status.
    /// </summary>
    public string HardwareStatus
    {
        get => _hardwareStatus;
        set => SetProperty(ref _hardwareStatus, value);
    }

    /// <summary>
    /// Gets or sets the NPU status (Available, In Use, Not Available).
    /// </summary>
    public string NpuStatus
    {
        get => _npuStatus;
        set => SetProperty(ref _npuStatus, value);
    }

    /// <summary>
    /// Gets or sets the GPU status.
    /// </summary>
    public string GpuStatus
    {
        get => _gpuStatus;
        set => SetProperty(ref _gpuStatus, value);
    }

    /// <summary>
    /// Gets or sets the number of tasks in the queue.
    /// </summary>
    public int QueuedTasks
    {
        get => _queuedTasks;
        set => SetProperty(ref _queuedTasks, value);
    }

    /// <summary>
    /// Gets or sets the number of completed tasks.
    /// </summary>
    public int CompletedTasks
    {
        get => _completedTasks;
        set => SetProperty(ref _completedTasks, value);
    }

    /// <summary>
    /// Gets or sets the current activity description.
    /// </summary>
    public string CurrentActivity
    {
        get => _currentActivity;
        set => SetProperty(ref _currentActivity, value);
    }

    /// <summary>
    /// Gets or sets the formatted timestamp of the last data update.
    /// </summary>
    public string LastUpdateTime
    {
        get => _lastUpdateTime;
        set => SetProperty(ref _lastUpdateTime, value);
    }

    /// <summary>
    /// Gets or sets whether the dashboard is actively monitoring for updates.
    /// </summary>
    public bool IsMonitoring
    {
        get => _isMonitoring;
        set => SetProperty(ref _isMonitoring, value);
    }

    /// <summary>
    /// Gets or sets the currently loaded model name (CT-REQ-004).
    /// </summary>
    public string? CurrentModel
    {
        get => _currentModel;
        set => SetProperty(ref _currentModel, value);
    }

    /// <summary>
    /// Gets or sets the top queued items (CT-REQ-004: top 3 items).
    /// </summary>
    public List<QueueItemSummary> TopQueueItems
    {
        get => _topQueueItems;
        set => SetProperty(ref _topQueueItems, value);
    }

    /// <summary>
    /// Gets or sets the average wait time for queue items in seconds (CT-REQ-004).
    /// </summary>
    public double? AverageWaitTimeSeconds
    {
        get => _averageWaitTimeSeconds;
        set => SetProperty(ref _averageWaitTimeSeconds, value);
    }

    /// <summary>
    /// Gets or sets the queue throughput (requests per minute) (CT-REQ-004).
    /// </summary>
    public double? ThroughputPerMinute
    {
        get => _throughputPerMinute;
        set => SetProperty(ref _throughputPerMinute, value);
    }

    /// <summary>
    /// Gets or sets the model utilization percentage (CT-REQ-004).
    /// </summary>
    public int? ModelUtilizationPercent
    {
        get => _modelUtilizationPercent;
        set => SetProperty(ref _modelUtilizationPercent, value);
    }

    /// <summary>
    /// Gets or sets the selected project filter for queue view (CT-REQ-004: per-project filtering).
    /// Null means show all projects.
    /// </summary>
    public string? SelectedProjectFilter
    {
        get => _selectedProjectFilter;
        set => SetProperty(ref _selectedProjectFilter, value);
    }

    /// <summary>
    /// Gets filtered queue items for the selected project.
    /// </summary>
    public List<QueueItemSummary> FilteredQueueItems
    {
        get
        {
            // This would normally be a cached property but for simplicity we compute on demand
            // In production, consider making this a backing field with notifications
            if (TopQueueItems == null || TopQueueItems.Count == 0)
                return [];
            
            if (string.IsNullOrEmpty(SelectedProjectFilter))
                return new List<QueueItemSummary>(TopQueueItems);
            
            return TopQueueItems
                .Where(item => item.ProjectId == SelectedProjectFilter)
                .ToList();
        }
    }

    /// <summary>
    /// Initializes monitoring and loads initial dashboard data.
    /// Called when the view becomes visible.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (IsBusy)
            return;

        _logger.LogInformation("Initializing dashboard");
        IsBusy = true;

        try
        {
            // Create cancellation token for this view's lifetime
            _viewLifetimeCts?.Dispose();
            _viewLifetimeCts = new CancellationTokenSource();

            // Load initial data
            await RefreshDashboardDataAsync(_viewLifetimeCts.Token).ConfigureAwait(false);

            // Start continuous monitoring
            // Use MainThread to ensure UI updates are marshaled safely
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await _dashboardService.StartMonitoringAsync(
                    refreshIntervalMs: 3000,
                    cancellationToken: _viewLifetimeCts.Token)
                    .ConfigureAwait(false);
            });

            IsMonitoring = true;

            // Subscribe to updates
            _dashboardService.DataUpdated += OnDashboardDataUpdated;

            _logger.LogInformation("Dashboard monitoring started");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing dashboard");
            CurrentActivity = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Stops monitoring and cleans up resources.
    /// Called when the view is destroyed.
    /// </summary>
    public async Task ShutdownAsync()
    {
        _logger.LogInformation("Shutting down dashboard");

        try
        {
            // Unsubscribe from updates
            _dashboardService.DataUpdated -= OnDashboardDataUpdated;

            // Stop monitoring
            await _dashboardService.StopMonitoringAsync().ConfigureAwait(false);

            IsMonitoring = false;

            // Cancel any pending operations
            _viewLifetimeCts?.Cancel();
            _viewLifetimeCts?.Dispose();
            _viewLifetimeCts = null;

            _logger.LogInformation("Dashboard shutdown complete");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error shutting down dashboard");
        }
    }

    /// <summary>
    /// Refreshes dashboard data once from the service.
    /// Used for initial load or manual refresh.
    /// </summary>
    private async Task RefreshDashboardDataAsync(CancellationToken cancellationToken)
    {
        try
        {
            var data = await _dashboardService.GetDashboardDataAsync(cancellationToken).ConfigureAwait(false);
            
            // Marshal UI updates to main thread (CT-NFR-001)
            MainThread.BeginInvokeOnMainThread(() =>
            {
                UpdateUIFromDashboardData(data);
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Dashboard refresh cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing dashboard data");
            CurrentActivity = $"Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Called when dashboard service provides updated data.
    /// This is the main event handler for data updates.
    /// Will be called from the background monitoring thread, so we must marshal to main thread.
    /// </summary>
    private void OnDashboardDataUpdated(object? sender, DashboardDataUpdatedEventArgs e)
    {
        // Marshal to main thread for UI safety (CT-NFR-001)
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                UpdateUIFromDashboardData(e.Data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing dashboard update");
            }
        });
    }

    /// <summary>
    /// Updates all UI properties from dashboard data.
    /// This method is always called on the main thread.
    /// </summary>
    private void UpdateUIFromDashboardData(DashboardData data)
    {
        // Update hardware status
        HardwareStatus = data.Hardware.OverallStatus;
        NpuStatus = data.Hardware.NpuStatus;
        GpuStatus = data.Hardware.GpuStatus;

        // Update queue status (CT-REQ-003 and CT-REQ-004)
        QueuedTasks = data.Queue.PendingCount;
        CompletedTasks = data.Queue.CompletedCount;
        CurrentModel = data.Queue.CurrentModel ?? "Not loaded";
        AverageWaitTimeSeconds = data.Queue.EstimatedWaitSeconds;
        ThroughputPerMinute = data.Queue.ThroughputPerMinute;
        ModelUtilizationPercent = data.Queue.ModelUtilizationPercent;
        
        // Update top queue items (CT-REQ-004: top 3 items highlighted)
        TopQueueItems = new List<QueueItemSummary>(data.Queue.TopItems);

        // Update activity and timestamp
        CurrentActivity = data.IsValid ? "Monitoring active" : $"Error: {data.CollectionError}";
        LastUpdateTime = data.CollectedAt.ToString("HH:mm:ss");

        _logger.LogDebug("Dashboard UI updated from gathered telemetry");
    }

    /// <summary>
    /// Manual refresh command for user-initiated refresh.
    /// </summary>
    public async Task ManualRefreshAsync()
    {
        if (IsBusy || _viewLifetimeCts?.Token.IsCancellationRequested != false)
            return;

        _logger.LogInformation("Manual dashboard refresh requested");
        await RefreshDashboardDataAsync(_viewLifetimeCts.Token).ConfigureAwait(false);
    }

    /// <summary>
    /// Implements IAsyncDisposable for proper resource cleanup.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await ShutdownAsync().ConfigureAwait(false);
    }
}
