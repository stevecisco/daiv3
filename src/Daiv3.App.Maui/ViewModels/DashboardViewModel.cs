using Microsoft.Extensions.Logging;
using Daiv3.App.Maui.Models;
using Daiv3.App.Maui.Services;

namespace Daiv3.App.Maui.ViewModels;

/// <summary>
/// ViewModel for the Status Dashboard.
/// Implements CT-REQ-003: Real-time transparency dashboard.
/// Implements CT-REQ-006: Agent activity, iterations, token usage, and system resource metrics.
/// Implements CT-REQ-007: Online token usage and budget status display.
/// Implements CT-NFR-001: Async/await patterns with debouncing for UI responsiveness.
/// </summary>
public sealed class DashboardViewModel : BaseViewModel, IAsyncDisposable
{
    private readonly ILogger<DashboardViewModel> _logger;
    private readonly IDashboardService _dashboardService;
    private CancellationTokenSource? _viewLifetimeCts;

    // ── Hardware / Core Status ────────────────────────────────────────
    private string _hardwareStatus = "Detecting...";
    private string _npuStatus = "Unknown";
    private string _gpuStatus = "Unknown";

    // ── Queue (CT-REQ-004) ────────────────────────────────────────────
    private int _queuedTasks;
    private int _completedTasks;
    private string? _currentModel;
    private List<QueueItemSummary> _topQueueItems = [];
    private double? _averageWaitTimeSeconds;
    private double? _throughputPerMinute;
    private int? _modelUtilizationPercent;
    private string? _selectedProjectFilter;

    // ── Shared Status ─────────────────────────────────────────────────
    private string _currentActivity = "Initializing...";
    private string _lastUpdateTime = "Never";
    private bool _isMonitoring;

    // ── Agent Activity (CT-REQ-006) ───────────────────────────────────
    private int _activeAgentCount;
    private int _totalSessionIterations;
    private long _totalSessionTokensUsed;
    private List<IndividualAgentActivity> _agentActivities = [];

    // ── System Resources (CT-REQ-006) ─────────────────────────────────
    private double _cpuUtilizationPercent;
    private double _cpuProgressValue;           // 0-1 for ProgressBar
    private double _memoryUtilizationPercent;
    private double _memoryProgressValue;        // 0-1 for ProgressBar
    private string _memoryUsageText = "– / – GB";
    private double _diskProgressValue;          // 0-1 for ProgressBar
    private string _diskFreeText = "– GB free";
    private string _processMemoryText = "– MB";
    private string _executionProviderText = "CPU";
    private int _cpuCoreCount;

    // ── Resource Alerts (CT-REQ-006) ──────────────────────────────────
    private bool _hasAnyAlert;
    private bool _hasHighCpuAlert;
    private bool _hasHighMemoryAlert;
    private bool _hasLowDiskAlert;
    private List<string> _alertMessages = [];

    // ── View Toggle ───────────────────────────────────────────────────
    private bool _showAgentView = true;
    private bool _showSystemView;

    private Command? _showAgentViewCommand;
    private Command? _showSystemViewCommand;

    public DashboardViewModel(
        ILogger<DashboardViewModel> logger,
        IDashboardService dashboardService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dashboardService = dashboardService ?? throw new ArgumentNullException(nameof(dashboardService));
        Title = "Dashboard";
        _logger.LogInformation("DashboardViewModel initialized");
    }

    // ── Hardware Status Properties ────────────────────────────────────

    public string HardwareStatus
    {
        get => _hardwareStatus;
        set => SetProperty(ref _hardwareStatus, value);
    }

    public string NpuStatus
    {
        get => _npuStatus;
        set => SetProperty(ref _npuStatus, value);
    }

    public string GpuStatus
    {
        get => _gpuStatus;
        set => SetProperty(ref _gpuStatus, value);
    }

    // ── Queue Properties (CT-REQ-004) ─────────────────────────────────

