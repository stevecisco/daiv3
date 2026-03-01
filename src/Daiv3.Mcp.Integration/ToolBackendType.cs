namespace Daiv3.Mcp.Integration;

/// <summary>
/// Specifies the backend type used to invoke a tool.
/// </summary>
/// <remarks>
/// The system uses a prioritized routing strategy:
/// <list type="number">
/// <item><description>Direct - C# interface invocation (lowest overhead, highest performance)</description></item>
/// <item><description>CLI - Command-line execution (minimal overhead, local execution)</description></item>
/// <item><description>RestAPI - HTTP REST API calls (moderate overhead, external services)</description></item>
/// <item><description>MCP - Model Context Protocol (highest overhead, for remote services)</description></item>
/// </list>
/// </remarks>
public enum ToolBackendType
{
    /// <summary>
    /// Tool is invoked via direct C# interface. Used for local services such as
    /// knowledge search, scheduling, model queue, document processing.
    /// Zero context overhead, highest performance.
    /// </summary>
    Direct = 0,

    /// <summary>
    /// Tool is invoked via CLI execution. Used for file operations, Windows native tools,
    /// local external binaries. Minimal context overhead.
    /// </summary>
    CLI = 1,

    /// <summary>
    /// Tool is invoked via Model Context Protocol. Used for persistent remote services
    /// such as GitHub API, AWS, external SaaS integrations. Highest context overhead.
    /// </summary>
    MCP = 2,

    /// <summary>
    /// Tool is invoked via REST API HTTP calls. Used for external web services,
    /// public APIs, internal enterprise APIs, third-party SaaS platforms.
    /// Moderate context overhead (HTTP headers and JSON payload).
    /// </summary>
    RestAPI = 3
}
