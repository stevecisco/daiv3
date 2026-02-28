using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Daiv3.Scheduler;

/// <summary>
/// Extension methods for registering the scheduler service with dependency injection.
/// </summary>
public static class SchedulerServiceExtensions
{
    /// <summary>
    /// Adds the custom scheduler service to the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection to add the scheduler to.</param>
    /// <param name="configureOptions">Optional delegate to configure scheduler options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// This method registers:
    /// - SchedulerHostedService as IHostedService (begins scheduling on application startup)
    /// - SchedulerHostedService as IScheduler (singleton for job scheduling)
    /// - SchedulerOptions with default or custom configuration
    /// 
    /// The scheduler will start automatically when the host starts and gracefully
    /// cancel all pending jobs when the host shuts down.
    /// </remarks>
    public static IServiceCollection AddScheduler(
        this IServiceCollection services,
        Action<SchedulerOptions>? configureOptions = null)
    {
        // Ensure logging services are available for SchedulerHostedService activation
        services.AddLogging();

        // Register options (before registering the hosted service)
        services.Configure<SchedulerOptions>(options =>
        {
            // Apply custom configuration if provided
            configureOptions?.Invoke(options);
        });

        // Register SchedulerHostedService as both IHostedService and IScheduler
        // Using TryAddSingleton to avoid duplicate registration if called multiple times
        services.TryAddSingleton<SchedulerHostedService>();
        services.TryAddSingleton<IHostedService>(provider => provider.GetRequiredService<SchedulerHostedService>());
        services.TryAddSingleton<IScheduler>(provider => provider.GetRequiredService<SchedulerHostedService>());

        return services;
    }
}
