using Daiv3.App.Maui.Models;
using Daiv3.App.Maui.Services;
using Daiv3.ModelExecution.Interfaces;
using Daiv3.Scheduler;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Daiv3.App.Maui.Tests;

/// <summary>
/// Unit tests for DashboardService.
/// Tests CT-REQ-003: Real-time transparency dashboard.
/// Tests CT-NFR-001: Async/await patterns for UI responsiveness.
/// </summary>
public class DashboardServiceTests
{
    private readonly Mock<ILogger<DashboardService>> _mockLogger;

    public DashboardServiceTests()
    {
        _mockLogger = new Mock<ILogger<DashboardService>>();
    }

    [Fact]
    public void Constructor_WithValidConfiguration_ShouldInitialize()
    {
        // Arrange
        var config = new DashboardConfiguration();

        // Act
        using var service = new DashboardService(_mockLogger.Object, null, config);

        // Assert
        Assert.NotNull(service);
        Assert.False(service.IsMonitoring);
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrow()
    {
        // Act & Assert
#pragma warning disable IDISP005 // Test validates exception throwing, instance not actually created
        Assert.Throws<ArgumentNullException>(() =>
            new DashboardService(null!, null, new DashboardConfiguration()));
#pragma warning restore IDISP005
    }

    [Fact]
    public void Constructor_WithNullConfiguration_ShouldUseDefault()
    {
        // Act
        using var service = new DashboardService(_mockLogger.Object, null, null);

        // Assert
        Assert.NotNull(service.Configuration);
        Assert.Equal(DashboardConfiguration.DefaultRefreshIntervalMs, service.Configuration.RefreshIntervalMs);
    }

    [Fact]
    public async Task GetDashboardDataAsync_ShouldReturnValidData()
    {
        // Arrange
        using var service = new DashboardService(_mockLogger.Object, null, null);

        // Act
        var data = await service.GetDashboardDataAsync();

        // Assert
        Assert.NotNull(data);
        Assert.NotNull(data.Hardware);
        Assert.NotNull(data.Queue);
        Assert.NotNull(data.Indexing);
        Assert.NotNull(data.Agent);
        Assert.NotNull(data.SystemResources);
        Assert.True(data.IsValid); // Should not have an error
    }

    [Fact]
    public async Task GetDashboardDataAsync_WithCancellation_ReturnsCancelledData()
    {
        // Arrange
        using var service = new DashboardService(_mockLogger.Object, null, null);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var data = await service.GetDashboardDataAsync(cts.Token);

        // Assert - Service returns error data on cancellation instead of throwing
        Assert.NotNull(data);
    }

    [Fact]
    public async Task GetDashboardDataAsync_WithVeryShortTimeout_MayTimeOut()
    {
        // Arrange - This test is timing-dependent, so we don't make strict assertions
        var config = new DashboardConfiguration { DataCollectionTimeoutMs = 10 }; // Very short but not unrealistic
        using var service = new DashboardService(_mockLogger.Object, null, config);

        // Act
        var data = await service.GetDashboardDataAsync();

        // Assert - Either completes successfully or times out, both are acceptable
        // The important thing is that it doesn't crash
        Assert.NotNull(data);
    }

    [Fact]
    public async Task StartMonitoringAsync_ShouldSetMonitoringFlag()
    {
        // Arrange
        using var service = new DashboardService(_mockLogger.Object, null, null);

        // Act
        await service.StartMonitoringAsync();
        await Task.Delay(100); // Give monitoring a moment to start

        // Assert
        Assert.True(service.IsMonitoring);

        // Cleanup
        await service.StopMonitoringAsync();
    }

    [Fact]
    public async Task StartMonitoringAsync_WhenAlreadyMonitoring_ShouldNotFail()
    {
        // Arrange
        using var service = new DashboardService(_mockLogger.Object, null, null);
        await service.StartMonitoringAsync();
        await Task.Delay(100);

        // Act
        await service.StartMonitoringAsync(); // Start again

        // Assert
        Assert.True(service.IsMonitoring);

        // Cleanup
        await service.StopMonitoringAsync();
    }

    [Fact]
    public async Task StartMonitoringAsync_WithInvalidInterval_ShouldThrow()
    {
        // Arrange
        using var service = new DashboardService(_mockLogger.Object, null, null);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.StartMonitoringAsync(1)); // Below minimum
    }

