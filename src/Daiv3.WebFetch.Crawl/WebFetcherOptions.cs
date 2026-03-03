namespace Daiv3.WebFetch.Crawl;

/// <summary>
/// Configuration options for the web fetcher service.
/// </summary>
/// <remarks>
/// Provides settings for HTTP client behavior, timeouts, size limits,
/// and default headers used when fetching web content.
/// </remarks>
public class WebFetcherOptions
{
    /// <summary>
    /// Gets or sets the maximum HTTP request timeout in milliseconds.
    /// Default: 30000 (30 seconds).
    /// </summary>
    public int RequestTimeoutMs { get; set; } = 30_000;

    /// <summary>
    /// Gets or sets the maximum content size in bytes that will be fetched.
    /// Default: 10485760 (10 MB).
    /// </summary>
    /// <remarks>
    /// Content larger than this limit will be rejected during the fetch operation
    /// before attempting to parse it.
    /// </remarks>
    public int MaxContentSizeBytes { get; set; } = 10 * 1024 * 1024; // 10 MB

    /// <summary>
    /// Gets or sets the User-Agent header to use in HTTP requests.
    /// Default: A descriptive bot user agent.
    /// </summary>
    /// <remarks>
    /// Some websites block requests with default HTTP client user agents.
    /// A realistic user agent helps avoid being blocked.
    /// </remarks>
    public string UserAgent { get; set; } = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) DAIv3/1.0 Chrome/91.0.4472.124 Safari/537.36";

    /// <summary>
    /// Gets or sets a value indicating whether to follow HTTP redirects.
    /// Default: true.
    /// </summary>
    public bool FollowRedirects { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of redirect hops to follow.
    /// Default: 10.
    /// </summary>
    /// <remarks>
    /// This prevents redirect loops and excessive redirect chains.
    /// </remarks>
    public int MaxRedirects { get; set; } = 10;

    /// <summary>
    /// Gets or sets the accept header for HTTP requests.
    /// Default: "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8"
    /// </summary>
    public string AcceptHeader { get; set; } = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";

    /// <summary>
    /// Gets or sets the accept-language header for HTTP requests.
    /// Default: "en-US,en;q=0.9"
    /// </summary>
    public string AcceptLanguageHeader { get; set; } = "en-US,en;q=0.9";

    /// <summary>
    /// Gets or sets the accept-encoding header for HTTP requests.
    /// Default: "gzip, deflate, br"
    /// </summary>
    public string AcceptEncodingHeader { get; set; } = "gzip, deflate, br";

    /// <summary>
    /// Gets or sets a value indicating whether to throw on non-2xx HTTP status codes.
    /// Default: true.
    /// </summary>
    /// <remarks>
    /// When true, HTTP responses with status codes outside 200-299 range will throw
    /// an HttpRequestException. When false, the response is returned as-is.
    /// </remarks>
    public bool ThrowOnResponseError { get; set; } = true;
}
