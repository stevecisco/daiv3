using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Daiv3.Scheduler;

/// <summary>
/// A custom implementation of IScheduler using IHostedService and System.Threading.Timer.
/// 
/// This service manages background job scheduling and execution without external dependencies.
/// It provides:
/// - In-memory job queue with multiple scheduling options
/// - Non-blocking job execution with configurable concurrency
/// - Job lifecycle tracking and logging
/// - Graceful startup and shutdown
/// 
/// Design principles:
/// - Do not block foreground operations (async-only)
/// - Configurable concurrency to prevent resource exhaustion
/// - Comprehensive logging for observability
/// - Support for job recovery on startup (optional)
/// </summary>
public class SchedulerHostedService : BackgroundService, IScheduler
{
    private readonly ILogger<SchedulerHostedService> _logger;
    private readonly SchedulerOptions _options;
    private readonly ConcurrentDictionary<string, ScheduledJobEntry> _jobs;
    private readonly ConcurrentDictionary<string, List<string>> _eventTriggeredJobs; // eventType -> list of jobIds
    private readonly SemaphoreSlim _concurrencySemaphore;
    private Timer? _checkTimer;

    // For job ID generation
    private volatile int _jobIdCounter = 0;

    public SchedulerHostedService(
        ILogger<SchedulerHostedService> logger,
        IOptions<SchedulerOptions> options)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);

        _logger = logger;
        _options = options.Value;
        _jobs = new ConcurrentDictionary<string, ScheduledJobEntry>();
        _eventTriggeredJobs = new ConcurrentDictionary<string, List<string>>();
        _concurrencySemaphore = new SemaphoreSlim(_options.MaxConcurrentJobs, _options.MaxConcurrentJobs);

        _logger.LogInformation(
            "SchedulerHostedService initialized with options: JobTimeoutSeconds={JobTimeout}, " +
            "CheckIntervalMs={CheckInterval}, MaxConcurrentJobs={MaxConcurrent}",
            _options.JobTimeoutSeconds,
            _options.CheckIntervalMilliseconds,
            _options.MaxConcurrentJobs);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SchedulerHostedService starting");

        try
        {
            // Start the job checking timer
            _checkTimer = new Timer(
                callback: _ => _ = CheckAndExecutePendingJobs(stoppingToken),
                state: null,
                dueTime: TimeSpan.FromMilliseconds(_options.CheckIntervalMilliseconds),
                period: TimeSpan.FromMilliseconds(_options.CheckIntervalMilliseconds));

            _logger.LogInformation("Job check timer started with interval {Interval}ms", _options.CheckIntervalMilliseconds);

            // Keep the service running until cancellation
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("SchedulerHostedService received cancellation request");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in SchedulerHostedService");
            throw;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("SchedulerHostedService stopping");

        // Dispose the timer
        _checkTimer?.Dispose();

        // Cancel all running jobs
        var runningJobs = _jobs.Values.Where(j => j.Status == ScheduledJobStatus.Running).ToList();
        foreach (var job in runningJobs)
        {
            _logger.LogInformation("Cancelling running job {JobId} ({JobName})", job.JobId, job.Job.Name);
            job.CancellationTokenSource.Cancel();
        }

        // Wait for running jobs to complete gracefully
        var runningTasks = runningJobs.Select(j => j.ExecutionTask).Where(t => t != null).Cast<Task>();
        try
        {
            await Task.WhenAll(runningTasks.Append(Task.CompletedTask));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error waiting for jobs to complete during shutdown");
        }

        await base.StopAsync(cancellationToken);
        _logger.LogInformation("SchedulerHostedService stopped");
    }

    public Task<string> ScheduleImmediateAsync(
        IScheduledJob job,
        TimeSpan? delay = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);

        var jobId = GenerateJobId();
        var scheduledTime = delay.HasValue ? DateTime.UtcNow.Add(delay.Value) : DateTime.UtcNow;

        var entry = new ScheduledJobEntry
        {
            JobId = jobId,
            Job = job,
            Status = ScheduledJobStatus.Pending,
            ScheduleType = ScheduleType.Immediate,
            CreatedAtUtc = DateTime.UtcNow,
            ScheduledAtUtc = scheduledTime,
            CancellationTokenSource = new CancellationTokenSource()
        };

        if (!_jobs.TryAdd(jobId, entry))
        {
            _logger.LogError("Failed to add job {JobId} to scheduler", jobId);
            throw new InvalidOperationException($"Failed to schedule job {jobId}");
        }

        _logger.LogInformation(
            "Job scheduled immediately: {JobId} ({JobName}), scheduled for {ScheduledTime:O}",
            jobId, job.Name, scheduledTime);

        return Task.FromResult(jobId);
    }

    public Task<string> ScheduleAtTimeAsync(
        IScheduledJob job,
        DateTime scheduledTime,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);

        if (scheduledTime.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Scheduled time must be in UTC", nameof(scheduledTime));
        }

        var jobId = GenerateJobId();
        var entry = new ScheduledJobEntry
        {
            JobId = jobId,
            Job = job,
            Status = ScheduledJobStatus.Scheduled,
            ScheduleType = ScheduleType.OneTime,
            CreatedAtUtc = DateTime.UtcNow,
            ScheduledAtUtc = scheduledTime,
            CancellationTokenSource = new CancellationTokenSource()
        };

        if (!_jobs.TryAdd(jobId, entry))
        {
            _logger.LogError("Failed to add job {JobId} to scheduler", jobId);
            throw new InvalidOperationException($"Failed to schedule job {jobId}");
        }

        _logger.LogInformation(
            "Job scheduled at specific time: {JobId} ({JobName}), scheduled for {ScheduledTime:O}",
            jobId, job.Name, scheduledTime);

        return Task.FromResult(jobId);
    }

    public Task<string> ScheduleRecurringAsync(
        IScheduledJob job,
        uint intervalSeconds,
        uint? delaySeconds = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);

        if (intervalSeconds == 0)
        {
            throw new ArgumentException("Interval must be greater than 0", nameof(intervalSeconds));
        }

        var jobId = GenerateJobId();
        var delayTimespan = delaySeconds.HasValue ? TimeSpan.FromSeconds(delaySeconds.Value) : TimeSpan.Zero;
        var scheduledTime = DateTime.UtcNow.Add(delayTimespan);

        var entry = new ScheduledJobEntry
        {
            JobId = jobId,
            Job = job,
            Status = ScheduledJobStatus.Pending,
            ScheduleType = ScheduleType.Recurring,
            CreatedAtUtc = DateTime.UtcNow,
            ScheduledAtUtc = scheduledTime,
            IntervalSeconds = intervalSeconds,
            CancellationTokenSource = new CancellationTokenSource()
        };

        if (!_jobs.TryAdd(jobId, entry))
        {
            _logger.LogError("Failed to add job {JobId} to scheduler", jobId);
            throw new InvalidOperationException($"Failed to schedule job {jobId}");
        }

        _logger.LogInformation(
            "Job scheduled recurring: {JobId} ({JobName}), interval={Interval}s, first run at {ScheduledTime:O}",
            jobId, job.Name, intervalSeconds, scheduledTime);

        return Task.FromResult(jobId);
    }

    public Task<string> ScheduleCronAsync(
        IScheduledJob job,
        string cronExpression,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        if (string.IsNullOrWhiteSpace(cronExpression))
        {
            throw new ArgumentException("Cron expression cannot be null or whitespace", nameof(cronExpression));
        }

        // Validate and parse the cron expression
        CronExpression cron;
        try
        {
            cron = new CronExpression(cronExpression);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Invalid cron expression: {CronExpression}", cronExpression);
            throw new ArgumentException($"Invalid cron expression: {cronExpression}", nameof(cronExpression), ex);
        }

        var jobId = GenerateJobId();
        var nextRun = cron.GetNextOccurrence(DateTime.UtcNow);

        if (nextRun == null)
        {
            throw new ArgumentException($"Cron expression '{cronExpression}' has no future occurrences", nameof(cronExpression));
        }

        var entry = new ScheduledJobEntry
        {
            JobId = jobId,
            Job = job,
            Status = ScheduledJobStatus.Scheduled,
            ScheduleType = ScheduleType.Cron,
            CreatedAtUtc = DateTime.UtcNow,
            ScheduledAtUtc = nextRun,
            CronExpression = cronExpression,
            CancellationTokenSource = new CancellationTokenSource()
        };

        if (!_jobs.TryAdd(jobId, entry))
        {
            _logger.LogError("Failed to add job {JobId} to scheduler", jobId);
            throw new InvalidOperationException($"Failed to schedule job {jobId}");
        }

        _logger.LogInformation(
            "Job scheduled with cron expression: {JobId} ({JobName}), expression={CronExpression}, next run at {NextRun:O}",
            jobId, job.Name, cronExpression, nextRun);

        return Task.FromResult(jobId);
    }

    public Task<string> ScheduleOnEventAsync(
        IScheduledJob job,
        string eventType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        if (string.IsNullOrWhiteSpace(eventType))
        {
            throw new ArgumentException("Event type cannot be null or whitespace", nameof(eventType));
        }

        var jobId = GenerateJobId();
        var entry = new ScheduledJobEntry
        {
            JobId = jobId,
            Job = job,
            Status = ScheduledJobStatus.Pending,
            ScheduleType = ScheduleType.EventTriggered,
            CreatedAtUtc = DateTime.UtcNow,
            EventType = eventType,
            CancellationTokenSource = new CancellationTokenSource()
        };

        if (!_jobs.TryAdd(jobId, entry))
        {
            _logger.LogError("Failed to add job {JobId} to scheduler", jobId);
            throw new InvalidOperationException($"Failed to schedule job {jobId}");
        }

        // Register the job in the event-triggered jobs dictionary
        _eventTriggeredJobs.AddOrUpdate(
            eventType,
            _ => new List<string> { jobId },
            (_, list) =>
            {
                lock (list)
                {
                    list.Add(jobId);
                }
                return list;
            });

        _logger.LogInformation(
            "Job scheduled on event: {JobId} ({JobName}), eventType={EventType}",
            jobId, job.Name, eventType);

        return Task.FromResult(jobId);
    }

    public async Task RaiseEventAsync(ISchedulerEvent schedulerEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(schedulerEvent);

        _logger.LogInformation(
            "Event raised: {EventType} at {OccurredAt:O}",
            schedulerEvent.EventType,
            schedulerEvent.OccurredAtUtc);

        // Find all jobs listening for this event type
        if (!_eventTriggeredJobs.TryGetValue(schedulerEvent.EventType, out var jobIds))
        {
            _logger.LogDebug("No jobs registered for event type: {EventType}", schedulerEvent.EventType);
            return;
        }

        List<string> jobIdsCopy;
        lock (jobIds)
        {
            jobIdsCopy = new List<string>(jobIds);
        }

        _logger.LogDebug("Found {Count} jobs registered for event type: {EventType}", jobIdsCopy.Count, schedulerEvent.EventType);

        // Trigger each job
        foreach (var jobId in jobIdsCopy)
        {
            if (!_jobs.TryGetValue(jobId, out var entry))
            {
                _logger.LogWarning("Job {JobId} not found for event {EventType}", jobId, schedulerEvent.EventType);
                continue;
            }

            if (entry.Status == ScheduledJobStatus.Cancelled)
            {
                _logger.LogDebug("Job {JobId} is cancelled, skipping event trigger", jobId);
                continue;
            }

            // Respect concurrency limit
            if (!await _concurrencySemaphore.WaitAsync(0))
            {
                _logger.LogDebug("Concurrency limit reached, deferring event-triggered job {JobId}", jobId);
                continue;
            }

            // Mark as running and execute
            entry.Status = ScheduledJobStatus.Running;
            entry.LastStartedAtUtc = DateTime.UtcNow;
            entry.ScheduledAtUtc = schedulerEvent.OccurredAtUtc;

            _logger.LogInformation(
                "Triggering event-based job: {JobId} ({JobName}) for event {EventType}",
                jobId, entry.Job.Name, schedulerEvent.EventType);

            // Fire-and-forget the job execution
            _ = ExecuteJobAsync(entry, cancellationToken)
                .ContinueWith(_ => _concurrencySemaphore.Release());
        }
    }

    public Task<bool> CancelJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            throw new ArgumentNullException(nameof(jobId));
        }

        if (!_jobs.TryGetValue(jobId, out var entry))
        {
            _logger.LogWarning("Job not found: {JobId}", jobId);
            return Task.FromResult(false);
        }

        if (entry.Status == ScheduledJobStatus.Cancelled)
        {
            _logger.LogInformation("Job already cancelled: {JobId}", jobId);
            return Task.FromResult(true);
        }

        var oldStatus = entry.Status;
        entry.Status = ScheduledJobStatus.Cancelled;
        entry.CancellationTokenSource.Cancel();

        // Remove from event-triggered jobs if applicable
        if (entry.ScheduleType == ScheduleType.EventTriggered && entry.EventType != null)
        {
            if (_eventTriggeredJobs.TryGetValue(entry.EventType, out var jobIds))
            {
                lock (jobIds)
                {
                    jobIds.Remove(jobId);
                }
            }
        }

        _logger.LogInformation(
            "Job cancelled: {JobId} ({JobName}), previous status={PreviousStatus}",
            jobId, entry.Job.Name, oldStatus);

        return Task.FromResult(true);
    }

    public Task<ScheduledJobMetadata?> GetJobMetadataAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            throw new ArgumentNullException(nameof(jobId));
        }

        if (!_jobs.TryGetValue(jobId, out var entry))
        {
            return Task.FromResult<ScheduledJobMetadata?>(null);
        }

        var metadata = CreateMetadata(entry);
        return Task.FromResult<ScheduledJobMetadata?>(metadata);
    }

    public Task<IReadOnlyList<ScheduledJobMetadata>> GetAllJobsAsync(CancellationToken cancellationToken = default)
    {
        var metadata = _jobs.Values.Select(CreateMetadata).ToList();
        return Task.FromResult<IReadOnlyList<ScheduledJobMetadata>>(metadata);
    }

    public Task<IReadOnlyList<ScheduledJobMetadata>> GetJobsByStatusAsync(
        ScheduledJobStatus status,
        CancellationToken cancellationToken = default)
    {
        var metadata = _jobs.Values
            .Where(j => j.Status == status)
            .Select(CreateMetadata)
            .ToList();
        return Task.FromResult<IReadOnlyList<ScheduledJobMetadata>>(metadata);
    }

    /// <summary>
    /// Checks for jobs that are ready to execute and executes them.
    /// This method is called periodically by the check timer.
    /// </summary>
    private async Task CheckAndExecutePendingJobs(CancellationToken cancellationToken)
    {
        try
        {
            var now = DateTime.UtcNow;
            var jobsToExecute = _jobs.Values
                .Where(j => j.ScheduledAtUtc <= now && (j.Status == ScheduledJobStatus.Scheduled || j.Status == ScheduledJobStatus.Pending))
                .ToList();

            if (jobsToExecute.Any())
            {
                _logger.LogDebug("Found {Count} jobs ready to execute", jobsToExecute.Count);
            }

            foreach (var jobEntry in jobsToExecute)
            {
                // Respect concurrency limit
                if (!await _concurrencySemaphore.WaitAsync(0))
                {
                    _logger.LogDebug("Concurrency limit reached, deferring job {JobId}", jobEntry.JobId);
                    continue;
                }

                // Mark as running and execute
                jobEntry.Status = ScheduledJobStatus.Running;
                jobEntry.LastStartedAtUtc = DateTime.UtcNow;

                // Fire-and-forget the job execution, but don't await it in the timer callback
                _ = ExecuteJobAsync(jobEntry, cancellationToken)
                    .ContinueWith(_ => _concurrencySemaphore.Release());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for pending jobs");
        }
    }

    /// <summary>
    /// Executes a single job with timeout and error handling.
    /// </summary>
    private async Task ExecuteJobAsync(ScheduledJobEntry jobEntry, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Starting execution of job {JobId} ({JobName})", jobEntry.JobId, jobEntry.Job.Name);

            // Use a timeout
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(
                jobEntry.CancellationTokenSource.Token,
                cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(_options.JobTimeoutSeconds));

            // Execute the job
            await jobEntry.Job.ExecuteAsync(cts.Token);

            stopwatch.Stop();
            jobEntry.LastCompletedAtUtc = DateTime.UtcNow;
            jobEntry.LastExecutionDuration = stopwatch.Elapsed;
            jobEntry.ExecutionCount++;
            jobEntry.LastErrorMessage = null;

            // Handle recurring jobs
            if (jobEntry.ScheduleType == ScheduleType.Recurring && jobEntry.IntervalSeconds.HasValue)
            {
                jobEntry.Status = ScheduledJobStatus.Pending;
                jobEntry.ScheduledAtUtc = DateTime.UtcNow.AddSeconds(jobEntry.IntervalSeconds.Value);
                _logger.LogInformation(
                    "Job completed successfully and rescheduled: {JobId} ({JobName}), duration={Duration}ms, next run at {NextRun:O}",
                    jobEntry.JobId, jobEntry.Job.Name, stopwatch.ElapsedMilliseconds, jobEntry.ScheduledAtUtc);
            }
            // Handle cron-based jobs
            else if (jobEntry.ScheduleType == ScheduleType.Cron && jobEntry.CronExpression != null)
            {
                try
                {
                    var cron = new CronExpression(jobEntry.CronExpression);
                    var nextRun = cron.GetNextOccurrence(DateTime.UtcNow);

                    if (nextRun != null)
                    {
                        jobEntry.Status = ScheduledJobStatus.Scheduled;
                        jobEntry.ScheduledAtUtc = nextRun;
                        _logger.LogInformation(
                            "Cron job completed successfully and rescheduled: {JobId} ({JobName}), duration={Duration}ms, next run at {NextRun:O}",
                            jobEntry.JobId, jobEntry.Job.Name, stopwatch.ElapsedMilliseconds, nextRun);
                    }
                    else
                    {
                        jobEntry.Status = ScheduledJobStatus.Completed;
                        _logger.LogInformation(
                            "Cron job completed successfully with no future occurrences: {JobId} ({JobName}), duration={Duration}ms",
                            jobEntry.JobId, jobEntry.Job.Name, stopwatch.ElapsedMilliseconds);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error rescheduling cron job {JobId}: {Error}", jobEntry.JobId, ex.Message);
                    jobEntry.Status = ScheduledJobStatus.Failed;
                    jobEntry.LastErrorMessage = $"Failed to reschedule: {ex.Message}";
                }
            }
            // Event-triggered jobs remain pending for the next event
            else if (jobEntry.ScheduleType == ScheduleType.EventTriggered)
            {
                jobEntry.Status = ScheduledJobStatus.Pending;
                _logger.LogInformation(
                    "Event-triggered job completed successfully: {JobId} ({JobName}), duration={Duration}ms, waiting for next event",
                    jobEntry.JobId, jobEntry.Job.Name, stopwatch.ElapsedMilliseconds);
            }
            else
            {
                jobEntry.Status = ScheduledJobStatus.Completed;
                _logger.LogInformation(
                    "Job completed successfully: {JobId} ({JobName}), duration={Duration}ms",
                    jobEntry.JobId, jobEntry.Job.Name, stopwatch.ElapsedMilliseconds);
            }
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            jobEntry.Status = ScheduledJobStatus.Cancelled;
            jobEntry.LastErrorMessage = "Job cancelled";
            jobEntry.LastExecutionDuration = stopwatch.Elapsed;

            _logger.LogWarning(
                "Job cancelled: {JobId} ({JobName}), duration={Duration}ms",
                jobEntry.JobId, jobEntry.Job.Name, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            jobEntry.Status = ScheduledJobStatus.Failed;
            jobEntry.LastErrorMessage = ex.Message;
            jobEntry.LastExecutionDuration = stopwatch.Elapsed;
            jobEntry.ExecutionCount++;

            _logger.LogError(
                ex,
                "Job failed: {JobId} ({JobName}), duration={Duration}ms, error={Error}",
                jobEntry.JobId, jobEntry.Job.Name, stopwatch.ElapsedMilliseconds, ex.Message);
        }
    }

    /// <summary>
    /// Generates a unique job ID.
    /// </summary>
    private string GenerateJobId()
    {
        var counter = Interlocked.Increment(ref _jobIdCounter);
        return $"job_{DateTime.UtcNow:yyyyMMddHHmmss}_{counter:D6}";
    }

    /// <summary>
    /// Creates ScheduledJobMetadata from a ScheduledJobEntry.
    /// </summary>
    private ScheduledJobMetadata CreateMetadata(ScheduledJobEntry entry)
    {
        return new ScheduledJobMetadata
        {
            JobId = entry.JobId,
            JobName = entry.Job.Name,
            Status = entry.Status,
            ScheduleType = entry.ScheduleType,
            CreatedAtUtc = entry.CreatedAtUtc,
            ScheduledAtUtc = entry.ScheduledAtUtc,
            LastStartedAtUtc = entry.LastStartedAtUtc,
            LastCompletedAtUtc = entry.LastCompletedAtUtc,
            LastExecutionDuration = entry.LastExecutionDuration,
            ExecutionCount = entry.ExecutionCount,
            LastErrorMessage = entry.LastErrorMessage,
            IntervalSeconds = entry.IntervalSeconds,
            CronExpression = entry.CronExpression,
            EventType = entry.EventType,
            Metadata = entry.Job.Metadata as IReadOnlyDictionary<string, object>
        };
    }

    /// <summary>
    /// Internal entry representing a scheduled job with execution state.
    /// </summary>
    private class ScheduledJobEntry
    {
        public required string JobId { get; init; }
        public required IScheduledJob Job { get; init; }
        public ScheduledJobStatus Status { get; set; }
        public required ScheduleType ScheduleType { get; init; }
        public DateTime CreatedAtUtc { get; init; }
        public DateTime? ScheduledAtUtc { get; set; }
        public DateTime? LastStartedAtUtc { get; set; }
        public DateTime? LastCompletedAtUtc { get; set; }
        public TimeSpan? LastExecutionDuration { get; set; }
        public int ExecutionCount { get; set; }
        public string? LastErrorMessage { get; set; }
        public uint? IntervalSeconds { get; init; }
        public string? CronExpression { get; init; }
        public string? EventType { get; init; }
        public required CancellationTokenSource CancellationTokenSource { get; init; }
        public Task? ExecutionTask { get; set; }
    }
}
