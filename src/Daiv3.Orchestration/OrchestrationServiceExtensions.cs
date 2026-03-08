using Daiv3.Knowledge.Embedding;
using Daiv3.Mcp.Integration;
using Daiv3.Orchestration.Interfaces;
using Daiv3.Orchestration.Messaging;
using Daiv3.Orchestration.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Daiv3.Orchestration;

/// <summary>
/// Extension methods for registering orchestration services with dependency injection.
/// </summary>
public static class OrchestrationServiceExtensions
{
    /// <summary>
    /// Registers orchestration layer services with the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOrchestrationServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Register embedding services (required for learning and knowledge management)
        services.AddEmbeddingServices();

        // Register MCP integration services (required by ToolRoutingService)
        services.AddMcpIntegration();

        // Register messaging services (required by AgentManager)
        services.AddMessageBroker();

        // Register options
        services.AddOptions<OrchestrationOptions>();
        services.AddOptions<AgentExecutionObservabilityOptions>();
        services.AddOptions<SkillSandboxConfiguration>();
        services.AddOptions<InternetKnowledgeDraftOptions>();

        // Register HttpClient for REST API tool invocation
        services.AddHttpClient("RestApiTool", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30); // Default timeout
            client.DefaultRequestHeaders.Add("User-Agent", "Daiv3-RestApiTool/1.0");
        });

        // Register metrics collection service
        services.TryAddSingleton<AgentExecutionMetricsCollector>();

        // Register skill sandboxing services
        services.TryAddScoped<SkillPermissionValidator>();

        // Register core orchestration services
        services.TryAddScoped<ITaskOrchestrator, TaskOrchestrator>();
        services.TryAddScoped<IIntentResolver, IntentResolver>();
        services.TryAddScoped<IAgentManager, AgentManager>();
        services.TryAddScoped<IDependencyResolver, DependencyResolver>();
        services.TryAddScoped<ITaskStatusTransitionValidator, TaskStatusTransitionValidator>();
        services.TryAddScoped<ISuccessCriteriaEvaluator, SuccessCriteriaEvaluator>();
        services.TryAddSingleton<IKnowledgePromotionService, KnowledgePromotionService>();

        // Register skill registry as singleton (shared across all scopes)
        services.TryAddSingleton<ISkillRegistry, SkillRegistry>();

        // Register skill executor for direct and agent-integrated skill execution
        services.TryAddScoped<ISkillExecutor, SkillExecutor>();

        // Register configuration loaders for runtime extensibility modules
        services.TryAddScoped<Configuration.SkillConfigFileLoader>();
        services.TryAddScoped<Configuration.AgentConfigFileLoader>();

        // Register startup module auto-load service once
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, ModuleAutoLoadHostedService>());

        // Register skill file watcher for progressive skill reload without rebuild.
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, SkillFileWatcherHostedService>());

        // Register tool registry as singleton (shared across all scopes)
        services.TryAddSingleton<IToolRegistry, ToolRegistry>();

        // Register tool invoker as scoped (per-request tool routing)
        services.TryAddScoped<IToolInvoker, ToolRoutingService>();

        // Register learning service for learning memory management (LM-REQ-001)
        services.TryAddScoped<ILearningService, LearningService>();

        // Register learning retrieval service for semantic learning injection (LM-REQ-005)
        // with metrics collection for transparency (LM-NFR-002)
        services.TryAddScoped<ILearningRetrievalService>(serviceProvider =>
        {
            var logger = serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<LearningRetrievalService>>();
            var storageService = serviceProvider.GetRequiredService<Daiv3.Persistence.ILearningStorageService>();
            var embeddingGenerator = serviceProvider.GetRequiredService<Daiv3.Knowledge.Embedding.IEmbeddingGenerator>();
            var vectorSimilarity = serviceProvider.GetRequiredService<Daiv3.Knowledge.Embedding.IVectorSimilarityService>();
            var metricsCollector = serviceProvider.GetService<Daiv3.Persistence.ILearningObserver>();
            return new LearningRetrievalService(logger, storageService, embeddingGenerator, vectorSimilarity, metricsCollector);
        });

        // Register knowledge summary service for generating summaries of knowledge promotions (KBP-REQ-004)
        services.TryAddScoped<IKnowledgeSummaryService, KnowledgeSummaryService>();

        // Register Internet promotion draft artifact service (KBP-REQ-005)
        services.TryAddScoped<IKnowledgeInternetDraftService, KnowledgeInternetDraftService>();

        // Register agent promotion proposal service for agent-proposed promotions requiring confirmation (KBP-REQ-003)
        services.TryAddScoped<IAgentPromotionProposalService>(serviceProvider =>
        {
            var logger = serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<AgentPromotionProposalService>>();
            var proposalRepository = serviceProvider.GetRequiredService<Daiv3.Persistence.Repositories.AgentPromotionProposalRepository>();
            var learningRepository = serviceProvider.GetRequiredService<Daiv3.Persistence.Repositories.LearningRepository>();
            var promotionRepository = serviceProvider.GetRequiredService<Daiv3.Persistence.Repositories.PromotionRepository>();
            var learningService = serviceProvider.GetRequiredService<Daiv3.Persistence.LearningStorageService>();
            return new AgentPromotionProposalService(logger, proposalRepository, learningRepository, promotionRepository, learningService);
        });

        // Register web content ingestion service for integrating fetched web content into knowledge pipeline (WFC-REQ-006)
        services.TryAddScoped<IWebContentIngestionService>(serviceProvider =>
        {
            var logger = serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<WebContentIngestionService>>();
            var contentStore = serviceProvider.GetRequiredService<Daiv3.WebFetch.Crawl.IMarkdownContentStore>();
            var documentProcessor = serviceProvider.GetRequiredService<Daiv3.Knowledge.IKnowledgeDocumentProcessor>();
            var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<WebContentIngestionOptions>>();
            return new WebContentIngestionService(logger, contentStore, documentProcessor, options);
        });

        // Register web content ingestion options
        services.AddOptions<WebContentIngestionOptions>();

        // Register transparency view service for system activity visibility (ES-REQ-004)
        services.TryAddScoped<ITransparencyViewService>(serviceProvider =>
        {
            var logger = serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<TransparencyViewService>>();
            var modelQueue = serviceProvider.GetService<Daiv3.ModelExecution.Interfaces.IModelQueue>();
            var indexingStatusService = serviceProvider.GetService<Daiv3.Knowledge.IIndexingStatusService>();
            var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
            var databaseContext = serviceProvider.GetService<Daiv3.Persistence.IDatabaseContext>();
            return new TransparencyViewService(logger, modelQueue, indexingStatusService, scopeFactory, databaseContext);
        });

        return services;
    }

    /// <summary>
    /// Registers orchestration layer services with custom options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Action to configure orchestration options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOrchestrationServices(
        this IServiceCollection services,
        Action<OrchestrationOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        services.AddOrchestrationServices();
        services.Configure(configureOptions);

        return services;
    }
}
