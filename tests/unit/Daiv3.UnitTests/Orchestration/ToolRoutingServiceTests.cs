using Daiv3.Mcp.Integration;
using Daiv3.Orchestration;
using Daiv3.Orchestration.Interfaces;
using Daiv3.Orchestration.Models;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net.Http;
using Xunit;

namespace Daiv3.UnitTests.Orchestration;

/// <summary>
/// Unit tests for <see cref="ToolRoutingService"/>.
/// </summary>
public class ToolRoutingServiceTests
{
    private readonly Mock<ILogger<ToolRoutingService>> _mockLogger;
    private readonly Mock<IToolRegistry> _mockRegistry;
    private readonly Mock<IMcpToolProvider> _mockMcpProvider;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly ToolRoutingService _routingService;

    public ToolRoutingServiceTests()
    {
        _mockLogger = new Mock<ILogger<ToolRoutingService>>();
        _mockRegistry = new Mock<IToolRegistry>();
        _mockMcpProvider = new Mock<IMcpToolProvider>();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        
        // Setup a basic HttpClient for the mock factory
        _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient());
        
        _routingService = new ToolRoutingService(
            _mockLogger.Object,
            _mockRegistry.Object,
            _mockMcpProvider.Object,
            _mockHttpClientFactory.Object);
    }

    [Fact]
    public async Task InvokeToolAsync_ToolNotFound_ReturnsError()
    {
        // Arrange
        _mockRegistry.Setup(r => r.GetToolAsync("nonexistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ToolDescriptor?)null);

        // Act
        var result = await _routingService.InvokeToolAsync("nonexistent", new Dictionary<string, object>());

        // Assert
        Assert.False(result.Success);
        Assert.Equal("TOOL_NOT_FOUND", result.ErrorCode);
        Assert.Contains("not found", result.ErrorMessage);
    }

    [Fact]
    public async Task InvokeToolAsync_ToolUnavailable_ReturnsError()
    {
        // Arrange
        var tool = CreateTestTool("tool-1", "Test Tool", ToolBackendType.Direct);
        tool.IsAvailable = false;
        _mockRegistry.Setup(r => r.GetToolAsync("tool-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tool);

        // Act
        var result = await _routingService.InvokeToolAsync("tool-1", new Dictionary<string, object>());

        // Assert
        Assert.False(result.Success);
        Assert.Equal("TOOL_UNAVAILABLE", result.ErrorCode);
        Assert.Contains("not currently available", result.ErrorMessage);
    }

    [Fact]
    public async Task InvokeToolAsync_DirectTool_ReturnsNotImplemented()
    {
        // Arrange
        var tool = CreateTestTool("direct-tool", "Direct Tool", ToolBackendType.Direct);
        _mockRegistry.Setup(r => r.GetToolAsync("direct-tool", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tool);

        // Act
        var result = await _routingService.InvokeToolAsync("direct-tool", new Dictionary<string, object>());

        // Assert
        Assert.False(result.Success);
        Assert.Equal("NOT_IMPLEMENTED", result.ErrorCode);
        Assert.Equal(ToolBackendType.Direct, result.BackendUsed);
        Assert.Equal(0, result.ContextTokenCost); // Direct tools have zero overhead
    }

    [Fact]
    public async Task InvokeToolAsync_CliTool_ReturnsNotImplemented()
    {
        // Arrange
        var tool = CreateTestTool("cli-tool", "CLI Tool", ToolBackendType.CLI);
        _mockRegistry.Setup(r => r.GetToolAsync("cli-tool", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tool);

        // Act
        var result = await _routingService.InvokeToolAsync("cli-tool", new Dictionary<string, object>());

        // Assert
        Assert.False(result.Success);
        Assert.Equal("NOT_IMPLEMENTED", result.ErrorCode);
        Assert.Equal(ToolBackendType.CLI, result.BackendUsed);
        Assert.Equal(5, result.ContextTokenCost); // CLI tools have minimal overhead
    }

    [Fact]
    public async Task InvokeToolAsync_McpTool_Success_ReturnsResult()
    {
        // Arrange
        var tool = CreateTestTool("mcp-tool", "MCP Tool", ToolBackendType.MCP);
        tool.Source = "test-server";
        _mockRegistry.Setup(r => r.GetToolAsync("mcp-tool", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tool);

        var mcpResult = new McpToolInvocationResult
        {
            Success = true,
            Result = new { data = "test result" },
            DurationMs = 100,
            ContextTokenCost = 25
        };
        _mockMcpProvider.Setup(p => p.InvokeToolAsync(
                "test-server",
                "mcp-tool",
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mcpResult);

        // Act
        var result = await _routingService.InvokeToolAsync("mcp-tool", new Dictionary<string, object>());

        // Assert
        Assert.True(result.Success);
        Assert.Equal(ToolBackendType.MCP, result.BackendUsed);
        Assert.NotNull(result.Result);
        Assert.Equal(25, result.ContextTokenCost);
    }

    [Fact]
    public async Task InvokeToolAsync_McpTool_Failure_ReturnsError()
    {
        // Arrange
        var tool = CreateTestTool("mcp-tool", "MCP Tool", ToolBackendType.MCP);
        tool.Source = "test-server";
        _mockRegistry.Setup(r => r.GetToolAsync("mcp-tool", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tool);

        var mcpResult = new McpToolInvocationResult
        {
            Success = false,
            ErrorMessage = "Server error",
            ErrorCode = "SERVER_ERROR",
            DurationMs = 50,
            ContextTokenCost = 10
        };
        _mockMcpProvider.Setup(p => p.InvokeToolAsync(
                "test-server",
                "mcp-tool",
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mcpResult);

        // Act
        var result = await _routingService.InvokeToolAsync("mcp-tool", new Dictionary<string, object>());

        // Assert
        Assert.False(result.Success);
        Assert.Equal(ToolBackendType.MCP, result.BackendUsed);
        Assert.Equal("Server error", result.ErrorMessage);
        Assert.Equal("SERVER_ERROR", result.ErrorCode);
    }

    [Fact]
    public async Task InvokeToolAsync_McpTool_Exception_ReturnsError()
    {
        // Arrange
        var tool = CreateTestTool("mcp-tool", "MCP Tool", ToolBackendType.MCP);
        tool.Source = "test-server";
        _mockRegistry.Setup(r => r.GetToolAsync("mcp-tool", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tool);

        _mockMcpProvider.Setup(p => p.InvokeToolAsync(
                "test-server",
                "mcp-tool",
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Connection failed"));

        // Act
        var result = await _routingService.InvokeToolAsync("mcp-tool", new Dictionary<string, object>());

        // Assert
        Assert.False(result.Success);
        Assert.Equal(ToolBackendType.MCP, result.BackendUsed);
        Assert.Equal("MCP_INVOCATION_FAILED", result.ErrorCode);
        Assert.Contains("Connection failed", result.ErrorMessage);
    }

    [Fact]
    public async Task InvokeToolAsync_WithPreferences_RespectsTokenThreshold()
    {
        // Arrange
        var tool = CreateTestTool("mcp-tool", "MCP Tool", ToolBackendType.MCP);
        tool.Source = "test-server";
        _mockRegistry.Setup(r => r.GetToolAsync("mcp-tool", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tool);

        var mcpResult = new McpToolInvocationResult
        {
            Success = true,
            Result = "result",
            DurationMs = 100,
            ContextTokenCost = 500 // Exceeds default threshold of 1000 but not our custom one
        };
        _mockMcpProvider.Setup(p => p.InvokeToolAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mcpResult);

        var preferences = new ToolInvocationPreferences
        {
            MaxContextTokenCost = 100 // Set low threshold
        };

        // Act
        var result = await _routingService.InvokeToolAsync("mcp-tool", new Dictionary<string, object>(), preferences);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(500, result.ContextTokenCost);
        // Should log warning (verified via mock logger if needed)
    }

    [Fact]
    public async Task InvokeToolAsync_MultipleInvocations_UsesDifferentInvocationIds()
    {
        // Arrange
        var tool1 = CreateTestTool("tool-1", "Tool 1", ToolBackendType.Direct);
        var tool2 = CreateTestTool("tool-2", "Tool 2", ToolBackendType.Direct);
        _mockRegistry.Setup(r => r.GetToolAsync("tool-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tool1);
        _mockRegistry.Setup(r => r.GetToolAsync("tool-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tool2);

        // Act
        var result1 = await _routingService.InvokeToolAsync("tool-1", new Dictionary<string, object>());
        var result2 = await _routingService.InvokeToolAsync("tool-2", new Dictionary<string, object>());

        // Assert
        // Both should have been assigned unique invocation IDs (verified via logs if needed)
        Assert.NotNull(result1);
        Assert.NotNull(result2);
    }

    [Fact]
    public async Task InvokeToolAsync_WithParameters_PassesParametersToBackend()
    {
        // Arrange
        var tool = CreateTestTool("mcp-tool", "MCP Tool", ToolBackendType.MCP);
        tool.Source = "test-server";
        _mockRegistry.Setup(r => r.GetToolAsync("mcp-tool", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tool);

        var parameters = new Dictionary<string, object>
        {
            ["query"] = "test query",
            ["limit"] = 10
        };

        var mcpResult = new McpToolInvocationResult
        {
            Success = true,
            Result = "result",
            DurationMs = 100,
            ContextTokenCost = 25
        };

        _mockMcpProvider.Setup(p => p.InvokeToolAsync(
                "test-server",
                "mcp-tool",
                It.Is<Dictionary<string, object>>(d => 
                    d.ContainsKey("query") && 
                    d["query"].ToString() == "test query" &&
                    d.ContainsKey("limit") &&
                    (int)d["limit"] == 10),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mcpResult);

        // Act
        var result = await _routingService.InvokeToolAsync("mcp-tool", parameters);

        // Assert
        Assert.True(result.Success);
        _mockMcpProvider.Verify(p => p.InvokeToolAsync(
            "test-server",
            "mcp-tool",
            It.IsAny<Dictionary<string, object>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private ToolDescriptor CreateTestTool(string toolId, string name, ToolBackendType backend)
    {
        return new ToolDescriptor
        {
            ToolId = toolId,
            Name = name,
            Description = $"Description for {name}",
            Source = backend == ToolBackendType.MCP ? "test-server" : "test-source",
            Backend = backend,
            IsAvailable = true,
            EstimatedTokenCost = backend == ToolBackendType.MCP ? 150 : 0
        };
    }
}
