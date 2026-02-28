using System.Text.Json.Serialization;

namespace Daiv3.Mcp.Integration;

/// <summary>
/// Describes an MCP tool including its schema, parameters, and metadata.
/// Maps MCP protocol tool definitions to the system's tool registry format.
/// </summary>
public sealed class McpToolDescriptor
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
    /// Gets or sets the MCP server that provides this tool.
    /// </summary>
    public required string ServerName { get; set; }

    /// <summary>
    /// Gets or sets the backend type for this tool (always MCP for MCP tools).
    /// </summary>
    public ToolBackendType Backend { get; set; } = ToolBackendType.MCP;

    /// <summary>
    /// Gets or sets the tool parameters schema.
    /// </summary>
    public List<McpToolParameter> Parameters { get; set; } = new();

    /// <summary>
    /// Gets or sets the estimated context token cost for this tool.
    /// Includes schema, description, and parameter definitions.
    /// </summary>
    public int EstimatedTokenCost { get; set; }

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
/// Describes a parameter for an MCP tool.
/// </summary>
public sealed class McpToolParameter
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
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? DefaultValue { get; set; }

    /// <summary>
    /// Gets or sets the enumeration of allowed values, if constrained.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? EnumValues { get; set; }
}
