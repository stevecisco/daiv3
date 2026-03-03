using Microsoft.Extensions.Logging;

namespace Daiv3.WebFetch.Crawl;

/// <summary>
/// Implements domain-bounded breadth-first crawling with configurable depth.
/// </summary>
public class WebCrawler : IWebCrawler
{
    private readonly IWebFetcher _webFetcher;
    private readonly IHtmlParser _htmlParser;
    private readonly ILogger<WebCrawler> _logger;
    private readonly WebCrawlerOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebCrawler"/> class.
    /// </summary>
    /// <param name="webFetcher">The web fetcher service.</param>
    /// <param name="htmlParser">The HTML parser service.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="options">Crawler options.</param>
    public WebCrawler(
        IWebFetcher webFetcher,
        IHtmlParser htmlParser,
        ILogger<WebCrawler> logger,
        WebCrawlerOptions? options = null)
    {
        _webFetcher = webFetcher ?? throw new ArgumentNullException(nameof(webFetcher));
        _htmlParser = htmlParser ?? throw new ArgumentNullException(nameof(htmlParser));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new WebCrawlerOptions();
    }

    /// <inheritdoc />
    public async Task<CrawlResult> CrawlAsync(string startUrl, int? maxDepth = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(startUrl))
            throw new ArgumentNullException(nameof(startUrl));

        if (!Uri.TryCreate(startUrl, UriKind.Absolute, out var startUri))
            throw new InvalidOperationException($"Invalid URL format: {startUrl}");

        if (!IsHttpScheme(startUri))
            throw new InvalidOperationException($"Only HTTP/HTTPS URLs are supported: {startUrl}");

        var effectiveDepth = maxDepth ?? _options.DefaultMaxDepth;
        if (effectiveDepth < 0)
            throw new InvalidOperationException("Crawl depth cannot be negative.");

        if (effectiveDepth > _options.MaxAllowedDepth)
            throw new InvalidOperationException(
                $"Crawl depth {effectiveDepth} exceeds configured maximum {_options.MaxAllowedDepth}.");

        var startedAt = DateTime.UtcNow;
        _logger.LogInformation(
            "Starting crawl at {StartUrl} with max depth {Depth} (restrict domain: {Restrict})",
            startUri.AbsoluteUri,
            effectiveDepth,
            _options.RestrictToSameDomain);

        var pages = new List<CrawlPageResult>();
        var skippedUrls = new List<string>();
        var queue = new Queue<(Uri Url, int Depth, string? ParentUrl)>();

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var enqueued = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var normalizedStartUrl = NormalizeUri(startUri);
        queue.Enqueue((startUri, 0, null));
        enqueued.Add(normalizedStartUrl);

        while (queue.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (pages.Count >= _options.MaxPagesToCrawl)
            {
                _logger.LogWarning(
                    "Stopping crawl after reaching MaxPagesToCrawl={MaxPages}",
                    _options.MaxPagesToCrawl);
                break;
            }

            var current = queue.Dequeue();
            var currentNormalizedUrl = NormalizeUri(current.Url);

            if (!visited.Add(currentNormalizedUrl))
            {
                skippedUrls.Add(currentNormalizedUrl);
                continue;
            }

            WebFetchResult fetchResult;
            try
            {
                fetchResult = await _webFetcher.FetchAsync(current.Url.AbsoluteUri, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Skipping URL due to fetch failure: {Url}", current.Url.AbsoluteUri);
                skippedUrls.Add(current.Url.AbsoluteUri);
                continue;
            }

            var discoveredLinks = Array.Empty<string>();
            try
            {
                var parsingBaseUrl = ResolveParsingBaseUri(fetchResult.ResolvedUrl, current.Url);
                var document = await _htmlParser.ParseAsync(fetchResult.HtmlContent, cancellationToken);
                discoveredLinks = ExtractNormalizedLinks(document, parsingBaseUrl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse links for URL: {Url}", current.Url.AbsoluteUri);
            }

            pages.Add(new CrawlPageResult
            {
                Url = current.Url.AbsoluteUri,
                Depth = current.Depth,
                ParentUrl = current.ParentUrl,
                FetchResult = fetchResult,
                DiscoveredLinks = discoveredLinks
            });

            if (current.Depth >= effectiveDepth)
            {
                continue;
            }

            foreach (var link in discoveredLinks)
            {
                if (!Uri.TryCreate(link, UriKind.Absolute, out var linkUri))
                {
                    skippedUrls.Add(link);
                    continue;
                }

                if (_options.RestrictToSameDomain && !IsWithinDomain(startUri, linkUri))
                {
                    skippedUrls.Add(linkUri.AbsoluteUri);
                    continue;
                }

                var normalizedLink = NormalizeUri(linkUri);
                if (visited.Contains(normalizedLink) || enqueued.Contains(normalizedLink))
                {
                    skippedUrls.Add(normalizedLink);
                    continue;
                }

                queue.Enqueue((linkUri, current.Depth + 1, current.Url.AbsoluteUri));
                enqueued.Add(normalizedLink);
            }
        }

        var completedAt = DateTime.UtcNow;
        _logger.LogInformation(
            "Crawl complete for {StartUrl}. Pages crawled: {PageCount}, skipped: {SkippedCount}",
            startUri.AbsoluteUri,
            pages.Count,
            skippedUrls.Count);

        return new CrawlResult
        {
            StartUrl = startUri.AbsoluteUri,
            Domain = startUri.Host,
            MaxDepth = effectiveDepth,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            Pages = pages,
            SkippedUrls = skippedUrls
        };
    }

    private static bool IsWithinDomain(Uri startUri, Uri candidateUri)
    {
        return string.Equals(startUri.Host, candidateUri.Host, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeUri(Uri uri)
    {
        var builder = new UriBuilder(uri)
        {
            Fragment = string.Empty
        };

        if (builder.Path.Length > 1)
        {
            builder.Path = builder.Path.TrimEnd('/');
        }

        return builder.Uri.AbsoluteUri;
    }

    private static bool IsHttpScheme(Uri uri)
    {
        return uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    private static Uri ResolveParsingBaseUri(string? resolvedUrl, Uri fallback)
    {
        if (!string.IsNullOrWhiteSpace(resolvedUrl)
            && Uri.TryCreate(resolvedUrl, UriKind.Absolute, out var resolvedUri)
            && IsHttpScheme(resolvedUri))
        {
            return resolvedUri;
        }

        return fallback;
    }

    private string[] ExtractNormalizedLinks(IHtmlDocument document, Uri baseUri)
    {
        var links = _htmlParser.ExtractLinks(document);
        var normalizedLinks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var link in links)
        {
            if (string.IsNullOrWhiteSpace(link.Url))
            {
                continue;
            }

            if (link.Url.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            if (link.Url.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)
                || link.Url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
                || link.Url.StartsWith("tel:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!Uri.TryCreate(baseUri, link.Url, out var resolvedLink) || !IsHttpScheme(resolvedLink))
            {
                continue;
            }

            normalizedLinks.Add(NormalizeUri(resolvedLink));
        }

        return normalizedLinks.ToArray();
    }
}
