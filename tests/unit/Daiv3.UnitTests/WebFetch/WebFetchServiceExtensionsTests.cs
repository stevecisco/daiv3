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
}
