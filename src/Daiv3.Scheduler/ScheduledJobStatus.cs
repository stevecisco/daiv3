namespace Daiv3.Scheduler;

/// <summary>
/// Represents the execution status of a scheduled job.
/// </summary>
public enum ScheduledJobStatus
{
    /// <summary>
    /// The job has been scheduled but has not yet been executed.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// The job is currently executing.
    /// </summary>
    Running = 1,

    /// <summary>
    /// The job has completed successfully.
    /// </summary>
    Completed = 2,

    /// <summary>
    /// The job execution failed.
    /// </summary>
    Failed = 3,

    /// <summary>
    /// The job was cancelled before execution.
    /// </summary>
    Cancelled = 4,

    /// <summary>
    /// The job is scheduled to run at a future time (not yet in pending queue).
    /// </summary>
    Scheduled = 5
}
