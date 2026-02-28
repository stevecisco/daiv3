namespace Daiv3.Scheduler;

/// <summary>
/// Represents a request to modify a scheduled job's schedule parameters.
/// </summary>
public class ScheduleModificationRequest
{
    /// <summary>
    /// Gets or sets the new scheduled time for one-time jobs (must be UTC).
    /// </summary>
    public DateTime? ScheduledAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the new interval in seconds for recurring jobs.
    /// </summary>
    public uint? IntervalSeconds { get; set; }

    /// <summary>
    /// Gets or sets the new cron expression for cron-based jobs.
    /// </summary>
    public string? CronExpression { get; set; }

    /// <summary>
    /// Gets or sets the new event type for event-triggered jobs.
    /// </summary>
    public string? EventType { get; set; }
}
