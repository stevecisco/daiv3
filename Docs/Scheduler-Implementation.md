# Scheduler Implementation (KLC-REQ-010)

## Overview

The scheduler is a custom implementation of background job scheduling for DAIv3, designed as a lightweight alternative to Quartz.NET. It uses `IHostedService` and `System.Threading.Timer` to provide:

- Non-blocking job scheduling and execution
- Multiple scheduling patterns (immediate, one-time, recurring)
- Configurable concurrency limits
- Comprehensive job lifecycle tracking
- Complete integration with .NET dependency injection

## Design Principles

1. **Non-blocking**: All scheduling and query operations are async-only to prevent blocking foreground UI operations
2. **Configurable Concurrency**: Prevents resource exhaustion by limiting concurrent job execution
3. **Observable**: Comprehensive logging at all stages of job lifecycle
4. **Recoverable**: Optional startup job recovery support for fault tolerance
5. **Lightweight**: No external scheduling dependencies (Quartz.NET rejected for complexity overhead)

## Architecture

### Core Components

| Component | Purpose |
|-----------|---------|
| `IScheduler` | Main scheduling interface for registering and managing jobs |
| `IScheduledJob` | Contract for jobs that can be scheduled and executed |
| `SchedulerHostedService` | Core implementation using `IHostedService` and timer |
| `SchedulerOptions` | Configuration for timeout, check interval, concurrency, etc. |
| `ScheduledJobMetadata` | Metadata about job execution state and history |

### Scheduling Types

The scheduler supports three scheduling patterns:

1. **Immediate** - Job runs immediately (or after optional delay)
2. **OneTime** - Job runs at a specific scheduled time
3. **Recurring** - Job runs repeatedly at configurable intervals

### Job States

Jobs progress through the following states:

```
Scheduled → Pending → Running → Completed/Failed/Cancelled
                                    ↓ (recurring only)
                              Pending (reschedule)
```

## Configuration

### Default Options

```csharp
var options = new SchedulerOptions
{
    JobTimeoutSeconds = 300,              // 5 minutes
    CheckIntervalMilliseconds = 1000,     // Check every 1 second
    MaxConcurrentJobs = 4,                // Max 4 concurrent jobs
    PersistJobHistory = true,             // Save history to DB
    MaxHistoryPerJob = 100,               // Retention per job
    EnableStartupRecovery = true          // Recover pending jobs on startup
};
```

### Registration in DI

```csharp
// Using default options
services.AddScheduler();

// With custom configuration
services.AddScheduler(options =>
{
    options.JobTimeoutSeconds = 600;
    options.MaxConcurrentJobs = 8;
});
```

### Configuration via appsettings.json

```json
{
  "Scheduler": {
    "JobTimeoutSeconds": 300,
    "CheckIntervalMilliseconds": 1000,
    "MaxConcurrentJobs": 4,
    "PersistJobHistory": true,
    "MaxHistoryPerJob": 100,
    "EnableStartupRecovery": true
  }
}
```

## Usage Examples

### Creating a Job

Implement `IScheduledJob`:

```csharp
public class DocumentIndexingJob : IScheduledJob
{
    private readonly IKnowledgeService _knowledgeService;
    private readonly ILogger<DocumentIndexingJob> _logger;

    public string Name => "document-indexing";
    
    public IDictionary<string, object>? Metadata { get; }

    public DocumentIndexingJob(
        IKnowledgeService knowledgeService,
        ILogger<DocumentIndexingJob> logger)
    {
        _knowledgeService = knowledgeService;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting document indexing");
        try
        {
            await _knowledgeService.IndexPendingDocumentsAsync(cancellationToken);
            _logger.LogInformation("Document indexing completed successfully");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Document indexing was cancelled");
            throw;
        }
    }
}
```

### Scheduling Jobs

```csharp
var scheduler = serviceProvider.GetRequiredService<IScheduler>();

// Immediate execution
var jobId = await scheduler.ScheduleImmediateAsync(
    new DocumentIndexingJob(knowledge, logger));

// Delayed execution
var jobId = await scheduler.ScheduleImmediateAsync(
    new DocumentIndexingJob(knowledge, logger),
    delay: TimeSpan.FromSeconds(30));

// Scheduled for specific time
var futureTime = DateTime.UtcNow.AddDays(1);
var jobId = await scheduler.ScheduleAtTimeAsync(
    new DocumentIndexingJob(knowledge, logger),
    futureTime);

// Recurring job (every 4 hours)
var jobId = await scheduler.ScheduleRecurringAsync(
    new DocumentIndexingJob(knowledge, logger),
    intervalSeconds: 14400);

// Recurring with initial delay (30 min delay, then every hour)
var jobId = await scheduler.ScheduleRecurringAsync(
    new DocumentIndexingJob(knowledge, logger),
    intervalSeconds: 3600,
    delaySeconds: 1800);
```

