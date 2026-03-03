namespace Daiv3.WebFetch.Crawl;

/// <summary>
/// Tracks crawl network-load metrics for politeness and threshold observability.
/// </summary>
public interface ICrawlLoadMetrics
{
    /// <summary>
    /// Records a network request made by the crawler.
    /// </summary>
    /// <param name="hostKey">Host key in scheme://authority format.</param>
    /// <param name="appliedDelayMs">Delay applied before request dispatch.</param>
    void RecordRequest(string hostKey, int appliedDelayMs);

    /// <summary>
    /// Records a URL skipped due to robots.txt policy.
    /// </summary>
    void RecordRobotsBlocked(string hostKey);

    /// <summary>
    /// Records a URL skipped due to per-host request cap.
    /// </summary>
    void RecordHostRequestCapSkip(string hostKey);

    /// <summary>
    /// Records a requests-per-minute threshold breach for a host.
    /// </summary>
    void RecordRequestsPerMinuteThresholdBreach(string hostKey);

    /// <summary>
    /// Gets current metrics snapshot.
    /// </summary>
    CrawlLoadMetricsSnapshot GetSnapshot();

    /// <summary>
    /// Resets all metrics.
    /// </summary>
    void Reset();
}

/// <summary>
/// Snapshot of crawl load metrics.
/// </summary>
public record CrawlLoadMetricsSnapshot(
    int TotalRequests,
    int RateLimitedRequests,
    long TotalAppliedDelayMs,
    double AverageAppliedDelayMs,
    int RobotsBlockedUrls,
    int HostRequestCapSkips,
    int RequestsPerMinuteThresholdBreaches,
    IReadOnlyDictionary<string, int> RequestsByHost,
    IReadOnlyDictionary<string, int> RobotsBlockedByHost,
    IReadOnlyDictionary<string, int> HostRequestCapSkipsByHost,
    IReadOnlyDictionary<string, int> RequestsPerMinuteThresholdBreachesByHost
);
