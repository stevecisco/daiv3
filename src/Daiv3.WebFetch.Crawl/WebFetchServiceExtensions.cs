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

    /// <summary>
    /// Adds web fetcher services to the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">An optional action to configure web fetcher options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// This method requires HtmlParser to be registered via AddHtmlParser().
    /// An HttpClient named "WebFetcher" is registered and configured with default headers.
    /// </remarks>
    public static IServiceCollection AddWebFetcher(
        this IServiceCollection services,
        Action<WebFetcherOptions>? configureOptions = null)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        // Create default options
        var options = new WebFetcherOptions();

        // Apply custom configuration if provided
        configureOptions?.Invoke(options);

        // Register options as singleton
        services.AddSingleton(options);

        // Register HttpClient with custom configuration
        services.AddHttpClient<IWebFetcher, WebFetcher>("WebFetcher")
            .ConfigureHttpClient((sp, client) =>
            {
                client.Timeout = TimeSpan.FromMilliseconds(options.RequestTimeoutMs);
                client.DefaultRequestHeaders.Add("User-Agent", options.UserAgent);
            });

        return services;
    }

    /// <summary>
    /// Adds web fetcher services with configuration from a delegate.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="optionsFactory">A delegate that creates web fetcher options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddWebFetcher(
        this IServiceCollection services,
        Func<IServiceProvider, WebFetcherOptions> optionsFactory)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        if (optionsFactory == null)
            throw new ArgumentNullException(nameof(optionsFactory));

        // Register options factory
        services.AddSingleton(optionsFactory);

        // Register HttpClient
        services.AddHttpClient<IWebFetcher, WebFetcher>("WebFetcher")
            .ConfigureHttpClient((sp, client) =>
            {
                var opts = optionsFactory(sp);
                client.Timeout = TimeSpan.FromMilliseconds(opts.RequestTimeoutMs);
                client.DefaultRequestHeaders.Add("User-Agent", opts.UserAgent);
            });

        return services;
    }

    /// <summary>
    /// Adds web crawler services to the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">An optional action to configure crawler options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// This method requires both HtmlParser and WebFetcher to be registered.
    /// </remarks>
    public static IServiceCollection AddWebCrawler(
        this IServiceCollection services,
        Action<WebCrawlerOptions>? configureOptions = null)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        var options = new WebCrawlerOptions();
        configureOptions?.Invoke(options);

        services.AddSingleton(options);
        services.AddScoped<IWebCrawler, WebCrawler>();

        return services;
    }

    /// <summary>
    /// Adds web crawler services with configuration from a delegate.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="optionsFactory">A delegate that creates crawler options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddWebCrawler(
        this IServiceCollection services,
        Func<IServiceProvider, WebCrawlerOptions> optionsFactory)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        if (optionsFactory == null)
            throw new ArgumentNullException(nameof(optionsFactory));

        services.AddSingleton(optionsFactory);
        services.AddScoped<IWebCrawler>(sp =>
            new WebCrawler(
                sp.GetRequiredService<IWebFetcher>(),
                sp.GetRequiredService<IHtmlParser>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<WebCrawler>>(),
                optionsFactory(sp)));

        return services;
    }

    /// <summary>
    /// Adds HTML to Markdown converter services to the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">An optional action to configure HTML to Markdown conversion options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHtmlToMarkdownConverter(
        this IServiceCollection services,
        Action<HtmlToMarkdownOptions>? configureOptions = null)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        // Create default options
        var options = new HtmlToMarkdownOptions();

        // Apply custom configuration if provided
        configureOptions?.Invoke(options);

        // Register options as singleton
        services.AddSingleton(options);

        // Register the HTML to Markdown converter
        services.AddScoped<IHtmlToMarkdownConverter, HtmlToMarkdownConverter>();

        return services;
    }

    /// <summary>
    /// Adds HTML to Markdown converter services with configuration from a delegate.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="optionsFactory">A delegate that creates HTML to Markdown conversion options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHtmlToMarkdownConverter(
        this IServiceCollection services,
        Func<IServiceProvider, HtmlToMarkdownOptions> optionsFactory)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        if (optionsFactory == null)
            throw new ArgumentNullException(nameof(optionsFactory));

        // Register options factory
        services.AddSingleton(optionsFactory);

        // Register the HTML to Markdown converter
        services.AddScoped<IHtmlToMarkdownConverter>(sp =>
            new HtmlToMarkdownConverter(
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<HtmlToMarkdownConverter>>(),
                optionsFactory(sp)));

        return services;
    }

    /// <summary>
    /// Adds Markdown content store services to the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">An optional action to configure content store options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMarkdownContentStore(
        this IServiceCollection services,
        Action<MarkdownContentStoreOptions>? configureOptions = null)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        // Create default options
        var options = new MarkdownContentStoreOptions();

        // Apply custom configuration if provided
        configureOptions?.Invoke(options);

        // Register options as singleton
        services.AddSingleton(options);

        // Register the content store
        services.AddScoped<IMarkdownContentStore, MarkdownContentStore>();

        return services;
    }

    /// <summary>
    /// Adds Markdown content store services with configuration from a delegate.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="optionsFactory">A delegate that creates content store options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMarkdownContentStore(
        this IServiceCollection services,
        Func<IServiceProvider, MarkdownContentStoreOptions> optionsFactory)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        if (optionsFactory == null)
            throw new ArgumentNullException(nameof(optionsFactory));

        // Register options factory
        services.AddSingleton(optionsFactory);

        // Register the content store
        services.AddScoped<IMarkdownContentStore>(sp =>
            new MarkdownContentStore(
                Microsoft.Extensions.Options.Options.Create(optionsFactory(sp)),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<MarkdownContentStore>>()));

        return services;
    }

    /// <summary>
    /// Adds web fetch metadata storage services to the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// This method requires IWebFetchRepository to be registered in the persistence layer.
    /// The metadata service provides utilities for storing source URLs, fetch dates, and content hashes.
    /// Implements WFC-REQ-007: The system SHALL store source URL and fetch date as metadata.
    /// </remarks>
    public static IServiceCollection AddWebFetchMetadataService(this IServiceCollection services)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        // Register the web fetch metadata service
        services.AddScoped<IWebFetchMetadataService, WebFetchMetadataService>();

        return services;
    }}