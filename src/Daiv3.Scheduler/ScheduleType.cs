namespace Daiv3.Scheduler;

/// <summary>
/// Represents the type of scheduling used for a job.
/// </summary>
public enum ScheduleType
{
    /// <summary>
    /// The job runs once immediately (or after a delay).
    /// </summary>
    Immediate = 0,

    /// <summary>
    /// The job runs once at a specific time.
    /// </summary>
    OneTime = 1,

    /// <summary>
    /// The job runs repeatedly at regular intervals.
    /// </summary>
    Recurring = 2
}
