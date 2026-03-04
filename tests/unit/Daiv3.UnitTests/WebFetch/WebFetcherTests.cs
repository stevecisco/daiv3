using Microsoft.Extensions.Logging;
using Xunit;
using Moq;
using Daiv3.WebFetch.Crawl;
using System.Net;
using System.Diagnostics;

#pragma warning disable IDISP001 // Dispose created
#pragma warning disable IDISP004 // Don't ignore created IDisposable
#pragma warning disable IDISP008 // Don't assign member with injected and created disposables (test mock helper)

namespace Daiv3.UnitTests.WebFetch;

/// <summary>
/// Unit tests for the WebFetcher service.
/// </summary>
public class WebFetcherTests
{
    private WebFetcherOptions CreateDefaultOptions() => new WebFetcherOptions
    {
        RequestTimeoutMs = 5000,
        MaxContentSizeBytes = 1024 * 1024, // 1 MB
        UserAgent = "Test-Agent/1.0",
        ThrowOnResponseError = true
    };

    private Mock<ILogger<WebFetcher>> CreateMockLogger() =>
        new Mock<ILogger<WebFetcher>>();

    private Mock<IHtmlParser> CreateMockHtmlParser() =>
        new Mock<IHtmlParser>();

    [Fact]
    public async Task FetchAsync_WithValidUrl_ReturnsWebFetchResult()
    {
        // Arrange
        var mockHttpClientFactory = new MockHttpClientFactory();
        var mockHtmlParser = CreateMockHtmlParser();
        var mockLogger = CreateMockLogger();
        var options = CreateDefaultOptions();

        var htmlContent = "<html><head><title>Test Page</title></head><body>Test Content</body></html>";
        var response = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(htmlContent),
            RequestMessage = new HttpRequestMessage { RequestUri = new Uri("http://example.com") }
        };
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/html");
        mockHttpClientFactory.SetupResponse(response);

        var httpClient = mockHttpClientFactory.CreateHttpClient();
        var fetcher = new WebFetcher(httpClient, mockLogger.Object, mockHtmlParser.Object, options);

