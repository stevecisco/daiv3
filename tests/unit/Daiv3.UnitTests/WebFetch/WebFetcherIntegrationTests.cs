using Daiv3.WebFetch.Crawl;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;
using Xunit;

#pragma warning disable IDISP001 // Dispose created
#pragma warning disable IDISP003 // Dispose previous before re-assigning
#pragma warning disable IDISP006 // Implement IDisposable

namespace Daiv3.UnitTests.WebFetch;

/// <summary>
/// Integration tests for web fetcher service with dependency injection.
/// Tests the complete web fetch pipeline including DI configuration.
/// </summary>
public class WebFetcherIntegrationTests
{
    private IServiceProvider CreateServiceProvider(
        Action<WebFetcherOptions>? configureFetcher = null,
        Action<HtmlParsingOptions>? configureParser = null,
        HttpMessageHandler? handler = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        // Add HTML parser
        if (configureParser != null)
            services.AddHtmlParser(configureParser);
        else
            services.AddHtmlParser();

        // Add web fetcher
        if (configureFetcher != null)
            services.AddWebFetcher(configureFetcher);
        else
            services.AddWebFetcher();

        // Register custom HTTP client handler if provided
        if (handler != null)
        {
            services.AddHttpClient<IWebFetcher, WebFetcher>("WebFetcher")
                .ConfigurePrimaryHttpMessageHandler(() => handler);
        }

        return services.BuildServiceProvider();
    }

    [Fact]
    public void ServiceProvider_CanResolveBothHtmlParserAndWebFetcher()
    {
        // Arrange & Act
        var provider = CreateServiceProvider();

        // Assert
        var parser = provider.GetRequiredService<IHtmlParser>();
        var fetcher = provider.GetRequiredService<IWebFetcher>();

        Assert.NotNull(parser);
        Assert.NotNull(fetcher);
    }

    [Fact]
    public void ServiceProvider_RegistersDefaultOptions()
    {
        // Arrange & Act
        var provider = CreateServiceProvider();

        // Assert
        var parserOptions = provider.GetRequiredService<HtmlParsingOptions>();
        var fetcherOptions = provider.GetRequiredService<WebFetcherOptions>();

        Assert.NotNull(parserOptions);
        Assert.NotNull(fetcherOptions);
        Assert.Equal(10 * 1024 * 1024, parserOptions.MaxContentSizeBytes);
        Assert.Equal(30_000, fetcherOptions.RequestTimeoutMs);
    }

    [Fact]
    public void ServiceProvider_AppliesCustomFetcherConfiguration()
    {
        // Arrange & Act
        var provider = CreateServiceProvider(
            configureFetcher: opts =>
            {
                opts.RequestTimeoutMs = 60_000;
                opts.MaxContentSizeBytes = 50 * 1024 * 1024;
            });

        // Assert
        var options = provider.GetRequiredService<WebFetcherOptions>();
        Assert.Equal(60_000, options.RequestTimeoutMs);
        Assert.Equal(50 * 1024 * 1024, options.MaxContentSizeBytes);
    }

    [Fact]
    public void ServiceProvider_AppliesCustomParserConfiguration()
    {
        // Arrange & Act
        var provider = CreateServiceProvider(
            configureParser: opts =>
            {
                opts.MaxContentSizeBytes = 5 * 1024 * 1024;
                opts.TimeoutMs = 15_000;
            });

        // Assert
        var options = provider.GetRequiredService<HtmlParsingOptions>();
        Assert.Equal(5 * 1024 * 1024, options.MaxContentSizeBytes);
        Assert.Equal(15_000, options.TimeoutMs);
    }

