using Daiv3.Mcp.Integration;

namespace Daiv3.Orchestration.Models;

/// <summary>
/// Describes a tool available for agent invocation, including its backend type and metadata.
/// </summary>
public sealed class ToolDescriptor
{
    /// <summary>
    /// Gets or sets the unique identifier for this tool.
    /// </summary>
    public required string ToolId { get; set; }

    /// <summary>
    /// Gets or sets the human-readable name of the tool.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the description of what the tool does.
    /// This is visible to agents during tool selection.
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// Gets or sets the backend type for this tool (Direct, CLI, or MCP).
    /// </summary>
    public ToolBackendType Backend { get; set; }

    /// <summary>
    /// Gets or sets the source of this tool.
    /// For Direct tools: assembly/class name
    /// For CLI tools: executable path
    /// For MCP tools: server name
    /// </summary>
    public required string Source { get; set; }

    /// <summary>
    /// Gets or sets the tool parameters.
    /// </summary>
    public List<ToolParameter> Parameters { get; set; } = new();

    /// <summary>
    /// Gets or sets the estimated context token cost for this tool.
    /// Direct tools have zero cost, CLI minimal, MCP highest.
    /// </summary>
    public int EstimatedTokenCost { get; set; }

    /// <summary>
    /// Gets or sets whether this tool is currently available.
    /// </summary>
    public bool IsAvailable { get; set; } = true;

    /// <summary>
    /// Gets or sets additional metadata for this tool.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// Gets or sets the timestamp when this tool was registered.
    /// </summary>
    public DateTimeOffset RegisteredAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Describes a parameter for a tool.
/// </summary>
public sealed class ToolParameter
{
    /// <summary>
    /// Gets or sets the parameter name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the parameter description.
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// Gets or sets the parameter type (string, number, boolean, object, array).
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// Gets or sets whether this parameter is required.
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Gets or sets the default value for this parameter, if any.
    /// </summary>
    public object? DefaultValue { get; set; }
}
