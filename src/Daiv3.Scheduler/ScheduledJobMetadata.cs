namespace Daiv3.Scheduler;

/// <summary>
/// Metadata about a scheduled job, including execution history and status information.
/// </summary>
public class ScheduledJobMetadata
{
    /// <summary>
    /// Gets the unique identifier for this scheduled job.
    /// </summary>
    public required string JobId { get; init; }

    /// <summary>
    /// Gets the name of the job.
    /// </summary>
    public required string JobName { get; init; }

    /// <summary>
    /// Gets the current status of the job.
    /// </summary>
    public required ScheduledJobStatus Status { get; set; }

    /// <summary>
    /// Gets the type of scheduling (immediate, one-time, recurring).
    /// </summary>
    public required ScheduleType ScheduleType { get; init; }

    /// <summary>
    /// Gets the time when the job was created in UTC.
    /// </summary>
    public DateTime CreatedAtUtc { get; init; }

    /// <summary>
    /// Gets the time when the job is scheduled to run (for one-time jobs) or next run (for recurring jobs) in UTC.
    /// </summary>
    public DateTime? ScheduledAtUtc { get; set; }

    /// <summary>
    /// Gets the time when the job last started execution in UTC, or null if never executed.
    /// </summary>
    public DateTime? LastStartedAtUtc { get; set; }

    /// <summary>
    /// Gets the time when the job last completed execution in UTC, or null if never executed.
    /// </summary>
    public DateTime? LastCompletedAtUtc { get; set; }

    /// <summary>
    /// Gets the duration of the last execution, or null if never executed.
    /// </summary>
    public TimeSpan? LastExecutionDuration { get; set; }

    /// <summary>
    /// Gets the number of times this job has been executed.
    /// </summary>
    public int ExecutionCount { get; set; }

    /// <summary>
    /// Gets the last error message if the job failed, or null if no errors.
    /// </summary>
    public string? LastErrorMessage { get; set; }

    /// <summary>
    /// Gets the recurrence interval in seconds for recurring jobs, or null for one-time jobs.
    /// </summary>
    public uint? IntervalSeconds { get; init; }

    /// <summary>
    /// Gets the cron expression for cron-based jobs, or null for non-cron jobs.
    /// </summary>
    public string? CronExpression { get; init; }

    /// <summary>
    /// Gets the event type for event-triggered jobs, or null for non-event jobs.
    /// </summary>
    public string? EventType { get; init; }

    /// <summary>
    /// Gets additional metadata about the job as a read-only dictionary.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }
}