    public int QueuedTasks
    {
        get => _queuedTasks;
        set => SetProperty(ref _queuedTasks, value);
    }

    public int CompletedTasks
    {
        get => _completedTasks;
        set => SetProperty(ref _completedTasks, value);
    }

    public string? CurrentModel
    {
        get => _currentModel;
        set => SetProperty(ref _currentModel, value);
    }

    public List<QueueItemSummary> TopQueueItems
    {
        get => _topQueueItems;
        set => SetProperty(ref _topQueueItems, value);
    }

    public double? AverageWaitTimeSeconds
    {
        get => _averageWaitTimeSeconds;
        set => SetProperty(ref _averageWaitTimeSeconds, value);
    }

    public double? ThroughputPerMinute
    {
        get => _throughputPerMinute;
        set => SetProperty(ref _throughputPerMinute, value);
    }

    public int? ModelUtilizationPercent
    {
        get => _modelUtilizationPercent;
        set => SetProperty(ref _modelUtilizationPercent, value);
    }

    public string? SelectedProjectFilter
    {
        get => _selectedProjectFilter;
        set => SetProperty(ref _selectedProjectFilter, value);
    }

    public List<QueueItemSummary> FilteredQueueItems
    {
        get
        {
            if (TopQueueItems == null || TopQueueItems.Count == 0)
                return [];

            if (string.IsNullOrEmpty(SelectedProjectFilter))
                return new List<QueueItemSummary>(TopQueueItems);

            return TopQueueItems
                .Where(item => item.ProjectId == SelectedProjectFilter)
                .ToList();
        }
    }

    // ── Shared Status Properties ──────────────────────────────────────

    public string CurrentActivity
    {
        get => _currentActivity;
        set => SetProperty(ref _currentActivity, value);
    }

    public string LastUpdateTime
    {
        get => _lastUpdateTime;
        set => SetProperty(ref _lastUpdateTime, value);
    }

    public bool IsMonitoring
    {
        get => _isMonitoring;
        set => SetProperty(ref _isMonitoring, value);
    }

    // ── Agent Activity Properties (CT-REQ-006) ────────────────────────

    /// <summary>Number of agents currently executing.</summary>
    public int ActiveAgentCount
    {
        get => _activeAgentCount;
        set => SetProperty(ref _activeAgentCount, value);
    }

    /// <summary>Total iterations across all agents in this session.</summary>
    public int TotalSessionIterations
    {
        get => _totalSessionIterations;
        set => SetProperty(ref _totalSessionIterations, value);
    }

    /// <summary>Total tokens used across all agents in this session.</summary>
    public long TotalSessionTokensUsed
    {
        get => _totalSessionTokensUsed;
        set => SetProperty(ref _totalSessionTokensUsed, value);
    }

    /// <summary>Formatted total token count for display.</summary>
    public string TotalSessionTokensText =>
        TotalSessionTokensUsed switch
        {
            >= 1_000_000 => $"{TotalSessionTokensUsed / 1_000_000.0:F1}M tokens",
            >= 1_000 => $"{TotalSessionTokensUsed / 1_000.0:F1}K tokens",
            _ => $"{TotalSessionTokensUsed} tokens"
        };

    /// <summary>Per-agent activity list.</summary>
    public List<IndividualAgentActivity> AgentActivities
    {
        get => _agentActivities;
        set => SetProperty(ref _agentActivities, value);
    }

    /// <summary>Whether any agents are currently executing.</summary>
    public bool HasActiveAgents => _activeAgentCount > 0;

    // ── System Resource Properties (CT-REQ-006) ───────────────────────

    /// <summary>Process CPU utilization % (0-100).</summary>
    public double CpuUtilizationPercent
    {
        get => _cpuUtilizationPercent;
        set => SetProperty(ref _cpuUtilizationPercent, value);
    }

    /// <summary>CPU progress bar value (0-1).</summary>
    public double CpuProgressValue
    {
        get => _cpuProgressValue;
        set => SetProperty(ref _cpuProgressValue, value);
    }

