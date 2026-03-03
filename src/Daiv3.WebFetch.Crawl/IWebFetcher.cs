namespace Daiv3.WebFetch.Crawl;

/// <summary>
/// Represents the result of a successful web fetch operation.
/// </summary>
public record WebFetchResult
{
    /// <summary>
    /// Gets the URL that was fetched.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// Gets the HTTP status code of the response.
    /// </summary>
    public required int StatusCode { get; init; }

    /// <summary>
    /// Gets the raw HTML content fetched from the URL.
    /// </summary>
    public required string HtmlContent { get; init; }

    /// <summary>
    /// Gets the Content-Type header of the response.
    /// </summary>
    public required string ContentType { get; init; }

    /// <summary>
    /// Gets the resolved final URL (after any redirects).
    /// </summary>
    public required string ResolvedUrl { get; init; }

    /// <summary>
    /// Gets the timestamp when the content was fetched.
    /// </summary>
    public required DateTime FetchedAt { get; init; }
}

/// <summary>
/// Interface for fetching web content from URLs.
/// </summary>
/// <remarks>
/// Provides methods to fetch HTML content from a given URL, with support for timeouts,
/// size limits, HTTP headers customization, and cancellation tokens.
/// </remarks>
public interface IWebFetcher
{
    /// <summary>
    /// Fetches HTML content from the specified URL.
    /// </summary>
    /// <param name="url">The URL to fetch.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the fetch result with HTML content.</returns>
    /// <exception cref="ArgumentNullException">Thrown when url is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when content exceeds maximum size or network error occurs.</exception>
    /// <exception cref="HttpRequestException">Thrown when HTTP request fails.</exception>
    Task<WebFetchResult> FetchAsync(string url, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches HTML content from the specified URL and extracts meaningful text content.
    /// </summary>
    /// <param name="url">The URL to fetch.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the fetch result with parsed text content.</returns>
    /// <remarks>
    /// This method combines fetching and HTML parsing in a single operation,
    /// providing a convenient way to get meaningful content from a web page.
    /// </remarks>
    Task<WebFetchResult> FetchAndExtractAsync(string url, CancellationToken cancellationToken = default);
}
