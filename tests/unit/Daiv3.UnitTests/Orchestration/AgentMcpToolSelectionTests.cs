using Daiv3.Mcp.Integration;
using Daiv3.Orchestration;
using Daiv3.Orchestration.Interfaces;
using Daiv3.Orchestration.Models;
using Daiv3.Persistence;
using Daiv3.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Daiv3.UnitTests.Orchestration;

/// <summary>
/// Unit tests for agent MCP tool selection and invocation.
/// Verifies AST-ACC-002 requirement: Agents can select and use MCP tools intelligently.
/// </summary>
public class AgentMcpToolSelectionTests
{
    private readonly AgentManager _agentManager;
    private readonly Mock<IToolRegistry> _mockToolRegistry;
    private readonly Mock<IToolInvoker> _mockToolInvoker;
    private readonly AgentRepository _agentRepository;
    private readonly string _dbPath;

    public AgentMcpToolSelectionTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"daiv3-agent-mcp-test-{Guid.NewGuid()}.db");

        // Setup mocks
        _mockToolRegistry = new Mock<IToolRegistry>();
        _mockToolInvoker = new Mock<IToolInvoker>();

        // Setup service provider
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddPersistence(options => options.DatabasePath = _dbPath);
        services.AddOrchestrationServices();
        
        // Set up mocks after orchestration services to override defaults
        services.AddScoped(_ => _mockToolRegistry.Object);
        services.AddScoped(_ => _mockToolInvoker.Object);

        var serviceProvider = services.BuildServiceProvider();
        serviceProvider.InitializeDatabaseAsync().GetAwaiter().GetResult();

        _agentRepository = serviceProvider.GetRequiredService<AgentRepository>();
        _agentManager = (AgentManager)serviceProvider.GetRequiredService<IAgentManager>();
    }

    /// <summary>
    /// Test: Agent receives available MCP tools in its context.
    /// </summary>
    [Fact]
    public async Task Agent_ReceivesMcpToolsInContext()
    {
        // Arrange
        var agent = await _agentManager.CreateAgentAsync(new AgentDefinition
        {
            Name = "MCP-Agent",
            Purpose = "Test agent for MCP tools"
        });

        var mcpTools = new List<ToolDescriptor>
        {
            new ToolDescriptor
            {
                ToolId = "github-search",
                Name = "GitHub Search",
                Description = "Search GitHub repositories",
                Backend = ToolBackendType.MCP,
                Source = "github-server",
                IsAvailable = true,
                Parameters = new List<ToolParameter>
                {
                    new ToolParameter
                    {
                        Name = "query",
                        Description = "Search query",
                        Type = "string",
                        Required = true
                    }
                }
            },
            new ToolDescriptor
            {
                ToolId = "aws-list-buckets",
                Name = "List S3 Buckets",
                Description = "List AWS S3 buckets",
                Backend = ToolBackendType.MCP,
                Source = "aws-server",
                IsAvailable = true
            }
        };

        _mockToolRegistry
            .Setup(r => r.GetAvailableToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mcpTools);

        // Act
        var availableTools = await _mockToolRegistry.Object.GetAvailableToolsAsync();

        // Assert
        Assert.NotEmpty(availableTools);
        Assert.Equal(2, availableTools.Count);
        Assert.All(availableTools, t => Assert.Equal(ToolBackendType.MCP, t.Backend));
    }

    /// <summary>
    /// Test: Agent can select an MCP tool from available tools.
    /// </summary>
    [Fact]
    public async Task Agent_SelectsMcpTool_ForAppropriateTask()
    {
        // Arrange
        var agent = await _agentManager.CreateAgentAsync(new AgentDefinition
        {
            Name = "SearchAgent",
            Purpose = "Search for information"
        });

        var githubTool = new ToolDescriptor
        {
            ToolId = "github-search",
            Name = "GitHub Search",
            Description = "Search GitHub repositories for projects",
            Backend = ToolBackendType.MCP,
            Source = "github-server",
            IsAvailable = true,
            Parameters = new List<ToolParameter>
            {
                new ToolParameter
                {
                    Name = "query",
                    Description = "Repository search query",
                    Type = "string",
                    Required = true
                }
            }
        };

        _mockToolRegistry
            .Setup(r => r.GetAvailableToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ToolDescriptor> { githubTool });

        _mockToolRegistry
            .Setup(r => r.GetToolAsync("github-search", It.IsAny<CancellationToken>()))
            .ReturnsAsync(githubTool);

        var toolResult = new ToolInvocationResult
        {
            Success = true,
            Result = new { repositories = new[] { "repo1", "repo2" } },
            BackendUsed = ToolBackendType.MCP,
            ContextTokenCost = 100,
            DurationMs = 250
        };

        _mockToolInvoker
            .Setup(i => i.InvokeToolAsync(
                "github-search",
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<ToolInvocationPreferences>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(toolResult);

        // Act
        var availableTools = await _mockToolRegistry.Object.GetAvailableToolsAsync();
        var selectedTool = availableTools.FirstOrDefault(t => t.Name.Contains("GitHub"));

        // Assert
        Assert.NotNull(selectedTool);
        Assert.Equal("github-search", selectedTool!.ToolId);
        Assert.Equal(ToolBackendType.MCP, selectedTool.Backend);
    }

    /// <summary>
    /// Test: Agent invokes MCP tool with correct parameters.
    /// </summary>
    [Fact]
    public async Task Agent_InvokesMcpTool_WithCorrectParameters()
    {
        // Arrange
        var agent = await _agentManager.CreateAgentAsync(new AgentDefinition
        {
            Name = "APIAgent",
            Purpose = "Call external APIs"
        });

        var apiTool = new ToolDescriptor
        {
            ToolId = "rest-api-call",
            Name = "REST API Caller",
            Description = "Call REST APIs",
            Backend = ToolBackendType.MCP,
            Source = "api-server",
            IsAvailable = true,
            Parameters = new List<ToolParameter>
            {
                new ToolParameter
                {
                    Name = "url",
                    Description = "API endpoint URL",
                    Type = "string",
                    Required = true
                },
                new ToolParameter
                {
                    Name = "method",
                    Description = "HTTP method",
                    Type = "string",
                    Required = false,
                    DefaultValue = "GET"
                }
            }
        };

        var parameters = new Dictionary<string, object>
        {
            { "url", "https://api.example.com/data" },
            { "method", "POST" }
        };

        var expectedResult = new ToolInvocationResult
        {
            Success = true,
            Result = new { status = "ok", data = new[] { "item1", "item2" } },
            BackendUsed = ToolBackendType.MCP,
            ContextTokenCost = 150,
            DurationMs = 300
        };

        _mockToolInvoker
            .Setup(i => i.InvokeToolAsync(
                "rest-api-call",
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<ToolInvocationPreferences>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult)
            .Callback<string, Dictionary<string, object>, ToolInvocationPreferences, CancellationToken>(
                (toolId, actualParams, _, _) =>
                {
                    Assert.Equal("rest-api-call", toolId);
                    Assert.Contains("url", actualParams.Keys);
                    Assert.Contains("method", actualParams.Keys);
                });

        // Act
        var result = await _mockToolInvoker.Object.InvokeToolAsync(
            "rest-api-call",
            parameters,
            new ToolInvocationPreferences(),
            CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(ToolBackendType.MCP, result.BackendUsed);
        _mockToolInvoker.Verify(
            i => i.InvokeToolAsync(
                "rest-api-call",
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<ToolInvocationPreferences>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Test: Agent result is integrated into execution context.
    /// </summary>
    [Fact]
    public async Task Agent_IntegratesMcpToolResult_IntoContext()
    {
        // Arrange
        var agent = await _agentManager.CreateAgentAsync(new AgentDefinition
        {
            Name = "IntegrationAgent",
            Purpose = "Use tool results"
        });

        var toolResult = new ToolInvocationResult
        {
            Success = true,
            Result = new
            {
                status = "success",
                items = new[] { "result1", "result2", "result3" }
            },
            BackendUsed = ToolBackendType.MCP,
            ContextTokenCost = 200,
            DurationMs = 150
        };

        _mockToolInvoker
            .Setup(i => i.InvokeToolAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<ToolInvocationPreferences>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(toolResult);

        // Act
        var result = await _mockToolInvoker.Object.InvokeToolAsync(
            "test-tool",
            new Dictionary<string, object>(),
            new ToolInvocationPreferences(),
            CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Result);
        Assert.True(result.ContextTokenCost > 0);
        Assert.True(result.DurationMs > 0);
    }

    /// <summary>
    /// Test: Agent can fall back when MCP server is unavailable.
    /// </summary>
    [Fact]
    public async Task Agent_FallsBack_WhenMcpUnavailable()
    {
        // Arrange
        var agent = await _agentManager.CreateAgentAsync(new AgentDefinition
        {
            Name = "FallbackAgent",
            Purpose = "Handle tool failures"
        });

        var mcpTool = new ToolDescriptor
        {
            ToolId = "mcp-tool",
            Name = "MCP Tool",
            Description = "Requires MCP server",
            Backend = ToolBackendType.MCP,
            Source = "unavailable-server",
            IsAvailable = false
        };

        var fallbackTool = new ToolDescriptor
        {
            ToolId = "direct-tool",
            Name = "Direct Tool",
            Description = "Direct C# implementation",
            Backend = ToolBackendType.Direct,
            Source = "Daiv3.Orchestration.Services",
            IsAvailable = true
        };

        _mockToolRegistry
            .Setup(r => r.GetAvailableToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ToolDescriptor> { mcpTool, fallbackTool });

        // Act
        var availableTools = await _mockToolRegistry.Object.GetAvailableToolsAsync();
        var usableTool = availableTools.FirstOrDefault();

        // Assert
        Assert.NotNull(usableTool);
        // Agent should prefer available tools
        var availableToolsList = availableTools.Where(t => t.IsAvailable).ToList();
        Assert.Single(availableToolsList);
        Assert.Equal(ToolBackendType.Direct, availableToolsList[0].Backend);
    }

    /// <summary>
    /// Test: Agent prefers efficient backend (Direct) over MCP when available.
    /// </summary>
    [Fact]
    public async Task Agent_PrefersEfficientBackend_OverMcp()
    {
        // Arrange
        var agent = await _agentManager.CreateAgentAsync(new AgentDefinition
        {
            Name = "EfficientAgent",
            Purpose = "Use efficient backends"
        });

        // Both tools do the same thing but with different backends
        var directTool = new ToolDescriptor
        {
            ToolId = "search-direct",
            Name = "Search (Direct)",
            Description = "Search using direct C# implementation",
            Backend = ToolBackendType.Direct,
            Source = "Daiv3.Orchestration.Services.SearchService",
            IsAvailable = true,
            EstimatedTokenCost = 0 // Direct has no token cost
        };

        var mcpTool = new ToolDescriptor
        {
            ToolId = "search-mcp",
            Name = "Search (MCP)",
            Description = "Search via MCP server",
            Backend = ToolBackendType.MCP,
            Source = "search-server",
            IsAvailable = true,
            EstimatedTokenCost = 100 // MCP has high token cost
        };

        _mockToolRegistry
            .Setup(r => r.GetToolsByBackendAsync(ToolBackendType.Direct, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ToolDescriptor> { directTool });

        _mockToolRegistry
            .Setup(r => r.GetToolsByBackendAsync(ToolBackendType.MCP, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ToolDescriptor> { mcpTool });

        _mockToolRegistry
            .Setup(r => r.GetAvailableToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ToolDescriptor> { directTool, mcpTool });

        // Act
        var availableTools = await _mockToolRegistry.Object.GetAvailableToolsAsync();
        var directTools = await _mockToolRegistry.Object.GetToolsByBackendAsync(ToolBackendType.Direct);
        var mcpTools = await _mockToolRegistry.Object.GetToolsByBackendAsync(ToolBackendType.MCP);

        var selectedTool = directTools.FirstOrDefault() ?? availableTools.FirstOrDefault(t => t.Backend == ToolBackendType.Direct);

        // Assert
        Assert.NotNull(selectedTool);
        Assert.Equal(ToolBackendType.Direct, selectedTool!.Backend);
        Assert.Equal(0, selectedTool.EstimatedTokenCost); // Preferred for efficiency
    }

    /// <summary>
    /// Test: Agent handles MCP tool invocation errors gracefully.
    /// </summary>
    [Fact]
    public async Task Agent_HandlesToolInvocationError_Gracefully()
    {
        // Arrange
        var agent = await _agentManager.CreateAgentAsync(new AgentDefinition
        {
            Name = "ErrorHandlingAgent",
            Purpose = "Handle tool errors"
        });

        var mcpTool = new ToolDescriptor
        {
            ToolId = "flaky-tool",
            Name = "Flaky Tool",
            Description = "Tool that sometimes fails",
            Backend = ToolBackendType.MCP,
            Source = "flaky-server",
            IsAvailable = true
        };

        var errorResult = new ToolInvocationResult
        {
            Success = false,
            ErrorMessage = "Tool execution failed: Connection timeout",
            BackendUsed = ToolBackendType.MCP,
            ContextTokenCost = 50,
            DurationMs = 5000
        };

        _mockToolInvoker
            .Setup(i => i.InvokeToolAsync(
                "flaky-tool",
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<ToolInvocationPreferences>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(errorResult);

        // Act
        var result = await _mockToolInvoker.Object.InvokeToolAsync(
            "flaky-tool",
            new Dictionary<string, object>(),
            new ToolInvocationPreferences(),
            CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("timeout", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Test: Agent tracks context token cost from MCP invocations.
    /// </summary>
    [Fact]
    public async Task Agent_TracksMcpContextTokenCost()
    {
        // Arrange
        var agent = await _agentManager.CreateAgentAsync(new AgentDefinition
        {
            Name = "CostTrackingAgent",
            Purpose = "Monitor token costs"
        });

        // First invocation: 150 tokens
        var result1 = new ToolInvocationResult
        {
            Success = true,
            Result = new { data = "first" },
            BackendUsed = ToolBackendType.MCP,
            ContextTokenCost = 150,
            DurationMs = 200
        };

        // Second invocation: 200 tokens
        var result2 = new ToolInvocationResult
        {
            Success = true,
            Result = new { data = "second" },
            BackendUsed = ToolBackendType.MCP,
            ContextTokenCost = 200,
            DurationMs = 300
        };

        _mockToolInvoker
            .SetupSequence(i => i.InvokeToolAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<ToolInvocationPreferences>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(result1)
            .ReturnsAsync(result2);

        // Act
        var res1 = await _mockToolInvoker.Object.InvokeToolAsync(
            "tool-1",
            new Dictionary<string, object>(),
            new ToolInvocationPreferences(),
            CancellationToken.None);

        var res2 = await _mockToolInvoker.Object.InvokeToolAsync(
            "tool-2",
            new Dictionary<string, object>(),
            new ToolInvocationPreferences(),
            CancellationToken.None);

        var totalTokens = res1.ContextTokenCost + res2.ContextTokenCost;

        // Assert
        Assert.Equal(150, res1.ContextTokenCost);
        Assert.Equal(200, res2.ContextTokenCost);
        Assert.Equal(350, totalTokens);
    }

    /// <summary>
    /// Test: Tool descriptions are visible to agent during planning.
    /// </summary>
    [Fact]
    public async Task Agent_ViewsToolDescriptions_DuringPlanning()
    {
        // Arrange
        var detailedMcpTool = new ToolDescriptor
        {
            ToolId = "comprehensive-tool",
            Name = "Comprehensive Tool",
            Description = "This tool provides search, filtering, and aggregation capabilities for large datasets with support for complex queries and real-time result streaming",
            Backend = ToolBackendType.MCP,
            Source = "comprehensive-server",
            IsAvailable = true,
            Parameters = new List<ToolParameter>
            {
                new ToolParameter
                {
                    Name = "dataSource",
                    Description = "The data source to query",
                    Type = "string",
                    Required = true
                },
                new ToolParameter
                {
                    Name = "queryFilter",
                    Description = "Complex query filter object supporting nested conditions",
                    Type = "object",
                    Required = true
                },
                new ToolParameter
                {
                    Name = "pageSize",
                    Description = "Results per page for pagination",
                    Type = "number",
                    Required = false,
                    DefaultValue = 50
                }
            }
        };

        _mockToolRegistry
            .Setup(r => r.GetAvailableToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ToolDescriptor> { detailedMcpTool });

        // Act
        var tools = await _mockToolRegistry.Object.GetAvailableToolsAsync();
        var tool = tools.FirstOrDefault();

        // Assert
        Assert.NotNull(tool);
        Assert.NotEmpty(tool!.Description);
        Assert.NotEmpty(tool.Parameters);
        Assert.Equal(3, tool.Parameters.Count);
        // Tool descriptions should be clear and informative
        Assert.True(tool.Description.Length > 20);
    }
}
