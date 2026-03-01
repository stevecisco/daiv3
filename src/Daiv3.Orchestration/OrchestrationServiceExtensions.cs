using Daiv3.Orchestration.Interfaces;
using Daiv3.Orchestration.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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

        // Register messaging services (required by AgentManager)
        services.AddMessageBroker();

        // Register options
        services.AddOptions<OrchestrationOptions>();

        // Register core orchestration services
        services.TryAddScoped<ITaskOrchestrator, TaskOrchestrator>();
        services.TryAddScoped<IIntentResolver, IntentResolver>();
        services.TryAddScoped<IAgentManager, AgentManager>();
        services.TryAddScoped<IDependencyResolver, DependencyResolver>();
        services.TryAddScoped<ITaskStatusTransitionValidator, TaskStatusTransitionValidator>();
        services.TryAddScoped<ISuccessCriteriaEvaluator, SuccessCriteriaEvaluator>();
        
        // Register skill registry as singleton (shared across all scopes)
        services.TryAddSingleton<ISkillRegistry, SkillRegistry>();

        // Register skill executor for direct and agent-integrated skill execution
        services.TryAddScoped<ISkillExecutor, SkillExecutor>();

        // Register tool registry as singleton (shared across all scopes)
        services.TryAddSingleton<IToolRegistry, ToolRegistry>();

        // Register tool invoker as scoped (per-request tool routing)
        services.TryAddScoped<IToolInvoker, ToolRoutingService>();

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
