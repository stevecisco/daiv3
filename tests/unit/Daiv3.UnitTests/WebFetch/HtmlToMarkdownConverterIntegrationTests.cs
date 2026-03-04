using Daiv3.WebFetch.Crawl;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

#pragma warning disable IDISP001 // Dispose created
#pragma warning disable IDISP004 // Don't ignore created IDisposable

namespace Daiv3.UnitTests.WebFetch;

/// <summary>
/// Integration tests for HtmlToMarkdownConverter with dependency injection.
/// Tests the complete HTML to Markdown conversion pipeline with DI configuration.
/// </summary>
public class HtmlToMarkdownConverterIntegrationTests
{
    private IServiceProvider CreateServiceProvider(
        Action<HtmlToMarkdownOptions>? configureConverter = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        if (configureConverter != null)
            services.AddHtmlToMarkdownConverter(configureConverter);
        else
            services.AddHtmlToMarkdownConverter();

        return services.BuildServiceProvider();
    }

    [Fact]
    public void ServiceProvider_CanResolveHtmlToMarkdownConverter()
    {
        // Arrange & Act
        var provider = CreateServiceProvider();

        // Assert
        var converter = provider.GetRequiredService<IHtmlToMarkdownConverter>();
        Assert.NotNull(converter);
        Assert.IsType<HtmlToMarkdownConverter>(converter);
    }

    [Fact]
    public void ServiceProvider_RegistersDefaultOptions()
    {
        // Arrange & Act
        var provider = CreateServiceProvider();

        // Assert
        var options = provider.GetRequiredService<HtmlToMarkdownOptions>();
        Assert.NotNull(options);
        Assert.Equal(5 * 1024 * 1024, options.MaxContentLength);
        Assert.True(options.KeepLinks);
        Assert.False(options.KeepImages);
    }

    [Fact]
    public void ServiceProvider_AppliesCustomConfiguration()
    {
        // Arrange & Act
        var provider = CreateServiceProvider(
            configureConverter: opts =>
            {
                opts.MaxContentLength = 1_000_000;
                opts.KeepImages = true;
                opts.RemoveEmptyLines = false;
            });

        // Assert
        var options = provider.GetRequiredService<HtmlToMarkdownOptions>();
        Assert.Equal(1_000_000, options.MaxContentLength);
        Assert.True(options.KeepImages);
        Assert.False(options.RemoveEmptyLines);
    }

    [Fact]
    public void ServiceProvider_CreatesNewInstancePerScope()
    {
        // Arrange
        var provider = CreateServiceProvider();

        // Act
        IHtmlToMarkdownConverter? converter1;
        IHtmlToMarkdownConverter? converter2;

        using (var scope1 = provider.CreateScope())
        {
            converter1 = scope1.ServiceProvider.GetRequiredService<IHtmlToMarkdownConverter>();
        }

        using (var scope2 = provider.CreateScope())
        {
            converter2 = scope2.ServiceProvider.GetRequiredService<IHtmlToMarkdownConverter>();
        }

        // Assert - scoped instances should be different
        Assert.NotSame(converter1, converter2);
    }

    [Fact]
    public async Task Converter_WithDI_ConvertsHtmlCorrectly()
    {
        // Arrange
        var provider = CreateServiceProvider();
        var converter = provider.GetRequiredService<IHtmlToMarkdownConverter>();
        var html = @"
<html>
<body>
<h1>Test Title</h1>
<p>This is test content.</p>
</body>
</html>";

        // Act
        var result = await converter.ConvertAsync(html);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Test Title", result);
        Assert.Contains("test content", result);
    }

    [Fact]
    public async Task Converter_WithCustomOptions_AppliesConfiguration()
    {
        // Arrange
        var provider = CreateServiceProvider(
            configureConverter: opts =>
            {
                opts.ExcludeTags.Add("p");
            });

        var converter = provider.GetRequiredService<IHtmlToMarkdownConverter>();
        var html = "<html><body><p>Excluded</p><h1>Kept</h1></body></html>";

        // Act
        var result = await converter.ConvertAsync(html);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Kept", result);
        Assert.DoesNotContain("Excluded", result);
    }

    [Fact]
    public async Task Converter_StripAds_FromRealWorldLayout()
    {
        // Arrange
        var provider = CreateServiceProvider();
        var converter = provider.GetRequiredService<IHtmlToMarkdownConverter>();

        var html = @"
<html>
<head></head>
<body>
  <div class='cookie-banner'>Accept cookies?</div>
  <header>
    <nav class='navigation'>
      <a href='/'>Home</a>
      <a href='/about'>About</a>
    </nav>
  </header>
  <main>
    <article>
      <h1>Article Title</h1>
      <p>This is the main article content that should be preserved.</p>
      <p>More details about the topic.</p>
    </article>
    <aside class='sidebar'>
      <div class='ads'>Advertisement content here</div>
    </aside>
  </main>
  <footer>
    <p>Copyright notice</p>
  </footer>
</body>
</html>";

        // Act
        var result = await converter.ConvertAsync(html);

        // Assert
        Assert.NotNull(result);
        // Main content should be preserved
        Assert.Contains("Article Title", result);
        Assert.Contains("main article content", result);
        Assert.Contains("More details", result);

        // Ads and navigation should be stripped
        Assert.DoesNotContain("Accept cookies", result);
        Assert.DoesNotContain("Advertisement content", result);
    }

