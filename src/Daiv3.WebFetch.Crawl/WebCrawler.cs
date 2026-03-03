using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.RegularExpressions;

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
    private readonly ICrawlLoadMetrics _crawlLoadMetrics;
    private readonly Dictionary<string, RobotsRules> _robotsRulesByHost = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> _lastRequestAtByHost = new(StringComparer.OrdinalIgnoreCase);

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
        WebCrawlerOptions? options = null,
        ICrawlLoadMetrics? crawlLoadMetrics = null)
    {
        _webFetcher = webFetcher ?? throw new ArgumentNullException(nameof(webFetcher));
        _htmlParser = htmlParser ?? throw new ArgumentNullException(nameof(htmlParser));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new WebCrawlerOptions();
        _crawlLoadMetrics = crawlLoadMetrics ?? new CrawlLoadMetrics();
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
        var requestsByHost = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var firstRequestAtByHost = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
        var lastRequestAtByHost = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
        var hostThresholdBreaches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var robotsPolicySkipCount = 0;
        var hostRequestCapSkipCount = 0;
        long totalAppliedRateLimitDelayMs = 0;
        var rateLimitedRequestCount = 0;

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

            var hostKey = GetHostKey(current.Url);
            var robotsRules = await GetRobotsRulesForHostAsync(current.Url, cancellationToken);
            if (_options.RespectRobotsTxt && !robotsRules.IsAllowed(current.Url.PathAndQuery))
            {
                _logger.LogInformation("Skipping URL due to robots.txt policy: {Url}", current.Url.AbsoluteUri);
                skippedUrls.Add(current.Url.AbsoluteUri);
                robotsPolicySkipCount++;
                _crawlLoadMetrics.RecordRobotsBlocked(hostKey);
                continue;
            }

            var hostRequestCount = requestsByHost.GetValueOrDefault(hostKey);
            if (_options.MaxRequestsPerHostPerCrawl > 0 && hostRequestCount >= _options.MaxRequestsPerHostPerCrawl)
            {
                _logger.LogWarning(
                    "Skipping URL due to host request cap. Host: {HostKey}, cap: {Cap}, url: {Url}",
                    hostKey,
                    _options.MaxRequestsPerHostPerCrawl,
                    current.Url.AbsoluteUri);

                skippedUrls.Add(current.Url.AbsoluteUri);
                hostRequestCapSkipCount++;
                _crawlLoadMetrics.RecordHostRequestCapSkip(hostKey);
                continue;
            }

            var appliedDelayMs = await ApplyRateLimitAsync(current.Url, robotsRules, cancellationToken);
            totalAppliedRateLimitDelayMs += appliedDelayMs;
            if (appliedDelayMs > 0)
            {
                rateLimitedRequestCount++;
            }

            var requestTimestamp = DateTimeOffset.UtcNow;
            if (!firstRequestAtByHost.ContainsKey(hostKey))
            {
                firstRequestAtByHost[hostKey] = requestTimestamp;
            }

            lastRequestAtByHost[hostKey] = requestTimestamp;
            requestsByHost[hostKey] = hostRequestCount + 1;
            _crawlLoadMetrics.RecordRequest(hostKey, appliedDelayMs);

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

        var requestsPerMinuteByHost = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var hostEntry in requestsByHost)
        {
            var hostKey = hostEntry.Key;
            var requestCount = hostEntry.Value;

            var firstRequestAt = firstRequestAtByHost[hostKey];
            var lastRequestAt = lastRequestAtByHost[hostKey];
            var observedDurationMs = Math.Max(1000d, (lastRequestAt - firstRequestAt).TotalMilliseconds);
            var requestsPerMinute = requestCount / (observedDurationMs / 60000d);

            requestsPerMinuteByHost[hostKey] = requestsPerMinute;

            if (_options.TargetMaxRequestsPerMinutePerHost > 0
                && requestsPerMinute > _options.TargetMaxRequestsPerMinutePerHost)
            {
                hostThresholdBreaches.Add(hostKey);
                _crawlLoadMetrics.RecordRequestsPerMinuteThresholdBreach(hostKey);

                _logger.LogWarning(
                    "Host request rate exceeded target threshold. Host: {HostKey}, rpm: {RequestsPerMinute:F2}, target: {TargetRpm}",
                    hostKey,
                    requestsPerMinute,
                    _options.TargetMaxRequestsPerMinutePerHost);
            }
        }

        var completedAt = DateTime.UtcNow;
        _logger.LogInformation(
            "Crawl complete for {StartUrl}. Pages crawled: {PageCount}, skipped: {SkippedCount}, rate-limited requests: {RateLimitedCount}, total applied delay-ms: {TotalDelayMs}, host-cap skips: {HostCapSkips}, robots skips: {RobotsSkips}",
            startUri.AbsoluteUri,
            pages.Count,
            skippedUrls.Count,
            rateLimitedRequestCount,
            totalAppliedRateLimitDelayMs,
            hostRequestCapSkipCount,
            robotsPolicySkipCount);

        return new CrawlResult
        {
            StartUrl = startUri.AbsoluteUri,
            Domain = startUri.Host,
            MaxDepth = effectiveDepth,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            Pages = pages,
            SkippedUrls = skippedUrls,
            RequestsByHost = requestsByHost,
            RequestsPerMinuteByHost = requestsPerMinuteByHost,
            TotalAppliedRateLimitDelayMs = totalAppliedRateLimitDelayMs,
            RateLimitedRequestCount = rateLimitedRequestCount,
            RobotsPolicySkipCount = robotsPolicySkipCount,
            HostRequestCapSkipCount = hostRequestCapSkipCount,
            RequestsPerMinuteThresholdBreaches = hostThresholdBreaches.ToArray()
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

    private async Task<RobotsRules> GetRobotsRulesForHostAsync(Uri url, CancellationToken cancellationToken)
    {
        if (!_options.RespectRobotsTxt)
        {
            return RobotsRules.AllowAll;
        }

        var hostKey = GetHostKey(url);
        if (_robotsRulesByHost.TryGetValue(hostKey, out var cachedRules))
        {
            return cachedRules;
        }

        var robotsUri = new Uri($"{url.Scheme}://{url.Authority}/robots.txt", UriKind.Absolute);

        try
        {
            var fetchResult = await _webFetcher.FetchAsync(robotsUri.AbsoluteUri, cancellationToken);
            var parsedRules = ParseRobots(fetchResult.HtmlContent, _options.RobotsUserAgent);
            _robotsRulesByHost[hostKey] = parsedRules;

            _logger.LogDebug(
                "Loaded robots.txt for {HostKey}. Allow rules: {AllowCount}, disallow rules: {DisallowCount}, crawl-delay-ms: {CrawlDelayMs}",
                hostKey,
                parsedRules.AllowPatterns.Count,
                parsedRules.DisallowPatterns.Count,
                parsedRules.CrawlDelayMs);

            return parsedRules;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to load robots.txt for {HostKey}. Proceeding with allow-all policy.", hostKey);
            _robotsRulesByHost[hostKey] = RobotsRules.AllowAll;
            return RobotsRules.AllowAll;
        }
    }

    private async Task<int> ApplyRateLimitAsync(Uri url, RobotsRules robotsRules, CancellationToken cancellationToken)
    {
        if (!_options.ApplyRateLimit)
        {
            return 0;
        }

        var configuredDelay = Math.Max(0, _options.RateLimitDelayMs);
        var robotsDelay = robotsRules.CrawlDelayMs.GetValueOrDefault(0);
        var effectiveDelayMs = Math.Max(configuredDelay, robotsDelay);

        if (effectiveDelayMs <= 0)
        {
            return 0;
        }

        var hostKey = GetHostKey(url);
        var appliedDelayMs = 0;

        if (_lastRequestAtByHost.TryGetValue(hostKey, out var lastRequestAt))
        {
            var elapsedMs = (DateTimeOffset.UtcNow - lastRequestAt).TotalMilliseconds;
            var remainingDelayMs = effectiveDelayMs - elapsedMs;

            if (remainingDelayMs > 0)
            {
                appliedDelayMs = (int)Math.Ceiling(remainingDelayMs);

                _logger.LogDebug(
                    "Applying crawl rate limit delay for {HostKey}: {DelayMs}ms",
                    hostKey,
                    appliedDelayMs);

                await Task.Delay(TimeSpan.FromMilliseconds(remainingDelayMs), cancellationToken);
            }
        }

        _lastRequestAtByHost[hostKey] = DateTimeOffset.UtcNow;
        return appliedDelayMs;
    }

    private static string GetHostKey(Uri uri)
    {
        return $"{uri.Scheme}://{uri.Authority}";
    }

    private static RobotsRules ParseRobots(string robotsContent, string userAgent)
    {
        if (string.IsNullOrWhiteSpace(robotsContent))
        {
            return RobotsRules.AllowAll;
        }

        var effectiveUserAgent = string.IsNullOrWhiteSpace(userAgent)
            ? "*"
            : userAgent.Trim();

        var allowPatterns = new List<string>();
        var disallowPatterns = new List<string>();
        int? crawlDelayMs = null;

        var groupAgents = new List<string>();
        var groupHasDirectives = false;

        foreach (var rawLine in robotsContent.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var commentIndex = line.IndexOf('#');
            if (commentIndex >= 0)
            {
                line = line[..commentIndex].Trim();
            }

            if (line.Length == 0)
            {
                continue;
            }

            var separatorIndex = line.IndexOf(':');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var directive = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();

            if (directive.Equals("User-agent", StringComparison.OrdinalIgnoreCase))
            {
                if (groupHasDirectives)
                {
                    groupAgents.Clear();
                    groupHasDirectives = false;
                }

                groupAgents.Add(value);
                continue;
            }

            if (groupAgents.Count == 0)
            {
                continue;
            }

            groupHasDirectives = true;
            var applies = groupAgents.Any(agent =>
                agent.Equals("*", StringComparison.Ordinal)
                || agent.Equals(effectiveUserAgent, StringComparison.OrdinalIgnoreCase));

            if (!applies)
            {
                continue;
            }

            if (directive.Equals("Allow", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    allowPatterns.Add(value);
                }

                continue;
            }

            if (directive.Equals("Disallow", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    disallowPatterns.Add(value);
                }

                continue;
            }

            if (directive.Equals("Crawl-delay", StringComparison.OrdinalIgnoreCase)
                && double.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var seconds)
                && seconds >= 0)
            {
                var parsedDelayMs = (int)Math.Round(seconds * 1000, MidpointRounding.AwayFromZero);
                crawlDelayMs = Math.Max(crawlDelayMs.GetValueOrDefault(0), parsedDelayMs);
            }
        }

        if (allowPatterns.Count == 0 && disallowPatterns.Count == 0 && crawlDelayMs is null)
        {
            return RobotsRules.AllowAll;
        }

        return new RobotsRules(allowPatterns, disallowPatterns, crawlDelayMs);
    }

    private sealed record RobotsRules(
        IReadOnlyList<string> AllowPatterns,
        IReadOnlyList<string> DisallowPatterns,
        int? CrawlDelayMs)
    {
        public static RobotsRules AllowAll { get; } = new(Array.Empty<string>(), Array.Empty<string>(), null);

        public bool IsAllowed(string pathAndQuery)
        {
            var normalizedPath = string.IsNullOrWhiteSpace(pathAndQuery) ? "/" : pathAndQuery;

            var bestAllowLength = GetBestMatchLength(AllowPatterns, normalizedPath);
            var bestDisallowLength = GetBestMatchLength(DisallowPatterns, normalizedPath);

            if (bestAllowLength == 0 && bestDisallowLength == 0)
            {
                return true;
            }

            return bestAllowLength >= bestDisallowLength;
        }

        private static int GetBestMatchLength(IReadOnlyList<string> patterns, string candidatePath)
        {
            var bestLength = 0;

            foreach (var pattern in patterns)
            {
                if (PatternMatches(pattern, candidatePath))
                {
                    bestLength = Math.Max(bestLength, pattern.Length);
                }
            }

            return bestLength;
        }

        private static bool PatternMatches(string pattern, string candidatePath)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return false;
            }

            var regexPattern = "^" + Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\$", "$");

            return Regex.IsMatch(candidatePath, regexPattern, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        }
    }
}
