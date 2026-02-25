using Daiv3.Infrastructure.Shared.Hardware;
using Microsoft.Extensions.DependencyInjection;

namespace Daiv3.Knowledge.Embedding;

/// <summary>
/// Extension methods for registering embedding services.
/// </summary>
public static class EmbeddingServiceExtensions
{
    public static IServiceCollection AddEmbeddingServices(
        this IServiceCollection services,
        Action<EmbeddingOnnxOptions>? configureOptions = null,
        Action<EmbeddingTokenizationOptions>? configureTokenizationOptions = null)
    {
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }
        else
        {
            services.Configure<EmbeddingOnnxOptions>(_ => { });
        }

        if (configureTokenizationOptions != null)
        {
            services.Configure(configureTokenizationOptions);
        }
        else
        {
            services.Configure<EmbeddingTokenizationOptions>(_ => { });
        }

        services.AddHardwareDetection();
        services.AddSingleton<IOnnxSessionOptionsFactory, OnnxSessionOptionsFactory>();
        services.AddSingleton<IOnnxInferenceSessionProvider, OnnxInferenceSessionProvider>();
        services.AddSingleton<IEmbeddingTokenizerProvider, EmbeddingTokenizerProvider>();
        services.AddSingleton<IOnnxEmbeddingModelRunner, OnnxEmbeddingModelRunner>();
        services.AddSingleton<IEmbeddingGenerator, OnnxEmbeddingGenerator>();
        services.AddSingleton<CpuVectorSimilarityService>();
        services.AddSingleton<IVectorSimilarityService, HardwareAwareVectorSimilarityService>();
        
        // Register model download service with HttpClient
        services.AddHttpClient<IEmbeddingModelDownloadService, EmbeddingModelDownloadService>();

        return services;
    }
}
