using Daiv3.Knowledge;
using Daiv3.Knowledge.DocProc;
using Daiv3.Knowledge.Embedding;
using Daiv3.Persistence;
using Daiv3.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Daiv3.Knowledge;

/// <summary>
/// Extension methods for registering Knowledge Layer services.
/// </summary>
public static class KnowledgeServiceExtensions
{
    /// <summary>
    /// Registers all Knowledge Layer services with the dependency injection container.
    /// Includes two-tier indexing, vector storage, and document processing.
    /// </summary>
    public static IServiceCollection AddKnowledgeLayer(
        this IServiceCollection services,
        Action<DocumentProcessingOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Register repositories
        services.AddScoped<TopicIndexRepository>();
        services.AddScoped<ChunkIndexRepository>();

        // Register core Knowledge Layer services
        services.AddScoped<IVectorStoreService, VectorStoreService>();
        services.AddScoped<ITwoTierIndexService, TwoTierIndexService>();

        // Register document processor
        var options = new DocumentProcessingOptions();
        configureOptions?.Invoke(options);
        services.AddSingleton(options);
        services.AddScoped<IKnowledgeDocumentProcessor, KnowledgeDocumentProcessor>();

        return services;
    }

    /// <summary>
    /// Initializes the Knowledge Layer at application startup.
    /// Loads Tier 1 embeddings into memory for fast search.
    /// </summary>
    public static async Task InitializeKnowledgeLayerAsync(
        this IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var indexService = serviceProvider.GetRequiredService<ITwoTierIndexService>();
        await indexService.InitializeAsync(cancellationToken).ConfigureAwait(false);
    }
}
