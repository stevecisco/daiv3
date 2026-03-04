using Daiv3.App.Maui.Models;

namespace Daiv3.App.Maui.Services;

/// <summary>
/// Service for aggregating and providing real-time dashboard telemetry data.
/// Implements CT-REQ-003: The system SHALL provide a real-time transparency dashboard.
/// Provides a single interface for collecting metrics from various system components.
/// </summary>
public interface IDashboardService
{
    /// <summary>
    /// Gets the current dashboard data snapshot.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Dashboard data containing all current metrics.</returns>
    Task<DashboardData> GetDashboardDataAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts continuous monitoring with periodic updates.
    /// Notifies via the <see cref="DataUpdated"/> event when new data is available.
    /// </summary>
    /// <param name="refreshIntervalMs">Interval in milliseconds between updates (default 3000ms).</param>
    /// <param name="cancellationToken">Cancellation token to stop monitoring.</param>
    /// <returns>Completed task when monitoring is started or if already running.</returns>
    Task StartMonitoringAsync(int refreshIntervalMs = 3000, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops continuous monitoring if it was previously started.
    /// </summary>
    /// <returns>Completed task when monitoring has stopped.</returns>
    Task StopMonitoringAsync();

    /// <summary>
    /// Gets whether continuous monitoring is currently active.
    /// </summary>
    bool IsMonitoring { get; }

    /// <summary>
    /// Event raised when dashboard data has been updated during monitoring.
    /// </summary>
    event EventHandler<DashboardDataUpdatedEventArgs>? DataUpdated;

    /// <summary>
    /// Gets configuration for dashboard behavior.
    /// </summary>
    DashboardConfiguration Configuration { get; }
}

/// <summary>
/// Event arguments for dashboard data updates.
/// </summary>
public class DashboardDataUpdatedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DashboardDataUpdatedEventArgs"/> class.
    /// </summary>
    /// <param name="data">The updated dashboard data.</param>
    /// <param name="timestamp">The timestamp of the update.</param>
    public DashboardDataUpdatedEventArgs(DashboardData data, DateTimeOffset timestamp)
    {
        Data = data;
        Timestamp = timestamp;
    }

    /// <summary>
    /// Gets the updated dashboard data.
    /// </summary>
    public DashboardData Data { get; }

    /// <summary>
    /// Gets the timestamp of when this update occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; }
}

/// <summary>
/// Configuration for dashboard service behavior.
/// Implements CT-DATA-001: Settings SHALL be versioned to support upgrades.
/// </summary>
public class DashboardConfiguration
{
    /// <summary>
    /// Default refresh interval in milliseconds.
    /// </summary>
    public const int DefaultRefreshIntervalMs = 3000;

    /// <summary>
    /// Minimum refresh interval to prevent excessive updates.
    /// </summary>
    public const int MinRefreshIntervalMs = 500;

    /// <summary>
    /// Maximum refresh interval.
    /// </summary>
    public const int MaxRefreshIntervalMs = 60000;

    /// <summary>
    /// Gets or sets the default refresh interval for monitoring (milliseconds).
    /// </summary>
    public int RefreshIntervalMs { get; set; } = DefaultRefreshIntervalMs;

    /// <summary>
    /// Gets or sets whether to enable caching of dashboard data between updates.
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to log update operations for debugging.
    /// </summary>
    public bool EnableLogging { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to continue monitoring on error or stop.
    /// </summary>
    public bool ContinueOnError { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum duration for a single dashboard data collection (timeout).
    /// </summary>
    public int DataCollectionTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Validates the configuration values.
    /// </summary>
    /// <returns>True if configuration is valid; throws on validation failure.</returns>
    public bool Validate()
    {
        if (RefreshIntervalMs < MinRefreshIntervalMs || RefreshIntervalMs > MaxRefreshIntervalMs)
            throw new ArgumentOutOfRangeException(
                nameof(RefreshIntervalMs),
                $"Refresh interval must be between {MinRefreshIntervalMs} and {MaxRefreshIntervalMs}ms");

        if (DataCollectionTimeoutMs <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(DataCollectionTimeoutMs),
                "Data collection timeout must be positive");

        return true;
    }
}