    [Fact]
    public async Task Converter_PreservesCode_InRealWorldExample()
    {
        // Arrange
        var provider = CreateServiceProvider();
        var converter = provider.GetRequiredService<IHtmlToMarkdownConverter>();

        var html = @"
<html>
<body>
  <h2>Example Code</h2>
  <p>Here is some example code:</p>
  <pre><code>
public class Example {
    public static void Main() {
        Console.WriteLine('Hello World');
    }
}
  </code></pre>
  <p>As shown above, this demonstrates the concept.</p>
</body>
</html>";

        // Act
        var result = await converter.ConvertAsync(html);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Example Code", result);
        Assert.Contains("Example", result);
        Assert.Contains("public", result);
    }

    [Fact]
    public async Task Converter_WithDetailsAsync_TracksBothConversion()
    {
        // Arrange
        var provider = CreateServiceProvider();
        var converter = provider.GetRequiredService<IHtmlToMarkdownConverter>();

        var html = @"
<html>
<body>
  <h1>Title</h1>
  <p>Content with <a href='#link1'>a link</a> and <a href='#link2'>another link</a>.</p>
  <img src='image.jpg' />
  <img src='image2.jpg' />
</body>
</html>";

        // Act
        var result = await converter.ConvertWithDetailsAsync(html);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.MarkdownContent);
        Assert.Equal(2, result.LinksExtracted);
        Assert.Equal(2, result.ImagesReferenced);
        Assert.True(result.OriginalContentLength > 0);
        Assert.True(result.MarkdownContentLength > 0);
        Assert.True(result.ConvertedAtUtc <= DateTime.UtcNow);
    }

    [Fact]
    public async Task Converter_MultipleRequests_WorksCorrectly()
    {
        // Arrange
        var provider = CreateServiceProvider();
        var converter = provider.GetRequiredService<IHtmlToMarkdownConverter>();

        var html1 = "<html><body><p>First content</p></body></html>";
        var html2 = "<html><body><p>Second content</p></body></html>";
        var html3 = "<html><body><p>Third content</p></body></html>";

        // Act
        var result1 = await converter.ConvertAsync(html1);
        var result2 = await converter.ConvertAsync(html2);
        var result3 = await converter.ConvertAsync(html3);

        // Assert
        Assert.Contains("First content", result1);
        Assert.Contains("Second content", result2);
        Assert.Contains("Third content", result3);
    }

    [Fact]
    public async Task Converter_CachesOptions_Singleton()
    {
        // Arrange
        var provider = CreateServiceProvider();

        // Act
        var options1 = provider.GetRequiredService<HtmlToMarkdownOptions>();
        var options2 = provider.GetRequiredService<HtmlToMarkdownOptions>();

        // Assert - options should be singleton
        Assert.Same(options1, options2);
    }

    [Fact]
    public void Converter_WithLogging_WorksCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        var logMessages = new List<string>();

        // Add logging that captures messages
        services.AddLogging(builder =>
        {
            builder.AddProvider(new SimpleTestLogProvider(logMessages));
        });

        services.AddHtmlToMarkdownConverter();
        var provider = services.BuildServiceProvider();

        // Act
        var converter = provider.GetRequiredService<IHtmlToMarkdownConverter>();

        // Assert
        Assert.NotNull(converter);
    }

    [Fact]
    public async Task Converter_WithAllServices_IntegratesCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHtmlParser();
        services.AddWebFetcher();
        services.AddHtmlToMarkdownConverter();
        var provider = services.BuildServiceProvider();

        // Act
        var parser = provider.GetRequiredService<IHtmlParser>();
        var fetcher = provider.GetRequiredService<IWebFetcher>();
        var converter = provider.GetRequiredService<IHtmlToMarkdownConverter>();

        // Assert
        Assert.NotNull(parser);
        Assert.NotNull(fetcher);
        Assert.NotNull(converter);
    }

    [Fact]
    public async Task Converter_HandlesErrorsGracefully()
    {
        // Arrange
        var provider = CreateServiceProvider();
        var converter = provider.GetRequiredService<IHtmlToMarkdownConverter>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => converter.ConvertAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(() => converter.ConvertAsync(""));
    }

    /// <summary>
    /// Simple test log provider for capturing log messages during tests.
    /// </summary>
    private class SimpleTestLogProvider : ILoggerProvider
    {
        private readonly List<string> _messages;

        public SimpleTestLogProvider(List<string> messages)
        {
            _messages = messages;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new SimpleTestLogger(_messages);
        }

        public void Dispose() { }
    }

    private class SimpleTestLogger : ILogger
    {
        private readonly List<string> _messages;

        public SimpleTestLogger(List<string> messages)
        {
            _messages = messages;
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
            _messages.Add(formatter(state, exception));
        }
    }
}
