using Daiv3.Mcp.Integration;
using Daiv3.Orchestration.Interfaces;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Daiv3.Orchestration;

/// <summary>
/// Default implementation of <see cref="IToolInvoker"/> providing intelligent tool routing.
/// </summary>
/// <remarks>
/// This implementation routes tool invocations to the appropriate backend based on the
/// tool's registered backend type. It prioritizes efficiency:
/// <list type="bullet">
/// <item><description>Direct C# tools are invoked through registered service interfaces</description></item>
/// <item><description>CLI tools are executed via process invocation</description></item>
/// <item><description>MCP tools are routed to the MCP tool provider</description></item>
/// </list>
/// 
/// <para>
/// The routing service tracks context token overhead for MCP tools and logs all routing
/// decisions for observability. It handles tool unavailability and backend failures gracefully.
/// </para>
/// </remarks>
public sealed class ToolRoutingService : IToolInvoker
{
    private readonly ILogger<ToolRoutingService> _logger;
    private readonly IToolRegistry _toolRegistry;
    private readonly IMcpToolProvider _mcpToolProvider;
    private int _invocationCounter = 0;

    public ToolRoutingService(
        ILogger<ToolRoutingService> logger,
        IToolRegistry toolRegistry,
        IMcpToolProvider mcpToolProvider)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _mcpToolProvider = mcpToolProvider ?? throw new ArgumentNullException(nameof(mcpToolProvider));
    }

    public Task<ToolInvocationResult> InvokeToolAsync(
        string toolId,
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken = default)
    {
        return InvokeToolAsync(toolId, parameters, new ToolInvocationPreferences(), cancellationToken);
    }

    public async Task<ToolInvocationResult> InvokeToolAsync(
        string toolId,
        Dictionary<string, object> parameters,
        ToolInvocationPreferences preferences,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolId);
        ArgumentNullException.ThrowIfNull(preferences);

        var invocationId = Interlocked.Increment(ref _invocationCounter);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Look up tool in registry
            var tool = await _toolRegistry.GetToolAsync(toolId, cancellationToken);
            if (tool == null)
            {
                _logger.LogWarning("Tool '{ToolId}' not found in registry (invocation #{InvocationId})",
                    toolId, invocationId);

                return new ToolInvocationResult
                {
                    Success = false,
                    ErrorMessage = $"Tool '{toolId}' not found",
                    ErrorCode = "TOOL_NOT_FOUND",
                    BackendUsed = ToolBackendType.Direct, // Dummy value
                    DurationMs = stopwatch.ElapsedMilliseconds
                };
            }

            // Check availability
            if (!tool.IsAvailable)
            {
                _logger.LogWarning("Tool '{ToolId}' is not currently available (invocation #{InvocationId})",
                    toolId, invocationId);

                return new ToolInvocationResult
                {
                    Success = false,
                    ErrorMessage = $"Tool '{toolId}' is not currently available",
                    ErrorCode = "TOOL_UNAVAILABLE",
                    BackendUsed = tool.Backend,
                    DurationMs = stopwatch.ElapsedMilliseconds
                };
            }

            _logger.LogInformation("Routing tool '{ToolId}' invocation #{InvocationId} to {Backend} backend (source: {Source})",
                toolId, invocationId, tool.Backend, tool.Source);

            // Route to appropriate backend
            ToolInvocationResult result = tool.Backend switch
            {
                ToolBackendType.Direct => await InvokeDirectToolAsync(tool, parameters, invocationId, cancellationToken),
                ToolBackendType.CLI => await InvokeCliToolAsync(tool, parameters, invocationId, cancellationToken),
                ToolBackendType.MCP => await InvokeMcpToolAsync(tool, parameters, invocationId, cancellationToken),
                _ => throw new NotSupportedException($"Backend type {tool.Backend} is not supported")
            };

            stopwatch.Stop();
            result.DurationMs = stopwatch.ElapsedMilliseconds;

            // Check context token threshold
            if (result.ContextTokenCost > preferences.MaxContextTokenCost)
            {
                _logger.LogWarning("Tool invocation '{ToolId}' exceeded context token threshold: " +
                    "{ActualCost} > {Threshold} tokens (invocation #{InvocationId})",
                    toolId, result.ContextTokenCost, preferences.MaxContextTokenCost, invocationId);
            }

            _logger.LogInformation("Tool '{ToolId}' invocation #{InvocationId} completed: " +
                "success={Success}, backend={Backend}, duration={DurationMs}ms, tokens={TokenCost}",
                toolId, invocationId, result.Success, result.BackendUsed, result.DurationMs, result.ContextTokenCost);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Unexpected error invoking tool '{ToolId}' (invocation #{InvocationId}): {ErrorMessage}",
                toolId, invocationId, ex.Message);

            return new ToolInvocationResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                ErrorCode = "INVOCATION_EXCEPTION",
                BackendUsed = ToolBackendType.Direct, // Dummy value
                DurationMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    // Private helper methods for backend-specific invocation

    private async Task<ToolInvocationResult> InvokeDirectToolAsync(
        Models.ToolDescriptor tool,
        Dictionary<string, object> parameters,
        int invocationId,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Direct tool invocation for '{ToolId}' (invocation #{InvocationId}) - " +
            "implementation pending service registry integration",
            tool.ToolId, invocationId);

        // TODO: Implement direct C# tool invocation via service registry/factory pattern
        // This will be implemented when direct tools are registered (e.g., knowledge search, scheduling)
        // For now, return a placeholder indicating the feature is under development

        await Task.CompletedTask; // Suppress async warning

        return new ToolInvocationResult
        {
            Success = false,
            ErrorMessage = "Direct tool invocation not yet implemented - pending service registry integration",
            ErrorCode = "NOT_IMPLEMENTED",
            BackendUsed = ToolBackendType.Direct,
            ContextTokenCost = 0 // Direct tools have zero context overhead
        };
    }

    private async Task<ToolInvocationResult> InvokeCliToolAsync(
        Models.ToolDescriptor tool,
        Dictionary<string, object> parameters,
        int invocationId,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("CLI tool invocation for '{ToolId}' (invocation #{InvocationId}) - " +
            "implementation pending CLI executor integration",
            tool.ToolId, invocationId);

        // TODO: Implement CLI tool invocation via process execution
        // This will invoke the tool.Source as a command with marshalled parameters
        // For now, return a placeholder indicating the feature is under development

        await Task.CompletedTask; // Suppress async warning

        return new ToolInvocationResult
        {
            Success = false,
            ErrorMessage = "CLI tool invocation not yet implemented - pending CLI executor integration",
            ErrorCode = "NOT_IMPLEMENTED",
            BackendUsed = ToolBackendType.CLI,
            ContextTokenCost = 5 // CLI tools have minimal context overhead
        };
    }

    private async Task<ToolInvocationResult> InvokeMcpToolAsync(
        Models.ToolDescriptor tool,
        Dictionary<string, object> parameters,
        int invocationId,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("MCP tool invocation for '{ToolId}' from server '{ServerName}' (invocation #{InvocationId})",
            tool.ToolId, tool.Source, invocationId);

        try
        {
            var mcpResult = await _mcpToolProvider.InvokeToolAsync(
                tool.Source, // Source is the server name for MCP tools
                tool.ToolId,
                parameters,
                cancellationToken);

            return new ToolInvocationResult
            {
                Success = mcpResult.Success,
                Result = mcpResult.Result,
                ErrorMessage = mcpResult.ErrorMessage,
                ErrorCode = mcpResult.ErrorCode,
                BackendUsed = ToolBackendType.MCP,
                DurationMs = mcpResult.DurationMs,
                ContextTokenCost = mcpResult.ContextTokenCost,
                Metadata = mcpResult.Metadata
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to invoke MCP tool '{ToolId}' from server '{ServerName}' " +
                "(invocation #{InvocationId}): {ErrorMessage}",
                tool.ToolId, tool.Source, invocationId, ex.Message);

            return new ToolInvocationResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                ErrorCode = "MCP_INVOCATION_FAILED",
                BackendUsed = ToolBackendType.MCP,
                ContextTokenCost = tool.EstimatedTokenCost
            };
        }
    }
}
