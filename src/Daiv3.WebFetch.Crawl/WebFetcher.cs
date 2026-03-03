using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Daiv3.WebFetch.Crawl;

/// <summary>
/// Implements web content fetching with configurable timeouts, size limits, and headers.
/// </summary>
/// <remarks>
/// Uses HttpClient for efficient HTTP request handling with support for
/// redirects, timeouts, and content size validation.
/// Also calculates content hashes for change detection and metadata storage.
/// Implements partial WFC-REQ-007: calculates metadata that can be stored by higher-level services.
/// </remarks>
public class WebFetcher : IWebFetcher
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WebFetcher> _logger;
    private readonly WebFetcherOptions _options;
    private readonly IHtmlParser _htmlParser;
    private readonly ICancellationMetrics _cancellationMetrics;
    private readonly System.Diagnostics.Stopwatch _stopwatch = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="WebFetcher"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client for making requests.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="htmlParser">The HTML parser for extracting meaningful content.</param>
    /// <param name="options">The web fetcher configuration options.</param>
    /// <param name="cancellationMetrics">The cancellation metrics service.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public WebFetcher(
        HttpClient httpClient,
        ILogger<WebFetcher> logger,
        IHtmlParser htmlParser,
        WebFetcherOptions? options = null,
        ICancellationMetrics? cancellationMetrics = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _htmlParser = htmlParser ?? throw new ArgumentNullException(nameof(htmlParser));
        _options = options ?? new WebFetcherOptions();
        _cancellationMetrics = cancellationMetrics ?? new CancellationMetrics();
    }

    /// <summary>
    /// Fetches HTML content from the specified URL.
    /// </summary>
    /// <param name="url">The URL to fetch.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when url is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when content exceeds maximum size.</exception>
    public async Task<WebFetchResult> FetchAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentNullException(nameof(url));

        _stopwatch.Restart();

        try
        {
            _logger.LogInformation("Starting fetch operation for URL: {Url}", url);

            // Validate URL format
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                throw new InvalidOperationException($"Invalid URL format: {url}");

            // Create request with proper headers
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Add("User-Agent", _options.UserAgent);
            request.Headers.Add("Accept", _options.AcceptHeader);
            request.Headers.Add("Accept-Language", _options.AcceptLanguageHeader);
            request.Headers.Add("Accept-Encoding", _options.AcceptEncodingHeader);

            // Create cancellation token with timeout
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_options.RequestTimeoutMs);

            _logger.LogDebug("Sending HTTP request to {Url} with timeout {TimeoutMs}ms", url, _options.RequestTimeoutMs);

            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

            // Check status code
            if (_options.ThrowOnResponseError && !response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "HTTP request failed with status code {StatusCode} for {Url}",
                    response.StatusCode,
                    url);

                if (response.StatusCode == HttpStatusCode.NotFound)
                    throw new InvalidOperationException($"URL not found (404): {url}");
                if (response.StatusCode == HttpStatusCode.Forbidden)
                    throw new InvalidOperationException($"Access forbidden (403): {url}");
                if (response.StatusCode == HttpStatusCode.BadRequest)
                    throw new InvalidOperationException($"Bad request (400): {url}");

                throw new InvalidOperationException($"HTTP request failed with status code {response.StatusCode}");
            }

            // Check content type
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "text/html";
            if (!IsHtmlContentType(contentType))
            {
                _logger.LogWarning(
                    "Response content type is not HTML: {ContentType} for {Url}",
                    contentType,
                    url);
            }

            // Check content length if available
            var contentLength = response.Content.Headers.ContentLength ?? 0;
            if (contentLength > _options.MaxContentSizeBytes && contentLength > 0)
            {
                _logger.LogWarning(
                    "Content size ({ContentLength} bytes) exceeds maximum ({MaxSize} bytes) for {Url}",
                    contentLength,
                    _options.MaxContentSizeBytes,
                    url);
                throw new InvalidOperationException(
                    $"Content size ({contentLength} bytes) exceeds maximum allowed ({_options.MaxContentSizeBytes} bytes)");
            }

            // Read content with size check
            var contentStream = await response.Content.ReadAsStreamAsync(cts.Token);
            byte[] contentBuffer = new byte[_options.MaxContentSizeBytes + 1]; // +1 to detect overflow
            int bytesRead = 0;
            int bytesToRead = contentBuffer.Length;

            try
            {
                bytesRead = await contentStream.ReadAsync(contentBuffer, 0, bytesToRead, cts.Token);
            }
            catch (OperationCanceledException ex) when (cts.Token.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                _stopwatch.Stop();
                _cancellationMetrics.RecordCancellation("Fetch", url, "Timeout", _stopwatch.ElapsedMilliseconds);
                _logger.LogError(ex, "Fetch operation timed out after {TimeoutMs}ms for {Url}", _options.RequestTimeoutMs, url);
                throw new InvalidOperationException($"HTTP request timed out after {_options.RequestTimeoutMs}ms", ex);
            }

            // Check if content exceeds limit
            if (bytesRead > _options.MaxContentSizeBytes)
            {
                _logger.LogWarning(
                    "Downloaded content size ({BytesRead} bytes) exceeds maximum ({MaxSize} bytes) for {Url}",
                    bytesRead,
                    _options.MaxContentSizeBytes,
                    url);
                throw new InvalidOperationException(
                    $"Downloaded content size ({bytesRead} bytes) exceeds maximum allowed ({_options.MaxContentSizeBytes} bytes)");
            }

            var htmlContent = System.Text.Encoding.UTF8.GetString(contentBuffer, 0, bytesRead);

            _logger.LogInformation(
                "Successfully fetched content from {Url} ({BytesRead} bytes, status {StatusCode})",
                response.RequestMessage?.RequestUri?.AbsoluteUri ?? url,
                bytesRead,
                response.StatusCode);

            // Calculate content hash for change detection (WFC-REQ-007)
            var contentHash = CalculateContentHash(htmlContent);

            return new WebFetchResult
            {
                Url = url,
                ResolvedUrl = response.RequestMessage?.RequestUri?.AbsoluteUri ?? url,
                StatusCode = (int)response.StatusCode,
                ContentType = contentType,
                HtmlContent = htmlContent,
                FetchedAt = DateTime.UtcNow,
                ContentHash = contentHash
            };
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _stopwatch.Stop();
            _cancellationMetrics.RecordCancellation("Fetch", url, "Timeout", _stopwatch.ElapsedMilliseconds);
            _logger.LogError(ex, "Fetch operation cancelled (timeout) for {Url}", url);
            throw new InvalidOperationException($"Fetch operation timed out for {url}", ex);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _stopwatch.Stop();
            _cancellationMetrics.RecordCancellation("Fetch", url, "UserRequested", _stopwatch.ElapsedMilliseconds);
            _logger.LogInformation("Fetch operation cancelled by user for {Url} after {ElapsedMs}ms", url, _stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request error fetching {Url}", url);
            throw new InvalidOperationException($"Failed to fetch URL: {url}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching {Url}", url);
            throw;
        }
    }

    /// <summary>
    /// Fetches HTML content from the specified URL and extracts meaningful text content.
    /// </summary>
    /// <param name="url">The URL to fetch.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<WebFetchResult> FetchAndExtractAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentNullException(nameof(url));

        _logger.LogInformation("Starting fetch and extract operation for URL: {Url}", url);

        try
        {
            // Fetch the initial content
            var fetchResult = await FetchAsync(url, cancellationToken);

            // Parse the HTML
            _logger.LogDebug("Parsing HTML content for deep extraction from {Url}", url);
            var parsedDocument = await _htmlParser.ParseAsync(fetchResult.HtmlContent, cancellationToken);

            // Extract meaningful text
            var extractedText = _htmlParser.ExtractText(parsedDocument);

            _logger.LogInformation(
                "Successfully extracted meaningful content from {Url} ({CharsExtracted} characters)",
                url,
                extractedText.Length);

            return fetchResult with
            {
                HtmlContent = extractedText
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during fetch and extract operation for {Url}", url);
            throw;
        }
    }

    /// <summary>
    /// Determines if the content type indicates HTML content.
    /// </summary>
    /// <param name="contentType">The content type string.</param>
    /// <returns>True if the content type is HTML or similar, false otherwise.</returns>
    private static bool IsHtmlContentType(string contentType)
    {
        var normalizedType = contentType.ToLowerInvariant().Split(';')[0].Trim();
        return normalizedType switch
        {
            "text/html" => true,
            "application/xhtml+xml" => true,
            "application/xml" => true,
            "text/xml" => true,
            _ => false
        };
    }

    /// <summary>
    /// Calculates the SHA256 hash of HTML content.
    /// Used for change detection and metadata storage (WFC-REQ-007).
    /// </summary>
    /// <param name="content">The HTML content to hash.</param>
    /// <returns>The SHA256 hash as a hexadecimal string (lowercase).</returns>
    private static string CalculateContentHash(string content)
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
        return BitConverter.ToString(hashedBytes).Replace("-", "").ToLowerInvariant();
    }
}