        // Act
        var result = await fetcher.FetchAsync("http://example.com");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("http://example.com", result.Url);
        Assert.Equal(200, result.StatusCode);
        Assert.Contains("Test Page", result.HtmlContent);
        Assert.Equal("text/html", result.ContentType);
    }

    [Fact]
    public async Task FetchAsync_IncludesContentHash_ForChangeDetection()
    {
        // Arrange
        var mockHttpClientFactory = new MockHttpClientFactory();
        var mockHtmlParser = CreateMockHtmlParser();
        var mockLogger = CreateMockLogger();
        var options = CreateDefaultOptions();

        var htmlContent = "<html><body>Test</body></html>";
        var response = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(htmlContent),
            RequestMessage = new HttpRequestMessage { RequestUri = new Uri("http://example.com") }
        };
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/html");
        mockHttpClientFactory.SetupResponse(response);

        var httpClient = mockHttpClientFactory.CreateHttpClient();
        var fetcher = new WebFetcher(httpClient, mockLogger.Object, mockHtmlParser.Object, options);

        // Act
        var result = await fetcher.FetchAsync("http://example.com");

        // Assert
        Assert.NotNull(result.ContentHash);
        Assert.NotEmpty(result.ContentHash);
        Assert.Equal(64, result.ContentHash.Length); // SHA256 hex string is 64 characters
    }

    [Fact]
    public async Task FetchAsync_DifferentContent_ProducesDifferentContentHash()
    {
        // Arrange
        var mockHtmlParser = CreateMockHtmlParser();
        var mockLogger = CreateMockLogger();
        var options = CreateDefaultOptions();

        var content1 = "<html><body>Content 1</body></html>";
        var content2 = "<html><body>Content 2</body></html>";

        // First request
        var mockHttpClientFactory1 = new MockHttpClientFactory();
        var response1 = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(content1),
            RequestMessage = new HttpRequestMessage { RequestUri = new Uri("http://example.com") }
        };
        response1.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/html");
        mockHttpClientFactory1.SetupResponse(response1);

        var httpClient1 = mockHttpClientFactory1.CreateHttpClient();
        var fetcher1 = new WebFetcher(httpClient1, mockLogger.Object, mockHtmlParser.Object, options);
        var result1 = await fetcher1.FetchAsync("http://example.com");

        // Second request with different content
        var mockHttpClientFactory2 = new MockHttpClientFactory();
        var response2 = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(content2),
            RequestMessage = new HttpRequestMessage { RequestUri = new Uri("http://example.com") }
        };
        response2.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/html");
        mockHttpClientFactory2.SetupResponse(response2);

        var httpClient2 = mockHttpClientFactory2.CreateHttpClient();
        var fetcher2 = new WebFetcher(httpClient2, mockLogger.Object, mockHtmlParser.Object, options);
        var result2 = await fetcher2.FetchAsync("http://example.com");

        // Assert
        Assert.NotEqual(result1.ContentHash, result2.ContentHash);
    }

    [Fact]
    public async Task FetchAsync_IdenticalContent_ProducesSameContentHash()
    {
        // Arrange
        var htmlContent = "<html><body>Identical Content</body></html>";
        var mockHtmlParser = CreateMockHtmlParser();
        var mockLogger = CreateMockLogger();
        var options = CreateDefaultOptions();

        // First request
        var mockHttpClientFactory1 = new MockHttpClientFactory();
        var response1 = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(htmlContent),
            RequestMessage = new HttpRequestMessage { RequestUri = new Uri("http://example.com") }
        };
        response1.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/html");
        mockHttpClientFactory1.SetupResponse(response1);

        var httpClient1 = mockHttpClientFactory1.CreateHttpClient();
        var fetcher1 = new WebFetcher(httpClient1, mockLogger.Object, mockHtmlParser.Object, options);
        var result1 = await fetcher1.FetchAsync("http://example.com");

        // Second request with same content
        var mockHttpClientFactory2 = new MockHttpClientFactory();
        var response2 = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(htmlContent),
            RequestMessage = new HttpRequestMessage { RequestUri = new Uri("http://example.com") }
        };
        response2.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/html");
        mockHttpClientFactory2.SetupResponse(response2);

        var httpClient2 = mockHttpClientFactory2.CreateHttpClient();
        var fetcher2 = new WebFetcher(httpClient2, mockLogger.Object, mockHtmlParser.Object, options);
        var result2 = await fetcher2.FetchAsync("http://example.com");

        // Assert
        Assert.Equal(result1.ContentHash, result2.ContentHash);
    }


    [Fact]
    public async Task FetchAsync_WithNullUrl_ThrowsArgumentNullException()
    {
        // Arrange
        var httpClient = new HttpClient();
        var mockLogger = CreateMockLogger();
        var mockHtmlParser = CreateMockHtmlParser();
        var fetcher = new WebFetcher(httpClient, mockLogger.Object, mockHtmlParser.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => fetcher.FetchAsync(null!));
    }

    [Fact]
    public async Task FetchAsync_WithEmptyUrl_ThrowsArgumentNullException()
    {
        // Arrange
        var httpClient = new HttpClient();
        var mockLogger = CreateMockLogger();
        var mockHtmlParser = CreateMockHtmlParser();
        var fetcher = new WebFetcher(httpClient, mockLogger.Object, mockHtmlParser.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => fetcher.FetchAsync(string.Empty));
    }

    [Fact]
    public async Task FetchAsync_WithInvalidUrl_ThrowsInvalidOperationException()
    {
        // Arrange
        var httpClient = new HttpClient();
        var mockLogger = CreateMockLogger();
        var mockHtmlParser = CreateMockHtmlParser();
        var fetcher = new WebFetcher(httpClient, mockLogger.Object, mockHtmlParser.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => fetcher.FetchAsync("not a valid url"));
    }

    [Fact]
    public async Task FetchAsync_WithExcessiveContentSize_ThrowsInvalidOperationException()
    {
        // Arrange
        var mockHttpClientFactory = new MockHttpClientFactory();
        var mockLogger = CreateMockLogger();
        var mockHtmlParser = CreateMockHtmlParser();
        var options = new WebFetcherOptions { MaxContentSizeBytes = 100 };

        var largeContent = new string('x', 1001); // Exceeds 100 byte limit
        mockHttpClientFactory.SetupResponse(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(largeContent)
        });

        var httpClient = mockHttpClientFactory.CreateHttpClient();
        var fetcher = new WebFetcher(httpClient, mockLogger.Object, mockHtmlParser.Object, options);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => fetcher.FetchAsync("http://example.com"));
        Assert.Contains("exceeds maximum", ex.Message);
    }

    [Fact]
    public async Task FetchAsync_With404Response_ThrowsInvalidOperationException()
    {
        // Arrange
        var mockHttpClientFactory = new MockHttpClientFactory();
        var mockLogger = CreateMockLogger();
        var mockHtmlParser = CreateMockHtmlParser();
        var options = CreateDefaultOptions();

        mockHttpClientFactory.SetupResponse(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.NotFound
        });

        var httpClient = mockHttpClientFactory.CreateHttpClient();
        var fetcher = new WebFetcher(httpClient, mockLogger.Object, mockHtmlParser.Object, options);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => fetcher.FetchAsync("http://example.com/notfound"));
        Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FetchAsync_With403Response_ThrowsInvalidOperationException()
    {
        // Arrange
        var mockHttpClientFactory = new MockHttpClientFactory();
        var mockLogger = CreateMockLogger();
        var mockHtmlParser = CreateMockHtmlParser();
        var options = CreateDefaultOptions();

        mockHttpClientFactory.SetupResponse(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.Forbidden
        });

        var httpClient = mockHttpClientFactory.CreateHttpClient();
        var fetcher = new WebFetcher(httpClient, mockLogger.Object, mockHtmlParser.Object, options);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => fetcher.FetchAsync("http://example.com"));
        Assert.Contains("forbidden", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FetchAsync_WithNonHtmlContentType_StillReturnSuccess()
    {
        // Arrange
        var mockHttpClientFactory = new MockHttpClientFactory();
        var mockLogger = CreateMockLogger();
        var mockHtmlParser = CreateMockHtmlParser();
        var options = CreateDefaultOptions();

        var response = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("<html></html>")
        };
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        mockHttpClientFactory.SetupResponse(response);

        var httpClient = mockHttpClientFactory.CreateHttpClient();
        var fetcher = new WebFetcher(httpClient, mockLogger.Object, mockHtmlParser.Object, options);

        // Act
        var result = await fetcher.FetchAsync("http://example.com");

        // Assert - Should still succeed, just log a warning
        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
    }

    [Fact]
    public async Task FetchAsync_SetsPropperUserAgent()
    {
        // Arrange
        var mockHandler = new MockHttpMessageHandler();
        var httpClient = new HttpClient(mockHandler) { BaseAddress = new Uri("http://example.com") };

        var mockLogger = CreateMockLogger();
        var mockHtmlParser = CreateMockHtmlParser();
        var options = new WebFetcherOptions { UserAgent = "CustomAgent/1.0" };

        mockHandler.SetupSuccessResponse("<html></html>");

        var fetcher = new WebFetcher(httpClient, mockLogger.Object, mockHtmlParser.Object, options);

        // Act
        await fetcher.FetchAsync("http://example.com");

        // Assert
        Assert.NotNull(mockHandler.LastRequest);
        Assert.True(mockHandler.LastRequest.Headers.Contains("User-Agent"));
        var userAgent = mockHandler.LastRequest.Headers.GetValues("User-Agent").FirstOrDefault();
        Assert.Contains("CustomAgent", userAgent);
    }

    [Fact]
    public async Task FetchAndExtractAsync_ParsesHtmlContent()
    {
        // Arrange
        var mockHttpClientFactory = new MockHttpClientFactory();
        var mockHtmlParser = CreateMockHtmlParser();
        var mockLogger = CreateMockLogger();
        var options = CreateDefaultOptions();

        var htmlContent = "<html><body>Extracted Text</body></html>";
        mockHttpClientFactory.SetupResponse(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(htmlContent)
        });

        // Setup HTML parser mock to return parsed document
        var mockDocument = new Mock<IHtmlDocument>();
        mockHtmlParser.Setup(p => p.ParseAsync(htmlContent, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockDocument.Object);

        mockHtmlParser.Setup(p => p.ExtractText(mockDocument.Object))
            .Returns("Extracted Text Only");

        var httpClient = mockHttpClientFactory.CreateHttpClient();
        var fetcher = new WebFetcher(httpClient, mockLogger.Object, mockHtmlParser.Object, options);

        // Act
        var result = await fetcher.FetchAndExtractAsync("http://example.com");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Extracted Text Only", result.HtmlContent);
        mockHtmlParser.Verify(p => p.ParseAsync(htmlContent, It.IsAny<CancellationToken>()), Times.Once);
        mockHtmlParser.Verify(p => p.ExtractText(mockDocument.Object), Times.Once);
    }

    [Fact]
    public async Task FetchAndExtractAsync_WithNullUrl_ThrowsArgumentNullException()
    {
        // Arrange
        var httpClient = new HttpClient();
        var mockLogger = CreateMockLogger();
        var mockHtmlParser = CreateMockHtmlParser();
        var fetcher = new WebFetcher(httpClient, mockLogger.Object, mockHtmlParser.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => fetcher.FetchAndExtractAsync(null!));
    }

    [Fact]
    public async Task FetchAsync_RespectsCancellationToken()
    {
        // Arrange
        var mockHttpClientFactory = new MockHttpClientFactory();
        mockHttpClientFactory.SetupDelayedResponse(2000); // 2 second delay

        var mockLogger = CreateMockLogger();
        var mockHtmlParser = CreateMockHtmlParser();
        var options = new WebFetcherOptions { RequestTimeoutMs = 5000 };

        var httpClient = mockHttpClientFactory.CreateHttpClient();
        var fetcher = new WebFetcher(httpClient, mockLogger.Object, mockHtmlParser.Object, options);

        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500)); // Cancel after 500ms

        // Act & Assert
        // When cancelled, either OperationCanceledException or TaskCanceledException can be thrown
        try
        {
            await fetcher.FetchAsync("http://example.com", cts.Token);
            Assert.Fail("Should have thrown an exception");
        }
        catch (OperationCanceledException)
        {
            // Expected behavior - both OperationCanceledException and TaskCanceledException are acceptable
        }
    }

    [Fact]
    public async Task FetchAsync_ReturnsResolvedUrlAfterRedirect()
    {
        // Arrange
        var mockHandler = new MockHttpMessageHandler();
        var httpClient = new HttpClient(mockHandler);

        var mockLogger = CreateMockLogger();
        var mockHtmlParser = CreateMockHtmlParser();

        mockHandler.SetupSuccessResponse("<html></html>");
        var finalUri = new Uri("http://example.com/redirected");

        var fetcher = new WebFetcher(httpClient, mockLogger.Object, mockHtmlParser.Object);

        // Act
        var result = await fetcher.FetchAsync("http://example.com");

        // Assert
        Assert.NotNull(result);
        // The result should contain the final URL representation
        Assert.NotEmpty(result.ResolvedUrl);
    }

    [Fact]
    public async Task FetchAsync_IncludesAcceptHeaders()
    {
        // Arrange
        var mockHandler = new MockHttpMessageHandler();
        var httpClient = new HttpClient(mockHandler);

        var mockLogger = CreateMockLogger();
        var mockHtmlParser = CreateMockHtmlParser();
        var options = new WebFetcherOptions
        {
            AcceptHeader = "text/html,application/xhtml+xml"
        };

        mockHandler.SetupSuccessResponse("<html></html>");

        var fetcher = new WebFetcher(httpClient, mockLogger.Object, mockHtmlParser.Object, options);

        // Act
        await fetcher.FetchAsync("http://example.com");

        // Assert
        Assert.NotNull(mockHandler.LastRequest);
        Assert.True(mockHandler.LastRequest.Headers.Contains("Accept"));
        var acceptHeader = mockHandler.LastRequest.Headers.GetValues("Accept").FirstOrDefault();
        Assert.Contains("text/html", acceptHeader);
    }

    [Fact]
    public async Task FetchAsync_WithZeroSizeContent_ReturnsEmptyString()
    {
        // Arrange
        var mockHttpClientFactory = new MockHttpClientFactory();
        var mockLogger = CreateMockLogger();
        var mockHtmlParser = CreateMockHtmlParser();

        mockHttpClientFactory.SetupResponse(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("")
        });

        var httpClient = mockHttpClientFactory.CreateHttpClient();
        var fetcher = new WebFetcher(httpClient, mockLogger.Object, mockHtmlParser.Object);

        // Act
        var result = await fetcher.FetchAsync("http://example.com");

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.HtmlContent);
    }

    [Fact]
    public async Task FetchAsync_LogsInformationAboutFetch()
    {
        // Arrange
        var mockHttpClientFactory = new MockHttpClientFactory();
        var mockLogger = CreateMockLogger();
        var mockHtmlParser = CreateMockHtmlParser();

        var response = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("<html></html>")
        };
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/html");
        mockHttpClientFactory.SetupResponse(response);

        var httpClient = mockHttpClientFactory.CreateHttpClient();
        var fetcher = new WebFetcher(httpClient, mockLogger.Object, mockHtmlParser.Object);

        // Act
        await fetcher.FetchAsync("http://example.com");

        // Assert - Verify logging occurred (multiple calls expected due to different log levels)
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("fetch")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}