    /// <summary>Memory utilization % (0-100).</summary>
    public double MemoryUtilizationPercent
    {
        get => _memoryUtilizationPercent;
        set => SetProperty(ref _memoryUtilizationPercent, value);
    }

    /// <summary>Memory progress bar value (0-1).</summary>
    public double MemoryProgressValue
    {
        get => _memoryProgressValue;
        set => SetProperty(ref _memoryProgressValue, value);
    }

    /// <summary>Formatted memory usage string, e.g. "12.5 / 32.0 GB".</summary>
    public string MemoryUsageText
    {
        get => _memoryUsageText;
        set => SetProperty(ref _memoryUsageText, value);
    }

    /// <summary>Disk progress bar value (0-1).</summary>
    public double DiskProgressValue
    {
        get => _diskProgressValue;
        set => SetProperty(ref _diskProgressValue, value);
    }

    /// <summary>Formatted disk free space string, e.g. "45.2 GB free".</summary>
    public string DiskFreeText
    {
        get => _diskFreeText;
        set => SetProperty(ref _diskFreeText, value);
    }

    /// <summary>Formatted process working-set memory, e.g. "256 MB".</summary>
    public string ProcessMemoryText
    {
        get => _processMemoryText;
        set => SetProperty(ref _processMemoryText, value);
    }

    /// <summary>Active execution provider label (CPU / GPU / NPU).</summary>
    public string ExecutionProviderText
    {
        get => _executionProviderText;
        set => SetProperty(ref _executionProviderText, value);
    }

    /// <summary>Number of logical CPU cores.</summary>
    public int CpuCoreCount
    {
        get => _cpuCoreCount;
        set => SetProperty(ref _cpuCoreCount, value);
    }

    // ── Resource Alert Properties (CT-REQ-006) ─────────────────────────

    public bool HasAnyAlert
    {
        get => _hasAnyAlert;
        set => SetProperty(ref _hasAnyAlert, value);
    }

    public bool HasHighCpuAlert
    {
        get => _hasHighCpuAlert;
        set => SetProperty(ref _hasHighCpuAlert, value);
    }

    public bool HasHighMemoryAlert
    {
        get => _hasHighMemoryAlert;
        set => SetProperty(ref _hasHighMemoryAlert, value);
    }

    public bool HasLowDiskAlert
    {
        get => _hasLowDiskAlert;
        set => SetProperty(ref _hasLowDiskAlert, value);
    }

    public List<string> AlertMessages
    {
        get => _alertMessages;
        set => SetProperty(ref _alertMessages, value);
    }

    // ── Online Provider Usage Properties (CT-REQ-007) ─────────────────

    private bool _hasOnlineProviders;
    private bool _hasActiveUsage;
    private List<ProviderUsageSummary> _providers = [];
    private long _totalDailyTokens;
    private long _totalMonthlyTokens;
    private bool _hasBudgetAlert;

    /// <summary>Whether any online providers are configured.</summary>
    public bool HasOnlineProviders
    {
        get => _hasOnlineProviders;
        set => SetProperty(ref _hasOnlineProviders, value);
    }

    /// <summary>Whether any online providers have active usage.</summary>
    public bool HasActiveUsage
    {
        get => _hasActiveUsage;
        set => SetProperty(ref _hasActiveUsage, value);
    }

    /// <summary>List of provider usage summaries.</summary>
    public List<ProviderUsageSummary> Providers
    {
        get => _providers;
        set => SetProperty(ref _providers, value);
    }

    /// <summary>Total tokens consumed today across all providers.</summary>
    public long TotalDailyTokens
    {
        get => _totalDailyTokens;
        set => SetProperty(ref _totalDailyTokens, value);
    }

    /// <summary>Total tokens consumed this month across all providers.</summary>
    public long TotalMonthlyTokens
    {
        get => _totalMonthlyTokens;
        set => SetProperty(ref _totalMonthlyTokens, value);
    }

