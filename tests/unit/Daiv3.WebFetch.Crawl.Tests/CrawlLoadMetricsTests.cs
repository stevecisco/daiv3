using Daiv3.WebFetch.Crawl;
using Xunit;

namespace Daiv3.WebFetch.Crawl.Tests;

/// <summary>
/// Tests for crawl-load metrics tracking.
/// </summary>
public class CrawlLoadMetricsTests
{
    [Fact]
    public void RecordRequest_WithDelay_TracksRequestAndDelayCounters()
    {
        var metrics = new CrawlLoadMetrics();

        metrics.RecordRequest("http://example.com", 120);

        var snapshot = metrics.GetSnapshot();
        Assert.Equal(1, snapshot.TotalRequests);
        Assert.Equal(1, snapshot.RateLimitedRequests);
        Assert.Equal(120, snapshot.TotalAppliedDelayMs);
        Assert.Equal(120, snapshot.AverageAppliedDelayMs);
        Assert.Equal(1, snapshot.RequestsByHost["http://example.com"]);
    }

    [Fact]
    public void RecordRequest_WithoutDelay_DoesNotIncrementRateLimitedCounter()
    {
        var metrics = new CrawlLoadMetrics();

        metrics.RecordRequest("http://example.com", 0);

        var snapshot = metrics.GetSnapshot();
        Assert.Equal(1, snapshot.TotalRequests);
        Assert.Equal(0, snapshot.RateLimitedRequests);
        Assert.Equal(0, snapshot.TotalAppliedDelayMs);
    }

    [Fact]
    public void RecordRobotsBlocked_IncrementsRobotsCounters()
    {
        var metrics = new CrawlLoadMetrics();

        metrics.RecordRobotsBlocked("http://example.com");

        var snapshot = metrics.GetSnapshot();
        Assert.Equal(1, snapshot.RobotsBlockedUrls);
        Assert.Equal(1, snapshot.RobotsBlockedByHost["http://example.com"]);
    }

    [Fact]
    public void RecordHostRequestCapSkip_IncrementsHostCapCounters()
    {
        var metrics = new CrawlLoadMetrics();

        metrics.RecordHostRequestCapSkip("http://example.com");

        var snapshot = metrics.GetSnapshot();
        Assert.Equal(1, snapshot.HostRequestCapSkips);
        Assert.Equal(1, snapshot.HostRequestCapSkipsByHost["http://example.com"]);
    }

    [Fact]
    public void RecordRequestsPerMinuteThresholdBreach_IncrementsThresholdCounters()
    {
        var metrics = new CrawlLoadMetrics();

        metrics.RecordRequestsPerMinuteThresholdBreach("http://example.com");

        var snapshot = metrics.GetSnapshot();
        Assert.Equal(1, snapshot.RequestsPerMinuteThresholdBreaches);
        Assert.Equal(1, snapshot.RequestsPerMinuteThresholdBreachesByHost["http://example.com"]);
    }

    [Fact]
    public void Reset_ClearsAllCounters()
    {
        var metrics = new CrawlLoadMetrics();
        metrics.RecordRequest("http://example.com", 100);
        metrics.RecordRobotsBlocked("http://example.com");
        metrics.RecordHostRequestCapSkip("http://example.com");
        metrics.RecordRequestsPerMinuteThresholdBreach("http://example.com");

        metrics.Reset();

        var snapshot = metrics.GetSnapshot();
        Assert.Equal(0, snapshot.TotalRequests);
        Assert.Equal(0, snapshot.RateLimitedRequests);
        Assert.Equal(0, snapshot.TotalAppliedDelayMs);
        Assert.Equal(0, snapshot.RobotsBlockedUrls);
        Assert.Equal(0, snapshot.HostRequestCapSkips);
        Assert.Equal(0, snapshot.RequestsPerMinuteThresholdBreaches);
        Assert.Empty(snapshot.RequestsByHost);
    }

    [Fact]
    public void RecordRequest_WithInvalidInputs_ThrowsArgumentException()
    {
        var metrics = new CrawlLoadMetrics();

        Assert.Throws<ArgumentException>(() => metrics.RecordRequest("", 10));
        Assert.Throws<ArgumentException>(() => metrics.RecordRequest("http://example.com", -1));
    }

    [Fact]
    public async Task RecordRequest_IsThreadSafe()
    {
        var metrics = new CrawlLoadMetrics();
        var tasks = new List<Task>();

        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < 100; j++)
                {
                    metrics.RecordRequest("http://example.com", 1);
                }
            }));
        }

        await Task.WhenAll(tasks);

        var snapshot = metrics.GetSnapshot();
        Assert.Equal(1000, snapshot.TotalRequests);
        Assert.Equal(1000, snapshot.RateLimitedRequests);
        Assert.Equal(1000, snapshot.TotalAppliedDelayMs);
    }
}