    [Fact]
    public async Task StopMonitoringAsync_ShouldClearMonitoringFlag()
    {
        // Arrange
        using var service = new DashboardService(_mockLogger.Object, null, null);
        await service.StartMonitoringAsync();
        await Task.Delay(100);

        // Act
        await service.StopMonitoringAsync();
        await Task.Delay(100);

        // Assert
        Assert.False(service.IsMonitoring);
    }

    [Fact]
    public async Task StopMonitoringAsync_WhenNotMonitoring_ShouldNotFail()
    {
        // Arrange
        using var service = new DashboardService(_mockLogger.Object, null, null);

        // Act
        await service.StopMonitoringAsync(); // Should not throw

        // Assert
        Assert.False(service.IsMonitoring);
    }

    [Fact]
    public async Task DataUpdated_ShouldRaiseEventWhenMonitoring()
    {
        // Arrange
        using var service = new DashboardService(_mockLogger.Object, null, null);
        var eventRaised = false;
        service.DataUpdated += (s, e) => { eventRaised = true; };

        // Act
        await service.StartMonitoringAsync();
        await Task.Delay(500); // Wait for at least one update

        // Assert
        Assert.True(eventRaised);

        // Cleanup
        await service.StopMonitoringAsync();
    }

    [Fact]
    public void Configuration_ShouldBeAccessible()
    {
        // Arrange
        var config = new DashboardConfiguration { RefreshIntervalMs = 5000 };
        using var service = new DashboardService(_mockLogger.Object, null, config);

        // Act
        var serviceConfig = service.Configuration;

        // Assert
        Assert.NotNull(serviceConfig);
        Assert.Equal(5000, serviceConfig.RefreshIntervalMs);
    }

#pragma warning disable IDISP016, IDISP017 // Intentionally testing disposal behavior
    [Fact]
    public void Dispose_ShouldCleanupResources()
    {
        // Arrange
        var service = new DashboardService(_mockLogger.Object, null, null);

        // Act
        service.Dispose();

        // Assert - Should not throw on subsequent dispose
        service.Dispose();
    }
#pragma warning restore IDISP016, IDISP017

#pragma warning disable IDISP016, IDISP017 // Intentionally testing disposal behavior
    [Fact]
    public async Task Dispose_WhileMonitoring_ShouldStopMonitoring()
    {
        // Arrange
        var service = new DashboardService(_mockLogger.Object, null, null);
        await service.StartMonitoringAsync();
        await Task.Delay(100);

        // Act
        service.Dispose();
        await Task.Delay(100);

        // Assert - Intentionally checking state after disposal
        Assert.False(service.IsMonitoring);
    }
#pragma warning restore IDISP016, IDISP017

#pragma warning disable IDISP016, IDISP017 // Intentionally testing disposal behavior
    [Fact]
    public async Task GetDashboardDataAsync_AfterDispose_ShouldThrow()
    {
        // Arrange
        var service = new DashboardService(_mockLogger.Object, null, null);
        service.Dispose();

        // Act & Assert - Intentionally calling method on disposed instance to verify exception
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => service.GetDashboardDataAsync());
    }
