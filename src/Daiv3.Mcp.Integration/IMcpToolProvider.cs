namespace Daiv3.Mcp.Integration;

/// <summary>
/// Provides connectivity to MCP tool servers and facilitates tool discovery and invocation.
/// </summary>
/// <remarks>
/// The MCP tool provider is responsible for:
/// <list type="bullet">
/// <item><description>Establishing connections to configured MCP servers</description></item>
/// <item><description>Discovering available tools from connected servers</description></item>
/// <item><description>Invoking tools with proper parameter marshalling</description></item>
/// <item><description>Tracking context token overhead for MCP invocations</description></item>
/// <item><description>Handling connection failures and timeouts gracefully</description></item>
/// </list>
/// </remarks>
public interface IMcpToolProvider
{
    /// <summary>
    /// Connects to an MCP server using the specified options.
    /// </summary>
    /// <param name="options">The server connection options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, returning true if connection succeeded.</returns>
    Task<bool> ConnectAsync(McpServerOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects from the specified MCP server.
    /// </summary>
    /// <param name="serverName">The name of the server to disconnect from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DisconnectAsync(string serverName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Discovers all available tools from the specified MCP server.
    /// </summary>
    /// <param name="serverName">The name of the MCP server.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, returning the list of discovered tools.</returns>
    Task<IReadOnlyList<McpToolDescriptor>> DiscoverToolsAsync(string serverName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invokes an MCP tool with the specified parameters.
    /// </summary>
    /// <param name="serverName">The name of the MCP server hosting the tool.</param>
    /// <param name="toolId">The unique identifier of the tool to invoke.</param>
    /// <param name="parameters">The parameters to pass to the tool.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, returning the invocation result.</returns>
    Task<McpToolInvocationResult> InvokeToolAsync(
        string serverName,
        string toolId,
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the connection status of the specified MCP server.
    /// </summary>
    /// <param name="serverName">The name of the MCP server.</param>
    /// <returns>True if connected, false otherwise.</returns>
    bool IsConnected(string serverName);

    /// <summary>
    /// Gets all currently connected MCP servers.
    /// </summary>
    /// <returns>A list of connected server names.</returns>
    IReadOnlyList<string> GetConnectedServers();
}

/// <summary>
/// Represents the result of an MCP tool invocation.
/// </summary>
public sealed class McpToolInvocationResult
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
    /// Gets or sets the duration of the invocation in milliseconds.
    /// </summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// Gets or sets the estimated context token cost of this invocation.
    /// Includes parameter serialization and result overhead.
    /// </summary>
    public int ContextTokenCost { get; set; }

    /// <summary>
    /// Gets or sets additional metadata from the invocation.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}