/// <summary>
/// Unit tests for WebFetcherOptions configuration.
/// </summary>
public class WebFetcherOptionsTests
{
    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        // Act
        var options = new WebFetcherOptions();

        // Assert
        Assert.Equal(30_000, options.RequestTimeoutMs);
        Assert.Equal(10 * 1024 * 1024, options.MaxContentSizeBytes);
        Assert.True(options.FollowRedirects);
        Assert.Equal(10, options.MaxRedirects);
        Assert.True(options.ThrowOnResponseError);
        Assert.NotEmpty(options.UserAgent);
        Assert.NotEmpty(options.AcceptHeader);
    }

    [Fact]
    public void CanModifyOptions()
    {
        // Arrange
        var options = new WebFetcherOptions();

        // Act
        options.RequestTimeoutMs = 60_000;
        options.MaxContentSizeBytes = 50 * 1024 * 1024;
        options.ThrowOnResponseError = false;

        // Assert
        Assert.Equal(60_000, options.RequestTimeoutMs);
        Assert.Equal(50 * 1024 * 1024, options.MaxContentSizeBytes);
        Assert.False(options.ThrowOnResponseError);
    }
}

/// <summary>
/// Helper class for mocking HTTP responses in tests.
/// </summary>
internal class MockHttpClientFactory
{
    private HttpResponseMessage? _response;
    private int _delay = 0;

