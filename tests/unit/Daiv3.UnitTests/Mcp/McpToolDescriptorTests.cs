using Daiv3.Mcp.Integration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Daiv3.UnitTests.Mcp;

/// <summary>
/// Unit tests for <see cref="McpToolDescriptor"/> and related data contracts.
/// </summary>
public class McpToolDescriptorTests
{
    [Fact]
    public void McpToolDescriptor_RequiredProperties_CanBeSet()
    {
        // Arrange & Act
        var descriptor = new McpToolDescriptor
        {
            ToolId = "test-tool",
            Name = "Test Tool",
            Description = "A test tool",
            ServerName = "test-server"
        };

        // Assert
        Assert.Equal("test-tool", descriptor.ToolId);
        Assert.Equal("Test Tool", descriptor.Name);
        Assert.Equal("A test tool", descriptor.Description);
        Assert.Equal("test-server", descriptor.ServerName);
        Assert.Equal(ToolBackendType.MCP, descriptor.Backend);
        Assert.Empty(descriptor.Parameters);
        Assert.Empty(descriptor.Metadata);
    }

    [Fact]
    public void McpToolDescriptor_WithParameters_CanBeCreated()
    {
        // Arrange & Act
        var descriptor = new McpToolDescriptor
        {
            ToolId = "search-tool",
            Name = "Search",
            Description = "Search for items",
            ServerName = "github-server",
            Parameters = new List<McpToolParameter>
            {
                new McpToolParameter
                {
                    Name = "query",
                    Description = "Search query",
                    Type = "string",
                    Required = true
                },
                new McpToolParameter
                {
                    Name = "limit",
                    Description = "Result limit",
                    Type = "number",
                    Required = false,
                    DefaultValue = 10
                }
            },
            EstimatedTokenCost = 150
        };

        // Assert
        Assert.Equal(2, descriptor.Parameters.Count);
        Assert.Equal("query", descriptor.Parameters[0].Name);
        Assert.True(descriptor.Parameters[0].Required);
        Assert.Equal("limit", descriptor.Parameters[1].Name);
        Assert.False(descriptor.Parameters[1].Required);
        Assert.Equal(10, descriptor.Parameters[1].DefaultValue);
        Assert.Equal(150, descriptor.EstimatedTokenCost);
    }

    [Fact]
    public void McpServerOptions_RequiredProperties_CanBeSet()
    {
        // Arrange & Act
        var options = new McpServerOptions
        {
            Name = "github-mcp",
            Endpoint = "node github-mcp-server.js",
            TransportType = McpTransportType.Stdio
        };

        // Assert
        Assert.Equal("github-mcp", options.Name);
        Assert.Equal("node github-mcp-server.js", options.Endpoint);
        Assert.Equal(McpTransportType.Stdio, options.TransportType);
        Assert.Equal(5000, options.ConnectionTimeoutMs);
        Assert.Equal(30000, options.InvocationTimeoutMs);
        Assert.True(options.AutoReconnect);
    }

    [Fact]
    public void McpToolInvocationResult_Success_CanBeCreated()
    {
        // Arrange & Act
        var result = new McpToolInvocationResult
        {
            Success = true,
            Result = new { data = "test", count = 5 },
            DurationMs = 150,
            ContextTokenCost = 25
        };

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Result);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(150, result.DurationMs);
        Assert.Equal(25, result.ContextTokenCost);
    }

    [Fact]
    public void McpToolInvocationResult_Failure_CanBeCreated()
    {
        // Arrange & Act
        var result = new McpToolInvocationResult
        {
            Success = false,
            ErrorMessage = "Tool execution failed",
            ErrorCode = "EXECUTION_ERROR",
            DurationMs = 50,
            ContextTokenCost = 10
        };

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Tool execution failed", result.ErrorMessage);
        Assert.Equal("EXECUTION_ERROR", result.ErrorCode);
        Assert.Null(result.Result);
    }

    [Theory]
    [InlineData(ToolBackendType.Direct, 0)]
    [InlineData(ToolBackendType.CLI, 1)]
    [InlineData(ToolBackendType.MCP, 2)]
    public void ToolBackendType_Values_AreCorrect(ToolBackendType backend, int expectedValue)
    {
        // Assert
        Assert.Equal(expectedValue, (int)backend);
    }

    [Theory]
    [InlineData(McpTransportType.Stdio, 0)]
    [InlineData(McpTransportType.Http, 1)]
    [InlineData(McpTransportType.WebSocket, 2)]
    public void McpTransportType_Values_AreCorrect(McpTransportType transport, int expectedValue)
    {
        // Assert
        Assert.Equal(expectedValue, (int)transport);
    }
}
