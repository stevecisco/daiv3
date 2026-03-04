using Daiv3.Scheduler;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

#pragma warning disable IDISP006 // Test classes don't need to implement IDisposable

namespace Daiv3.UnitTests.Scheduler;

/// <summary>
/// Unit tests for edge cases, concurrency, and performance scenarios.
/// </summary>
public class SchedulerConcurrencyTests : IAsyncLifetime
{
    private readonly IServiceCollection _serviceCollection;
    private IServiceProvider? _serviceProvider;
    private IScheduler? _scheduler;
    private SchedulerHostedService? _schedulerService;

    public SchedulerConcurrencyTests()
    {
        _serviceCollection = new ServiceCollection();
        _serviceCollection.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
    }

    public async Task InitializeAsync()
    {
        _serviceCollection.AddScheduler(options =>
        {
            options.CheckIntervalMilliseconds = 100;
            options.MaxConcurrentJobs = 2; // Limit concurrency for testing
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
    public async Task MultipleJobs_ExecuteWithinConcurrencyLimit()
    {
        // Arrange
        var job1 = new ConcurrencyTestJob { Name = "concurrent-1", DelayMs = 500 };
        var job2 = new ConcurrencyTestJob { Name = "concurrent-2", DelayMs = 500 };
        var job3 = new ConcurrencyTestJob { Name = "concurrent-3", DelayMs = 500 };

        // Act
        var jobId1 = await _scheduler!.ScheduleImmediateAsync(job1);
        var jobId2 = await _scheduler.ScheduleImmediateAsync(job2);
        var jobId3 = await _scheduler.ScheduleImmediateAsync(job3);

        await Task.Delay(2000); // Wait for all jobs to complete

        var metadata1 = await _scheduler.GetJobMetadataAsync(jobId1);
        var metadata2 = await _scheduler.GetJobMetadataAsync(jobId2);
        var metadata3 = await _scheduler.GetJobMetadataAsync(jobId3);

        // Assert - all jobs should complete
        Assert.NotNull(metadata1);
        Assert.NotNull(metadata2);
        Assert.NotNull(metadata3);
        Assert.Equal(ScheduledJobStatus.Completed, metadata1.Status);
        Assert.Equal(ScheduledJobStatus.Completed, metadata2.Status);
        Assert.Equal(ScheduledJobStatus.Completed, metadata3.Status);
    }

    [Fact]
    public void AddScheduler_DefaultOptions_HasExpectedDefaults()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddScheduler();
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SchedulerOptions>>();

        // Assert
        Assert.Equal(300u, options.Value.JobTimeoutSeconds);
        Assert.Equal(1000u, options.Value.CheckIntervalMilliseconds);
        Assert.Equal(4, options.Value.MaxConcurrentJobs);
        Assert.True(options.Value.PersistJobHistory);
        Assert.Equal(100u, options.Value.MaxHistoryPerJob);
        Assert.True(options.Value.EnableStartupRecovery);
    }

    [Fact]
    public async Task ConcurrencyLimit_PreventsOverexecution()
    {
        // Arrange
        var options = _serviceProvider!
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<SchedulerOptions>>()
            .Value;

        var maxConcurrentJobs = options.MaxConcurrentJobs;

        var jobs = Enumerable.Range(0, maxConcurrentJobs + 2)
            .Select(i => new ConcurrencyTestJob { Name = $"job-{i}", DelayMs = 300 })
            .ToList();

        // Act
        foreach (var job in jobs)
        {
            await _scheduler!.ScheduleImmediateAsync(job);
        }

        // Check concurrent execution count at a point in time
        await Task.Delay(150);

        // Get running jobs count
        var runningJobs = await _scheduler!.GetJobsByStatusAsync(ScheduledJobStatus.Running);

        // Assert - should not exceed concurrency limit
        Assert.True(runningJobs.Count <= maxConcurrentJobs,
            $"Running jobs ({runningJobs.Count}) should not exceed limit ({maxConcurrentJobs})");
    }

    [Fact]
    public async Task JobTimeout_CancelJobsExceedingDuration()
    {
        // Arrange
        await using var shortTimeoutProvider = new ServiceCollection()
            .AddLogging()
            .AddScheduler(options =>
            {
                options.JobTimeoutSeconds = 1; // 1 second timeout
                options.CheckIntervalMilliseconds = 100;
            })
            .BuildServiceProvider();

        var shortTimeoutScheduler = shortTimeoutProvider.GetRequiredService<IScheduler>();
        var shortTimeoutHostedService = shortTimeoutProvider.GetRequiredService<SchedulerHostedService>();

        var longJob = new ConcurrencyTestJob { Name = "long-job", DelayMs = 2000 }; // 2 seconds

        // Act
        await shortTimeoutHostedService.StartAsync(CancellationToken.None);
        var jobId = await shortTimeoutScheduler.ScheduleImmediateAsync(longJob);

        // Wait for timeout to occur
        await Task.Delay(2500);

        var metadata = await shortTimeoutScheduler.GetJobMetadataAsync(jobId);

        await shortTimeoutHostedService.StopAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(metadata);
        // Job should be cancelled due to timeout
        Assert.Equal(ScheduledJobStatus.Cancelled, metadata.Status);
    }

    [Fact]
    public async Task ScheduleRecurring_ContinuesAfterExecution()
    {
        // Arrange
        var recurringJob = new ConcurrencyTestJob { Name = "recurring", DelayMs = 50 };

        // Act
        var jobId = await _scheduler!.ScheduleRecurringAsync(recurringJob, 1); // 1 second interval

        // Wait for multiple cycles
        await Task.Delay(3500);

        var metadata = await _scheduler.GetJobMetadataAsync(jobId);

        // Assert
        Assert.NotNull(metadata);
        Assert.Equal(ScheduleType.Recurring, metadata.ScheduleType);
        // Should still be pending (scheduled for next run) or completed with multiple executions
        Assert.True(metadata.ExecutionCount >= 2,
            $"Expected at least 2 executions, got {metadata.ExecutionCount}");
    }

    [Fact]
    public async Task ScheduleImmediateAsync_WithMultipleInstances_GeneratesUniqueJobIds()
    {
        // Arrange
        var job1 = new ConcurrencyTestJob { Name = "unique-1" };
        var job2 = new ConcurrencyTestJob { Name = "unique-2" };
        var job3 = new ConcurrencyTestJob { Name = "unique-3" };

        // Act
        var jobId1 = await _scheduler!.ScheduleImmediateAsync(job1);
        var jobId2 = await _scheduler.ScheduleImmediateAsync(job2);
        var jobId3 = await _scheduler.ScheduleImmediateAsync(job3);

        // Assert
        Assert.NotEqual(jobId1, jobId2);
        Assert.NotEqual(jobId2, jobId3);
        Assert.NotEqual(jobId1, jobId3);
    }

    [Fact]
    public async Task RecurringJobWithDelay_StartsAfterDelay()
    {
        // Arrange
        var delayedJob = new ConcurrencyTestJob { Name = "delayed-recurring" };

        // Act
        var jobId = await _scheduler!.ScheduleRecurringAsync(delayedJob, 1, delaySeconds: 1);

        // Check status immediately
        var metadataImmediate = await _scheduler.GetJobMetadataAsync(jobId);

        // Wait for the delay to pass
        await Task.Delay(2000);

        var metadataAfter = await _scheduler.GetJobMetadataAsync(jobId);

        // Assert
        Assert.NotNull(metadataImmediate);
        Assert.NotNull(metadataAfter);
        Assert.Equal(0, metadataImmediate.ExecutionCount); // Not executed yet
        Assert.True(metadataAfter.ExecutionCount >= 1); // Executed after delay
    }

    [Fact]
    public async Task GetJobsByStatus_ReturnsCorrectJobs()
    {
        // Arrange
        var job1 = new ConcurrencyTestJob { Name = "status-test-1" };
        var job2 = new ConcurrencyTestJob { Name = "status-test-2", DelayMs = 1000 };

        var jobId1 = await _scheduler!.ScheduleImmediateAsync(job1);
        var jobId2 = await _scheduler.ScheduleImmediateAsync(job2);

        // Wait for job1 to complete but job2 to still be running
        await Task.Delay(300);

        // Act
        var completedJobs = await _scheduler.GetJobsByStatusAsync(ScheduledJobStatus.Completed);
        var runningJobs = await _scheduler.GetJobsByStatusAsync(ScheduledJobStatus.Running);

        // Assert
        Assert.Contains(completedJobs, j => j.JobId == jobId1);
    }

    /// <summary>
    /// A test job for concurrency testing that can delay execution.
    /// </summary>
    private class ConcurrencyTestJob : IScheduledJob
    {
        public string Name { get; set; } = "concurrency-test";
        public IDictionary<string, object>? Metadata { get; set; }
        public int DelayMs { get; set; }

        public async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            if (DelayMs > 0)
            {
                await Task.Delay(DelayMs, cancellationToken);
            }
        }
    }
}
