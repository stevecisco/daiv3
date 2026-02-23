using Microsoft.Extensions.DependencyInjection;

namespace Daiv3.Infrastructure.Shared.Hardware;

/// <summary>
/// Dependency injection extension for hardware detection services.
/// 
/// Registers the hardware detection provider and makes it available throughout the application
/// for hardware-aware configuration and optimization.
/// </summary>
public static class HardwareDetectionServiceExtensions
{
    /// <summary>
    /// Registers the hardware detection provider and related services.
    /// </summary>
    /// <param name="services">The service collection to register with.</param>
    /// <returns>The service collection for fluent chaining.</returns>
    public static IServiceCollection AddHardwareDetection(this IServiceCollection services)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        services.AddSingleton<IHardwareDetectionProvider, HardwareDetectionProvider>();

        return services;
    }
}