    /// <summary>Whether any provider is near or over budget.</summary>
    public bool HasBudgetAlert
    {
        get => _hasBudgetAlert;
        set => SetProperty(ref _hasBudgetAlert, value);
    }

    /// <summary>Formatted total daily tokens for display.</summary>
    public string TotalDailyTokensText =>
        TotalDailyTokens switch
        {
            >= 1_000_000 => $"{TotalDailyTokens / 1_000_000.0:F1}M",
            >= 1_000 => $"{TotalDailyTokens / 1_000.0:F1}K",
            _ => $"{TotalDailyTokens}"
        };

    /// <summary>Formatted total monthly tokens for display.</summary>
    public string TotalMonthlyTokensText =>
        TotalMonthlyTokens switch
        {
            >= 1_000_000 => $"{TotalMonthlyTokens / 1_000_000.0:F1}M",
            >= 1_000 => $"{TotalMonthlyTokens / 1_000.0:F1}K",
            _ => $"{TotalMonthlyTokens}"
        };

    // ── View Toggle Properties (CT-REQ-006 Dual Layout) ───────────────

    /// <summary>When true, the Agent Activity panel is visible.</summary>
    public bool ShowAgentView
    {
        get => _showAgentView;
        set => SetProperty(ref _showAgentView, value);
    }

    /// <summary>When true, the System Resources panel is visible.</summary>
    public bool ShowSystemView
    {
        get => _showSystemView;
        set => SetProperty(ref _showSystemView, value);
    }

    /// <summary>Switches dashboard to Agent Activity view.</summary>
    public Command ShowAgentViewCommand =>
        _showAgentViewCommand ??= new Command(() =>
        {
            ShowAgentView = true;
            ShowSystemView = false;
        });

    /// <summary>Switches dashboard to System Resources view.</summary>
    public Command ShowSystemViewCommand =>
        _showSystemViewCommand ??= new Command(() =>
        {
            ShowAgentView = false;
            ShowSystemView = true;
        });

    // ── Lifecycle ─────────────────────────────────────────────────────

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
            _viewLifetimeCts?.Dispose();
            _viewLifetimeCts = new CancellationTokenSource();

