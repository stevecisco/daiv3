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
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        Action<PersistenceOptions>? configureOptions = null)
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

        // Register database context
        services.AddSingleton<IDatabaseContext, DatabaseContext>();

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
