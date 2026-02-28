using Daiv3.Orchestration.Models;

namespace Daiv3.Orchestration.Interfaces;

/// <summary>
/// Provides intelligent routing and invocation of tools across multiple backends.
/// </summary>
/// <remarks>
/// The tool invoker implements a prioritized routing strategy:
/// <list type="number">
/// <item><description>Direct C# service interface (lowest overhead, highest performance)</description></item>
/// <item><description>CLI execution (minimal overhead, local execution)</description></item>
/// <item><description>MCP invocation (highest overhead, for remote services)</description></item>
/// </list>
/// 
/// <para>
/// The invoker tracks context token overhead for MCP tools and logs routing decisions
/// for observability. It handles backend failures gracefully and provides fallback options
/// where applicable.
/// </para>
/// </remarks>
public interface IToolInvoker
{
    /// <summary>
    /// Invokes a tool by its identifier with the specified parameters.
    /// </summary>
    /// <param name="toolId">The unique identifier of the tool to invoke.</param>
    /// <param name="parameters">The parameters to pass to the tool.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, returning the invocation result.</returns>
    Task<ToolInvocationResult> InvokeToolAsync(
        string toolId,
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invokes a tool with routing preferences.
    /// </summary>
    /// <param name="toolId">The unique identifier of the tool to invoke.</param>
    /// <param name="parameters">The parameters to pass to the tool.</param>
    /// <param name="preferences">Routing preferences for this invocation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, returning the invocation result.</returns>
    Task<ToolInvocationResult> InvokeToolAsync(
        string toolId,
        Dictionary<string, object> parameters,
        ToolInvocationPreferences preferences,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the result of a tool invocation.
/// </summary>
public sealed class ToolInvocationResult
{
    /// <summary>
    /// Gets or sets whether the invocation succeeded.
    /// </summary>
    public required bool Success { get; set; }

    /// <summary>
    /// Gets or sets the result data from the tool invocation.
    /// </summary>
    public object? Result { get; set; }

    /// <summary>
    /// Gets or sets the error message if the invocation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the error code if the invocation failed.
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Gets or sets the backend that was used for this invocation.
    /// </summary>
    public required Daiv3.Mcp.Integration.ToolBackendType BackendUsed { get; set; }

    /// <summary>
    /// Gets or sets the duration of the invocation in milliseconds.
    /// </summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// Gets or sets the estimated context token cost of this invocation.
    /// </summary>
    public int ContextTokenCost { get; set; }

    /// <summary>
    /// Gets or sets additional metadata from the invocation.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Specifies preferences for tool invocation routing.
/// </summary>
public sealed class ToolInvocationPreferences
{
    /// <summary>
    /// Gets or sets whether to prefer lower-overhead backends when multiple options are available.
    /// Default is true.
    /// </summary>
    public bool PreferLowOverhead { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to retry on transient failures.
    /// Default is true.
    /// </summary>
    public bool RetryOnFailure { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum allowed context token cost.
    /// Invocations exceeding this threshold will be logged as warnings.
    /// Default is 1000 tokens.
    /// </summary>
    public int MaxContextTokenCost { get; set; } = 1000;
}
