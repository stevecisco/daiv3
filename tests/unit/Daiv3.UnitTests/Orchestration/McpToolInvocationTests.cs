using Daiv3.Mcp.Integration;
using Daiv3.Orchestration;
using Daiv3.Orchestration.Interfaces;
using Daiv3.Orchestration.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Daiv3.UnitTests.Orchestration;

/// <summary>
/// Unit tests for MCP tool invocation.
/// Verifies AST-ACC-002 requirement: Agents can invoke registered MCP tools.
/// </summary>
public class McpToolInvocationTests
{
    private readonly ToolRegistry _registry;
    private readonly Mock<IMcpToolProvider> _mockMcpProvider;
    private readonly ToolRoutingService _routingService;

    public McpToolInvocationTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        // Setup mocks
        _mockMcpProvider = new Mock<IMcpToolProvider>();

        // Register orchestration services first to get all dependencies
        services.AddOrchestrationServices();
        
        // Then override the MCP provider with our mock
        services.AddScoped(_ => _mockMcpProvider.Object);

        var serviceProvider = services.BuildServiceProvider();
        _registry = (ToolRegistry)serviceProvider.GetRequiredService<IToolRegistry>();
        _routingService = (ToolRoutingService)serviceProvider.GetRequiredService<IToolInvoker>();
    }

    /// <summary>
    /// Test: MCP tool is successfully registered and discoverable.
    /// </summary>
    [Fact]
    public async Task RegisterMcpTool_ToolBecomesAvailable()
    {
        // Arrange
        var tool = new ToolDescriptor
        {
            ToolId = "github-search",
            Name = "GitHub Search",
            Description = "Search GitHub repositories",
            Backend = ToolBackendType.MCP,
            Source = "github-server",
            Parameters = new List<ToolParameter>
            {
                new ToolParameter
                {
                    Name = "query",
                    Description = "Search query",
                    Type = "string",
                    Required = true
                }
            },
            EstimatedTokenCost = 50,
            IsAvailable = true
        };

        // Act
        var registered = await _registry.RegisterToolAsync(tool);

        // Assert
        Assert.True(registered);
        var retrieved = await _registry.GetToolAsync("github-search");
        Assert.NotNull(retrieved);
        Assert.Equal("github-search", retrieved!.ToolId);
        Assert.Equal(ToolBackendType.MCP, retrieved.Backend);
        Assert.Equal("github-server", retrieved.Source);
    }

    /// <summary>
    /// Test: MCP tool parameters are validated correctly.
    /// </summary>
    [Fact]
    public async Task McpToolInvocation_ValidateParameters_Success()
    {
        // Arrange
        var tool = new ToolDescriptor
        {
            ToolId = "github-search",
            Name = "GitHub Search",
            Description = "Search GitHub repositories",
            Backend = ToolBackendType.MCP,
            Source = "github-server",
            Parameters = new List<ToolParameter>
            {
                new ToolParameter
                {
                    Name = "query",
                    Description = "Search query",
                    Type = "string",
                    Required = true
                },
                new ToolParameter
                {
                    Name = "maxResults",
                    Description = "Maximum results",
                    Type = "number",
                    Required = false,
                    DefaultValue = 10
                }
            },
            IsAvailable = true
        };

        await _registry.RegisterToolAsync(tool);

        var toolResult = new McpToolInvocationResult
        {
            Success = true,
            Result = "Found 5 repositories matching 'ML frameworks'",
            ContextTokenCost = 75
        };

        _mockMcpProvider
            .Setup(p => p.InvokeToolAsync(
                "github-server",
                "github-search",
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(toolResult);

        var parameters = new Dictionary<string, object>
        {
            { "query", "ML frameworks" }
        };

        // Act
        var result = await _routingService.InvokeToolAsync(
            "github-search",
            parameters,
            new ToolInvocationPreferences(),
            CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(ToolBackendType.MCP, result.BackendUsed);
        Assert.True(result.ContextTokenCost > 0);
    }

    /// <summary>
    /// Test: MCP tool invocation returns results in correct format.
    /// </summary>
    [Fact]
    public async Task McpToolInvocation_ReturnsFormattedResults()
    {
        // Arrange
        var tool = new ToolDescriptor
        {
            ToolId = "api-call",
            Name = "REST API Call",
            Description = "Call external REST API",
            Backend = ToolBackendType.MCP,
            Source = "api-server",
            IsAvailable = true
        };

        await _registry.RegisterToolAsync(tool);

        var apiResponse = new { status = "success", data = new[] { "item1", "item2" } };
        var toolResult = new McpToolInvocationResult
        {
            Success = true,
            Result = System.Text.Json.JsonSerializer.Serialize(apiResponse),
            ContextTokenCost = 100
        };

        _mockMcpProvider
            .Setup(p => p.InvokeToolAsync(
                "api-server",
                "api-call",
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(toolResult);

        // Act
        var result = await _routingService.InvokeToolAsync(
            "api-call",
            new Dictionary<string, object> { { "endpoint", "/data" } },
            new ToolInvocationPreferences(),
            CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Result);
    }

    /// <summary>
    /// Test: MCP server connection failure is handled gracefully.
    /// </summary>
    [Fact]
    public async Task McpToolInvocation_ServerUnavailable_ReturnsError()
    {
        // Arrange
        var tool = new ToolDescriptor
        {
            ToolId = "unavailable-tool",
            Name = "Unavailable Tool",
            Description = "Tool on unavailable server",
            Backend = ToolBackendType.MCP,
            Source = "dead-server",
            IsAvailable = false
        };

        await _registry.RegisterToolAsync(tool);

        _mockMcpProvider
            .Setup(p => p.InvokeToolAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new McpToolInvocationResult
            {
                Success = false,
                ErrorMessage = "Server connection failed"
            });

        // Act
        var result = await _routingService.InvokeToolAsync(
            "unavailable-tool",
            new Dictionary<string, object>(),
            new ToolInvocationPreferences(),
            CancellationToken.None);

        // Assert
        Assert.False(result.Success);
    }

    /// <summary>
    /// Test: MCP tool not found in registry.
    /// </summary>
    [Fact]
    public async Task McpToolInvocation_ToolNotFound_ReturnsError()
    {
        // Act
        var result = await _routingService.InvokeToolAsync(
            "nonexistent-tool",
            new Dictionary<string, object>(),
            new ToolInvocationPreferences(),
            CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not found", result.ErrorMessage ?? "", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Test: Context token cost is tracked for MCP invocations.
    /// </summary>
    [Fact]
    public async Task McpToolInvocation_ContextTokensCostTracked()
    {
        // Arrange
        var tool = new ToolDescriptor
        {
            ToolId = "expensive-tool",
            Name = "Expensive Tool",
            Description = "Tool with high token cost",
            Backend = ToolBackendType.MCP,
            Source = "mcp-server",
            EstimatedTokenCost = 150,
            IsAvailable = true
        };

        await _registry.RegisterToolAsync(tool);

        var toolResult = new McpToolInvocationResult
        {
            Success = true,
            Result = "Result",
            ContextTokenCost = 250 // Actual cost from MCP provider
        };

        _mockMcpProvider
            .Setup(p => p.InvokeToolAsync(
                "mcp-server",
                "expensive-tool",
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(toolResult);

        // Act
        var result = await _routingService.InvokeToolAsync(
            "expensive-tool",
            new Dictionary<string, object>(),
            new ToolInvocationPreferences(),
            CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(250, result.ContextTokenCost);
    }

    /// <summary>
    /// Test: Invocation duration is measured.
    /// </summary>
    [Fact]
    public async Task McpToolInvocation_MeasuresExecutionDuration()
    {
        // Arrange
        var tool = new ToolDescriptor
        {
            ToolId = "slow-tool",
            Name = "Slow Tool",
            Description = "Tool with measurable latency",
            Backend = ToolBackendType.MCP,
            Source = "mcp-server",
            IsAvailable = true
        };

        await _registry.RegisterToolAsync(tool);

        var toolResult = new McpToolInvocationResult
        {
            Success = true,
            Result = "Delayed result"
        };

        _mockMcpProvider
            .Setup(p => p.InvokeToolAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (string _, string _, Dictionary<string, object> _, CancellationToken _) =>
            {
                await Task.Delay(50);
                return toolResult;
            });

        // Act
        var result = await _routingService.InvokeToolAsync(
            "slow-tool",
            new Dictionary<string, object>(),
            new ToolInvocationPreferences(),
            CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.DurationMs >= 50); // At least 50ms delay
    }

    /// <summary>
    /// Test: Multiple MCP tools can coexist in registry.
    /// </summary>
    [Fact]
    public async Task McpToolRegistry_MultipleTools_AllAvailable()
    {
        // Arrange
        var tools = new[]
        {
            new ToolDescriptor
            {
                ToolId = "github-tool",
                Name = "GitHub Tool",
                Description = "GitHub operations",
                Backend = ToolBackendType.MCP,
                Source = "github-server",
                IsAvailable = true
            },
            new ToolDescriptor
            {
                ToolId = "aws-tool",
                Name = "AWS Tool",
                Description = "AWS operations",
                Backend = ToolBackendType.MCP,
                Source = "aws-server",
                IsAvailable = true
            },
            new ToolDescriptor
            {
                ToolId = "slack-tool",
                Name = "Slack Tool",
                Description = "Slack operations",
                Backend = ToolBackendType.MCP,
                Source = "slack-server",
                IsAvailable = true
            }
        };

        // Act
        foreach (var tool in tools)
        {
            await _registry.RegisterToolAsync(tool);
        }

        var allTools = await _registry.GetAllToolsAsync();
        var mcpTools = await _registry.GetToolsByBackendAsync(ToolBackendType.MCP);

        // Assert
        Assert.Equal(3, allTools.Count);
        Assert.Equal(3, mcpTools.Count);
        Assert.All(mcpTools, t => Assert.Equal(ToolBackendType.MCP, t.Backend));
    }

    /// <summary>
    /// Test: MCP tool becomes unavailable correctly.
    /// </summary>
    [Fact]
    public async Task McpToolAvailability_MarkUnavailable()
    {
        // Arrange
        var tool = new ToolDescriptor
        {
            ToolId = "test-tool",
            Name = "Test Tool",
            Description = "Test",
            Backend = ToolBackendType.MCP,
            Source = "server",
            IsAvailable = true
        };

        await _registry.RegisterToolAsync(tool);

        // Act
        var registeredTool = await _registry.GetToolAsync("test-tool");
        Assert.NotNull(registeredTool);
        registeredTool.IsAvailable = false;
        await _registry.RegisterToolAsync(registeredTool); // Re-register with IsAvailable=false

        var availableTools = await _registry.GetAvailableToolsAsync();

        // Assert
        Assert.DoesNotContain(availableTools, t => t.ToolId == "test-tool");
    }

    /// <summary>
    /// Test: MCP tool timeout is handled gracefully.
    /// </summary>
    [Fact]
    public async Task McpToolInvocation_TimeoutHandled()
    {
        // Arrange
        var tool = new ToolDescriptor
        {
            ToolId = "timeout-tool",
            Name = "Timeout Tool",
            Description = "Tool that times out",
            Backend = ToolBackendType.MCP,
            Source = "mcp-server",
            IsAvailable = true
        };

        await _registry.RegisterToolAsync(tool);

        var cts = new CancellationTokenSource(100); // 100ms timeout

        _mockMcpProvider
            .Setup(p => p.InvokeToolAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(),
                cts.Token))
            .Returns(async (string _, string _, Dictionary<string, object> _, CancellationToken ct) =>
            {
                try
                {
                    await Task.Delay(5000, ct);
                }
                catch (OperationCanceledException)
                {
                    return new McpToolInvocationResult
                    {
                        Success = false,
                        ErrorMessage = "Operation timed out"
                    };
                }
                return new McpToolInvocationResult { Success = true };
            });

        // Act
        var result = await _routingService.InvokeToolAsync(
            "timeout-tool",
            new Dictionary<string, object>(),
            new ToolInvocationPreferences(),
            cts.Token);

        // Assert
        // The invocation should fail due to timeout
        Assert.False(result.Success);
    }
}
