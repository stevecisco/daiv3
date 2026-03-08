using Daiv3.FoundryLocal.Management;
using Daiv3.ModelExecution.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Daiv3.ModelExecution;

/// <summary>
/// Extension methods for registering Model Execution services.
/// </summary>
public static class ModelExecutionServiceExtensions
{
    /// <summary>
    /// Registers Model Execution Layer services with dependency injection.
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddModelExecutionServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register configuration options
        services.AddOptions<ModelQueueOptions>()
            .Bind(configuration.GetSection("ModelQueue"));

        services.AddOptions<OnlineProviderOptions>()
            .Bind(configuration.GetSection("OnlineProviders"));

        services.AddOptions<ModelLifecycleOptions>()
            .Bind(configuration.GetSection("ModelLifecycle"));

        services.AddOptions<TaskTypeClassifierOptions>()
            .Bind(configuration.GetSection(TaskTypeClassifierOptions.SectionName));

        services.AddOptions<ModelSelectorOptions>()
            .Bind(configuration.GetSection(ModelSelectorOptions.SectionName));

        services.AddOptions<PriorityAssignerOptions>()
            .Bind(configuration.GetSection(PriorityAssignerOptions.SectionName));

        // Register MQ-REQ-011 and MQ-REQ-012 configurations
        services.AddOptions<LocalModelIntentClassificationOptions>()
            .Bind(configuration.GetSection("LocalModelIntentClassification"));

        services.AddOptions<TaskToModelMappingConfiguration>()
            .Bind(configuration.GetSection("TaskToModelMapping"));

        // Register ES-REQ-001 local-first routing configuration
        services.AddOptions<LocalFirstRouteOptions>()
            .Bind(configuration.GetSection("LocalFirstRouting"));

        // Register Foundry Local management service
        services.AddSingleton<FoundryLocalManagementService>(serviceProvider =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<FoundryLocalManagementService>>();
            return new FoundryLocalManagementService(logger);
        });
        services.AddSingleton<IFoundryLocalManagementService>(serviceProvider =>
            serviceProvider.GetRequiredService<FoundryLocalManagementService>());

        // Register core services
        services.AddSingleton<IModelLifecycleManager, ModelLifecycleManager>();
        services.AddSingleton<IModelQueue, ModelQueue>();
        services.AddSingleton<IFoundryBridge, FoundryBridge>();
        services.AddSingleton<IOnlineProviderRouter, OnlineProviderRouter>();

        // Register MQ-REQ-013: Network connectivity service for offline queueing
        services.AddHttpClient("ConnectivityCheck");
        services.AddSingleton<INetworkConnectivityService, NetworkConnectivityService>();

        // Note: IModelQueueRepository is registered by AddPersistenceServices()
        // in Daiv3.Persistence.PersistenceServiceExtensions

        // Register intent resolution services (MQ-REQ-008, MQ-REQ-009, MQ-REQ-010)
        services.AddSingleton<ITaskTypeClassifier, TaskTypeClassifier>();
        services.AddSingleton<IModelSelector, ModelSelector>();
        services.AddSingleton<IPriorityAssigner, PriorityAssigner>();

        // Register MQ-REQ-011 local model intent classifier
        services.AddSingleton<ILocalModelIntentClassifier, LocalModelIntentClassifier>();

        return services;
    }

    /// <summary>
    /// Registers Model Execution Layer services with custom options.
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configureQueue">Model queue options configuration</param>
    /// <param name="configureOnline">Online provider options configuration</param>
    /// <param name="configureLifecycle">Model lifecycle options configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddModelExecutionServices(
        this IServiceCollection services,
        Action<ModelQueueOptions>? configureQueue = null,
        Action<OnlineProviderOptions>? configureOnline = null,
        Action<ModelLifecycleOptions>? configureLifecycle = null)
    {
        // Register configuration options with defaults
        if (configureQueue != null)
        {
            services.Configure(configureQueue);
        }
        else
        {
            services.Configure<ModelQueueOptions>(_ => { });
        }

        if (configureOnline != null)
        {
            services.Configure(configureOnline);
        }
        else
        {
            services.Configure<OnlineProviderOptions>(_ => { });
        }

        if (configureLifecycle != null)
        {
            services.Configure(configureLifecycle);
        }
        else
        {
            services.Configure<ModelLifecycleOptions>(_ => { });
        }

        // Configure intent resolution services with defaults
        services.Configure<TaskTypeClassifierOptions>(_ => { });
        services.Configure<ModelSelectorOptions>(_ => { });
        services.Configure<PriorityAssignerOptions>(_ => { });

        // Configure MQ-REQ-011 and MQ-REQ-012 options with defaults
        services.Configure<LocalModelIntentClassificationOptions>(_ => { });
        services.Configure<TaskToModelMappingConfiguration>(_ => { });

        // Configure ES-REQ-001 local-first routing with defaults
        services.Configure<LocalFirstRouteOptions>(_ => { });

        // Register Foundry Local management service
        services.AddSingleton<FoundryLocalManagementService>(serviceProvider =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<FoundryLocalManagementService>>();
            return new FoundryLocalManagementService(logger);
        });
        services.AddSingleton<IFoundryLocalManagementService>(serviceProvider =>
            serviceProvider.GetRequiredService<FoundryLocalManagementService>());

        // Register core services
        services.AddSingleton<IModelLifecycleManager, ModelLifecycleManager>();
        services.AddSingleton<IModelQueue, ModelQueue>();
        services.AddSingleton<IFoundryBridge, FoundryBridge>();
        services.AddSingleton<IOnlineProviderRouter, OnlineProviderRouter>();

        // Register MQ-REQ-013: Network connectivity service for offline queueing
        services.AddHttpClient("ConnectivityCheck");
        services.AddSingleton<INetworkConnectivityService, NetworkConnectivityService>();

        // Note: IModelQueueRepository is registered by AddPersistenceServices()
        // in Daiv3.Persistence.PersistenceServiceExtensions

        // Register intent resolution services (MQ-REQ-008, MQ-REQ-009, MQ-REQ-010)
        services.AddSingleton<ITaskTypeClassifier, TaskTypeClassifier>();
        services.AddSingleton<IModelSelector, ModelSelector>();
        services.AddSingleton<IPriorityAssigner, PriorityAssigner>();

        // Register MQ-REQ-011 local model intent classifier
        services.AddSingleton<ILocalModelIntentClassifier, LocalModelIntentClassifier>();

        return services;
    }
}
