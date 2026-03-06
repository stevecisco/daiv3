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
        set
        {
            if (SetProperty(ref _isPolling, value))
            {
                OnPropertyChanged(nameof(PollingStatusText));
            }
        }
    }

    public string PollingStatusText => IsPolling ? "Polling" : "Idle";

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

            _cancellationTokenSource?.Dispose();
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

    // ── Computed Display Properties ───────────────────────────────────

    /// <summary>
    /// CPU utilization percentage formatted for display.
    /// </summary>
    public string CpuPercentText => CurrentMetrics?.Cpu.OverallUtilizationPercent.ToString("F1") is string s ? $"{s}%" : "0%";

    /// <summary>
    /// CPU progress (0-1) for ProgressBar binding.
    /// </summary>
    public double CpuProgress => CurrentMetrics?.Cpu.OverallUtilizationPercent / 100.0 ?? 0;

    /// <summary>
    /// GPU utilization percentage formatted for display.
    /// </summary>
    public string GpuPercentText => CurrentMetrics?.Gpu.UtilizationPercent.ToString("F1") is string s ? $"{s}%" : "0%";

    /// <summary>
    /// GPU progress (0-1) for ProgressBar binding.
    /// </summary>
    public double GpuProgress => CurrentMetrics?.Gpu.UtilizationPercent / 100.0 ?? 0;

    /// <summary>
    /// Memory utilization percentage formatted for display.
    /// </summary>
    public string MemoryPercentText
    {
        get
        {
            if (CurrentMetrics?.Memory is null) return "0%";
            var percent = (CurrentMetrics.Memory.PhysicalRamUsedBytes * 100.0) / CurrentMetrics.Memory.PhysicalRamTotalBytes;
            return $"{percent:F1}%";
        }
    }

    /// <summary>
    /// Memory progress (0-1) for ProgressBar binding.
    /// </summary>
    public double MemoryProgress
    {
        get
        {
            if (CurrentMetrics?.Memory is null) return 0;
            return (CurrentMetrics.Memory.PhysicalRamUsedBytes / (double)CurrentMetrics.Memory.PhysicalRamTotalBytes);
        }
    }

    /// <summary>
    /// Used memory formatted for display.
    /// </summary>
    public string MemoryUsedText
    {
        get
        {
            if (CurrentMetrics?.Memory is null) return "0 MB";
            var mb = CurrentMetrics.Memory.PhysicalRamUsedBytes / (1024.0 * 1024.0);
            return $"{mb:F1} MB";
        }
    }

    /// <summary>
    /// Total memory formatted for display.
    /// </summary>
    public string MemoryTotalText
    {
        get
        {
            if (CurrentMetrics?.Memory is null) return "0 MB";
            var mb = CurrentMetrics.Memory.PhysicalRamTotalBytes / (1024.0 * 1024.0);
            return $"{mb:F1} MB";
        }
    }

    /// <summary>
    /// Disk utilization percentage formatted for display.
    /// </summary>
    public string DiskPercentText
    {
        get
        {
            if (CurrentMetrics?.Storage is null) return "0%";
            var used = CurrentMetrics.Storage.TotalDiskSpaceBytes - CurrentMetrics.Storage.AvailableDiskSpaceBytes;
            var percent = (used * 100.0) / CurrentMetrics.Storage.TotalDiskSpaceBytes;
            return $"{percent:F1}%";
        }
    }

    /// <summary>
    /// Disk progress (0-1) for ProgressBar binding.
    /// </summary>
    public double DiskProgress
    {
        get
        {
            if (CurrentMetrics?.Storage is null) return 0;
            var used = CurrentMetrics.Storage.TotalDiskSpaceBytes - CurrentMetrics.Storage.AvailableDiskSpaceBytes;
            return (used / (double)CurrentMetrics.Storage.TotalDiskSpaceBytes);
        }
    }

    /// <summary>
    /// Used disk space formatted for display.
    /// </summary>
    public string DiskUsedText
    {
        get
        {
            if (CurrentMetrics?.Storage is null) return "0 GB";
            var used = CurrentMetrics.Storage.TotalDiskSpaceBytes - CurrentMetrics.Storage.AvailableDiskSpaceBytes;
            var gb = used / (1024.0 * 1024.0 * 1024.0);
            return $"{gb:F2} GB";
        }
    }

    /// <summary>
    /// Total disk space formatted for display.
    /// </summary>
    public string DiskTotalText
    {
        get
        {
            if (CurrentMetrics?.Storage is null) return "0 GB";
            var gb = CurrentMetrics.Storage.TotalDiskSpaceBytes / (1024.0 * 1024.0 * 1024.0);
            return $"{gb:F2} GB";
        }
    }

    /// <summary>
    /// Total tokens used (cumulative across all agents).
    /// </summary>
    public string TotalTokensText
    {
        get
        {
            if (CurrentMetrics?.Agents is null || CurrentMetrics.Agents.Count == 0) return "0";
            var total = CurrentMetrics.Agents.Sum(a => a.TokensUsed);
            return total > 1000000 ? $"{total / 1000000.0:F1}M" : $"{total:N0}";
        }
    }

    /// <summary>
    /// Whether the queue is experiencing bottleneck conditions.
    /// </summary>
    public bool IsQueueBottlenecked => CurrentMetrics?.Queue.IsBottlenecked ?? false;

    /// <summary>
    /// Whether there are active agents.
    /// </summary>
    public bool HasActiveAgents => CurrentMetrics?.Agents?.Count > 0;

    /// <summary>
    /// CPU alert message.
    /// </summary>
    public string CpuAlertText => $"CPU at {CurrentMetrics?.Cpu.OverallUtilizationPercent:F1}% (threshold: >85%)";

    /// <summary>
    /// GPU alert message.
    /// </summary>
    public string GpuAlertText => $"GPU at {CurrentMetrics?.Gpu.UtilizationPercent:F1}% (threshold: >90%)";

    /// <summary>
    /// Memory alert message.
    /// </summary>
    public string MemoryAlertText
    {
        get
        {
            if (CurrentMetrics?.Memory is null) return "Memory unavailable";
            var percent = (CurrentMetrics.Memory.PhysicalRamUsedBytes * 100.0) / CurrentMetrics.Memory.PhysicalRamTotalBytes;
            return $"Memory at {percent:F1}% (threshold: >80%)";
        }
    }

    /// <summary>
    /// Disk alert message.
    /// </summary>
    public string DiskAlertText
    {
        get
        {
            if (CurrentMetrics?.Storage is null) return "Disk unavailable";
            var free = CurrentMetrics.Storage.AvailableDiskSpaceBytes / (1024.0 * 1024.0 * 1024.0);
            return $"Free space at {free:F2} GB (threshold: <1 GB)";
        }
    }

    /// <summary>
    /// Queue alert message.
    /// </summary>
    public string QueueAlertText => $"Queue at {CurrentMetrics?.Queue.TotalQueuedItems} items (threshold: >100)";

    // ── Relay Commands ────────────────────────────────────────────────

    private Command? _refreshCommand;
    private Command? _startPollingCommand;
    private Command? _stopPollingCommand;

    /// <summary>
    /// Command to refresh metrics immediately.
    /// </summary>
    public Command RefreshMetricsCommand => _refreshCommand ??= new Command(async () => await RefreshMetricsAsync());

    /// <summary>
    /// Command to start background polling.
    /// </summary>
    public Command StartPollingCommand => _startPollingCommand ??= new Command(async () => await StartPollingAsync());

    /// <summary>
    /// Command to stop background polling.
    /// </summary>
    public Command StopPollingCommand => _stopPollingCommand ??= new Command(async () => await StopPollingAsync());

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
