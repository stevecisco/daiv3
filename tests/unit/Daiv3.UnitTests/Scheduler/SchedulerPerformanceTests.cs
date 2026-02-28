using System.Diagnostics;
using Daiv3.Scheduler;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace Daiv3.UnitTests.Scheduler;

/// <summary>
/// Performance tests for the scheduler to verify PTS-NFR-002: 
/// "Scheduling SHOULD not block foreground UI interactions."
/// 
/// These tests ensure that scheduler operations complete within acceptable time thresholds
/// to maintain a responsive user interface.
/// </summary>
public sealed class SchedulerPerformanceTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<SchedulerHostedService> _logger;
    private readonly SchedulerHostedService _scheduler;

    // Performance thresholds (in milliseconds) - matching SchedulerOptions defaults
    private const int ScheduleOperationThreshold = 10; // P95 target
    private const int QueryOperationThreshold = 5;     // P95 target
    private const int EventRaiseThreshold = 15;        // P95 target

    // Test tolerances (add some buffer for test reliability)
    private const int ScheduleOperationTolerance = 20;  // 2x threshold
    private const int QueryOperationTolerance = 10;     // 2x threshold
    private const int EventRaiseTolerance = 30;         // 2x threshold

    public SchedulerPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Warning);
        });
        _logger = loggerFactory.CreateLogger<SchedulerHostedService>();

        var options = Options.Create(new SchedulerOptions
        {
            CheckIntervalMilliseconds = 100,
            MaxConcurrentJobs = 4,
            JobTimeoutSeconds = 60,
            EnablePerformanceInstrumentation = true,
            ScheduleOperationWarningThresholdMs = ScheduleOperationThreshold,
            QueryOperationWarningThresholdMs = QueryOperationThreshold,
            EventRaiseWarningThresholdMs = EventRaiseThreshold
        });

        _scheduler = new SchedulerHostedService(_logger, options);
    }

    public void Dispose()
    {
        _scheduler.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
        _scheduler.Dispose();
    }

    [Fact]
    public async Task ScheduleImmediateAsync_CompletesWithinThreshold()
    {
        // Arrange
        var job = new TestJob("perf-test-1");
        var stopwatch = Stopwatch.StartNew();

        // Act
        var jobId = await _scheduler.ScheduleImmediateAsync(job);
        stopwatch.Stop();

        // Assert
        Assert.NotNull(jobId);
        _output.WriteLine($"ScheduleImmediateAsync completed in {stopwatch.ElapsedMilliseconds}ms");
        Assert.True(stopwatch.ElapsedMilliseconds < ScheduleOperationTolerance,
            $"Schedule operation took {stopwatch.ElapsedMilliseconds}ms, expected < {ScheduleOperationTolerance}ms");
    }

    [Fact]
    public async Task ScheduleAtTimeAsync_CompletesWithinThreshold()
    {
        // Arrange
        var job = new TestJob("perf-test-2");
        var scheduledTime = DateTime.UtcNow.AddHours(1);
        var stopwatch = Stopwatch.StartNew();

        // Act
        var jobId = await _scheduler.ScheduleAtTimeAsync(job, scheduledTime);
        stopwatch.Stop();

        // Assert
        Assert.NotNull(jobId);
        _output.WriteLine($"ScheduleAtTimeAsync completed in {stopwatch.ElapsedMilliseconds}ms");
        Assert.True(stopwatch.ElapsedMilliseconds < ScheduleOperationTolerance,
            $"Schedule operation took {stopwatch.ElapsedMilliseconds}ms, expected < {ScheduleOperationTolerance}ms");
    }

    [Fact]
    public async Task ScheduleRecurringAsync_CompletesWithinThreshold()
    {
        // Arrange
        var job = new TestJob("perf-test-3");
        var stopwatch = Stopwatch.StartNew();

        // Act
        var jobId = await _scheduler.ScheduleRecurringAsync(job, intervalSeconds: 60);
        stopwatch.Stop();

        // Assert
        Assert.NotNull(jobId);
        _output.WriteLine($"ScheduleRecurringAsync completed in {stopwatch.ElapsedMilliseconds}ms");
        Assert.True(stopwatch.ElapsedMilliseconds < ScheduleOperationTolerance,
            $"Schedule operation took {stopwatch.ElapsedMilliseconds}ms, expected < {ScheduleOperationTolerance}ms");
    }

    [Fact]
    public async Task ScheduleCronAsync_CompletesWithinThreshold()
    {
        // Arrange
        var job = new TestJob("perf-test-4");
        var stopwatch = Stopwatch.StartNew();

        // Act
        var jobId = await _scheduler.ScheduleCronAsync(job, "0 12 * * *");
        stopwatch.Stop();

        // Assert
        Assert.NotNull(jobId);
        _output.WriteLine($"ScheduleCronAsync completed in {stopwatch.ElapsedMilliseconds}ms");
        Assert.True(stopwatch.ElapsedMilliseconds < ScheduleOperationTolerance,
            $"Schedule operation took {stopwatch.ElapsedMilliseconds}ms, expected < {ScheduleOperationTolerance}ms");
    }

    [Fact]
    public async Task ScheduleOnEventAsync_CompletesWithinThreshold()
    {
        // Arrange
        var job = new TestJob("perf-test-5");
        var stopwatch = Stopwatch.StartNew();

        // Act
        var jobId = await _scheduler.ScheduleOnEventAsync(job, "test-event");
        stopwatch.Stop();

        // Assert
        Assert.NotNull(jobId);
        _output.WriteLine($"ScheduleOnEventAsync completed in {stopwatch.ElapsedMilliseconds}ms");
        Assert.True(stopwatch.ElapsedMilliseconds < ScheduleOperationTolerance,
            $"Schedule operation took {stopwatch.ElapsedMilliseconds}ms, expected < {ScheduleOperationTolerance}ms");
    }

    [Fact]
    public async Task CancelJobAsync_CompletesWithinThreshold()
    {
        // Arrange
        var job = new TestJob("perf-test-6");
        var jobId = await _scheduler.ScheduleAtTimeAsync(job, DateTime.UtcNow.AddHours(1));
        var stopwatch = Stopwatch.StartNew();

        // Act
        var cancelled = await _scheduler.CancelJobAsync(jobId);
        stopwatch.Stop();

        // Assert
        Assert.True(cancelled);
        _output.WriteLine($"CancelJobAsync completed in {stopwatch.ElapsedMilliseconds}ms");
        Assert.True(stopwatch.ElapsedMilliseconds < ScheduleOperationTolerance,
            $"Cancel operation took {stopwatch.ElapsedMilliseconds}ms, expected < {ScheduleOperationTolerance}ms");
    }

    [Fact]
    public async Task GetJobMetadataAsync_CompletesWithinThreshold()
    {
        // Arrange
        var job = new TestJob("perf-test-7");
        var jobId = await _scheduler.ScheduleAtTimeAsync(job, DateTime.UtcNow.AddHours(1));
        var stopwatch = Stopwatch.StartNew();

        // Act
        var metadata = await _scheduler.GetJobMetadataAsync(jobId);
        stopwatch.Stop();

        // Assert
        Assert.NotNull(metadata);
        _output.WriteLine($"GetJobMetadataAsync completed in {stopwatch.ElapsedMilliseconds}ms");
        Assert.True(stopwatch.ElapsedMilliseconds < QueryOperationTolerance,
            $"Query operation took {stopwatch.ElapsedMilliseconds}ms, expected < {QueryOperationTolerance}ms");
    }

    [Fact]
    public async Task GetAllJobsAsync_CompletesWithinThreshold_SmallDataset()
    {
        // Arrange - schedule 10 jobs
        for (int i = 0; i < 10; i++)
        {
            await _scheduler.ScheduleAtTimeAsync(new TestJob($"perf-test-bulk-{i}"), DateTime.UtcNow.AddHours(1));
        }

        var stopwatch = Stopwatch.StartNew();

        // Act
        var jobs = await _scheduler.GetAllJobsAsync();
        stopwatch.Stop();

        // Assert
        Assert.True(jobs.Count >= 10);
        _output.WriteLine($"GetAllJobsAsync completed in {stopwatch.ElapsedMilliseconds}ms for {jobs.Count} jobs");
        Assert.True(stopwatch.ElapsedMilliseconds < QueryOperationTolerance,
            $"Query operation took {stopwatch.ElapsedMilliseconds}ms, expected < {QueryOperationTolerance}ms");
    }

    [Fact]
    public async Task GetAllJobsAsync_CompletesWithinThreshold_LargeDataset()
    {
        // Arrange - schedule 100 jobs
        for (int i = 0; i < 100; i++)
        {
            await _scheduler.ScheduleAtTimeAsync(new TestJob($"perf-test-large-{i}"), DateTime.UtcNow.AddHours(1));
        }

        var stopwatch = Stopwatch.StartNew();

        // Act
        var jobs = await _scheduler.GetAllJobsAsync();
        stopwatch.Stop();

        // Assert
        Assert.True(jobs.Count >= 100);
        _output.WriteLine($"GetAllJobsAsync completed in {stopwatch.ElapsedMilliseconds}ms for {jobs.Count} jobs");
        
        // For larger datasets, we allow more time but should still be reasonable
        Assert.True(stopwatch.ElapsedMilliseconds < 50,
            $"Query operation for large dataset took {stopwatch.ElapsedMilliseconds}ms, expected < 50ms");
    }

    [Fact]
    public async Task GetJobsByStatusAsync_CompletesWithinThreshold()
    {
        // Arrange - schedule some jobs
        for (int i = 0; i < 10; i++)
        {
            await _scheduler.ScheduleAtTimeAsync(new TestJob($"perf-test-status-{i}"), DateTime.UtcNow.AddHours(1));
        }

        var stopwatch = Stopwatch.StartNew();

        // Act
        var jobs = await _scheduler.GetJobsByStatusAsync(ScheduledJobStatus.Scheduled);
        stopwatch.Stop();

        // Assert
        Assert.True(jobs.Count >= 10);
        _output.WriteLine($"GetJobsByStatusAsync completed in {stopwatch.ElapsedMilliseconds}ms for {jobs.Count} jobs");
        Assert.True(stopwatch.ElapsedMilliseconds < QueryOperationTolerance,
            $"Query operation took {stopwatch.ElapsedMilliseconds}ms, expected < {QueryOperationTolerance}ms");
    }

    [Fact]
    public async Task RaiseEventAsync_CompletesWithinThreshold()
    {
        // Arrange - register a job for an event
        var job = new TestJob("perf-test-event");
        await _scheduler.ScheduleOnEventAsync(job, "perf-test-event-type");
        
        var schedulerEvent = new TestSchedulerEvent
        {
            EventType = "perf-test-event-type",
            OccurredAtUtc = DateTime.UtcNow,
            Metadata = new Dictionary<string, object> { { "test", "value" } }
        };

        var stopwatch = Stopwatch.StartNew();

        // Act
        await _scheduler.RaiseEventAsync(schedulerEvent);
        stopwatch.Stop();

        // Assert
        _output.WriteLine($"RaiseEventAsync completed in {stopwatch.ElapsedMilliseconds}ms");
        Assert.True(stopwatch.ElapsedMilliseconds < EventRaiseTolerance,
            $"Event raise operation took {stopwatch.ElapsedMilliseconds}ms, expected < {EventRaiseTolerance}ms");
    }

    [Fact]
    public async Task PauseJobAsync_CompletesWithinThreshold()
    {
        // Arrange
        var job = new TestJob("perf-test-pause");
        var jobId = await _scheduler.ScheduleAtTimeAsync(job, DateTime.UtcNow.AddHours(1));
        var stopwatch = Stopwatch.StartNew();

        // Act
        var paused = await _scheduler.PauseJobAsync(jobId);
        stopwatch.Stop();

        // Assert
        Assert.True(paused);
        _output.WriteLine($"PauseJobAsync completed in {stopwatch.ElapsedMilliseconds}ms");
        Assert.True(stopwatch.ElapsedMilliseconds < ScheduleOperationTolerance,
            $"Pause operation took {stopwatch.ElapsedMilliseconds}ms, expected < {ScheduleOperationTolerance}ms");
    }

    [Fact]
    public async Task ResumeJobAsync_CompletesWithinThreshold()
    {
        // Arrange
        var job = new TestJob("perf-test-resume");
        var jobId = await _scheduler.ScheduleAtTimeAsync(job, DateTime.UtcNow.AddHours(1));
        await _scheduler.PauseJobAsync(jobId);
        var stopwatch = Stopwatch.StartNew();

        // Act
        var resumed = await _scheduler.ResumeJobAsync(jobId);
        stopwatch.Stop();

        // Assert
        Assert.True(resumed);
        _output.WriteLine($"ResumeJobAsync completed in {stopwatch.ElapsedMilliseconds}ms");
        Assert.True(stopwatch.ElapsedMilliseconds < ScheduleOperationTolerance,
            $"Resume operation took {stopwatch.ElapsedMilliseconds}ms, expected < {ScheduleOperationTolerance}ms");
    }

    [Fact]
    public async Task ModifyJobScheduleAsync_CompletesWithinThreshold()
    {
        // Arrange
        var job = new TestJob("perf-test-modify");
        var jobId = await _scheduler.ScheduleRecurringAsync(job, intervalSeconds: 60);
        
        var modificationRequest = new ScheduleModificationRequest
        {
            IntervalSeconds = 120
        };

        var stopwatch = Stopwatch.StartNew();

        // Act
        var modified = await _scheduler.ModifyJobScheduleAsync(jobId, modificationRequest);
        stopwatch.Stop();

        // Assert
        Assert.True(modified);
        _output.WriteLine($"ModifyJobScheduleAsync completed in {stopwatch.ElapsedMilliseconds}ms");
        Assert.True(stopwatch.ElapsedMilliseconds < ScheduleOperationTolerance,
            $"Modify operation took {stopwatch.ElapsedMilliseconds}ms, expected < {ScheduleOperationTolerance}ms");
    }

    [Fact]
    public async Task MultipleScheduleOperations_MaintainPerformance()
    {
        // Arrange & Act - perform multiple scheduling operations in sequence
        var stopwatch = Stopwatch.StartNew();
        
        for (int i = 0; i < 20; i++)
        {
            await _scheduler.ScheduleAtTimeAsync(new TestJob($"perf-burst-{i}"), DateTime.UtcNow.AddHours(1));
        }
        
        stopwatch.Stop();

        // Assert
        _output.WriteLine($"20 schedule operations completed in {stopwatch.ElapsedMilliseconds}ms ({stopwatch.ElapsedMilliseconds / 20.0:F2}ms average)");
        
        // Average should be well under threshold
        var averageMs = stopwatch.ElapsedMilliseconds / 20.0;
        Assert.True(averageMs < ScheduleOperationThreshold,
            $"Average schedule operation took {averageMs:F2}ms, expected < {ScheduleOperationThreshold}ms");
    }

    [Fact]
    public async Task SequentialQueryOperations_MaintainPerformance()
    {
        // Arrange - create some jobs
        var jobIds = new List<string>();
        for (int i = 0; i < 10; i++)
        {
            var jobId = await _scheduler.ScheduleAtTimeAsync(new TestJob($"perf-query-{i}"), DateTime.UtcNow.AddHours(1));
            jobIds.Add(jobId);
        }

        // Act - perform multiple query operations
        var stopwatch = Stopwatch.StartNew();
        
        for (int i = 0; i < 20; i++)
        {
            await _scheduler.GetJobMetadataAsync(jobIds[i % jobIds.Count]);
        }
        
        stopwatch.Stop();

        // Assert
        _output.WriteLine($"20 query operations completed in {stopwatch.ElapsedMilliseconds}ms ({stopwatch.ElapsedMilliseconds / 20.0:F2}ms average)");
        
        var averageMs = stopwatch.ElapsedMilliseconds / 20.0;
        Assert.True(averageMs < QueryOperationThreshold,
            $"Average query operation took {averageMs:F2}ms, expected < {QueryOperationThreshold}ms");
    }

    /// <summary>
    /// Test job implementation for performance testing.
    /// </summary>
    private class TestJob : IScheduledJob
    {
        public string Name { get; }
        public IDictionary<string, object>? Metadata { get; }

        public TestJob(string name)
        {
            Name = name;
            Metadata = new Dictionary<string, object>();
        }

        public Task ExecuteAsync(CancellationToken cancellationToken)
        {
            // No-op for performance tests
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Test scheduler event implementation.
    /// </summary>
    private class TestSchedulerEvent : ISchedulerEvent
    {
        public required string EventType { get; init; }
        public required DateTime OccurredAtUtc { get; init; }
        public IReadOnlyDictionary<string, object>? Metadata { get; init; }
    }
}
