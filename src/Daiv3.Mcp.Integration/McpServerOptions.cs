namespace Daiv3.Mcp.Integration;

/// <summary>
/// Configuration options for connecting to an MCP tool server.
/// </summary>
public sealed class McpServerOptions
{
    /// <summary>
    /// Gets or sets the unique name identifier for this MCP server.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the server URL or command for connection.
    /// For HTTP/WebSocket servers, this is a URL (http://localhost:3000).
    /// For stdio servers, this is the executable path or command.
    /// </summary>
    public required string Endpoint { get; set; }

    /// <summary>
    /// Gets or sets the transport type for this MCP server.
    /// </summary>
    public McpTransportType TransportType { get; set; } = McpTransportType.Stdio;

    /// <summary>
    /// Gets or sets optional authentication token for the server.
    /// </summary>
    public string? AuthToken { get; set; }

    /// <summary>
    /// Gets or sets the connection timeout in milliseconds.
    /// Default is 5000ms (5 seconds).
    /// </summary>
    public int ConnectionTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Gets or sets the invocation timeout in milliseconds.
    /// Default is 30000ms (30 seconds).
    /// </summary>
    public int InvocationTimeoutMs { get; set; } = 30000;

    /// <summary>
    /// Gets or sets whether to automatically reconnect on connection loss.
    /// Default is true.
    /// </summary>
    public bool AutoReconnect { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum context token overhead threshold.
    /// When exceeded, a warning is logged. Default is 1000 tokens.
    /// </summary>
    public int ContextTokenThreshold { get; set; } = 1000;
}

/// <summary>
/// Specifies the transport mechanism for MCP server communication.
/// </summary>
public enum McpTransportType
{
    /// <summary>
    /// Standard input/output communication (default for local MCP servers).
    /// </summary>
    Stdio = 0,

    /// <summary>
    /// HTTP-based communication (for network-accessible MCP servers).
    /// </summary>
    Http = 1,

    /// <summary>
    /// WebSocket-based communication (for persistent connection MCP servers).
    /// </summary>
    WebSocket = 2
}
