namespace Daiv3.App.Maui.Services;

using Daiv3.App.Maui.Models;

/// <summary>
/// Service for collecting and aggregating system infrastructure metrics for the System Admin Dashboard.
/// Implements CT-REQ-010: System Admin Dashboard.
/// </summary>
public interface IAdminDashboardService
{
    /// <summary>
    /// Collects current system metrics snapshot.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Complete metrics snapshot.</returns>
    Task<AdminDashboardMetrics> GetMetricsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current alert state based on configured thresholds.
    /// </summary>
    /// <returns>Alert state.</returns>
    DashboardAlerts GetAlerts();

    /// <summary>
    /// Gets historical metrics for trend analysis.
    /// </summary>
    /// <param name="hoursBack">Number of hours of history to return.</param>
    /// <returns>Historical metrics snapshots.</returns>
    IReadOnlyList<SystemMetricsSnapshot> GetMetricsHistory(int hoursBack = 24);

    /// <summary>
    /// Starts background polling of metrics.
    /// </summary>
    /// <param name="refreshIntervalSeconds">Refresh interval in seconds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StartMetricsPollingAsync(int refreshIntervalSeconds = 3, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops background polling of metrics.
    /// </summary>
    Task StopMetricsPollingAsync();

    /// <summary>
    /// Event fired when new metrics are available.
    /// </summary>
    event EventHandler<AdminDashboardMetrics>? MetricsUpdated;

    /// <summary>
    /// Event fired when an alert condition is triggered.
    /// </summary>
    event EventHandler<DashboardAlerts>? AlertsChanged;
}