    [Fact]
    public async Task Fetcher_WithHtmlParser_CanExtractContent()
    {
        // This test verifies that the fetcher properly integrates with the HTML parser
        // Note: This uses a mock HTTP handler internally
        var handler = new RecordingHttpMessageHandler();
        handler.SetupSuccessResponse("<html><body>Test Content</body></html>");

        var provider = CreateServiceProvider(handler: handler);
        var fetcher = provider.GetRequiredService<IWebFetcher>();

        // Act
        var result = await fetcher.FetchAsync("http://example.com");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Test Content", result.HtmlContent);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task Fetcher_WithInvalidContentSize_ThrowsException()
    {
        // Arrange
        var provider = CreateServiceProvider(
            configureFetcher: opts => opts.MaxContentSizeBytes = 100);

        var handler = new RecordingHttpMessageHandler();
        var largeContent = new string('x', 1001);
        handler.SetupSuccessResponse(largeContent);

        // Create a new fetcher with the custom handler
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHtmlParser();
        services.AddHttpClient<IWebFetcher, WebFetcher>("WebFetcher")
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        services.AddSingleton(new WebFetcherOptions { MaxContentSizeBytes = 100 });
        var serviceProvider = services.BuildServiceProvider();

        var fetcher = serviceProvider.GetRequiredService<IWebFetcher>();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fetcher.FetchAsync("http://example.com"));
    }

    [Fact]
    public async Task Fetcher_LogsAreConfiguredViaServiceProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        var logRecorder = new FakeLogCollector();
        services.AddLogging(builder =>
        {
            builder.AddProvider(logRecorder);
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        services.AddHtmlParser();
        services.AddWebFetcher();

        var handler = new RecordingHttpMessageHandler();
        handler.SetupSuccessResponse("<html>Content</html>");
        services.AddHttpClient<IWebFetcher, WebFetcher>("WebFetcher")
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        var provider = services.BuildServiceProvider();
        var fetcher = provider.GetRequiredService<IWebFetcher>();

        // Act
        var result = await fetcher.FetchAsync("http://example.com");

        // Assert
        Assert.NotNull(result);
        // Verify that logging was configured
        Assert.NotEmpty(logRecorder.LogEntries);
        Assert.Contains(logRecorder.LogEntries, e => e.Contains("fetch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task MultipleScopes_ProvideDifferentServiceInstances()
    {
        // Arrange
        var provider = CreateServiceProvider();

        // Act - Get fetchers in different scopes
        IWebFetcher fetcher1;
        IWebFetcher fetcher2;
        using (var scope1 = provider.CreateScope())
        {
            fetcher1 = scope1.ServiceProvider.GetRequiredService<IWebFetcher>();
        }
        using (var scope2 = provider.CreateScope())
        {
            fetcher2 = scope2.ServiceProvider.GetRequiredService<IWebFetcher>();
        }

        // Assert - Scoped services should return different instances
        Assert.NotSame(fetcher1, fetcher2);
    }

    [Fact]
    public async Task Fetcher_HandlesMultipleRequests()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler();
        handler.SetupSuccessResponse("<html><body>Content</body></html>");

        var provider = CreateServiceProvider(handler: handler);
        var fetcher = provider.GetRequiredService<IWebFetcher>();

        // Act
        var result1 = await fetcher.FetchAsync("http://example.com/page1");
        var result2 = await fetcher.FetchAsync("http://example.com/page2");
        var result3 = await fetcher.FetchAsync("http://example.com/page3");

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.NotNull(result3);
        Assert.Equal(3, handler.RequestCount);
    }
}

/// <summary>
/// Mock HTTP message handler that records requests.
/// </summary>
internal class RecordingHttpMessageHandler : HttpMessageHandler
{
    private HttpResponseMessage? _response;
    public int RequestCount { get; private set; }

    public void SetupSuccessResponse(string content)
    {
        _response = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(content)
        };
        _response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/html");
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        RequestCount++;

        if (_response == null)
        {
            _response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("<html></html>")
            };
            _response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/html");
        }

        return Task.FromResult(_response);
    }
}

/// <summary>
/// Fake logging provider to collect log entries during tests.
/// </summary>
internal class FakeLogCollector : ILoggerProvider
{
    private readonly List<string> _logEntries = new();
    public IReadOnlyList<string> LogEntries => _logEntries;

    public ILogger CreateLogger(string categoryName)
    {
        return new FakeLogger(this);
    }

    public void Dispose() { }

    internal void Collect(string message)
    {
        _logEntries.Add(message);
    }

    private class FakeLogger : ILogger
    {
        private readonly FakeLogCollector _parent;

        public FakeLogger(FakeLogCollector parent)
        {
            _parent = parent;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            _parent.Collect(message);
        }
    }
}