### Monitoring Jobs

```csharp
// Get specific job metadata
var metadata = await scheduler.GetJobMetadataAsync(jobId);
Console.WriteLine($"Job {metadata.JobName}: {metadata.Status}");
Console.WriteLine($"Executed: {metadata.ExecutionCount} times");
Console.WriteLine($"Duration: {metadata.LastExecutionDuration?.TotalSeconds}s");

// Get all jobs
var allJobs = await scheduler.GetAllJobsAsync();

// Filter by status
var runningJobs = await scheduler.GetJobsByStatusAsync(ScheduledJobStatus.Running);
var failedJobs = await scheduler.GetJobsByStatusAsync(ScheduledJobStatus.Failed);

// Cancel a job
await scheduler.CancelJobAsync(jobId);
```

## Implementation Details

### Job Execution Flow

1. **Check Timer**: Every `CheckIntervalMilliseconds`, the scheduler checks for jobs ready to execute
2. **Status Transition**: Ready jobs transition from `Pending`/`Scheduled` to `Running`
3. **Concurrency Check**: Uses `SemaphoreSlim` to respect `MaxConcurrentJobs` limit
4. **Timeout Protection**: Jobs are wrapped in a timeout token (default 5 minutes)
5. **Execution**: Job's `ExecuteAsync()` is called
6. **Error Handling**: Exceptions are caught, logged, and job status set to `Failed`
7. **Recurring Rescheduling**: For recurring jobs, status is reset to `Pending` with new `ScheduledAtUtc`

### Key Design Decisions

1. **Timer-based vs Event-based**: Timer approach is simpler, more deterministic, less reactive to system time changes
2. **In-memory vs Database**: Currently in-memory for performance; optional persistence layer can be added later (KLC-REQ-010 scope doesn't require DB)
3. **Concurrency Semaphore**: Prevents resource exhaustion while allowing parallel execution
4. **No External Dependencies**: Uses only .NET Framework APIs for portability

## Testing Coverage

Test suites include:

1. **SchedulerHostedServiceTests**: Core functionality (scheduling, execution, cancellation)
2. **SchedulerConcurrencyTests**: Concurrency limits, timeouts, recovery
3. **SchedulerServiceExtensionsTests**: DI registration and configuration

All major code paths are covered:
- ✅ Immediate, one-time, and recurring scheduling
- ✅ Job cancellation (before and during execution)
- ✅ Exception handling and error tracking
- ✅ Metadata tracking and historical data
- ✅ Concurrency limiting
- ✅ DI registration and configuration

## Performance Characteristics

### Tier 1 Expectations

- **Job Registration**: O(1) - atomic dictionary insertion
- **Job Status Check**: O(1) per job - concurrent dictionary lookup
- **Tier 1 Scheduler Query**: O(n) where n = number of active jobs (typically <100)
- **Check Interval**: Configurable (default 1000ms) - trades responsiveness vs CPU
- **Memory Overhead**: ~1KB per scheduled job (metadata)

### Optimization Opportunities

For future phases:
- Add database persistence for permanent job history (KLC-REQ-010 scope doesn't require this)
- Implement HNSW indexing for job queries (KM-NFR-002 already plans this pattern)
- Add message queue integration for distributed scheduling (Phase 6)

## Future Enhancements

As documented in PTS-REQ-007 (dependent on KLC-REQ-010):
- Cron-based scheduling support
- Event-triggered task execution
- Job dependency chains
- Advanced filtering and querying

## Logging

The scheduler logs at the following levels:

- **Information**: Service startup/shutdown, job scheduling, completion
- **Warning**: Job cancellation, timeout failures, shutdown issues
- **Error**: Job execution failures, fatal scheduling errors
- **Debug**: Job check cycles (high volume - only in detailed debugging)

Example log output:
```
[08:30:15 INF] SchedulerHostedService starting
[08:30:15 INF] Job check timer started with interval 1000ms
[08:30:16 INF] Job scheduled immediately: job_20250228_083016_000001 (document-indexing), scheduled for 2025-02-28T08:30:16.0000000Z
[08:31:16 INF] Starting execution of job job_20250228_083016_000001 (document-indexing)
[08:31:22 INF] Job completed successfully: job_20250228_083016_000001 (document-indexing), duration=6045ms
```

## Troubleshooting

### No jobs are executing
- Check `CheckIntervalMilliseconds` - might be too high
- Verify job's `ExecuteAsync()` implementation doesn't have infinite waits
- Check logs for any timeout errors

### Too many concurrent jobs
- Increase `MaxConcurrentJobs` in options
- Or reduce number of jobs per time window

### Jobs timing out
- Increase `JobTimeoutSeconds` if jobs legitimately take longer
- Or optimize job implementation

### Cannot start host
- Ensure `IScheduler` is properly registered via `AddScheduler()`
- Check that dependent services (logging, etc.) are also registered
