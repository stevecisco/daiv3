namespace Daiv3.WebFetch.Crawl;

/// <summary>
/// Represents a crawled page and associated metadata.
/// </summary>
public record CrawlPageResult
{
    /// <summary>
    /// Gets the crawled page URL.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// Gets the crawl depth for this page, where 0 is the start page.
    /// </summary>
    public required int Depth { get; init; }

    /// <summary>
    /// Gets the parent URL that discovered this page.
    /// </summary>
    public string? ParentUrl { get; init; }

    /// <summary>
    /// Gets the web fetch result for this page.
    /// </summary>
    public required WebFetchResult FetchResult { get; init; }

    /// <summary>
    /// Gets links discovered on this page after normalization and filtering.
    /// </summary>
    public required IReadOnlyList<string> DiscoveredLinks { get; init; }
}

/// <summary>
/// Represents the outcome of a crawl operation.
/// </summary>
public record CrawlResult
{
    /// <summary>
    /// Gets the start URL for the crawl.
    /// </summary>
    public required string StartUrl { get; init; }

    /// <summary>
    /// Gets the host/domain used as the crawl boundary.
    /// </summary>
    public required string Domain { get; init; }

    /// <summary>
    /// Gets the configured maximum crawl depth.
    /// </summary>
    public required int MaxDepth { get; init; }

    /// <summary>
    /// Gets the UTC timestamp when crawling started.
    /// </summary>
    public required DateTime StartedAt { get; init; }

    /// <summary>
    /// Gets the UTC timestamp when crawling completed.
    /// </summary>
    public required DateTime CompletedAt { get; init; }

    /// <summary>
    /// Gets the pages successfully crawled.
    /// </summary>
    public required IReadOnlyList<CrawlPageResult> Pages { get; init; }

    /// <summary>
    /// Gets URLs skipped due to boundary, duplication, errors, or filtering.
    /// </summary>
    public required IReadOnlyList<string> SkippedUrls { get; init; }

    /// <summary>
    /// Gets the number of pages successfully crawled.
    /// </summary>
    public int PagesCrawled => Pages.Count;

    /// <summary>
    /// Gets request counts by host observed during this crawl.
    /// </summary>
    public IReadOnlyDictionary<string, int> RequestsByHost { get; init; } = new Dictionary<string, int>();

    /// <summary>
    /// Gets requests-per-minute values by host observed during this crawl.
    /// </summary>
    public IReadOnlyDictionary<string, double> RequestsPerMinuteByHost { get; init; } = new Dictionary<string, double>();

    /// <summary>
    /// Gets total delay applied by rate limiting before requests.
    /// </summary>
    public long TotalAppliedRateLimitDelayMs { get; init; }

    /// <summary>
    /// Gets the number of requests where rate-limit delay was applied.
    /// </summary>
    public int RateLimitedRequestCount { get; init; }

    /// <summary>
    /// Gets number of URLs skipped due to robots.txt policy.
    /// </summary>
    public int RobotsPolicySkipCount { get; init; }

    /// <summary>
    /// Gets number of URLs skipped due to host request cap.
    /// </summary>
    public int HostRequestCapSkipCount { get; init; }

    /// <summary>
    /// Gets hosts that exceeded configured requests-per-minute target.
    /// </summary>
    public IReadOnlyList<string> RequestsPerMinuteThresholdBreaches { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Interface for crawling web pages within a domain and configurable depth.
/// </summary>
public interface IWebCrawler
{
    /// <summary>
    /// Crawls pages starting from the provided URL using breadth-first traversal.
    /// </summary>
    /// <param name="startUrl">The absolute start URL.</param>
    /// <param name="maxDepth">
    /// Optional maximum depth. If null, <see cref="WebCrawlerOptions.DefaultMaxDepth"/> is used.
    /// </param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A crawl result containing pages and skipped URLs.</returns>
    Task<CrawlResult> CrawlAsync(string startUrl, int? maxDepth = null, CancellationToken cancellationToken = default);
}
