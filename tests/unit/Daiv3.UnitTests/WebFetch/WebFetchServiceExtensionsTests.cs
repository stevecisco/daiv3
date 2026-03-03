using Daiv3.WebFetch.Crawl;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Daiv3.UnitTests.WebFetch;

/// <summary>
/// Unit tests for WebFetchServiceExtensions dependency injection configuration.
/// </summary>
public class WebFetchServiceExtensionsTests
{
    private ServiceCollection CreateServiceCollection()
    {
        var services = new ServiceCollection();
        // Add logging to support HtmlParser dependency
        services.AddLogging();
        return services;
    }
    [Fact]
    public void AddHtmlParser_RegistersHtmlParserService()
    {
        // Arrange
        var services = CreateServiceCollection();

        // Act
        services.AddHtmlParser();
        var provider = services.BuildServiceProvider();

        // Assert
        var parser = provider.GetService<IHtmlParser>();
        Assert.NotNull(parser);
        Assert.IsType<HtmlParser>(parser);
    }

    [Fact]
    public void AddHtmlParser_RegistersWithDefaultOptions()
    {
        // Arrange
        var services = CreateServiceCollection();

        // Act
        services.AddHtmlParser();
        var provider = services.BuildServiceProvider();

        // Assert
        var options = provider.GetService<HtmlParsingOptions>();
        Assert.NotNull(options);
        Assert.Equal(10 * 1024 * 1024, options.MaxContentSizeBytes);
    }

    [Fact]
    public void AddHtmlParser_WithCustomOptions_UsesCustomConfiguration()
    {
        // Arrange
        var services = CreateServiceCollection();

        // Act
        services.AddHtmlParser(opts => opts.MaxContentSizeBytes = 5_000_000);
        var provider = services.BuildServiceProvider();

        // Assert
        var options = provider.GetService<HtmlParsingOptions>();
        Assert.NotNull(options);
        Assert.Equal(5_000_000, options.MaxContentSizeBytes);
    }

    [Fact]
    public void AddHtmlParser_WithOptionsFactory_CreatesOptionsPerCall()
    {
        // Arrange
        var services = CreateServiceCollection();
        var callCount = 0;

        // Act
        services.AddHtmlParser(_ =>
        {
            callCount++;
            return new HtmlParsingOptions { MaxContentSizeBytes = 1_000_000 };
        });
        var provider = services.BuildServiceProvider();

        // Assert
        var parser1 = provider.GetRequiredService<IHtmlParser>();
        var parser2 = provider.GetRequiredService<IHtmlParser>();
        Assert.NotNull(parser1);
        Assert.NotNull(parser2);
        // Factory should be called when building each parser
        Assert.True(callCount > 0);
    }

    [Fact]
    public void AddHtmlParser_ReturnsServiceCollection()
    {
        // Arrange & Act
        var services = new ServiceCollection();
        var result = services.AddHtmlParser();

        // Assert - verify it returns IServiceCollection for chaining
        Assert.Same(services, result);
    }

    [Fact]
    public void AddHtmlParser_Multiple_ReturnsDifferentInstancesInDifferentScopes()
    {
        // Arrange
        var services = CreateServiceCollection();

        // Act
        services.AddHtmlParser();
        var provider = services.BuildServiceProvider();

        // Get parsers in different scopes
        IHtmlParser? parser1;
        IHtmlParser? parser2;
        using (var scope1 = provider.CreateScope())
        {
            parser1 = scope1.ServiceProvider.GetRequiredService<IHtmlParser>();
        }

        using (var scope2 = provider.CreateScope())
        {
            parser2 = scope2.ServiceProvider.GetRequiredService<IHtmlParser>();
        }

        // Assert - scoped services should return different instances across different scopes
        Assert.NotSame(parser1, parser2);
    }

