using Microsoft.Extensions.DependencyInjection;

namespace Daiv3.Knowledge.DocProc;

/// <summary>
/// Extension methods for registering document processing services.
/// </summary>
public static class DocumentProcessingServiceExtensions
{
    public static IServiceCollection AddDocumentProcessingServices(
        this IServiceCollection services,
        Action<TokenizationOptions>? configureOptions = null)
    {
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }
        else
        {
            services.Configure<TokenizationOptions>(_ => { });
        }

        services.AddSingleton<ITokenizerProvider, TokenizerProvider>();
        services.AddSingleton<ITextChunker, TextChunker>();

        return services;
    }

    /// <summary>
    /// Adds file system watcher services to the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Optional configuration action</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddFileSystemWatcher(
        this IServiceCollection services,
        Action<FileSystemWatcherOptions>? configureOptions = null)
    {
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }
        else
        {
            services.Configure<FileSystemWatcherOptions>(_ => { });
        }

        services.AddSingleton<IFileSystemWatcher, FileSystemWatcherService>();

        return services;
    }
}
