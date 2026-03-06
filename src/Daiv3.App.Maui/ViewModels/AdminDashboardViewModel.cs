using Microsoft.Extensions.Logging;
using Daiv3.App.Maui.Models;
using Daiv3.App.Maui.Services;

namespace Daiv3.App.Maui.ViewModels;

/// <summary>
/// ViewModel for the System Admin Dashboard.
/// Implements CT-REQ-010: System Admin Dashboard with real-time infrastructure metrics.
/// </summary>
public sealed class AdminDashboardViewModel : BaseViewModel, IAsyncDisposable
{
    private readonly IAdminDashboardService _adminDashboardService;
    private readonly ILogger<AdminDashboardViewModel> _logger;
    private CancellationTokenSource? _cancellationTokenSource;

    // Metrics properties
    private AdminDashboardMetrics? _currentMetrics;
    private DashboardAlerts? _currentAlerts;

    // UI state properties
    private string _cpuBarColor = "#90EE90"; // Green
    private string _memoryBarColor = "#90EE90";
    private string _gpuBarColor = "#90EE90";
    private string _diskBarColor = "#90EE90";
    private bool _isPolling;
    private int _refreshIntervalSeconds = 3;
    private string _lastUpdatedText = "Never";
    private string? _errorMessage;

    public AdminDashboardViewModel(
        IAdminDashboardService adminDashboardService,
        ILogger<AdminDashboardViewModel> logger)
    {
        _adminDashboardService = adminDashboardService ?? throw new ArgumentNullException(nameof(adminDashboardService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        Title = "System Admin Dashboard";

        // Subscribe to events
        _adminDashboardService.MetricsUpdated += OnMetricsUpdated;
        _adminDashboardService.AlertsChanged += OnAlertsChanged;
    }

    // ── Metrics Properties ────────────────────────────────────────────

    public AdminDashboardMetrics? CurrentMetrics
    {
        get => _currentMetrics;
        set => SetProperty(ref _currentMetrics, value);
    }

    public DashboardAlerts? CurrentAlerts
    {
        get => _currentAlerts;
        set => SetProperty(ref _currentAlerts, value);
    }

    // ── UI State Properties ───────────────────────────────────────────

    public string CpuBarColor
    {
        get => _cpuBarColor;
        set => SetProperty(ref _cpuBarColor, value);
    }

    public string MemoryBarColor
    {
        get => _memoryBarColor;
        set => SetProperty(ref _memoryBarColor, value);
    }

    public string GpuBarColor
    {
        get => _gpuBarColor;
        set => SetProperty(ref _gpuBarColor, value);
    }

    public string DiskBarColor
    {
        get => _diskBarColor;
        set => SetProperty(ref _diskBarColor, value);
    }

    public bool IsPolling
    {
        get => _isPolling;
        set => SetProperty(ref _isPolling, value);
    }

    public int RefreshIntervalSeconds
    {
        get => _refreshIntervalSeconds;
        set => SetProperty(ref _refreshIntervalSeconds, value);
    }

    public string LastUpdatedText
    {
        get => _lastUpdatedText;
        set => SetProperty(ref _lastUpdatedText, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    // ── Commands ──────────────────────────────────────────────────────

    /// <summary>
    /// Loads current metrics.
    /// </summary>
    public async Task RefreshMetricsAsync()
    {
        try
        {
            IsBusy = true;
            ErrorMessage = null;

            var metrics = await _adminDashboardService.GetMetricsAsync();
            CurrentMetrics = metrics;

            if (!metrics.IsHealthy)
            {
                ErrorMessage = metrics.CollectionError;
            }

            UpdateLastUpdatedTime();
            UpdateColorScheme();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh metrics");
            ErrorMessage = $"Failed to load metrics: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Starts background polling of metrics.
    /// </summary>
    public async Task StartPollingAsync()
    {
        try
        {
            IsBusy = true;
            ErrorMessage = null;

            _cancellationTokenSource = new CancellationTokenSource();
            await _adminDashboardService.StartMetricsPollingAsync(RefreshIntervalSeconds, _cancellationTokenSource.Token);
            IsPolling = true;

            // Load initial metrics
            await RefreshMetricsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start metrics polling");
            ErrorMessage = $"Failed to start polling: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Stops background polling of metrics.
    /// </summary>
    public async Task StopPollingAsync()
    {
        try
        {
            IsBusy = true;
            await _adminDashboardService.StopMetricsPollingAsync();
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            IsPolling = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop metrics polling");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Gets historical metrics for trend analysis.
    /// </summary>
    public IReadOnlyList<SystemMetricsSnapshot> GetMetricsHistory(int hoursBack = 24)
    {
        return _adminDashboardService.GetMetricsHistory(hoursBack);
    }

    /// <summary>
    /// Async disposal.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await StopPollingAsync();
        _cancellationTokenSource?.Dispose();
    }

    // ── Private Event Handlers ────────────────────────────────────────

    private void OnMetricsUpdated(object? sender, AdminDashboardMetrics metrics)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            CurrentMetrics = metrics;
            UpdateLastUpdatedTime();
            UpdateColorScheme();
        });
    }

    private void OnAlertsChanged(object? sender, DashboardAlerts alerts)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            CurrentAlerts = alerts;
        });
    }

    private void UpdateColorScheme()
    {
        if (CurrentMetrics == null) return;

        // CPU color scheme (green < 50%, yellow 50-75%, red > 75%)
        CpuBarColor = CurrentMetrics.Cpu.OverallUtilizationPercent switch
        {
            < 50 => "#90EE90", // Green
            < 75 => "#FFD700", // Yellow
            _ => "#FF6B6B"     // Red
        };

        // Memory color scheme
        var memPercent = (CurrentMetrics.Memory.PhysicalRamUsedBytes * 100.0) / CurrentMetrics.Memory.PhysicalRamTotalBytes;
        MemoryBarColor = memPercent switch
        {
            < 50 => "#90EE90",
            < 75 => "#FFD700",
            _ => "#FF6B6B"
        };

        // GPU color scheme
        GpuBarColor = CurrentMetrics.Gpu.UtilizationPercent switch
        {
            < 50 => "#90EE90",
            < 75 => "#FFD700",
            _ => "#FF6B6B"
        };

        // Disk color scheme
        var diskPercent = ((CurrentMetrics.Storage.TotalDiskSpaceBytes - CurrentMetrics.Storage.AvailableDiskSpaceBytes) * 100.0) / CurrentMetrics.Storage.TotalDiskSpaceBytes;
        DiskBarColor = diskPercent switch
        {
            < 80 => "#90EE90",
            < 90 => "#FFD700",
            _ => "#FF6B6B"
        };
    }

    private void UpdateLastUpdatedTime()
    {
        if (CurrentMetrics != null)
        {
            var timeAgo = DateTimeOffset.UtcNow - CurrentMetrics.LastUpdated;
            LastUpdatedText = timeAgo.TotalSeconds < 60
                ? $"{(int)timeAgo.TotalSeconds}s ago"
                : $"{(int)timeAgo.TotalMinutes}m ago";
        }
    }
}
