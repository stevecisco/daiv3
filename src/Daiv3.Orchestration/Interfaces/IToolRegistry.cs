using Daiv3.Mcp.Integration;
using Daiv3.Orchestration.Models;

namespace Daiv3.Orchestration.Interfaces;

/// <summary>
/// Provides a registry for tools available to agents, supporting multiple backend types.
/// </summary>
/// <remarks>
/// The tool registry maintains a catalog of all tools available to agents, regardless of
/// their backend implementation (Direct C#, CLI, or MCP). This enables:
/// <list type="bullet">
/// <item><description>Unified tool discovery for agents</description></item>
/// <item><description>Backend-agnostic tool selection</description></item>
/// <item><description>Intelligent routing based on backend type</description></item>
/// <item><description>Dynamic tool registration (especially for MCP servers)</description></item>
/// </list>
/// </remarks>
public interface IToolRegistry
{
    /// <summary>
    /// Registers a tool in the registry.
    /// </summary>
    /// <param name="tool">The tool descriptor to register.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, returning true if registration succeeded.</returns>
    Task<bool> RegisterToolAsync(ToolDescriptor tool, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unregisters a tool from the registry.
    /// </summary>
    /// <param name="toolId">The unique identifier of the tool to unregister.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, returning true if unregistration succeeded.</returns>
    Task<bool> UnregisterToolAsync(string toolId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a tool by its identifier.
    /// </summary>
    /// <param name="toolId">The unique identifier of the tool.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, returning the tool descriptor or null if not found.</returns>
    Task<ToolDescriptor?> GetToolAsync(string toolId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all registered tools.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, returning all registered tools.</returns>
    Task<IReadOnlyList<ToolDescriptor>> GetAllToolsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets tools filtered by backend type.
    /// </summary>
    /// <param name="backend">The backend type to filter by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, returning matching tools.</returns>
    Task<IReadOnlyList<ToolDescriptor>> GetToolsByBackendAsync(
        ToolBackendType backend,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all available tools (where IsAvailable = true).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, returning available tools.</returns>
    Task<IReadOnlyList<ToolDescriptor>> GetAvailableToolsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the availability status of a tool.
    /// </summary>
    /// <param name="toolId">The unique identifier of the tool.</param>
    /// <param name="isAvailable">The new availability status.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, returning true if update succeeded.</returns>
    Task<bool> UpdateToolAvailabilityAsync(
        string toolId,
        bool isAvailable,
        CancellationToken cancellationToken = default);
}
