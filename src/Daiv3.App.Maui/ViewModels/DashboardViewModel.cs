using Microsoft.Extensions.Logging;

namespace Daiv3.App.Maui.ViewModels;

/// <summary>
/// ViewModel for the Status Dashboard.
/// Displays system status, hardware info, model queue status, and recent activity.
/// </summary>
public class DashboardViewModel : BaseViewModel
{
    private readonly ILogger<DashboardViewModel> _logger;
    private string _hardwareStatus = "Detecting...";
    private string _npuStatus = "Unknown";
    private string _gpuStatus = "Unknown";
    private int _queuedTasks;
    private int _completedTasks;
    private string _currentActivity = "Idle";

    public DashboardViewModel(ILogger<DashboardViewModel> logger)
    {
        _logger = logger;
        Title = "Dashboard";
        _logger.LogInformation("DashboardViewModel initialized");

        // Initialize with placeholder data
        LoadDashboardData();
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

    private void LoadDashboardData()
    {
        IsBusy = true;

        // TODO: Integrate with hardware detection and model queue services
        Task.Run(async () =>
        {
            await Task.Delay(500); // Simulate data loading

            MainThread.BeginInvokeOnMainThread(() =>
            {
                HardwareStatus = "System Ready";
                NpuStatus = "Available (NPU detection pending)";
                GpuStatus = "Available (GPU detection pending)";
                QueuedTasks = 0;
                CompletedTasks = 0;
                CurrentActivity = "Ready for tasks";
                IsBusy = false;

                _logger.LogInformation("Dashboard data loaded");
            });
        });
    }

    /// <summary>
    /// Refreshes the dashboard data.
    /// </summary>
    public void Refresh()
    {
        _logger.LogInformation("Refreshing dashboard");
        LoadDashboardData();
    }
}
