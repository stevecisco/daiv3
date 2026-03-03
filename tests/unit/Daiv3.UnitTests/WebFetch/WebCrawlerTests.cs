using Daiv3.WebFetch.Crawl;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;
using System.Net.Http.Headers;
using System.Diagnostics;
using Xunit;

namespace Daiv3.UnitTests.WebFetch;

/// <summary>
/// Unit tests for WebCrawler domain and depth behavior.
/// </summary>
public class WebCrawlerTests
{
    private static WebCrawler CreateCrawler(RouteHttpMessageHandler handler, WebCrawlerOptions? crawlerOptions = null)
    {
        var htmlParser = new HtmlParser(new Mock<ILogger<HtmlParser>>().Object);

        var fetcher = new WebFetcher(
            new HttpClient(handler),
            new Mock<ILogger<WebFetcher>>().Object,
            htmlParser,
            new WebFetcherOptions
            {
                ThrowOnResponseError = true,
                RequestTimeoutMs = 5000,
                MaxContentSizeBytes = 1024 * 1024
            });

        return new WebCrawler(
            fetcher,
            htmlParser,
            new Mock<ILogger<WebCrawler>>().Object,
            crawlerOptions ?? new WebCrawlerOptions
            {
                RespectRobotsTxt = false,
                ApplyRateLimit = false
            });
    }

    [Fact]
    public async Task CrawlAsync_DepthZero_CrawlsOnlyStartPage()
    {
        var handler = new RouteHttpMessageHandler();
        handler.AddHtml("http://example.com", "<a href='/a'>A</a>");
        handler.AddHtml("http://example.com/a", "<p>child</p>");

        var crawler = CreateCrawler(handler);

        var result = await crawler.CrawlAsync("http://example.com", maxDepth: 0);

        Assert.Equal(1, result.PagesCrawled);
        Assert.Single(result.Pages);
        Assert.Equal("http://example.com/", result.Pages[0].Url);
    }

    [Fact]
    public async Task CrawlAsync_DepthOne_CrawlsChildrenButNotGrandChildren()
    {
        var handler = new RouteHttpMessageHandler();
        handler.AddHtml("http://example.com", "<a href='/a'>A</a>");
        handler.AddHtml("http://example.com/a", "<a href='/b'>B</a>");
        handler.AddHtml("http://example.com/b", "<p>grandchild</p>");

        var crawler = CreateCrawler(handler);

        var result = await crawler.CrawlAsync("http://example.com", maxDepth: 1);

        Assert.Equal(2, result.PagesCrawled);
        Assert.Contains(result.Pages, p => p.Url == "http://example.com/");
        Assert.Contains(result.Pages, p => p.Url == "http://example.com/a");
        Assert.DoesNotContain(result.Pages, p => p.Url == "http://example.com/b");
    }

