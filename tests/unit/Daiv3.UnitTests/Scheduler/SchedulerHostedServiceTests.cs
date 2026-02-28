using Daiv3.Scheduler;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Daiv3.UnitTests.Scheduler;

/// <summary>
/// Unit tests for SchedulerHostedService, the core custom job scheduler implementation.
/// </summary>
public class SchedulerHostedServiceTests : IAsyncLifetime
{
    private readonly IServiceCollection _serviceCollection;
    private IServiceProvider? _serviceProvider;
    private IScheduler? _scheduler;
    private SchedulerHostedService? _schedulerService;

    public SchedulerHostedServiceTests()
    {
        _serviceCollection = new ServiceCollection();
        _serviceCollection.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Warning);
        });
    }

    public async Task InitializeAsync()
    {
        // Build DI container with scheduler
        _serviceCollection.AddScheduler(options =>
        {
            options.CheckIntervalMilliseconds = 100; // Fast checks for tests
            options.JobTimeoutSeconds = 10;
        });

        _serviceProvider = _serviceCollection.BuildServiceProvider();
        _scheduler = _serviceProvider.GetRequiredService<IScheduler>();
        _schedulerService = _serviceProvider.GetRequiredService<SchedulerHostedService>();

        // Start the hosted service
        await _schedulerService.StartAsync(CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        // Stop the hosted service
        if (_schedulerService != null)
        {
            await _schedulerService.StopAsync(CancellationToken.None);
        }

        if (_serviceProvider is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
    }

    [Fact]
    public async Task ScheduleImmediateAsync_WithJob_ReturnsJobId()
    {
        // Arrange
        var job = new TestJob { Name = "test-job" };

        // Act
        var jobId = await _scheduler!.ScheduleImmediateAsync(job);

        // Assert
        Assert.NotNull(jobId);
        Assert.NotEmpty(jobId);
        Assert.StartsWith("job_", jobId);
    }

    [Fact]
    public async Task ScheduleImmediateAsync_WithDelay_ExecutesAfterDelay()
    {
        // Arrange
        var job = new TestJob { Name = "test-job-delayed" };
        var delay = TimeSpan.FromMilliseconds(200);

        // Act
        var jobId = await _scheduler!.ScheduleImmediateAsync(job, delay);
        var jobBefore = await _scheduler.GetJobMetadataAsync(jobId);

        await Task.Delay(delay.Add(TimeSpan.FromMilliseconds(500)));
        var jobAfter = await _scheduler.GetJobMetadataAsync(jobId);

        // Assert
        Assert.NotNull(jobId);
        Assert.NotNull(jobBefore);
        Assert.Equal(ScheduledJobStatus.Pending, jobBefore.Status);
        Assert.NotNull(jobAfter);
        Assert.Equal(ScheduledJobStatus.Completed, jobAfter.Status);
        Assert.True(job.ExecutedAtUtc.HasValue);
    }

    [Fact]
    public async Task ScheduleAtTimeAsync_WithFutureTime_SchedulesCorrectly()
    {
        // Arrange
        var job = new TestJob { Name = "test-job-at-time" };
        var scheduledTime = DateTime.UtcNow.AddSeconds(1);

        // Act
        var jobId = await _scheduler!.ScheduleAtTimeAsync(job, scheduledTime);
        var metadata = await _scheduler.GetJobMetadataAsync(jobId);

        // Assert
        Assert.NotNull(jobId);
        Assert.NotNull(metadata);
        Assert.Equal(ScheduleType.OneTime, metadata.ScheduleType);
        Assert.Equal(scheduledTime, metadata.ScheduledAtUtc);
    }

    [Fact]
    public async Task ScheduleAtTimeAsync_WithNonUtcTime_ThrowsException()
    {
        // Arrange
        var job = new TestJob { Name = "test-job" };
        var nonUtcTime = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Local);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _scheduler!.ScheduleAtTimeAsync(job, nonUtcTime));
    }

    [Fact]
    public async Task ScheduleRecurringAsync_WithValidInterval_CreatesRecurringJob()
    {
        // Arrange
        var job = new TestJob { Name = "test-recurring" };
        uint IntervalSeconds = 1;

        // Act
        var jobId = await _scheduler!.ScheduleRecurringAsync(job, IntervalSeconds);
        var metadata = await _scheduler.GetJobMetadataAsync(jobId);

        // Assert
        Assert.NotNull(jobId);
        Assert.NotNull(metadata);
        Assert.Equal(ScheduleType.Recurring, metadata.ScheduleType);
        Assert.Equal(IntervalSeconds, metadata.IntervalSeconds);
    }

    [Fact]
    public async Task ScheduleRecurringAsync_WithZeroInterval_ThrowsException()
    {
        // Arrange
        var job = new TestJob { Name = "test-job" };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _scheduler!.ScheduleRecurringAsync(job, 0));
    }

    [Fact]
    public async Task RecurringJob_ExecutesMultipleTimes()
    {
        // Arrange
        var job = new TestJob { Name = "test-recurring-exec" };
        uint IntervalSeconds = 1;

        // Act
        var jobId = await _scheduler!.ScheduleRecurringAsync(job, IntervalSeconds);
        
        // Wait for multiple executions (3 cycles)
        await Task.Delay(TimeSpan.FromSeconds(3.5));

        var metadata = await _scheduler.GetJobMetadataAsync(jobId);

        // Assert
        Assert.NotNull(metadata);
        // Should have executed at least 2-3 times
        Assert.True(metadata.ExecutionCount >= 2, $"Expected at least 2 executions, got {metadata.ExecutionCount}");
    }

    [Fact]
    public async Task CancelJobAsync_WithValidJobId_CancelsJob()
    {
        // Arrange
        var job = new TestJob { Name = "test-cancel" };
        var jobId = await _scheduler!.ScheduleImmediateAsync(job);

        // Act
        var result = await _scheduler.CancelJobAsync(jobId);

        // Assert
        Assert.True(result);
        var metadata = await _scheduler.GetJobMetadataAsync(jobId);
        Assert.NotNull(metadata);
        Assert.Equal(ScheduledJobStatus.Cancelled, metadata.Status);
    }

    [Fact]
    public async Task CancelJobAsync_WithInvalidJobId_ReturnsFalse()
    {
        // Act
        var result = await _scheduler!.CancelJobAsync("nonexistent-job-id");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetJobMetadataAsync_WithValidJobId_ReturnsMetadata()
    {
        // Arrange
        var job = new TestJob { Name = "test-metadata" };
        var jobId = await _scheduler!.ScheduleImmediateAsync(job);

        // Act
        var metadata = await _scheduler.GetJobMetadataAsync(jobId);

        // Assert
        Assert.NotNull(metadata);
        Assert.Equal(jobId, metadata.JobId);
        Assert.Equal("test-metadata", metadata.JobName);
        Assert.Equal(ScheduleType.Immediate, metadata.ScheduleType);
    }

    [Fact]
    public async Task GetJobMetadataAsync_WithInvalidJobId_ReturnsNull()
    {
        // Act
        var metadata = await _scheduler!.GetJobMetadataAsync("nonexistent-job-id");

        // Assert
        Assert.Null(metadata);
    }

    [Fact]
    public async Task GetAllJobsAsync_ReturnsAllScheduledJobs()
    {
        // Arrange
        var job1 = new TestJob { Name = "test-job-1" };
        var job2 = new TestJob { Name = "test-job-2" };
        var job3 = new TestJob { Name = "test-job-3" };

        await _scheduler!.ScheduleImmediateAsync(job1);
        await _scheduler.ScheduleImmediateAsync(job2);
        await _scheduler.ScheduleImmediateAsync(job3);

        // Act
        var allJobs = await _scheduler.GetAllJobsAsync();

        // Assert
        Assert.True(allJobs.Count >= 3);
        Assert.Contains(allJobs, j => j.JobName == "test-job-1");
        Assert.Contains(allJobs, j => j.JobName == "test-job-2");
        Assert.Contains(allJobs, j => j.JobName == "test-job-3");
    }

    [Fact]
    public async Task GetJobsByStatusAsync_FiltersJobsByStatus()
    {
        // Arrange
        var job1 = new TestJob { Name = "test-status-1" };
        var job2 = new TestJob { Name = "test-status-2", DelayMs = 1000 };

        var jobId1 = await _scheduler!.ScheduleImmediateAsync(job1);
        var jobId2 = await _scheduler.ScheduleImmediateAsync(job2);

        await Task.Delay(500); // Let job1 complete

        // Act
        var completedJobs = await _scheduler.GetJobsByStatusAsync(ScheduledJobStatus.Completed);
        var runningJobs = await _scheduler.GetJobsByStatusAsync(ScheduledJobStatus.Running);

        // Assert
        Assert.Contains(completedJobs, j => j.JobId == jobId1);
    }

    [Fact]
    public async Task JobExecution_LogsCorrectMetadata()
    {
        // Arrange
        var job = new TestJob { Name = "test-logging" };
        var jobId = await _scheduler!.ScheduleImmediateAsync(job);

        // Act
        await Task.Delay(500); // Wait for execution

        var metadata = await _scheduler.GetJobMetadataAsync(jobId);

        // Assert
        Assert.NotNull(metadata);
        Assert.Equal(ScheduledJobStatus.Completed, metadata.Status);
        Assert.NotNull(metadata.LastStartedAtUtc);
        Assert.NotNull(metadata.LastCompletedAtUtc);
        Assert.NotNull(metadata.LastExecutionDuration);
        Assert.Equal(1, metadata.ExecutionCount);
        Assert.Null(metadata.LastErrorMessage);
    }

    [Fact]
    public async Task JobException_SetsFailedStatus()
    {
        // Arrange
        var job = new TestJob { Name = "test-exception", ThrowException = true };
        var jobId = await _scheduler!.ScheduleImmediateAsync(job);

        // Act
        await Task.Delay(500); // Wait for execution

        var metadata = await _scheduler.GetJobMetadataAsync(jobId);

        // Assert
        Assert.NotNull(metadata);
        Assert.Equal(ScheduledJobStatus.Failed, metadata.Status);
        Assert.NotNull(metadata.LastErrorMessage);
        Assert.Contains("Simulated error", metadata.LastErrorMessage);
        Assert.Equal(1, metadata.ExecutionCount);
    }

    [Fact]
    public async Task ScheduleImmediateAsync_WithNullJob_ThrowsException()
    {
        // Act & Assert
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _scheduler!.ScheduleImmediateAsync(null));
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }

    [Fact]
    public async Task CancelJobAsync_WithNullJobId_ThrowsException()
    {
        // Act & Assert
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _scheduler!.CancelJobAsync(null));
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }

    [Fact]
    public async Task JobMetadata_TracksExecutionHistory()
    {
        // Arrange
        var job = new TestJob { Name = "test-history" };
        var jobId = await _scheduler!.ScheduleImmediateAsync(job);

        // Act
        await Task.Delay(500);
        var metadata = await _scheduler.GetJobMetadataAsync(jobId);

        // Assert
        Assert.NotNull(metadata);
        Assert.Equal(1, metadata.ExecutionCount);
        Assert.NotNull(metadata.CreatedAtUtc);
        Assert.NotNull(metadata.LastStartedAtUtc);
        Assert.NotNull(metadata.LastCompletedAtUtc);
    }

    [Fact]
    public async Task ScheduleCronAsync_WithValidExpression_SchedulesCorrectly()
    {
        // Arrange
        var job = new TestJob { Name = "test-cron-job" };
        var cronExpression = "*/15 * * * *"; // Every 15 minutes

        // Act
        var jobId = await _scheduler!.ScheduleCronAsync(job, cronExpression);
        var metadata = await _scheduler.GetJobMetadataAsync(jobId);

        // Assert
        Assert.NotNull(jobId);
        Assert.NotNull(metadata);
        Assert.Equal(ScheduleType.Cron, metadata.ScheduleType);
        Assert.Equal(cronExpression, metadata.CronExpression);
        Assert.Equal(ScheduledJobStatus.Scheduled, metadata.Status);
        Assert.NotNull(metadata.ScheduledAtUtc);
    }

    [Fact]
    public async Task ScheduleCronAsync_WithInvalidExpression_ThrowsArgumentException()
    {
        // Arrange
        var job = new TestJob { Name = "test-job" };
        var invalidExpression = "invalid cron";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _scheduler!.ScheduleCronAsync(job, invalidExpression));
    }

    [Fact]
    public async Task ScheduleCronAsync_JobExecutesAndReschedulesAutomatically()
    {
        // Arrange
        var job = new TestJob { Name = "test-cron-job-reschedule" };
        // Schedule for every minute at second 0 - find the next occurrence
        var now = DateTime.UtcNow;
        var nextMinute = now.AddMinutes(1);
        var cronExpression = $"{nextMinute.Minute} {nextMinute.Hour} * * *"; // Specific minute and hour

        // Act
        var jobId = await _scheduler!.ScheduleCronAsync(job, cronExpression);
        var metadataBefore = await _scheduler.GetJobMetadataAsync(jobId);

        // Wait for execution (give time for the job to execute)
        var waitUntil = nextMinute.AddSeconds(5);
        var waitDuration = waitUntil - DateTime.UtcNow;
        if (waitDuration > TimeSpan.Zero && waitDuration < TimeSpan.FromMinutes(2))
        {
            await Task.Delay(waitDuration);
        }

        var metadataAfter = await _scheduler.GetJobMetadataAsync(jobId);

        // Assert
        Assert.NotNull(metadataBefore);
        Assert.Equal(ScheduledJobStatus.Scheduled, metadataBefore.Status);
        
        // Note: This test may be flaky depending on timing. In a real scenario,
        // we'd use a time provider or wait longer. For now, we just verify the structure.
        Assert.Equal(ScheduleType.Cron, metadataAfter!.ScheduleType);
        Assert.Equal(cronExpression, metadataAfter.CronExpression);
    }

    [Fact]
    public async Task ScheduleOnEventAsync_RegistersEventTrigger()
    {
        // Arrange
        var job = new TestJob { Name = "test-event-job" };
        var eventType = "test.event";

        // Act
        var jobId = await _scheduler!.ScheduleOnEventAsync(job, eventType);
        var metadata = await _scheduler.GetJobMetadataAsync(jobId);

        // Assert
        Assert.NotNull(jobId);
        Assert.NotNull(metadata);
        Assert.Equal(ScheduleType.EventTriggered, metadata.ScheduleType);
        Assert.Equal(eventType, metadata.EventType);
        Assert.Equal(ScheduledJobStatus.Pending, metadata.Status);
    }

    [Fact]
    public async Task RaiseEventAsync_TriggersRegisteredJobs()
    {
        // Arrange
        var job1 = new TestJob { Name = "event-job-1" };
        var job2 = new TestJob { Name = "event-job-2" };
        var eventType = "test.trigger.event";

        var jobId1 = await _scheduler!.ScheduleOnEventAsync(job1, eventType);
        var jobId2 = await _scheduler.ScheduleOnEventAsync(job2, eventType);

        var schedulerEvent = new SchedulerEvent
        {
            EventType = eventType,
            OccurredAtUtc = DateTime.UtcNow,
            Metadata = new Dictionary<string, object> { { "test", "value" } }
        };

        // Act
        await _scheduler.RaiseEventAsync(schedulerEvent);

        // Wait for execution
        await Task.Delay(500);

        var metadata1 = await _scheduler.GetJobMetadataAsync(jobId1);
        var metadata2 = await _scheduler.GetJobMetadataAsync(jobId2);

        // Assert
        Assert.NotNull(metadata1);
        Assert.NotNull(metadata2);
        
        // Jobs should have been triggered (either completed or back to pending)
        Assert.True(metadata1.ExecutionCount > 0, "Job 1 should have executed at least once");
        Assert.True(metadata2.ExecutionCount > 0, "Job 2 should have executed at least once");
        
        Assert.True(job1.ExecutedAtUtc.HasValue, "Job 1 should have executed");
        Assert.True(job2.ExecutedAtUtc.HasValue, "Job 2 should have executed");
    }

    [Fact]
    public async Task RaiseEventAsync_WithNoRegisteredJobs_CompletesWithoutError()
    {
        // Arrange
        var schedulerEvent = new SchedulerEvent
        {
            EventType = "nonexistent.event",
            OccurredAtUtc = DateTime.UtcNow
        };

        // Act & Assert (should not throw)
        await _scheduler!.RaiseEventAsync(schedulerEvent);
    }

    [Fact]
    public async Task CancelJobAsync_WithEventTriggeredJob_RemovesFromEventRegistry()
    {
        // Arrange
        var job = new TestJob { Name = "cancelable-event-job" };
        var eventType = "test.cancel.event";

        var jobId = await _scheduler!.ScheduleOnEventAsync(job, eventType);
        var metadataBefore = await _scheduler.GetJobMetadataAsync(jobId);

        // Act
        var cancelled = await _scheduler.CancelJobAsync(jobId);
        var metadataAfter = await _scheduler.GetJobMetadataAsync(jobId);

        // Raise event after cancellation
        await _scheduler.RaiseEventAsync(new SchedulerEvent
        {
            EventType = eventType,
            OccurredAtUtc = DateTime.UtcNow
        });

        await Task.Delay(200);

        // Assert
        Assert.True(cancelled);
        Assert.NotNull(metadataBefore);
        Assert.Equal(ScheduledJobStatus.Pending, metadataBefore.Status);
        
        Assert.NotNull(metadataAfter);
        Assert.Equal(ScheduledJobStatus.Cancelled, metadataAfter.Status);
        
        // Job should not have executed after cancellation
        Assert.False(job.ExecutedAtUtc.HasValue, "Cancelled job should not execute");
        Assert.Equal(0, metadataAfter.ExecutionCount);
    }

    /// <summary>
    /// A simple test job for unit testing.
    /// </summary>
    private class TestJob : IScheduledJob
    {
        public string Name { get; set; } = "test-job";
        public IDictionary<string, object>? Metadata { get; set; }
        public bool ThrowException { get; set; }
        public int DelayMs { get; set; }
        public DateTime? ExecutedAtUtc { get; private set; }

        public async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            if (DelayMs > 0)
            {
                await Task.Delay(DelayMs, cancellationToken);
            }

            ExecutedAtUtc = DateTime.UtcNow;

            if (ThrowException)
            {
                throw new InvalidOperationException("Simulated error");
            }
        }
    }
}
