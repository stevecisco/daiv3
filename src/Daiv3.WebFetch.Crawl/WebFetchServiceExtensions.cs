using Microsoft.Extensions.DependencyInjection;

namespace Daiv3.WebFetch.Crawl;

/// <summary>
/// Extension methods for adding web fetch services to the dependency injection container.
/// </summary>
public static class WebFetchServiceExtensions
{
    /// <summary>
    /// Adds HTML parsing services to the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">An optional action to configure HTML parsing options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHtmlParser(
        this IServiceCollection services,
        Action<HtmlParsingOptions>? configureOptions = null)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        // Create default options
        var options = new HtmlParsingOptions();

        // Apply custom configuration if provided
        configureOptions?.Invoke(options);

        // Register options as singleton
        services.AddSingleton(options);

        // Register the HTML parser
        services.AddScoped<IHtmlParser, HtmlParser>();

        return services;
    }

    /// <summary>
    /// Adds HTML parsing services with configuration from a delegate.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="optionsFactory">A delegate that creates HTML parsing options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHtmlParser(
        this IServiceCollection services,
        Func<IServiceProvider, HtmlParsingOptions> optionsFactory)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        if (optionsFactory == null)
            throw new ArgumentNullException(nameof(optionsFactory));

        // Register options factory
        services.AddSingleton(optionsFactory);

        // Register the HTML parser
        services.AddScoped<IHtmlParser>(sp =>
            new HtmlParser(
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<HtmlParser>>(),
                optionsFactory(sp)));

        return services;
    }
}