    [Fact]
    public async Task CrawlAsync_RestrictToDomain_SkipsExternalLinks()
    {
        var handler = new RouteHttpMessageHandler();
        handler.AddHtml("http://example.com", "<a href='/a'>A</a><a href='https://other.com/page'>X</a>");
        handler.AddHtml("http://example.com/a", "<p>inside</p>");

        var crawler = CreateCrawler(handler, new WebCrawlerOptions
        {
            RestrictToSameDomain = true
        });

        var result = await crawler.CrawlAsync("http://example.com", maxDepth: 1);

        Assert.Equal(2, result.PagesCrawled);
        Assert.DoesNotContain(result.Pages, p => p.Url.Contains("other.com", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.SkippedUrls, u => u.Contains("other.com", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CrawlAsync_ResolvesRelativeLinks()
    {
        var handler = new RouteHttpMessageHandler();
        handler.AddHtml("http://example.com/start", "<a href='child'>child</a>");
        handler.AddHtml("http://example.com/child", "<p>ok</p>");

        var crawler = CreateCrawler(handler);

        var result = await crawler.CrawlAsync("http://example.com/start", maxDepth: 1);

        Assert.Equal(2, result.PagesCrawled);
        Assert.Contains(result.Pages, p => p.Url == "http://example.com/child");
    }

    [Fact]
    public async Task CrawlAsync_DeduplicatesSameUrlAndFragmentVariants()
    {
        var handler = new RouteHttpMessageHandler();
        handler.AddHtml("http://example.com", "<a href='/a'>A</a><a href='/a/'>A2</a><a href='/a#section'>A3</a>");
        handler.AddHtml("http://example.com/a", "<p>child</p>");

        var crawler = CreateCrawler(handler);

        var result = await crawler.CrawlAsync("http://example.com", maxDepth: 1);

        Assert.Equal(2, result.PagesCrawled);
        Assert.Single(result.Pages.Where(p => p.Url.StartsWith("http://example.com/a", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task CrawlAsync_FetchFailure_ContinuesCrawl()
    {
        var handler = new RouteHttpMessageHandler();
        handler.AddHtml("http://example.com", "<a href='/a'>A</a><a href='/missing'>Missing</a>");
        handler.AddHtml("http://example.com/a", "<p>ok</p>");

        var crawler = CreateCrawler(handler);

        var result = await crawler.CrawlAsync("http://example.com", maxDepth: 1);

        Assert.Equal(2, result.PagesCrawled);
        Assert.Contains(result.Pages, p => p.Url == "http://example.com/a");
        Assert.Contains(result.SkippedUrls, u => u.Contains("missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CrawlAsync_InvalidStartUrl_ThrowsInvalidOperationException()
    {
        var crawler = CreateCrawler(new RouteHttpMessageHandler());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => crawler.CrawlAsync("not-a-url"));

        Assert.Contains("Invalid URL format", ex.Message);
    }

    [Fact]
    public async Task CrawlAsync_DepthAboveMaxAllowed_ThrowsInvalidOperationException()
    {
        var handler = new RouteHttpMessageHandler();
        handler.AddHtml("http://example.com", "<p>root</p>");

        var crawler = CreateCrawler(handler, new WebCrawlerOptions
        {
            MaxAllowedDepth = 1
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => crawler.CrawlAsync("http://example.com", maxDepth: 2));

        Assert.Contains("exceeds configured maximum", ex.Message);
    }

    [Fact]
    public async Task CrawlAsync_WhenRobotsDisallowsPath_SkipsDisallowedUrl()
    {
        var handler = new RouteHttpMessageHandler();
        handler.AddHtml("http://example.com/robots.txt", "User-agent: *\nDisallow: /private");
        handler.AddHtml("http://example.com", "<a href='/private/page'>private</a><a href='/public'>public</a>");
        handler.AddHtml("http://example.com/public", "<p>ok</p>");
        handler.AddHtml("http://example.com/private/page", "<p>blocked</p>");

        var crawler = CreateCrawler(handler, new WebCrawlerOptions
        {
            RespectRobotsTxt = true,
            RobotsUserAgent = "Daiv3Crawler",
            ApplyRateLimit = false
        });

        var result = await crawler.CrawlAsync("http://example.com", maxDepth: 1);

        Assert.Equal(2, result.PagesCrawled);
        Assert.Contains(result.Pages, p => p.Url == "http://example.com/");
        Assert.Contains(result.Pages, p => p.Url == "http://example.com/public");
        Assert.DoesNotContain(result.Pages, p => p.Url.Contains("/private/", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.SkippedUrls, u => u.Contains("/private/page", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CrawlAsync_WhenRobotsAllowOverridesDisallow_CrawlsAllowedPath()
    {
        var handler = new RouteHttpMessageHandler();
        handler.AddHtml("http://example.com/robots.txt", "User-agent: *\nDisallow: /private\nAllow: /private/safe");
        handler.AddHtml("http://example.com", "<a href='/private/safe/page'>safe</a>");
        handler.AddHtml("http://example.com/private/safe/page", "<p>ok</p>");

        var crawler = CreateCrawler(handler, new WebCrawlerOptions
        {
            RespectRobotsTxt = true,
            ApplyRateLimit = false
        });

        var result = await crawler.CrawlAsync("http://example.com", maxDepth: 1);

        Assert.Equal(2, result.PagesCrawled);
        Assert.Contains(result.Pages, p => p.Url == "http://example.com/private/safe/page");
    }

    [Fact]
    public async Task CrawlAsync_WhenRateLimitEnabled_AppliesDelayBetweenHostRequests()
    {
        var handler = new RouteHttpMessageHandler();
        handler.AddHtml("http://example.com", "<a href='/a'>A</a>");
        handler.AddHtml("http://example.com/a", "<p>child</p>");

        var crawler = CreateCrawler(handler, new WebCrawlerOptions
        {
            RespectRobotsTxt = false,
            ApplyRateLimit = true,
            RateLimitDelayMs = 150
        });

        var stopwatch = Stopwatch.StartNew();
        var result = await crawler.CrawlAsync("http://example.com", maxDepth: 1);
        stopwatch.Stop();

        Assert.Equal(2, result.PagesCrawled);
        Assert.True(stopwatch.ElapsedMilliseconds >= 120, $"Expected rate limit delay to apply, actual elapsed: {stopwatch.ElapsedMilliseconds}ms");
    }
}

internal sealed class RouteHttpMessageHandler : HttpMessageHandler
{
    private readonly Dictionary<string, RouteResponse> _responses = new(StringComparer.OrdinalIgnoreCase);

    public void AddHtml(string absoluteUrl, string html, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _responses[NormalizeUrl(absoluteUrl)] = new RouteResponse(html, statusCode);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var requestUrl = NormalizeUrl(request.RequestUri?.AbsoluteUri ?? string.Empty);

        if (!_responses.TryGetValue(requestUrl, out var route))
        {
            route = new RouteResponse("not found", HttpStatusCode.NotFound);
        }

        var response = new HttpResponseMessage(route.StatusCode)
        {
            Content = new StringContent(route.Content),
            RequestMessage = request
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html");

        return Task.FromResult(response);
    }

    private static string NormalizeUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return url;
        }

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

    private sealed record RouteResponse(string Content, HttpStatusCode StatusCode);
}