    [Fact]
    public void AddHtmlParser_NullServices_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => ((IServiceCollection)null!).AddHtmlParser());
    }

    [Fact]
    public void AddHtmlParser_WithNullFactory_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => services.AddHtmlParser((Func<IServiceProvider, HtmlParsingOptions>)null!));
    }

    [Fact]
    public void AddWebFetcher_RegistersWebFetcherService()
    {
        // Arrange
        var services = CreateServiceCollection();
        services.AddHtmlParser();

        // Act
        services.AddWebFetcher();
        var provider = services.BuildServiceProvider();

        // Assert
        var fetcher = provider.GetService<IWebFetcher>();
        Assert.NotNull(fetcher);
        Assert.IsType<WebFetcher>(fetcher);
    }

    [Fact]
    public void AddWebFetcher_RegistersWithDefaultOptions()
    {
        // Arrange
        var services = CreateServiceCollection();
        services.AddHtmlParser();

        // Act
        services.AddWebFetcher();
        var provider = services.BuildServiceProvider();

        // Assert
        var options = provider.GetService<WebFetcherOptions>();
        Assert.NotNull(options);
        Assert.Equal(30_000, options.RequestTimeoutMs);
        Assert.Equal(10 * 1024 * 1024, options.MaxContentSizeBytes);
    }

    [Fact]
    public void AddWebFetcher_WithCustomOptions_UsesCustomConfiguration()
    {
        // Arrange
        var services = CreateServiceCollection();
        services.AddHtmlParser();

        // Act
        services.AddWebFetcher(opts =>
        {
            opts.RequestTimeoutMs = 60_000;
            opts.MaxContentSizeBytes = 50 * 1024 * 1024;
        });
        var provider = services.BuildServiceProvider();

        // Assert
        var options = provider.GetService<WebFetcherOptions>();
        Assert.NotNull(options);
        Assert.Equal(60_000, options.RequestTimeoutMs);
        Assert.Equal(50 * 1024 * 1024, options.MaxContentSizeBytes);
    }

    [Fact]
    public void AddWebFetcher_WithOptionsFactory_CreatesOptionsPerCall()
    {
        // Arrange
        var services = CreateServiceCollection();
        services.AddHtmlParser();
        var callCount = 0;

        // Act
        services.AddWebFetcher(_ =>
        {
            callCount++;
            return new WebFetcherOptions { RequestTimeoutMs = 45_000 };
        });
        var provider = services.BuildServiceProvider();

        // Assert
        var fetcher1 = provider.GetRequiredService<IWebFetcher>();
        var fetcher2 = provider.GetRequiredService<IWebFetcher>();
        Assert.NotNull(fetcher1);
        Assert.NotNull(fetcher2);
        Assert.True(callCount > 0);
    }

    [Fact]
    public void AddWebFetcher_ReturnsServiceCollection()
    {
        // Arrange
        var services = CreateServiceCollection();
        services.AddHtmlParser();

        // Act
        var result = services.AddWebFetcher();

        // Assert - verify it returns IServiceCollection for chaining
        Assert.Same(services, result);
    }

    [Fact]
    public void AddWebFetcher_Multiple_ReturnsDifferentInstancesInDifferentScopes()
    {
        // Arrange
        var services = CreateServiceCollection();
        services.AddHtmlParser();

        // Act
        services.AddWebFetcher();
        var provider = services.BuildServiceProvider();

        // Get fetchers in different scopes
        IWebFetcher? fetcher1;
        IWebFetcher? fetcher2;
        using (var scope1 = provider.CreateScope())
        {
            fetcher1 = scope1.ServiceProvider.GetRequiredService<IWebFetcher>();
        }

        using (var scope2 = provider.CreateScope())
        {
            fetcher2 = scope2.ServiceProvider.GetRequiredService<IWebFetcher>();
        }

        // Assert - scoped services should return different instances across different scopes
        Assert.NotSame(fetcher1, fetcher2);
    }

    [Fact]
    public void AddWebFetcher_NullServices_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => ((IServiceCollection)null!).AddWebFetcher());
    }

    [Fact]
    public void AddWebFetcher_WithNullFactory_ThrowsArgumentNullException()
    {
        // Arrange
        var services = CreateServiceCollection();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => services.AddWebFetcher((Func<IServiceProvider, WebFetcherOptions>)null!));
    }

    [Fact]
    public void AddHtmlParserAndAddWebFetcher_CanBeUsedTogether()
    {
        // Arrange
        var services = CreateServiceCollection();

        // Act
        services.AddHtmlParser();
        services.AddWebFetcher();
        var provider = services.BuildServiceProvider();

        // Assert
        var parser = provider.GetRequiredService<IHtmlParser>();
        var fetcher = provider.GetRequiredService<IWebFetcher>();
        var parserOptions = provider.GetRequiredService<HtmlParsingOptions>();
        var fetcherOptions = provider.GetRequiredService<WebFetcherOptions>();

        Assert.NotNull(parser);
        Assert.NotNull(fetcher);
        Assert.NotNull(parserOptions);
        Assert.NotNull(fetcherOptions);
    }

    [Fact]
    public void AddWebCrawler_RegistersCrawlerService()
    {
        // Arrange
        var services = CreateServiceCollection();
        services.AddHtmlParser();
        services.AddWebFetcher();

        // Act
        services.AddWebCrawler();
        var provider = services.BuildServiceProvider();

        // Assert
        var crawler = provider.GetService<IWebCrawler>();
        Assert.NotNull(crawler);
        Assert.IsType<WebCrawler>(crawler);
    }

    [Fact]
    public void AddWebCrawler_RegistersWithDefaultOptions()
    {
        // Arrange
        var services = CreateServiceCollection();
        services.AddHtmlParser();
        services.AddWebFetcher();

        // Act
        services.AddWebCrawler();
        var provider = services.BuildServiceProvider();

        // Assert
        var options = provider.GetService<WebCrawlerOptions>();
        Assert.NotNull(options);
        Assert.Equal(1, options.DefaultMaxDepth);
        Assert.Equal(5, options.MaxAllowedDepth);
        Assert.True(options.RestrictToSameDomain);
    }

    [Fact]
    public void AddWebCrawler_WithCustomOptions_UsesCustomConfiguration()
    {
        // Arrange
        var services = CreateServiceCollection();
        services.AddHtmlParser();
        services.AddWebFetcher();

        // Act
        services.AddWebCrawler(opts =>
        {
            opts.DefaultMaxDepth = 2;
            opts.MaxAllowedDepth = 10;
            opts.RestrictToSameDomain = false;
        });
        var provider = services.BuildServiceProvider();

        // Assert
        var options = provider.GetService<WebCrawlerOptions>();
        Assert.NotNull(options);
        Assert.Equal(2, options.DefaultMaxDepth);
        Assert.Equal(10, options.MaxAllowedDepth);
        Assert.False(options.RestrictToSameDomain);
    }

    [Fact]
    public void AddWebCrawler_WithNullFactory_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => services.AddWebCrawler((Func<IServiceProvider, WebCrawlerOptions>)null!));
    }

    [Fact]
    public void AddWebCrawler_NullServices_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => ((IServiceCollection)null!).AddWebCrawler());
    }

    [Fact]
    public void AddHtmlToMarkdownConverter_RegistersConverterService()
    {
        // Arrange
        var services = CreateServiceCollection();

        // Act
        services.AddHtmlToMarkdownConverter();
        var provider = services.BuildServiceProvider();

        // Assert
        var converter = provider.GetService<IHtmlToMarkdownConverter>();
        Assert.NotNull(converter);
        Assert.IsType<HtmlToMarkdownConverter>(converter);
    }

    [Fact]
    public void AddHtmlToMarkdownConverter_RegistersWithDefaultOptions()
    {
        // Arrange
        var services = CreateServiceCollection();

        // Act
        services.AddHtmlToMarkdownConverter();
        var provider = services.BuildServiceProvider();

        // Assert
        var options = provider.GetService<HtmlToMarkdownOptions>();
        Assert.NotNull(options);
        Assert.Equal(5 * 1024 * 1024, options.MaxContentLength);
        Assert.True(options.KeepLinks);
        Assert.False(options.KeepImages);
        Assert.True(options.KeepCodeBlocks);
    }

    [Fact]
    public void AddHtmlToMarkdownConverter_WithCustomOptions_UsesCustomConfiguration()
    {
        // Arrange
        var services = CreateServiceCollection();

        // Act
        services.AddHtmlToMarkdownConverter(opts =>
        {
            opts.MaxContentLength = 1_000_000;
            opts.KeepImages = true;
            opts.RemoveEmptyLines = false;
        });
        var provider = services.BuildServiceProvider();

        // Assert
        var options = provider.GetService<HtmlToMarkdownOptions>();
        Assert.NotNull(options);
        Assert.Equal(1_000_000, options.MaxContentLength);
        Assert.True(options.KeepImages);
        Assert.False(options.RemoveEmptyLines);
    }

    [Fact]
    public void AddHtmlToMarkdownConverter_WithOptionsFactory_UsesFactory()
    {
        // Arrange
        var services = CreateServiceCollection();
        var factoryCalled = false;

        // Act
        services.AddHtmlToMarkdownConverter(_ =>
        {
            factoryCalled = true;
            return new HtmlToMarkdownOptions { MaxContentLength = 2_000_000 };
        });
        var provider = services.BuildServiceProvider();

        // Assert
        var converter = provider.GetRequiredService<IHtmlToMarkdownConverter>();
        Assert.NotNull(converter);
        Assert.True(factoryCalled);
    }

    [Fact]
    public void AddHtmlToMarkdownConverter_ReturnsServiceCollection()
    {
        // Arrange & Act
        var services = new ServiceCollection();
        services.AddLogging();
        var result = services.AddHtmlToMarkdownConverter();

        // Assert - verify it returns IServiceCollection for chaining
        Assert.Same(services, result);
    }

    [Fact]
    public void AddHtmlToMarkdownConverter_Multiple_ReturnsDifferentInstancesInDifferentScopes()
    {
        // Arrange
        var services = CreateServiceCollection();

        // Act
        services.AddHtmlToMarkdownConverter();
        var provider = services.BuildServiceProvider();

        // Get converters in different scopes
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

        // Assert - scoped services should return different instances across different scopes
        Assert.NotSame(converter1, converter2);
    }

    [Fact]
    public void AddHtmlToMarkdownConverter_NullServices_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => ((IServiceCollection)null!).AddHtmlToMarkdownConverter());
    }

    [Fact]
    public void AddHtmlToMarkdownConverter_WithNullFactory_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => services.AddHtmlToMarkdownConverter((Func<IServiceProvider, HtmlToMarkdownOptions>)null!));
    }

    [Fact]
    public void AllWebFetchServices_CanBeUsedTogether()
    {
        // Arrange
        var services = CreateServiceCollection();

        // Act
        services.AddHtmlParser();
        services.AddWebFetcher();
        services.AddHtmlToMarkdownConverter();
        var provider = services.BuildServiceProvider();

        // Assert
        var parser = provider.GetRequiredService<IHtmlParser>();
        var fetcher = provider.GetRequiredService<IWebFetcher>();
        var converter = provider.GetRequiredService<IHtmlToMarkdownConverter>();

        Assert.NotNull(parser);
        Assert.NotNull(fetcher);
        Assert.NotNull(converter);
    }
}
