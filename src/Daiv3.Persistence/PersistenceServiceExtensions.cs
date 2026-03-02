using Daiv3.ModelExecution.Interfaces;
using Daiv3.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Daiv3.Persistence;

/// <summary>
/// Extension methods for registering persistence services with dependency injection.
/// </summary>
public static class PersistenceServiceExtensions
{
    /// <summary>
    /// Adds persistence services to the dependency injection container.
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configureOptions">Optional configuration action for persistence options</param>
    /// <param name="configureLearningObservability">Optional configuration for learning observability</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        Action<PersistenceOptions>? configureOptions = null,
        Action<LearningObservabilityOptions>? configureLearningObservability = null)
    {
        // Register options
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }
        else
        {
            services.Configure<PersistenceOptions>(options => { });
        }

        // Register learning observability options
        if (configureLearningObservability != null)
        {
            services.Configure(configureLearningObservability);
        }
        else
        {
            services.Configure<LearningObservabilityOptions>(options => { });
        }

        // Register database context
        services.AddSingleton<IDatabaseContext, DatabaseContext>();

        // Register repositories
        // Note: Using Scoped for repositories to support transactional operations
        services.AddScoped<DocumentRepository>();
        services.AddScoped<ProjectRepository>();
        services.AddScoped<TaskRepository>();
        services.AddScoped<AgentRepository>();
        
        // LM-REQ-003: Learning repository for learning memory persistence
        services.AddScoped<LearningRepository>();
        
        // KBP-DATA-001/002: Promotion repository for learning promotion tracking
        services.AddScoped<PromotionRepository>();
        
        // KBP-NFR-001: Revert promotion repository for reversible promotions
        services.AddScoped<RevertPromotionRepository>();
        
        // KBP-NFR-001: Promotion metrics repository for transparency/instrumentation
        services.AddScoped<PromotionMetricRepository>();
        
        // KBP-REQ-003: Agent promotion proposal repository for agent-proposed promotions requiring confirmation
        services.AddScoped<AgentPromotionProposalRepository>();
        
        // MQ-REQ-013: Model queue repository for offline queueing
        services.AddScoped<IModelQueueRepository, ModelQueueRepository>();

        // LM-NFR-002: Learning metrics collector for transparency and auditability
        services.AddSingleton<LearningMetricsCollector>(serviceProvider =>
        {
            var repository = serviceProvider.GetRequiredService<LearningRepository>();
            var logger = serviceProvider.GetRequiredService<ILogger<LearningMetricsCollector>>();
            return new LearningMetricsCollector(repository, logger);
        });
        services.AddSingleton<ILearningObserver>(sp => sp.GetRequiredService<LearningMetricsCollector>());

        // Register services
        // LM-REQ-003: Learning storage service for managing learning persistence
        // KBP-DATA-001/002: With promotion tracking
        // KBP-NFR-001: With revert and metrics support
        services.AddScoped<ILearningStorageService>(serviceProvider =>
        {
            var repository = serviceProvider.GetRequiredService<LearningRepository>();
            var logger = serviceProvider.GetRequiredService<ILogger<LearningStorageService>>();
            var metricsCollector = serviceProvider.GetRequiredService<ILearningObserver>();
            var promotionRepository = serviceProvider.GetRequiredService<PromotionRepository>();
            var revertPromotionRepository = serviceProvider.GetRequiredService<RevertPromotionRepository>();
            var promotionMetricRepository = serviceProvider.GetRequiredService<PromotionMetricRepository>();
            return new LearningStorageService(
                repository, logger, metricsCollector, promotionRepository,
                revertPromotionRepository, promotionMetricRepository);
        });
        services.AddScoped<LearningStorageService>(serviceProvider =>
        {
            var repository = serviceProvider.GetRequiredService<LearningRepository>();
            var logger = serviceProvider.GetRequiredService<ILogger<LearningStorageService>>();
            var metricsCollector = serviceProvider.GetRequiredService<ILearningObserver>();
            var promotionRepository = serviceProvider.GetRequiredService<PromotionRepository>();
            var revertPromotionRepository = serviceProvider.GetRequiredService<RevertPromotionRepository>();
            var promotionMetricRepository = serviceProvider.GetRequiredService<PromotionMetricRepository>();
            return new LearningStorageService(
                repository, logger, metricsCollector, promotionRepository,
                revertPromotionRepository, promotionMetricRepository);
        });

        return services;
    }

    /// <summary>
    /// Initializes the database schema.
    /// Should be called during application startup.
    /// </summary>
    /// <param name="serviceProvider">Service provider</param>
    /// <param name="ct">Cancellation token</param>
    public static async Task InitializeDatabaseAsync(
        this IServiceProvider serviceProvider,
        CancellationToken ct = default)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<DatabaseContext>>();
        logger.LogInformation("Initializing persistence layer");

        var dbContext = serviceProvider.GetRequiredService<IDatabaseContext>();
        await dbContext.InitializeAsync(ct).ConfigureAwait(false);

        logger.LogInformation("Persistence layer initialized successfully");
    }
}
