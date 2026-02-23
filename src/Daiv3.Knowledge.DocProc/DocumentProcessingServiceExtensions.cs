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
}
