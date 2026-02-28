using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Daiv3.Mcp.Integration;

/// <summary>
/// Extension methods for registering MCP integration services.
/// </summary>
public static class McpIntegrationServiceExtensions
{
    /// <summary>
    /// Adds MCP integration services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMcpIntegration(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IMcpToolProvider, McpToolProvider>();

        return services;
    }
}