#pragma warning restore IDISP016, IDISP017

    /// <summary>
    /// Tests for CT-REQ-004: Queue status collection.
    /// </summary>
    [Fact]
    public async Task GetDashboardDataAsync_WithModelQueue_ShouldPopulateQueueStatus()
    {
        // Arrange
        var mockQueue = new Mock<IModelQueue>();
        var queueStatus = new Daiv3.ModelExecution.Models.QueueStatus
        {
            ImmediateCount = 2,
            NormalCount = 3,
            BackgroundCount = 1,
            CurrentModelId = "test-model",
            LastModelSwitch = DateTimeOffset.UtcNow.AddSeconds(-10)
        };
        mockQueue.Setup(q => q.GetQueueStatusAsync()).ReturnsAsync(queueStatus);
        
        var metrics = new Daiv3.ModelExecution.Models.QueueMetrics
        {
            TotalCompleted = 10,
            AverageExecutionDurationMs = 1500.0,
            AverageQueueWaitMs = 500.0,
            InFlightExecutions = 2
        };
        mockQueue.Setup(q => q.GetMetricsAsync()).ReturnsAsync(metrics);
        
        using var service = new DashboardService(_mockLogger.Object, mockQueue.Object, null);

        // Act
        var data = await service.GetDashboardDataAsync();

        // Assert
        Assert.NotNull(data.Queue);
        Assert.Equal(6, data.Queue.PendingCount); // 2 + 3 + 1
        Assert.Equal(10, data.Queue.CompletedCount);
        Assert.Equal("test-model", data.Queue.CurrentModel);
        Assert.Equal(2, data.Queue.ImmediateCount);
        Assert.Equal(3, data.Queue.NormalCount);
        Assert.Equal(1, data.Queue.BackgroundCount);
    }

    [Fact]
    public async Task GetDashboardDataAsync_WithModelQueue_ShouldCalculateMetrics()
    {
        // Arrange
        var mockQueue = new Mock<IModelQueue>();
        var queueStatus = new Daiv3.ModelExecution.Models.QueueStatus
        {
            ImmediateCount = 0,
            NormalCount = 0,
            BackgroundCount = 0,
            CurrentModelId = null,
            LastModelSwitch = DateTimeOffset.UtcNow
        };
        mockQueue.Setup(q => q.GetQueueStatusAsync()).ReturnsAsync(queueStatus);
        
        var metrics = new Daiv3.ModelExecution.Models.QueueMetrics
        {
            TotalCompleted = 60,
            AverageExecutionDurationMs = 2000.0,  // 2 seconds per request
            AverageQueueWaitMs = 1000.0,         // 1 second average wait
            InFlightExecutions = 3                // 3 concurrent
        };
        mockQueue.Setup(q => q.GetMetricsAsync()).ReturnsAsync(metrics);
        
        using var service = new DashboardService(_mockLogger.Object, mockQueue.Object, null);

        // Act
        var data = await service.GetDashboardDataAsync();

        // Assert
        Assert.NotNull(data.Queue);
        Assert.NotNull(data.Queue.AverageTaskDurationSeconds);
        Assert.Equal(2.0, data.Queue.AverageTaskDurationSeconds.Value, precision: 1);
        Assert.NotNull(data.Queue.EstimatedWaitSeconds);
        Assert.Equal(1.0, data.Queue.EstimatedWaitSeconds.Value, precision: 1);
        Assert.NotNull(data.Queue.ThroughputPerMinute);
        Assert.Equal(60.0, data.Queue.ThroughputPerMinute.Value);
        Assert.True(data.Queue.ModelUtilizationPercent >= 0 && data.Queue.ModelUtilizationPercent <= 100);
    }

    [Fact]
    public async Task GetDashboardDataAsync_WithoutModelQueue_ShouldReturnDefaultQueueStatus()
    {
        // Arrange - No model queue injected
        using var service = new DashboardService(_mockLogger.Object, null, null);

        // Act
        var data = await service.GetDashboardDataAsync();

        // Assert
        Assert.NotNull(data.Queue);
        Assert.Equal(0, data.Queue.PendingCount);
        Assert.Equal(0, data.Queue.CompletedCount);
        Assert.Null(data.Queue.CurrentModel);
        Assert.Empty(data.Queue.TopItems);
        Assert.Empty(data.Queue.AllPendingItems);
    }

    [Fact]
    public async Task GetDashboardDataAsync_WhenModelQueueThrows_ShouldReturnDefaults()
    {
        // Arrange
        var mockQueue = new Mock<IModelQueue>();
        mockQueue.Setup(q => q.GetQueueStatusAsync())
            .ThrowsAsync(new InvalidOperationException("Queue service error"));
        
        using var service = new DashboardService(_mockLogger.Object, mockQueue.Object, null);

        // Act
        var data = await service.GetDashboardDataAsync();

        // Assert
        Assert.NotNull(data.Queue);
        Assert.Equal(0, data.Queue.PendingCount);
        Assert.Equal(0, data.Queue.CompletedCount);
    }

    [Fact]
    public void QueueStatus_GetItemsByProject_ShouldFilterCorrectly()
    {
        // Arrange
        var queueStatus = new Daiv3.App.Maui.Models.QueueStatus
        {
            AllPendingItems = new List<Daiv3.App.Maui.Models.QueueItemSummary>
            {
                new() { Id = "1", ProjectId = "proj-A" },
                new() { Id = "2", ProjectId = "proj-B" },
                new() { Id = "3", ProjectId = "proj-A" },
                new() { Id = "4", ProjectId = null }
            }
        };

        // Act
        var projAItems = queueStatus.GetItemsByProject("proj-A");
        var projBItems = queueStatus.GetItemsByProject("proj-B");
        var allItems = queueStatus.GetItemsByProject(null);

        // Assert
        Assert.Equal(2, projAItems.Count);
        Assert.Single(projBItems);
        Assert.Equal(4, allItems.Count);
    }

    // ── Scheduled Jobs Tests (CT-REQ-008) ──────────────────────────────

    [Fact]
    public async Task GetDashboardDataAsync_WithNoScheduler_ShouldReturnEmptyScheduledJobs()
    {
        // Arrange
        using var service = new DashboardService(_mockLogger.Object, null, null, null, null, null, null, null);

        // Act
        var data = await service.GetDashboardDataAsync();

        // Assert
        Assert.NotNull(data.ScheduledJobs);
        Assert.False(data.ScheduledJobs.HasScheduledJobs);
        Assert.Equal(0, data.ScheduledJobs.TotalJobs);
        Assert.Empty(data.ScheduledJobs.Jobs);
    }

    [Fact]
    public async Task GetDashboardDataAsync_WithNoJobs_ShouldReturnEmptyScheduledJobs()
    {
        // Arrange
        var mockScheduler = new Mock<IScheduler>();
        mockScheduler.Setup(s => s.GetAllJobsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ScheduledJobMetadata>());

        using var service = new DashboardService(_mockLogger.Object, null, null, null, null, null, null, mockScheduler.Object);

        // Act
        var data = await service.GetDashboardDataAsync();

        // Assert
        Assert.NotNull(data.ScheduledJobs);
        Assert.False(data.ScheduledJobs.HasScheduledJobs);
        Assert.Equal(0, data.ScheduledJobs.TotalJobs);
    }

    [Fact]
    public async Task GetDashboardDataAsync_WithScheduledJobs_ShouldReturnCorrectCounts()
    {
        // Arrange
        var mockScheduler = new Mock<IScheduler>();
        var jobMetadata = new List<ScheduledJobMetadata>
        {
            new() { JobId = "job1", JobName = "Job 1", Status = ScheduledJobStatus.Pending, ScheduleType = ScheduleType.OneTime, CreatedAtUtc = DateTime.UtcNow },
            new() { JobId = "job2", JobName = "Job 2", Status = ScheduledJobStatus.Running, ScheduleType = ScheduleType.Recurring, CreatedAtUtc = DateTime.UtcNow },
            new() { JobId = "job3", JobName = "Job 3", Status = ScheduledJobStatus.Scheduled, ScheduleType = ScheduleType.Cron, CreatedAtUtc = DateTime.UtcNow },
            new() { JobId = "job4", JobName = "Job 4", Status = ScheduledJobStatus.Failed, ScheduleType = ScheduleType.OneTime, CreatedAtUtc = DateTime.UtcNow, LastErrorMessage = "Test error" },
            new() { JobId = "job5", JobName = "Job 5", Status = ScheduledJobStatus.Completed, ScheduleType = ScheduleType.OneTime, CreatedAtUtc = DateTime.UtcNow }
        };

        mockScheduler.Setup(s => s.GetAllJobsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(jobMetadata);

        using var service = new DashboardService(_mockLogger.Object, null, null, null, null, null, null, mockScheduler.Object);

        // Act
        var data = await service.GetDashboardDataAsync();

        // Assert
        Assert.NotNull(data.ScheduledJobs);
        Assert.True(data.ScheduledJobs.HasScheduledJobs);
        Assert.Equal(5, data.ScheduledJobs.TotalJobs);
        Assert.Equal(1, data.ScheduledJobs.PendingCount);
        Assert.Equal(1, data.ScheduledJobs.RunningCount);
        Assert.Equal(1, data.ScheduledJobs.ScheduledCount);
        Assert.Equal(1, data.ScheduledJobs.FailedCount);
        Assert.Equal(1, data.ScheduledJobs.CompletedCount);
        Assert.Equal(5, data.ScheduledJobs.Jobs.Count);
    }

    [Fact]
    public async Task GetDashboardDataAsync_WithScheduledJobs_ShouldMapJobProperties()
    {
        // Arrange
        var mockScheduler = new Mock<IScheduler>();
        var scheduledTime = DateTime.UtcNow.AddHours(1);
        var lastCompleted = DateTime.UtcNow.AddMinutes(-30);
        var jobMetadata = new List<ScheduledJobMetadata>
        {
            new()
            {
                JobId = "job1",
                JobName = "Test Job",
                Status = ScheduledJobStatus.Scheduled,
                ScheduleType = ScheduleType.Cron,
                CreatedAtUtc = DateTime.UtcNow,
                ScheduledAtUtc = scheduledTime,
                LastCompletedAtUtc = lastCompleted,
                LastExecutionDuration = TimeSpan.FromSeconds(5.5),
                ExecutionCount = 3,
                CronExpression = "0 0 * * *",
                LastErrorMessage = null
            }
        };

        mockScheduler.Setup(s => s.GetAllJobsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(jobMetadata);

        using var service = new DashboardService(_mockLogger.Object, null, null, null, null, null, null, mockScheduler.Object);

        // Act
        var data = await service.GetDashboardDataAsync();

        // Assert
        var job = data.ScheduledJobs.Jobs.First();
        Assert.Equal("job1", job.JobId);
        Assert.Equal("Test Job", job.JobName);
        Assert.Equal("Scheduled", job.Status);
        Assert.Equal("Cron", job.ScheduleType);
        Assert.Equal(scheduledTime, job.NextRunTime);
        Assert.Equal(lastCompleted, job.LastCompletionTime);
        Assert.Equal(TimeSpan.FromSeconds(5.5), job.LastExecutionDuration);
        Assert.Equal(3, job.ExecutionCount);
        Assert.Equal("0 0 * * *", job.CronExpression);
        Assert.Null(job.LastErrorMessage);
    }

    [Fact]
    public async Task GetDashboardDataAsync_WhenSchedulerThrows_ShouldReturnEmptyScheduledJobs()
    {
        // Arrange
        var mockScheduler = new Mock<IScheduler>();
        mockScheduler.Setup(s => s.GetAllJobsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Scheduler error"));

        using var service = new DashboardService(_mockLogger.Object, null, null, null, null, null, null, mockScheduler.Object);

        // Act
        var data = await service.GetDashboardDataAsync();

        // Assert
        Assert.NotNull(data.ScheduledJobs);
        Assert.False(data.ScheduledJobs.HasScheduledJobs);
        Assert.Equal(0, data.ScheduledJobs.TotalJobs);
    }

    // ── CT-REQ-012: Background Tasks Tests ─────────────────────────────

    [Fact]
    public async Task GetDashboardDataAsync_BackgroundTasks_ShouldBePopulated()
    {
        // Arrange
        var mockScheduler = new Mock<IScheduler>();
        var runningJob = new ScheduledJobMetadata
        {
            JobId = "test-job-1",
            JobName = "Test Running Job",
            Status = ScheduledJobStatus.Running,
            ScheduleType = ScheduleType.OneTime,
            LastStartedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            ExecutionCount = 1
        };
        mockScheduler.Setup(s => s.GetAllJobsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ScheduledJobMetadata> { runningJob });

        using var service = new DashboardService(_mockLogger.Object, null, null, null, null, null, null, mockScheduler.Object);

        // Act
        var data = await service.GetDashboardDataAsync();

        // Assert
        Assert.NotNull(data.BackgroundTasks);
        Assert.True(data.BackgroundTasks.HasTasks);
        Assert.Equal(1, data.BackgroundTasks.TotalTasks);
        Assert.Equal(1, data.BackgroundTasks.RunningCount);
        Assert.Single(data.BackgroundTasks.Tasks);
        
        var task = data.BackgroundTasks.Tasks[0];
        Assert.Equal("test-job-1", task.TaskId);
        Assert.Equal("Test Running Job", task.Name);
        Assert.Equal(Models.TaskStatus.Running, task.Status);
        Assert.Equal("Scheduler", task.AgentName);
    }
}

/// <summary>
/// Unit tests for DashboardConfiguration.
/// </summary>
public class DashboardConfigurationTests
{
    [Fact]
    public void Constructor_ShouldSetDefaults()
    {
        // Act
        var config = new DashboardConfiguration();

        // Assert
        Assert.Equal(DashboardConfiguration.DefaultRefreshIntervalMs, config.RefreshIntervalMs);
        Assert.True(config.EnableCaching);
        Assert.True(config.EnableLogging);
        Assert.True(config.ContinueOnError);
    }

    [Fact]
    public void Validate_WithValidValues_ShouldSucceed()
    {
        // Arrange
        var config = new DashboardConfiguration();

        // Act & Assert
        Assert.True(config.Validate());
    }

    [Fact]
    public void Validate_WithRefreshIntervalTooLow_ShouldThrow()
    {
        // Arrange
        var config = new DashboardConfiguration
        {
            RefreshIntervalMs = DashboardConfiguration.MinRefreshIntervalMs - 1
        };

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => config.Validate());
    }

    [Fact]
    public void Validate_WithRefreshIntervalTooHigh_ShouldThrow()
    {
        // Arrange
        var config = new DashboardConfiguration
        {
            RefreshIntervalMs = DashboardConfiguration.MaxRefreshIntervalMs + 1
        };

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => config.Validate());
    }

    [Fact]
    public void Validate_WithNegativeTimeout_ShouldThrow()
    {
        // Arrange
        var config = new DashboardConfiguration
        {
            DataCollectionTimeoutMs = -1
        };

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => config.Validate());
    }
}

