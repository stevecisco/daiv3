using System.Text.Json.Serialization;

namespace Daiv3.Mcp.Integration;

/// <summary>
/// Base class for MCP protocol messages (JSON-RPC 2.0 format).
/// </summary>
internal abstract class McpMessage
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";
}

/// <summary>
/// Represents an MCP protocol request message.
/// </summary>
internal sealed class McpRequest : McpMessage
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("method")]
    public required string Method { get; set; }

    [JsonPropertyName("params")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Params { get; set; }
}

/// <summary>
/// Represents an MCP protocol response message.
/// </summary>
internal sealed class McpResponse : McpMessage
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Result { get; set; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public McpError? Error { get; set; }
}

/// <summary>
/// Represents an error in an MCP protocol response.
/// </summary>
internal sealed class McpError
{
    [JsonPropertyName("code")]
    public required int Code { get; set; }

    [JsonPropertyName("message")]
    public required string Message { get; set; }

    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Data { get; set; }
}

/// <summary>
/// Represents the payload for tool discovery requests.
/// </summary>
internal sealed class McpToolsListRequest
{
    // Empty object as per MCP spec for tools/list
}

/// <summary>
/// Represents the response from tool discovery.
/// </summary>
internal sealed class McpToolsListResponse
{
    [JsonPropertyName("tools")]
    public List<McpToolInfo> Tools { get; set; } = new();
}

/// <summary>
/// Represents information about a tool from MCP tool discovery.
/// </summary>
internal sealed class McpToolInfo
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("inputSchema")]
    public McpToolSchema? InputSchema { get; set; }
}

/// <summary>
/// Represents the JSON schema for tool parameters.
/// </summary>
internal sealed class McpToolSchema
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "object";

    [JsonPropertyName("properties")]
    public Dictionary<string, McpPropertySchema> Properties { get; set; } = new();

    [JsonPropertyName("required")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Required { get; set; }
}

/// <summary>
/// Represents a property schema within a tool parameter schema.
/// </summary>
internal sealed class McpPropertySchema
{
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }

    [JsonPropertyName("enum")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Enum { get; set; }

    [JsonPropertyName("default")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Default { get; set; }
}

/// <summary>
/// Represents the payload for tool invocation.
/// </summary>
internal sealed class McpToolCallRequest
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("arguments")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Arguments { get; set; }
}

/// <summary>
/// Represents the response from tool invocation.
/// </summary>
internal sealed class McpToolCallResponse
{
    [JsonPropertyName("content")]
    public List<McpContent> Content { get; set; } = new();

    [JsonPropertyName("isError")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsError { get; set; }
}

/// <summary>
/// Represents a content item in an MCP response.
/// </summary>
internal sealed class McpContent
{
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; set; }

    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Data { get; set; }
}
