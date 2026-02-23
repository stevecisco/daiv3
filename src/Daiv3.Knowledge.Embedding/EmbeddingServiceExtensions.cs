using Microsoft.Extensions.DependencyInjection;

namespace Daiv3.Knowledge.Embedding;

/// <summary>
/// Extension methods for registering embedding services.
/// </summary>
public static class EmbeddingServiceExtensions
{
    public static IServiceCollection AddEmbeddingServices(
        this IServiceCollection services,
        Action<EmbeddingOnnxOptions>? configureOptions = null)
    {
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }
        else
        {
            services.Configure<EmbeddingOnnxOptions>(_ => { });
        }

        services.AddSingleton<IOnnxSessionOptionsFactory, OnnxSessionOptionsFactory>();
        services.AddSingleton<IOnnxInferenceSessionProvider, OnnxInferenceSessionProvider>();
        services.AddSingleton<IVectorSimilarityService, CpuVectorSimilarityService>();

        return services;
    }
}
