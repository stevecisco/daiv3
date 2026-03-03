namespace Daiv3.WebFetch.Crawl;

/// <summary>
/// Configuration options for web crawling operations.
/// </summary>
public class WebCrawlerOptions
{
    /// <summary>
    /// Gets or sets the default maximum crawl depth when not explicitly provided.
    /// Default: 1 (start page + directly linked pages).
    /// </summary>
    public int DefaultMaxDepth { get; set; } = 1;

    /// <summary>
    /// Gets or sets the hard upper limit for crawl depth.
    /// Default: 5.
    /// </summary>
    public int MaxAllowedDepth { get; set; } = 5;

    /// <summary>
    /// Gets or sets the maximum number of pages allowed in a crawl operation.
    /// Default: 100.
    /// </summary>
    public int MaxPagesToCrawl { get; set; } = 100;

    /// <summary>
    /// Gets or sets whether crawling is restricted to the start URL host.
    /// Default: true.
    /// </summary>
    public bool RestrictToSameDomain { get; set; } = true;
}
