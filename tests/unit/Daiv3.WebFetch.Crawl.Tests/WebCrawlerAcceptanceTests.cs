using Daiv3.WebFetch.Crawl;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;
using System.Net.Http.Headers;
using Xunit;

namespace Daiv3.WebFetch.Crawl.Tests;

/// <summary>
/// Acceptance tests for WFC-ACC-002 crawl behavior.
/// Verifies crawl mode respects configured depth and domain limits.
/// </summary>
public class WebCrawlerAcceptanceTests
{
    private static HttpClient CreateHttpClient(HttpMessageHandler handler) => new(handler);

    private static WebCrawler CreateCrawler(AcceptanceRouteHttpMessageHandler handler, WebCrawlerOptions? crawlerOptions = null)
    {
        var htmlParser = new HtmlParser(new Mock<ILogger<HtmlParser>>().Object);

        var fetcher = new WebFetcher(
            CreateHttpClient(handler),
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
                ApplyRateLimit = false,
                RestrictToSameDomain = true
            });
    }

    [Fact]
    public async Task Acceptance_CrawlModeRespectsDepthAndDomainLimits_AtDepthOne()
    {
        using var handler = new AcceptanceRouteHttpMessageHandler();
        handler.AddHtml("http://example.com", "<a href='/a'>A</a><a href='https://outside.test/x'>External</a>");
        handler.AddHtml("http://example.com/a", "<a href='/b'>B</a><a href='https://outside.test/y'>External2</a>");
        handler.AddHtml("http://example.com/b", "<p>should not be crawled at depth 1</p>");

        var crawler = CreateCrawler(handler, new WebCrawlerOptions
        {
            RestrictToSameDomain = true,
            RespectRobotsTxt = false,
            ApplyRateLimit = false,
            DefaultMaxDepth = 1,
            MaxAllowedDepth = 5
        });

        var result = await crawler.CrawlAsync("http://example.com", maxDepth: 1);

        Assert.Equal(2, result.PagesCrawled);
        Assert.Contains(result.Pages, p => p.Url == "http://example.com/");
        Assert.Contains(result.Pages, p => p.Url == "http://example.com/a");
        Assert.DoesNotContain(result.Pages, p => p.Url.Contains("outside.test", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Pages, p => p.Url == "http://example.com/b");
        Assert.Contains(result.SkippedUrls, u => u.Contains("outside.test", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Acceptance_CrawlModeRespectsDomainLimit_AcrossMultipleDepthLevels()
    {
        using var handler = new AcceptanceRouteHttpMessageHandler();
        handler.AddHtml("http://example.com", "<a href='/a'>A</a><a href='https://other.example/one'>External</a>");
        handler.AddHtml("http://example.com/a", "<a href='/b'>B</a><a href='https://other.example/two'>External2</a>");
        handler.AddHtml("http://example.com/b", "<p>reachable at depth 2</p>");

        var crawler = CreateCrawler(handler, new WebCrawlerOptions
        {
            RestrictToSameDomain = true,
            RespectRobotsTxt = false,
            ApplyRateLimit = false,
            DefaultMaxDepth = 2,
            MaxAllowedDepth = 5
        });

        var result = await crawler.CrawlAsync("http://example.com", maxDepth: 2);

        Assert.Equal(3, result.PagesCrawled);
        Assert.Contains(result.Pages, p => p.Url == "http://example.com/");
        Assert.Contains(result.Pages, p => p.Url == "http://example.com/a");
        Assert.Contains(result.Pages, p => p.Url == "http://example.com/b");
        Assert.DoesNotContain(result.Pages, p => p.Url.Contains("other.example", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.SkippedUrls, u => u.Contains("other.example", StringComparison.OrdinalIgnoreCase));
    }
}

internal sealed class AcceptanceRouteHttpMessageHandler : HttpMessageHandler
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
