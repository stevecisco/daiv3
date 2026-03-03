using Daiv3.Scheduler;
using Daiv3.WebFetch.Crawl;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Daiv3.UnitTests.WebFetch;

/// <summary>
/// Unit tests for the WebRefreshScheduler service.
/// Tests the core refetch scheduling functionality for WFC-REQ-008.
/// </summary>
public class WebRefreshSchedulerTests
{
    private readonly Mock<IScheduler> _mockScheduler = new();
    private readonly Mock<IWebFetcher> _mockWebFetcher = new();
    private readonly Mock<IMarkdownContentStore> _mockContentStore = new();
    private readonly ILogger<WebRefreshScheduler> _logger;
    private readonly IServiceProvider _serviceProvider;

    public WebRefreshSchedulerTests()
    {
        // Set up logging for tests
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddDebug());
        _serviceProvider = services.BuildServiceProvider();
        _logger = _serviceProvider.GetRequiredService<ILogger<WebRefreshScheduler>>();
    }

    [Fact]
    public async Task ScheduleRefetchAsync_WithValidUrl_SchedulesSuccessfully()
    {
        // Arrange
        var options = new WebRefreshSchedulerOptions();
        var scheduler = new WebRefreshScheduler(
            _mockScheduler.Object,
            _mockWebFetcher.Object,
            _mockContentStore.Object,
            _logger,
            options);

        var url = "https://example.com/page";
        const uint intervalSeconds = 3600;
        var expectedJobId = "job-123";

        _mockScheduler
            .Setup(s => s.ScheduleRecurringAsync(
                It.IsAny<IScheduledJob>(),
                It.Is<uint>(i => i == intervalSeconds),
                It.IsAny<uint?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedJobId);

        // Act
        var result = await scheduler.ScheduleRefetchAsync(url, intervalSeconds);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(expectedJobId, result.SchedulerJobId);
        Assert.Null(result.ErrorMessage);
        Assert.Null(result.FailureReason);
    }

    [Fact]
    public async Task ScheduleRefetchAsync_WithIntervalBelowMinimum_Fails()
    {
        // Arrange
        var options = new WebRefreshSchedulerOptions { MinIntervalSeconds = 3600 };
        var scheduler = new WebRefreshScheduler(
            _mockScheduler.Object,
            _mockWebFetcher.Object,
            _mockContentStore.Object,
            _logger,
            options);

        var url = "https://example.com/page";
        const uint invalidIntervalSeconds = 60; // Below minimum of 3600

        // Act
        var result = await scheduler.ScheduleRefetchAsync(url, invalidIntervalSeconds);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("InvalidInterval", result.FailureReason);
        Assert.NotNull(result.ErrorMessage);
        Assert.Null(result.SchedulerJobId);
    }

    [Fact]
    public async Task ScheduleRefetchAsync_WithNullUrl_Fails()
    {
        // Arrange
        var options = new WebRefreshSchedulerOptions();
        var scheduler = new WebRefreshScheduler(
            _mockScheduler.Object,
            _mockWebFetcher.Object,
            _mockContentStore.Object,
            _logger,
            options);

        const uint intervalSeconds = 3600;

        // Act
        var result = await scheduler.ScheduleRefetchAsync(null!, intervalSeconds);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("InvalidUrl", result.FailureReason);
    }

    [Fact]
    public async Task ScheduleRefetchAsync_WithDisabledScheduler_Fails()
    {
        // Arrange
        var options = new WebRefreshSchedulerOptions { Enabled = false };
        var scheduler = new WebRefreshScheduler(
            _mockScheduler.Object,
            _mockWebFetcher.Object,
            _mockContentStore.Object,
            _logger,
            options);

        var url = "https://example.com/page";
        const uint intervalSeconds = 3600;

        // Act
        var result = await scheduler.ScheduleRefetchAsync(url, intervalSeconds);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("SchedulerDisabled", result.FailureReason);
    }

    [Fact]
    public async Task ScheduleRefetchAsync_WithSameUrlTwice_UpdatesInterval()
    {
        // Arrange
        var options = new WebRefreshSchedulerOptions();
        var scheduler = new WebRefreshScheduler(
            _mockScheduler.Object,
            _mockWebFetcher.Object,
            _mockContentStore.Object,
            _logger,
            options);

        var url = "https://example.com/page";
        const uint initialIntervalSeconds = 3600;
        const uint newIntervalSeconds = 7200;
        const string jobId = "job-123";

        _mockScheduler
            .Setup(s => s.ScheduleRecurringAsync(
                It.IsAny<IScheduledJob>(),
                It.IsAny<uint>(),
                It.IsAny<uint?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(jobId);

        _mockScheduler
            .Setup(s => s.ModifyJobScheduleAsync(
                It.Is<string>(j => j == jobId),
                It.IsAny<ScheduleModificationRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act - Schedule initially
        var result1 = await scheduler.ScheduleRefetchAsync(url, initialIntervalSeconds);
        Assert.True(result1.Success);

        // Act - Schedule same URL with new interval
        var result2 = await scheduler.ScheduleRefetchAsync(url, newIntervalSeconds);

        // Assert
        Assert.True(result2.Success);
        _mockScheduler.Verify(
            s => s.ModifyJobScheduleAsync(
                It.Is<string>(j => j == jobId),
                It.IsAny<ScheduleModificationRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CancelRefetchAsync_WithScheduledUrl_CancelsSuccessfully()
    {
        // Arrange
        var options = new WebRefreshSchedulerOptions();
        var scheduler = new WebRefreshScheduler(
            _mockScheduler.Object,
            _mockWebFetcher.Object,
            _mockContentStore.Object,
            _logger,
            options);

        var url = "https://example.com/page";
        const uint intervalSeconds = 3600;
        const string jobId = "job-123";

        _mockScheduler
            .Setup(s => s.ScheduleRecurringAsync(
                It.IsAny<IScheduledJob>(),
                It.IsAny<uint>(),
                It.IsAny<uint?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(jobId);

        _mockScheduler
            .Setup(s => s.CancelJobAsync(
                It.Is<string>(j => j == jobId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Schedule first
        await scheduler.ScheduleRefetchAsync(url, intervalSeconds);

        // Act
        var cancelled = await scheduler.CancelRefetchAsync(url);

        // Assert
        Assert.True(cancelled);
        _mockScheduler.Verify(
            s => s.CancelJobAsync(
                It.Is<string>(j => j == jobId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CancelRefetchAsync_WithUnscheduledUrl_ReturnsFalse()
    {
        // Arrange
        var options = new WebRefreshSchedulerOptions();
        var scheduler = new WebRefreshScheduler(
            _mockScheduler.Object,
            _mockWebFetcher.Object,
            _mockContentStore.Object,
            _logger,
            options);

        var url = "https://example.com/page";

        // Act
        var cancelled = await scheduler.CancelRefetchAsync(url);

        // Assert
        Assert.False(cancelled);
        _mockScheduler.Verify(
            s => s.CancelJobAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetScheduledRefetchesAsync_ReturnsAllScheduledUrls()
    {
        // Arrange
        var options = new WebRefreshSchedulerOptions();
        var scheduler = new WebRefreshScheduler(
            _mockScheduler.Object,
            _mockWebFetcher.Object,
            _mockContentStore.Object,
            _logger,
            options);

        var url1 = "https://example.com/page1";
        var url2 = "https://example.com/page2";
        const uint intervalSeconds = 3600;
        const string jobId1 = "job-1";
        const string jobId2 = "job-2";

        _mockScheduler
            .SetupSequence(s => s.ScheduleRecurringAsync(
                It.IsAny<IScheduledJob>(),
                It.IsAny<uint>(),
                It.IsAny<uint?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(jobId1)
            .ReturnsAsync(jobId2);

        // Act
        await scheduler.ScheduleRefetchAsync(url1, intervalSeconds);
        await scheduler.ScheduleRefetchAsync(url2, intervalSeconds);
        var scheduled = await scheduler.GetScheduledRefetchesAsync();

        // Assert
        Assert.NotNull(scheduled);
        Assert.Equal(2, scheduled.Count);
        Assert.Contains(scheduled, m => m.SourceUrl == url1);
        Assert.Contains(scheduled, m => m.SourceUrl == url2);
    }

    [Fact]
    public async Task GetRefetchMetadataAsync_WithScheduledUrl_ReturnsMetadata()
    {
        // Arrange
        var options = new WebRefreshSchedulerOptions();
        var scheduler = new WebRefreshScheduler(
            _mockScheduler.Object,
            _mockWebFetcher.Object,
            _mockContentStore.Object,
            _logger,
            options);

        var url = "https://example.com/page";
        const uint intervalSeconds = 3600;
        const string jobId = "job-123";

        _mockScheduler
            .Setup(s => s.ScheduleRecurringAsync(
                It.IsAny<IScheduledJob>(),
                It.IsAny<uint>(),
                It.IsAny<uint?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(jobId);

        // Schedule
        await scheduler.ScheduleRefetchAsync(url, intervalSeconds);

        // Act
        var metadata = await scheduler.GetRefetchMetadataAsync(url);

        // Assert
        Assert.NotNull(metadata);
        Assert.Equal(url, metadata.SourceUrl);
        Assert.Equal(jobId, metadata.SchedulerJobId);
        Assert.Equal(intervalSeconds, metadata.IntervalSeconds);
        Assert.Equal("Active", metadata.Status);
    }

    [Fact]
    public async Task GetRefetchMetadataAsync_WithUnscheduledUrl_ReturnsNull()
    {
        // Arrange
        var options = new WebRefreshSchedulerOptions();
        var scheduler = new WebRefreshScheduler(
            _mockScheduler.Object,
            _mockWebFetcher.Object,
            _mockContentStore.Object,
            _logger,
            options);

        var url = "https://example.com/nonexistent";

        // Act
        var metadata = await scheduler.GetRefetchMetadataAsync(url);

        // Assert
        Assert.Null(metadata);
    }

    [Fact]
    public async Task UpdateRefetchIntervalAsync_WithValidInterval_UpdatesSuccessfully()
    {
        // Arrange
        var options = new WebRefreshSchedulerOptions();
        var scheduler = new WebRefreshScheduler(
            _mockScheduler.Object,
            _mockWebFetcher.Object,
            _mockContentStore.Object,
            _logger,
            options);

        var url = "https://example.com/page";
        const uint initialIntervalSeconds = 3600;
        const uint newIntervalSeconds = 7200;
        const string jobId = "job-123";

        _mockScheduler
            .Setup(s => s.ScheduleRecurringAsync(
                It.IsAny<IScheduledJob>(),
                It.IsAny<uint>(),
                It.IsAny<uint?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(jobId);

        _mockScheduler
            .Setup(s => s.ModifyJobScheduleAsync(
                It.Is<string>(j => j == jobId),
                It.IsAny<ScheduleModificationRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Schedule first
        await scheduler.ScheduleRefetchAsync(url, initialIntervalSeconds);

        // Act
        var updated = await scheduler.UpdateRefetchIntervalAsync(url, newIntervalSeconds);

        // Assert
        Assert.True(updated);
        var metadata = await scheduler.GetRefetchMetadataAsync(url);
        Assert.NotNull(metadata);
        Assert.Equal(newIntervalSeconds, metadata.IntervalSeconds);
    }

    [Fact]
    public async Task UpdateRefetchIntervalAsync_WithInvalidInterval_Fails()
    {
        // Arrange
        var options = new WebRefreshSchedulerOptions { MinIntervalSeconds = 3600 };
        var scheduler = new WebRefreshScheduler(
            _mockScheduler.Object,
            _mockWebFetcher.Object,
            _mockContentStore.Object,
            _logger,
            options);

        var url = "https://example.com/page";
        const uint initialIntervalSeconds = 7200;
        const uint invalidNewIntervalSeconds = 60; // Below minimum

        _mockScheduler
            .Setup(s => s.ScheduleRecurringAsync(
                It.IsAny<IScheduledJob>(),
                It.IsAny<uint>(),
                It.IsAny<uint?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("job-123");

        // Schedule first
        await scheduler.ScheduleRefetchAsync(url, initialIntervalSeconds);

        // Act
        var updated = await scheduler.UpdateRefetchIntervalAsync(url, invalidNewIntervalSeconds);

        // Assert
        Assert.False(updated);
    }
}