/// <summary>
/// Unit tests for DashboardData model.
/// </summary>
public class DashboardDataTests
{
    [Fact]
    public void IsValid_WhenNoError_ShouldBeTrue()
    {
        // Arrange
        var data = new DashboardData();

        // Act
        var isValid = data.IsValid;

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void IsValid_WhenErrorPresent_ShouldBeFalse()
    {
        // Arrange
        var data = new DashboardData { CollectionError = "Test error" };

        // Act
        var isValid = data.IsValid;

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void QueueStatus_ShouldHaveDefaultValues()
    {
        // Arrange
        var data = new DashboardData();

        // Act
        var queue = data.Queue;

        // Assert
        Assert.NotNull(queue);
        Assert.Equal(0, queue.PendingCount);
        Assert.NotNull(queue.TopItems);
    }

    [Fact]
    public void SystemResourceMetrics_ShouldCalculateDiskUtilization()
    {
        // Arrange
        var metrics = new SystemResourceMetrics
        {
            TotalDiskBytes = 1000,
            AvailableDiskBytes = 300
        };

        // Act
        var utilization = metrics.DiskUtilizationPercent;

        // Assert
        Assert.Equal(70.0, utilization);
    }

    [Fact]
    public void SystemResourceMetrics_WithZeroTotal_ShouldReturnZeroUtilization()
    {
        // Arrange
        var metrics = new SystemResourceMetrics
        {
            TotalDiskBytes = 0,
            AvailableDiskBytes = 0
        };

        // Act
        var utilization = metrics.DiskUtilizationPercent;

        // Assert
        Assert.Equal(0, utilization);
    }

    [Fact]
    public void ScheduledJobsStatus_ShouldHaveDefaultValues()
    {
        // Arrange
        var data = new DashboardData();

        // Act
        var scheduled = data.ScheduledJobs;

        // Assert
        Assert.NotNull(scheduled);
        Assert.False(scheduled.HasScheduledJobs);
        Assert.Equal(0, scheduled.TotalJobs);
        Assert.NotNull(scheduled.Jobs);
    }

    [Fact]
    public void ScheduledJobsStatus_NextJob_ShouldReturnEarliestScheduled()
    {
        // Arrange
        var status = new ScheduledJobsStatus
        {
            Jobs = new List<ScheduledJobSummary>
            {
                new() { JobId = "job1", JobName = "Later Job", NextRunTime = DateTime.UtcNow.AddHours(5) },
                new() { JobId = "job2", JobName = "Earlier Job", NextRunTime = DateTime.UtcNow.AddHours(1) },
                new() { JobId = "job3", JobName = "Latest Job", NextRunTime = DateTime.UtcNow.AddHours(10) }
            }
        };

        // Act
        var nextJob = status.NextJob;

        // Assert
        Assert.NotNull(nextJob);
        Assert.Equal("job2", nextJob!.JobId);
        Assert.Equal("Earlier Job", nextJob.JobName);
    }

    [Fact]
    public void ScheduledJobsStatus_NextJob_WithNoScheduledJobs_ShouldReturnNull()
    {
        // Arrange
        var status = new ScheduledJobsStatus
        {
            Jobs = new List<ScheduledJobSummary>
            {
                new() { JobId = "job1", JobName = "Completed Job", Status = "Completed" }
            }
        };

        // Act
        var nextJob = status.NextJob;

        // Assert
        Assert.Null(nextJob);
    }

    [Fact]
    public void ScheduledJobsStatus_HasErrors_ShouldBeTrueWhenFailuresExist()
    {
        // Arrange
        var status = new ScheduledJobsStatus
        {
            FailedCount = 3
        };

        // Act
        var hasErrors = status.HasErrors;

        // Assert
        Assert.True(hasErrors);
    }

    [Fact]
    public void ScheduledJobsStatus_ActiveJobsCount_ShouldSumPendingRunningScheduled()
    {
        // Arrange
        var status = new ScheduledJobsStatus
        {
            PendingCount = 2,
            RunningCount = 1,
            ScheduledCount = 5
        };

        // Act
        var activeCount = status.ActiveJobsCount;

        // Assert
        Assert.Equal(8, activeCount);
    }

    [Fact]
    public void ScheduledJobSummary_NextRunDescription_ForEventTriggered_ShouldReturnOnEvent()
    {
        // Arrange
        var job = new ScheduledJobSummary
        {
            ScheduleType = "EventTriggered"
        };

        // Act
        var description = job.NextRunDescription;

        // Assert
        Assert.Equal("On event", description);
    }

    [Fact]
    public void ScheduledJobSummary_NextRunDescription_ForOverdue_ShouldReturnOverdue()
    {
        // Arrange
        var job = new ScheduledJobSummary
        {
            ScheduleType = "OneTime",
            NextRunTime = DateTime.UtcNow.AddMinutes(-10)
        };

        // Act
        var description = job.NextRunDescription;

        // Assert
        Assert.Equal("Overdue", description);
    }

    [Fact]
    public void ScheduledJobSummary_ScheduleDescription_ForCron_ShouldIncludeCronExpression()
    {
        // Arrange
        var job = new ScheduledJobSummary
        {
            CronExpression = "0 0 * * *"
        };

        // Act
        var description = job.ScheduleDescription;

        // Assert
        Assert.Equal("Cron: 0 0 * * *", description);
    }

    [Fact]
    public void ScheduledJobSummary_ScheduleDescription_ForRecurring_ShouldShowInterval()
    {
        // Arrange
        var job = new ScheduledJobSummary
        {
            IntervalSeconds = 300
        };

        // Act
        var description = job.ScheduleDescription;

        // Assert
        Assert.Equal("Every 300s", description);
    }

    [Fact]
    public void ScheduledJobSummary_HasError_WithErrorMessage_ShouldBeTrue()
    {
        // Arrange
        var job = new ScheduledJobSummary
        {
            LastErrorMessage = "Connection timeout"
        };

        // Act
        var hasError = job.HasError;

        // Assert
        Assert.True(hasError);
    }

    [Fact]
    public void ScheduledJobSummary_HasError_WithFailedStatus_ShouldBeTrue()
    {
        // Arrange
        var job = new ScheduledJobSummary
        {
            Status = "Failed"
        };

        // Act
        var hasError = job.HasError;

        // Assert
        Assert.True(hasError);
    }

    // ── CT-REQ-012: Background Task Model Tests ───────────────────────

    [Fact]
    public void BackgroundTaskInfo_StatusIcon_ShouldReturnCorrectEmojis()
    {
        // Arrange & Act & Assert
        var queuedTask = new BackgroundTaskInfo { Status = Models.TaskStatus.Queued };
        Assert.Equal("🔵", queuedTask.StatusIcon);

        var runningTask = new BackgroundTaskInfo { Status = Models.TaskStatus.Running };
        Assert.Equal("🟢", runningTask.StatusIcon);

        var failedTask = new BackgroundTaskInfo { Status = Models.TaskStatus.Failed };
        Assert.Equal("❌", failedTask.StatusIcon);

        var completedTask = new BackgroundTaskInfo { Status = Models.TaskStatus.Completed };
        Assert.Equal("✅", completedTask.StatusIcon);
    }

    [Fact]
    public void BackgroundTaskInfo_CanCancel_ShouldBeCorrect()
    {
        // Arrange
        var queuedTask = new BackgroundTaskInfo { Status = Models.TaskStatus.Queued };
        var runningTask = new BackgroundTaskInfo { Status = Models.TaskStatus.Running };
        var completedTask = new BackgroundTaskInfo { Status = Models.TaskStatus.Completed };

        // Act & Assert
        Assert.True(queuedTask.CanCancel);
        Assert.True(runningTask.CanCancel);
        Assert.False(completedTask.CanCancel);
    }

    [Fact]
    public void BackgroundTaskInfo_ElapsedTimeText_ShouldFormatCorrectly()
    {
        // Arrange
        var task1 = new BackgroundTaskInfo { ElapsedTime = TimeSpan.FromSeconds(45) };
        var task2 = new BackgroundTaskInfo { ElapsedTime = TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(30)) };
        var task3 = new BackgroundTaskInfo { ElapsedTime = TimeSpan.FromHours(2).Add(TimeSpan.FromMinutes(15)) };

        // Act & Assert
        Assert.Equal("45s", task1.ElapsedTimeText);
        Assert.Equal("5m 30s", task2.ElapsedTimeText);
        Assert.Equal("2h 15m", task3.ElapsedTimeText);
    }

    [Fact]
    public void TaskMetrics_MemoryText_ShouldFormatCorrectly()
    {
        // Arrange
        var metrics1 = new TaskMetrics { MemoryBytes = 512 };
        var metrics2 = new TaskMetrics { MemoryBytes = 256 * 1024 * 1024 };
        var metrics3 = new TaskMetrics { MemoryBytes = 2L * 1024 * 1024 * 1024 };

        // Act & Assert
        Assert.Equal("512 B", metrics1.MemoryText);
        Assert.Equal("256.0 MB", metrics2.MemoryText);
        Assert.Equal("2.0 GB", metrics3.MemoryText);
    }

    [Fact]
    public void BackgroundTasksStatus_Aggregations_ShouldCalculateCorrectly()
    {
        // Arrange
        var status = new BackgroundTasksStatus
        {
            Tasks = new List<BackgroundTaskInfo>
            {
                new BackgroundTaskInfo
                {
                    Status = Models.TaskStatus.Running,
                    Metrics = new TaskMetrics { CpuPercent = 25.0, MemoryBytes = 100 * 1024 * 1024 }
                },
                new BackgroundTaskInfo
                {
                    Status = Models.TaskStatus.Running,
                    Metrics = new TaskMetrics { CpuPercent = 15.0, MemoryBytes = 50 * 1024 * 1024 }
                },
                new BackgroundTaskInfo
                {
                    Status = Models.TaskStatus.Completed,
                    Metrics = new TaskMetrics { CpuPercent = 0, MemoryBytes = 0 }
                }
            },
            RunningCount = 2,
            CompletedCount = 1
        };

        // Act
        var totalCpu = status.TotalCpuPercent;
        var totalMemory = status.TotalMemoryBytes;

        // Assert
        Assert.Equal(40.0, totalCpu); // 25 + 15 (completed task excluded)
        Assert.Equal(150 * 1024 * 1024, totalMemory); // 100MB + 50MB
    }
}