            await RefreshDashboardDataAsync(_viewLifetimeCts.Token).ConfigureAwait(false);

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await _dashboardService.StartMonitoringAsync(
                    refreshIntervalMs: 3000,
                    cancellationToken: _viewLifetimeCts.Token)
                    .ConfigureAwait(false);
            });

            IsMonitoring = true;
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
            _dashboardService.DataUpdated -= OnDashboardDataUpdated;
            await _dashboardService.StopMonitoringAsync().ConfigureAwait(false);

            IsMonitoring = false;
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

    private async Task RefreshDashboardDataAsync(CancellationToken cancellationToken)
    {
        try
        {
            var data = await _dashboardService.GetDashboardDataAsync(cancellationToken).ConfigureAwait(false);

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
    /// Updates all UI properties from fresh dashboard data.
    /// Always called on the main thread.
    /// </summary>
    private void UpdateUIFromDashboardData(DashboardData data)
    {
        // Hardware
        HardwareStatus = data.Hardware.OverallStatus;
        NpuStatus = data.Hardware.NpuStatus;
        GpuStatus = data.Hardware.GpuStatus;

        // Queue (CT-REQ-004)
        QueuedTasks = data.Queue.PendingCount;
        CompletedTasks = data.Queue.CompletedCount;
        CurrentModel = data.Queue.CurrentModel ?? "Not loaded";
        AverageWaitTimeSeconds = data.Queue.EstimatedWaitSeconds;
        ThroughputPerMinute = data.Queue.ThroughputPerMinute;
        ModelUtilizationPercent = data.Queue.ModelUtilizationPercent;
        TopQueueItems = new List<QueueItemSummary>(data.Queue.TopItems);

        // Agent Activity (CT-REQ-006)
        ActiveAgentCount = data.Agent.ActiveAgentCount;
        TotalSessionIterations = data.Agent.TotalIterations;
        TotalSessionTokensUsed = data.Agent.TotalTokensUsed;
        AgentActivities = new List<IndividualAgentActivity>(data.Agent.Activities);
        OnPropertyChanged(nameof(HasActiveAgents));
        OnPropertyChanged(nameof(TotalSessionTokensText));

        // System Resources (CT-REQ-006)
        var res = data.SystemResources;
        CpuUtilizationPercent = res.CpuUtilizationPercent;
        CpuProgressValue = Math.Max(0.0, Math.Min(1.0, res.CpuUtilizationPercent / 100.0));

        MemoryUtilizationPercent = res.MemoryUtilizationPercent;
        MemoryProgressValue = Math.Max(0.0, Math.Min(1.0, res.MemoryUtilizationPercent / 100.0));
        MemoryUsageText = res.MemoryTotalBytes > 0
            ? $"{BytesToGb(res.MemoryUsedBytes):F1} / {BytesToGb(res.MemoryTotalBytes):F1} GB"
            : "– / – GB";

        var diskUsed = res.TotalDiskBytes - res.AvailableDiskBytes;
        DiskProgressValue = res.TotalDiskBytes > 0
            ? Math.Max(0.0, Math.Min(1.0, (double)diskUsed / res.TotalDiskBytes))
            : 0.0;
        DiskFreeText = res.AvailableDiskBytes > 0
            ? $"{BytesToGb(res.AvailableDiskBytes):F1} GB free"
            : "– GB free";

        ProcessMemoryText = res.ProcessMemoryBytes > 0
            ? $"{res.ProcessMemoryBytes / (1024.0 * 1024):F0} MB"
            : "– MB";

        ExecutionProviderText = res.ActiveExecutionProvider;
        CpuCoreCount = res.CpuCoreCount;

        // Resource Alerts (CT-REQ-006)
        HasAnyAlert = data.Alerts.HasAnyAlert;
        HasHighCpuAlert = data.Alerts.HasHighCpuAlert;
        HasHighMemoryAlert = data.Alerts.HasHighMemoryAlert;
        HasLowDiskAlert = data.Alerts.HasLowDiskAlert;
        AlertMessages = new List<string>(data.Alerts.AlertMessages);

        // Online Provider Usage (CT-REQ-007)
        HasOnlineProviders = data.OnlineUsage.HasOnlineProviders;
        HasActiveUsage = data.OnlineUsage.HasActiveUsage;
        Providers = new List<ProviderUsageSummary>(data.OnlineUsage.Providers);
        TotalDailyTokens = data.OnlineUsage.TotalDailyTokens;
        TotalMonthlyTokens = data.OnlineUsage.TotalMonthlyTokens;
        HasBudgetAlert = data.OnlineUsage.HasBudgetAlert;
        OnPropertyChanged(nameof(TotalDailyTokensText));
        OnPropertyChanged(nameof(TotalMonthlyTokensText));

        // Status
        CurrentActivity = data.IsValid ? "Monitoring active" : $"Error: {data.CollectionError}";
        LastUpdateTime = data.CollectedAt.ToLocalTime().ToString("h:mm:ss tt");

        _logger.LogDebug("Dashboard UI updated from gathered telemetry");
    }

    /// <summary>Manual refresh command for user-initiated refresh.</summary>
    public async Task ManualRefreshAsync()
    {
        if (IsBusy || _viewLifetimeCts?.Token.IsCancellationRequested != false)
            return;

        _logger.LogInformation("Manual dashboard refresh requested");
        await RefreshDashboardDataAsync(_viewLifetimeCts.Token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await ShutdownAsync().ConfigureAwait(false);
    }

    private static double BytesToGb(long bytes) => bytes / (1024.0 * 1024 * 1024);
}
