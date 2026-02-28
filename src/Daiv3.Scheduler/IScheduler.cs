namespace Daiv3.Scheduler;

/// <summary>
/// Defines the contract for background job scheduling within the DAIv3 system.
/// 
/// The scheduler is responsible for:
/// - Registering and managing scheduled jobs
/// - Executing jobs at their scheduled times
/// - Handling job lifecycle (creation, execution, completion, cancellation)
/// - Logging job execution and failures
/// 
/// This is a lightweight custom implementation using IHostedService and System.Threading.Timer,
/// chosen for better control and reduced external dependencies compared to alternatives like Quartz.NET.
/// </summary>
public interface IScheduler
{
    /// <summary>
    /// Schedules a job to run immediately or at a specified delay.
    /// </summary>
    /// <param name="job">The job to schedule.</param>
    /// <param name="delay">The delay before the job should run. If null, runs immediately.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The ID of the scheduled job.</returns>
    Task<string> ScheduleImmediateAsync(
        IScheduledJob job,
        TimeSpan? delay = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Schedules a job to run at a specific time.
    /// </summary>
    /// <param name="job">The job to schedule.</param>
    /// <param name="scheduledTime">The UTC time when the job should run.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The ID of the scheduled job.</returns>
    Task<string> ScheduleAtTimeAsync(
        IScheduledJob job,
        DateTime scheduledTime,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Schedules a job to run on a recurring interval.
    /// </summary>
    /// <param name="job">The job to schedule.</param>
    /// <param name="intervalSeconds">The interval in seconds between job executions.</param>
    /// <param name="delaySeconds">Optional initial delay before first execution in seconds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The ID of the scheduled job.</returns>
    Task<string> ScheduleRecurringAsync(
        IScheduledJob job,
        uint intervalSeconds,
        uint? delaySeconds = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Schedules a job to run based on a cron expression.
    /// </summary>
    /// <param name="job">The job to schedule.</param>
    /// <param name="cronExpression">The cron expression (5 fields: minute hour day month dayOfWeek).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The ID of the scheduled job.</returns>
    /// <exception cref="ArgumentException">If the cron expression is invalid.</exception>
    Task<string> ScheduleCronAsync(
        IScheduledJob job,
        string cronExpression,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Schedules a job to run when a specific event type occurs.
    /// </summary>
    /// <param name="job">The job to schedule.</param>
    /// <param name="eventType">The event type that will trigger this job.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The ID of the scheduled job.</returns>
    Task<string> ScheduleOnEventAsync(
        IScheduledJob job,
        string eventType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Raises an event that may trigger event-based scheduled jobs.
    /// </summary>
    /// <param name="schedulerEvent">The event to raise.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RaiseEventAsync(ISchedulerEvent schedulerEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a scheduled job by ID.
    /// </summary>
    /// <param name="jobId">The ID of the job to cancel.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the job was found and cancelled, false if the job doesn't exist.</returns>
    Task<bool> CancelJobAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the metadata for a scheduled job by ID.
    /// </summary>
    /// <param name="jobId">The ID of the job.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The job metadata if found, null otherwise.</returns>
    Task<ScheduledJobMetadata?> GetJobMetadataAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all scheduled jobs.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of job metadata for all scheduled jobs.</returns>
    Task<IReadOnlyList<ScheduledJobMetadata>> GetAllJobsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all jobs in a specific status.
    /// </summary>
    /// <param name="status">The status to filter by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of job metadata matching the specified status.</returns>
    Task<IReadOnlyList<ScheduledJobMetadata>> GetJobsByStatusAsync(
        ScheduledJobStatus status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pauses a scheduled job. A paused job will not execute until it is resumed.
    /// </summary>
    /// <param name="jobId">The ID of the job to pause.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the job was found and paused, false if the job doesn't exist.</returns>
    Task<bool> PauseJobAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes a paused job.
    /// </summary>
    /// <param name="jobId">The ID of the job to resume.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the job was found and resumed, false if the job doesn't exist.</returns>
    Task<bool> ResumeJobAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Modifies the schedule of an existing job.
    /// </summary>
    /// <param name="jobId">The ID of the job to modify.</param>
    /// <param name="modificationRequest">The modification parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the job was found and modified, false if the job doesn't exist.</returns>
    /// <exception cref="ArgumentException">If the modification parameters are invalid for the job's schedule type.</exception>
    Task<bool> ModifyJobScheduleAsync(
        string jobId,
        ScheduleModificationRequest modificationRequest,
        CancellationToken cancellationToken = default);
}
