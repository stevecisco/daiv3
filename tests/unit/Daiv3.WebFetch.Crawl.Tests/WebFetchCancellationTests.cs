using Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using Daiv3.WebFetch.Crawl;

#pragma warning disable IDISP006, IDISP003 // Test classes don't need to implement IDisposable; Test methods create disposable instances without explicit disposal (xUnit cleanup handles it)

namespace Daiv3.WebFetch.Crawl.Tests;

/// <summary>
/// Integration tests for fetch operation cancellation behavior.
/// </summary>
public class WebFetchCancellationTests : IAsyncLifetime
{
    private HttpClient _httpClient = null!;
    private Mock<IHtmlParser> _mockHtmlParser = null!;
    private ILogger<WebFetcher> _logger = null!;
    private CancellationMetrics _metrics = null!;

    public Task InitializeAsync()
    {
        _httpClient = new HttpClient();
        _mockHtmlParser = new Mock<IHtmlParser>();
        _mockHtmlParser
            .Setup(p => p.ParseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<IHtmlDocument>());

        var mockLoggerFactory = new Mock<ILoggerFactory>();
        var mockLogger = new Mock<ILogger<WebFetcher>>();
        mockLoggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(mockLogger.Object);

        _logger = mockLogger.Object;
        _metrics = new CancellationMetrics();

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _httpClient?.Dispose();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task FetchAsync_WithUserCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var fetcher = new WebFetcher(_httpClient, _logger, _mockHtmlParser.Object, null, _metrics);
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(50); // Cancel immediately

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => fetcher.FetchAsync("https://httpbin.org/delay/30", cts.Token));
    }

    [Fact]
    public async Task FetchAsync_WithUserCancellation_RecordsCancellationMetrics()
    {
        // Arrange
        var fetcher = new WebFetcher(_httpClient, _logger, _mockHtmlParser.Object, null, _metrics);
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(50);

        // Act
        try
        {
            await fetcher.FetchAsync("https://httpbin.org/delay/30", cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert
        var snapshot = _metrics.GetSnapshot();
        Assert.Equal(1, snapshot.TotalCancellations);
        Assert.Equal(1, snapshot.UserRequestedCancellations);
        Assert.Contains("Fetch", snapshot.CancellationsByOperationType.Keys);
    }

    [Fact]
    public async Task FetchAsync_WithTimeoutCancellation_RecordsTimeoutMetrics()
    {
        // Arrange
        var options = new WebFetcherOptions { RequestTimeoutMs = 50 };
        var fetcher = new WebFetcher(_httpClient, _logger, _mockHtmlParser.Object, options, _metrics);

        // Act
        try
        {
            await fetcher.FetchAsync("https://httpbin.org/delay/30");
        }
        catch (InvalidOperationException ex)
        {
            // Timeout throws InvalidOperationException wrapping the timeout
            Assert.Contains("timed out", ex.Message);
        }

        // Assert
        var snapshot = _metrics.GetSnapshot();
        Assert.Equal(1, snapshot.TotalCancellations);
        Assert.Equal(1, snapshot.TimeoutCancellations);
    }

    [Fact]
    public async Task FetchAsync_CancellationLatencyIsRecorded()
    {
        // Arrange
        var fetcher = new WebFetcher(_httpClient, _logger, _mockHtmlParser.Object, null, _metrics);
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(50); // Cancel after 50ms

        // Act
        try
        {
            await fetcher.FetchAsync("https://httpbin.org/delay/30", cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert
        var snapshot = _metrics.GetSnapshot();
        Assert.True(snapshot.FastestCancellationMs >= 0);
        Assert.True(snapshot.AverageCancellationLatencyMs >= 0);
    }

    [Fact]
    public async Task FetchAsync_MultipleCancellations_AggregatesMetrics()
    {
        // Arrange
        var fetcher = new WebFetcher(_httpClient, _logger, _mockHtmlParser.Object, null, _metrics);

        // Act - Record multiple cancellations
        for (int i = 0; i < 3; i++)
        {
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(50);

            try
            {
                await fetcher.FetchAsync("https://httpbin.org/delay/30", cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        // Assert
        var snapshot = _metrics.GetSnapshot();
        Assert.Equal(3, snapshot.TotalCancellations);
        Assert.Equal(3, snapshot.UserRequestedCancellations);
        Assert.Contains("Fetch", snapshot.CancellationsByOperationType.Keys);
    }

    [Fact]
    public async Task FetchAsync_WithNullUrl_ThrowsArgumentNullException()
    {
        // Arrange
        var fetcher = new WebFetcher(_httpClient, _logger, _mockHtmlParser.Object, null, _metrics);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => fetcher.FetchAsync(null!));
    }

    [Fact]
    public void WebFetcher_WithoutMetricsService_UsesDefaultMetrics()
    {
        // Arrange & Act
        var fetcher = new WebFetcher(_httpClient, _logger, _mockHtmlParser.Object, null, null);

        // Assert - Should not throw (default metrics will be created)
        Assert.NotNull(fetcher);
    }

    [Fact]
    public async Task FetchAsync_CancellationRecordsOperationType()
    {
        // Arrange
        var fetcher = new WebFetcher(_httpClient, _logger, _mockHtmlParser.Object, null, _metrics);
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(50);

        // Act
        try
        {
            await fetcher.FetchAsync("https://httpbin.org/delay/30", cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert
        var snapshot = _metrics.GetSnapshot();
        Assert.Contains("Fetch", snapshot.CancellationsByOperationType.Keys);
        Assert.Equal(1, snapshot.CancellationsByOperationType["Fetch"]);
    }
}
