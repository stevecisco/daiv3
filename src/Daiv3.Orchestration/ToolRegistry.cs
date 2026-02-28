using Daiv3.Mcp.Integration;
using Daiv3.Orchestration.Interfaces;
using Daiv3.Orchestration.Models;
using Microsoft.Extensions.Logging;

namespace Daiv3.Orchestration;

/// <summary>
/// Default implementation of <see cref="IToolRegistry"/> providing in-memory tool catalog.
/// </summary>
/// <remarks>
/// This implementation maintains an in-memory registry of tools. For production scenarios
/// with tool persistence requirements, this could be extended to use database storage.
/// 
/// <para>
/// Tools are registered with their backend type (Direct, CLI, MCP), and the registry
/// tracks availability status for each tool. Tools from MCP servers are dynamically
/// registered when servers connect.
/// </para>
/// </remarks>
public sealed class ToolRegistry : IToolRegistry
{
    private readonly ILogger<ToolRegistry> _logger;
    private readonly Dictionary<string, ToolDescriptor> _tools = new();
    private readonly object _lock = new();

    public ToolRegistry(ILogger<ToolRegistry> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<bool> RegisterToolAsync(ToolDescriptor tool, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentException.ThrowIfNullOrWhiteSpace(tool.ToolId);

        lock (_lock)
        {
            if (_tools.ContainsKey(tool.ToolId))
            {
                _logger.LogWarning("Tool '{ToolId}' is already registered (backend: {Backend}, source: {Source}). " +
                    "Updating registration.", tool.ToolId, tool.Backend, tool.Source);
            }

            _tools[tool.ToolId] = tool;

            _logger.LogInformation("Registered tool '{ToolId}' ({Name}) with backend {Backend} from source '{Source}' " +
                "(estimated cost: {TokenCost} tokens)",
                tool.ToolId, tool.Name, tool.Backend, tool.Source, tool.EstimatedTokenCost);

            return Task.FromResult(true);
        }
    }

    public Task<bool> UnregisterToolAsync(string toolId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolId);

        lock (_lock)
        {
            if (_tools.Remove(toolId))
            {
                _logger.LogInformation("Unregistered tool '{ToolId}'", toolId);
                return Task.FromResult(true);
            }

            _logger.LogWarning("Cannot unregister tool '{ToolId}': not found in registry", toolId);
            return Task.FromResult(false);
        }
    }

    public Task<ToolDescriptor?> GetToolAsync(string toolId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolId);

        lock (_lock)
        {
            return Task.FromResult(_tools.TryGetValue(toolId, out var tool) ? tool : null);
        }
    }

    public Task<IReadOnlyList<ToolDescriptor>> GetAllToolsAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<ToolDescriptor>>(_tools.Values.ToList());
        }
    }

    public Task<IReadOnlyList<ToolDescriptor>> GetToolsByBackendAsync(
        ToolBackendType backend,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var tools = _tools.Values.Where(t => t.Backend == backend).ToList();
            return Task.FromResult<IReadOnlyList<ToolDescriptor>>(tools);
        }
    }

    public Task<IReadOnlyList<ToolDescriptor>> GetAvailableToolsAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var tools = _tools.Values.Where(t => t.IsAvailable).ToList();
            return Task.FromResult<IReadOnlyList<ToolDescriptor>>(tools);
        }
    }

    public Task<bool> UpdateToolAvailabilityAsync(
        string toolId,
        bool isAvailable,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolId);

        lock (_lock)
        {
            if (_tools.TryGetValue(toolId, out var tool))
            {
                tool.IsAvailable = isAvailable;
                _logger.LogInformation("Updated tool '{ToolId}' availability to {IsAvailable}",
                    toolId, isAvailable);
                return Task.FromResult(true);
            }

            _logger.LogWarning("Cannot update availability for tool '{ToolId}': not found in registry", toolId);
            return Task.FromResult(false);
        }
    }
}