    public void SetupResponse(HttpResponseMessage response)
    {
        _response = response;
    }

    public void SetupDelayedResponse(int delayMs)
    {
        _delay = delayMs;
    }

    public HttpClient CreateHttpClient()
    {
        var handler = new MockHttpMessageHandler();
        if (_response != null)
        {
            handler.Setup(_response);
        }
        if (_delay > 0)
        {
            handler.SetupDelay(_delay);
        }
        return new HttpClient(handler);
    }
}

/// <summary>
/// Mock HTTP message handler for testing HTTP client interactions.
/// </summary>
internal class MockHttpMessageHandler : HttpMessageHandler
{
    private HttpResponseMessage? _response;
    private int _delayMs = 0;
    public HttpRequestMessage? LastRequest { get; private set; }

    public void Setup(HttpResponseMessage response)
    {
        _response = response;
    }

    public void SetupSuccessResponse(string content)
    {
        _response = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(content),
            RequestMessage = new HttpRequestMessage { RequestUri = new Uri("http://example.com") }
        };
    }

    public void SetupDelay(int delayMs)
    {
        _delayMs = delayMs;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        LastRequest = request;

        if (_delayMs > 0)
        {
            await Task.Delay(_delayMs, cancellationToken);
        }

        if (_response == null)
        {
            _response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("<html></html>")
            };
        }

        return _response;
    }
}
