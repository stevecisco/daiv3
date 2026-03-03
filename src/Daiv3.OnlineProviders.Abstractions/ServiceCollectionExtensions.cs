using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Daiv3.OnlineProviders.Abstractions;

/// <summary>
/// Extension methods for registering online providers in a DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers a specific online provider implementation using Microsoft.Extensions.AI abstractions.
    /// </summary>
    /// <typeparam name="TImplementation">The provider implementation type (must implement IOnlineProvider).</typeparam>
    /// <param name="services">Service collection.</param>
    /// <returns>Service collection for chaining.</returns>
    /// <remarks>
    /// Implements KLC-REQ-006: Uses Microsoft.Extensions.AI abstractions for online providers.
    /// Provider implementations should use IChatClient from Microsoft.Extensions.AI to interact with API SDKs.
    /// </remarks>
    public static IServiceCollection AddOnlineProvider<TImplementation>(
        this IServiceCollection services)
        where TImplementation : class, IOnlineProvider
    {
        services.AddSingleton<IOnlineProvider, TImplementation>();
        return services;
    }

    /// <summary>
    /// Registers multiple online provider implementations.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="providerTypes">Types of provider implementations to register (must implement IOnlineProvider).</param>
    /// <returns>Service collection for chaining.</returns>
    public static IServiceCollection AddOnlineProviders(
        this IServiceCollection services,
        params Type[] providerTypes)
    {
        foreach (var type in providerTypes)
        {
            if (!typeof(IOnlineProvider).IsAssignableFrom(type))
            {
                throw new ArgumentException(
                    $"Type '{type.Name}' must implement IOnlineProvider interface.",
                    nameof(providerTypes));
            }

            services.AddSingleton(typeof(IOnlineProvider), type);
        }

        return services;
    }

    /// <summary>
    /// Adds a provider factory for lazy initialization of providers.
    /// </summary>
    /// <remarks>
    /// Useful when provider instances are expensive to create and may not all be needed.
    /// </remarks>
    /// <param name="services">Service collection.</param>
    /// <returns>Service collection for chaining.</returns>
    public static IServiceCollection AddOnlineProviderFactory(
        this IServiceCollection services)
    {
        services.AddSingleton<IOnlineProviderFactory>(sp =>
            new DefaultOnlineProviderFactory(sp.GetRequiredService<ILogger<DefaultOnlineProviderFactory>>()));

        return services;
    }
}

/// <summary>
/// Factory for retrieving online provider instances by name.
/// </summary>
public interface IOnlineProviderFactory
{
    /// <summary>
    /// Gets a provider by its name.
    /// </summary>
    /// <param name="providerName">Name of the provider (e.g., "openai", "azure-openai").</param>
    /// <returns>Provider instance, or null if not found.</returns>
    IOnlineProvider? GetProvider(string providerName);

    /// <summary>
    /// Gets all registered providers.
    /// </summary>
    /// <returns>List of all providers.</returns>
    IReadOnlyList<IOnlineProvider> GetAllProviders();
}

/// <summary>
/// Default implementation of IOnlineProviderFactory.
/// </summary>
internal class DefaultOnlineProviderFactory : IOnlineProviderFactory
{
    private readonly ILogger<DefaultOnlineProviderFactory> _logger;
    private readonly Dictionary<string, IOnlineProvider> _providers = new(StringComparer.OrdinalIgnoreCase);

    public DefaultOnlineProviderFactory(ILogger<DefaultOnlineProviderFactory> logger)
    {
        _logger = logger;
    }

    public IOnlineProvider? GetProvider(string providerName)
    {
        if (_providers.TryGetValue(providerName, out var provider))
        {
            return provider;
        }

        _logger.LogWarning("Provider '{ProviderName}' not found in factory", providerName);
        return null;
    }

    public IReadOnlyList<IOnlineProvider> GetAllProviders()
    {
        return _providers.Values.ToList();
    }

    /// <summary>
    /// Registers a provider instance with the factory.
    /// </summary>
    internal void RegisterProvider(IOnlineProvider provider)
    {
        _providers[provider.ProviderName] = provider;
        _logger.LogInformation("Registered provider '{ProviderName}'", provider.ProviderName);
    }
}
