namespace Daiv3.Scheduler;

/// <summary>
/// Configuration options for the scheduler service.
/// </summary>
public class SchedulerOptions
{
    /// <summary>
    /// Gets or sets the default timeout for job execution in seconds.
    /// Jobs that exceed this timeout will be forcefully cancelled.
    /// Default: 300 seconds (5 minutes).
    /// </summary>
    public uint JobTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Gets or sets the interval at which the scheduler checks for jobs to execute, in milliseconds.
    /// Lower values mean more responsive job execution, but higher CPU usage.
    /// Default: 1000 milliseconds (1 second).
    /// </summary>
    public uint CheckIntervalMilliseconds { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the maximum number of jobs to execute concurrently.
    /// Set to 1 for sequential execution, higher values for parallel execution.
    /// Default: 4.
    /// </summary>
    public int MaxConcurrentJobs { get; set; } = 4;

    /// <summary>
    /// Gets or sets a value indicating whether to persist job execution history to the database.
    /// If true, job metadata and execution history are saved to SQLite for recovery and auditing.
    /// Default: true.
    /// </summary>
    public bool PersistJobHistory { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of historical records to retain per job.
    /// Older records are automatically cleaned up.
    /// Default: 100.
    /// </summary>
    public uint MaxHistoryPerJob { get; set; } = 100;

    /// <summary>
    /// Gets or sets a value indicating whether to enable startup job recovery.
    /// If true, pending jobs from a previous session will be recovered and scheduled on startup.
    /// Default: true.
    /// </summary>
    public bool EnableStartupRecovery { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to enable performance instrumentation.
    /// If true, all scheduler operations will be timed and logged.
    /// Default: true.
    /// </summary>
    public bool EnablePerformanceInstrumentation { get; set; } = true;

    /// <summary>
    /// Gets or sets the performance warning threshold for schedule operations in milliseconds.
    /// If a schedule operation takes longer than this threshold, a warning will be logged.
    /// This helps ensure scheduling does not block foreground UI interactions.
    /// Default: 10 milliseconds (P95 target).
    /// </summary>
    public uint ScheduleOperationWarningThresholdMs { get; set; } = 10;

    /// <summary>
    /// Gets or sets the performance warning threshold for query operations in milliseconds.
    /// If a query operation (GetJobMetadata, GetAllJobs, etc.) takes longer than this threshold, 
    /// a warning will be logged.
    /// Default: 5 milliseconds (P95 target).
    /// </summary>
    public uint QueryOperationWarningThresholdMs { get; set; } = 5;

    /// <summary>
    /// Gets or sets the performance warning threshold for event raise operations in milliseconds.
    /// If an event raise operation takes longer than this threshold, a warning will be logged.
    /// Default: 15 milliseconds (P95 target).
    /// </summary>
    public uint EventRaiseWarningThresholdMs { get; set; } = 15;
}
