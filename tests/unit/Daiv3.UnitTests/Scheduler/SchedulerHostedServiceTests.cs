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
