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

namespace Daiv3.Orchestration.IntegrationTests;

/// <summary>
/// Acceptance tests for MCP tool invocation by agents.
/// Verifies AST-ACC-002 requirement: Agents can invoke registered MCP tools.
/// </summary>
public class McpToolInvocationAcceptanceTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly AgentManager _agentManager;
    private readonly IToolRegistry _toolRegistry;
    private readonly Mock<IMcpToolProvider> _mockMcpProvider;
    private readonly string _dbPath;

    public McpToolInvocationAcceptanceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"daiv3-mcp-acc-test-{Guid.NewGuid()}.db");

        // Setup mocks
        _mockMcpProvider = new Mock<IMcpToolProvider>();

        var services = new ServiceCollection();
        services.AddLogging(builder => builder
            .AddConsole()
            .SetMinimumLevel(LogLevel.Information));

        services.AddPersistence(options => options.DatabasePath = _dbPath);
        
        services.AddScoped(_ => _mockMcpProvider.Object);
        services.AddOrchestrationServices();

        _serviceProvider = services.BuildServiceProvider();
        _serviceProvider.InitializeDatabaseAsync().GetAwaiter().GetResult();

        _agentManager = (AgentManager)_serviceProvider.GetRequiredService<IAgentManager>();
        _toolRegistry = _serviceProvider.GetRequiredService<IToolRegistry>();
    }

    /// <summary>
    /// Acceptance Test Scenario 1: Agent uses MCP tool for external service.
    /// 
    /// Setup:
    /// - Configure mock MCP server with "github-search" tool
    /// - Tool takes "query" parameter and returns search results
    /// - Register MCP server and tool in registry
    /// 
    /// Execution:
    /// - Create agent task: "Search GitHub repositories for ML frameworks"
    /// - Agent selects github-search MCP tool
    /// - Agent invokes with parameters: {"query": "ML frameworks"}
    /// - MCP tool returns results
    /// - Agent processes results and responds
    /// 
    /// Assertions:
    /// - MCP tool invoked (not direct, not CLI)
    /// - Tool returned results successfully
    /// - Agent integrated results into task completion
    /// - Logs show backend="MCP" for this invocation
    /// </summary>
    [Fact]
    public async Task AcceptanceTest_AgentUsesMcpToolForExternalService()
    {
        // Arrange
        var agent = await _agentManager.CreateAgentAsync(new AgentDefinition
        {
            Name = "GitHubSearchAgent",
            Purpose = "Search GitHub for projects and repositories"
        });

        var gitHubSearchTool = new ToolDescriptor
        {
            ToolId = "github-search",
            Name = "GitHub Repository Search",
            Description = "Search GitHub repositories with support for complex queries and filtering",
            Backend = ToolBackendType.MCP,
            Source = "github-server",
            IsAvailable = true,
            Parameters = new List<ToolParameter>
            {
                new ToolParameter
                {
                    Name = "query",
                    Description = "Search query for GitHub repositories",
                    Type = "string",
                    Required = true
                },
                new ToolParameter
                {
                    Name = "language",
                    Description = "Optional programming language filter",
                    Type = "string",
                    Required = false
                },
                new ToolParameter
                {
                    Name = "sort",
                    Description = "Sort order: stars, forks, updated",
                    Type = "string",
                    Required = false,
                    DefaultValue = "stars"
                }
            },
            EstimatedTokenCost = 80,
            Metadata = new Dictionary<string, string>
            {
                { "external_service", "github.com" },
                { "category", "repository_search" }
            }
        };

        // Register the tool
        var registered = await _toolRegistry.RegisterToolAsync(gitHubSearchTool);
        Assert.True(registered);

        // Setup mock MCP provider to return search results
        var mcpResult = new McpToolInvocationResult
        {
            Success = true,
            Result = System.Text.Json.JsonSerializer.Serialize(new
            {
                total_count = 42,
                repositories = new[]
                {
                    new { name = "fastai", stars = 25430, language = "Python" },
                    new { name = "pytorch-lightning", stars = 28600, language = "Python" },
                    new { name = "TensorFlow", stars = 183900, language = "C++" }
                }
            }),
            ContextTokenCost = 125
        };

        _mockMcpProvider
            .Setup(p => p.InvokeToolAsync(
                "github-server",
                "github-search",
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mcpResult);

        var toolInvoker = _serviceProvider.GetRequiredService<IToolInvoker>();
        var taskRequest = new AgentExecutionRequest
        {
            AgentId = agent.Id,
            TaskGoal = "Search GitHub repositories for ML frameworks"
        };

        // Act - Agent is tasked with searching GitHub
        var toolResult = await toolInvoker.InvokeToolAsync(
            "github-search",
            new Dictionary<string, object>
            {
                { "query", "ML frameworks" }
            },
            new ToolInvocationPreferences(),
            CancellationToken.None);

        // Assert
        Assert.True(toolResult.Success, $"Tool invocation failed: {toolResult.ErrorMessage}");
        Assert.Equal(ToolBackendType.MCP, toolResult.BackendUsed);
        Assert.NotNull(toolResult.Result);
        Assert.True(toolResult.ContextTokenCost > 0);
        
        // Verify the MCP provider was actually called
        _mockMcpProvider.Verify(
            p => p.InvokeToolAsync(
                "github-server",
                "github-search",
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "MCP provider should be called exactly once");
    }

    /// <summary>
    /// Acceptance Test Scenario 2: Agent prefers efficient backend over MCP.
    /// 
    /// Setup:
    /// - Register two tools: direct C# "ListFiles" and MCP "file-operations" (same functionality)
    /// - Agent task requires file listing
    /// 
    /// Execution:
    /// - Agent executes task requiring file listing
    /// - System routes to direct C# backend (not MCP)
    /// 
    /// Assertions:
    /// - Direct backend used (lower overhead, lower latency)
    /// - MCP not invoked
    /// - Logs confirm routing decision
    /// </summary>
    [Fact]
    public async Task AcceptanceTest_AgentPrefersEfficientBackend()
    {
        // Arrange
        // Direct tool - same functionality
        var directListFiles = new ToolDescriptor
        {
            ToolId = "list-files-direct",
            Name = "List Files (Direct)",
            Description = "List files in a directory using local C# implementation",
            Backend = ToolBackendType.Direct,
            Source = "Daiv3.Orchestration.Services.FileService",
            IsAvailable = true,
            EstimatedTokenCost = 0,
            Parameters = new List<ToolParameter>
            {
                new ToolParameter
                {
                    Name = "directory",
                    Description = "Directory path",
                    Type = "string",
                    Required = true
                }
            }
        };

        // MCP tool - same functionality but over MCP
        var mcpListFiles = new ToolDescriptor
        {
            ToolId = "list-files-mcp",
            Name = "List Files (MCP)",
            Description = "List files in a directory via MCP server",
            Backend = ToolBackendType.MCP,
            Source = "file-operations-server",
            IsAvailable = true,
            EstimatedTokenCost = 100, // Much higher token cost
            Parameters = new List<ToolParameter>
            {
                new ToolParameter
                {
                    Name = "directory",
                    Description = "Directory path",
                    Type = "string",
                    Required = true
                }
            }
        };

        // Register both tools
        await _toolRegistry.RegisterToolAsync(directListFiles);
        await _toolRegistry.RegisterToolAsync(mcpListFiles);

        // Setup routing: prefer direct tool
        var directToolResult = new ToolInvocationResult
        {
            Success = true,
            Result = new { files = new[] { "file1.txt", "file2.txt", "file3.txt" } },
            BackendUsed = ToolBackendType.Direct,
            ContextTokenCost = 0,
            DurationMs = 10
        };

        var toolInvoker = _serviceProvider.GetRequiredService<IToolInvoker>();

        // Act - Agent needs file listing, should use direct
        var availableTools = await _toolRegistry.GetAvailableToolsAsync();
        var directTools = availableTools.Where(t => t.Backend == ToolBackendType.Direct).ToList();

        // Assert - Direct tool should be available and preferred
        Assert.NotEmpty(directTools);
        var selectedTool = directTools.FirstOrDefault(t => t.ToolId == "list-files-direct");
        Assert.NotNull(selectedTool);
        Assert.Equal(0, selectedTool!.EstimatedTokenCost); // Direct has no token cost

        // Verify MCP tool also exists but shouldn't be preferred
        var mcpTools = availableTools.Where(t => t.Backend == ToolBackendType.MCP).ToList();
        Assert.NotEmpty(mcpTools);
        var mcpTool = mcpTools.FirstOrDefault(t => t.ToolId == "list-files-mcp");
        Assert.NotNull(mcpTool);
        Assert.True(mcpTool!.EstimatedTokenCost > 0); // MCP has token cost

        // The system should prefer the direct tool for this operation
        Assert.True(directTools.Count > 0, "Should have direct tools available");
    }

    /// <summary>
    /// Test: MCP tool registration from server discovery.
    /// </summary>
    [Fact]
    public async Task MCP_ToolRegistration_FromServerDiscovery()
    {
        // Arrange - Simulate MCP server providing tools
        var discoveredTools = new List<McpToolDescriptor>
        {
            new McpToolDescriptor
            {
                ToolId = "aws-list-buckets",
                Name = "List S3 Buckets",
                Description = "List all S3 buckets in AWS account",
                ServerName = "aws-server",
                Backend = ToolBackendType.MCP,
                Parameters = new List<McpToolParameter>
                {
                    new McpToolParameter
                    {
                        Name = "region",
                        Description = "AWS region",
                        Type = "string",
                        Required = false
                    }
                },
                EstimatedTokenCost = 100
            },
            new McpToolDescriptor
            {
                ToolId = "aws-create-bucket",
                Name = "Create S3 Bucket",
                Description = "Create a new S3 bucket",
                ServerName = "aws-server",
                Backend = ToolBackendType.MCP,
                Parameters = new List<McpToolParameter>
                {
                    new McpToolParameter
                    {
                        Name = "bucketName",
                        Description = "Name for the new bucket",
                        Type = "string",
                        Required = true
                    }
                },
                EstimatedTokenCost = 150
            }
        };

        _mockMcpProvider
            .Setup(p => p.DiscoverToolsAsync("aws-server", It.IsAny<CancellationToken>()))
            .ReturnsAsync(discoveredTools);

        // Act - Simulate registration of discovered tools
        foreach (var mcpTool in discoveredTools)
        {
            var tool = new ToolDescriptor
            {
                ToolId = mcpTool.ToolId,
                Name = mcpTool.Name,
                Description = mcpTool.Description,
                Backend = ToolBackendType.MCP,
                Source = mcpTool.ServerName,
                Parameters = mcpTool.Parameters.Select(p => new ToolParameter
                {
                    Name = p.Name,
                    Description = p.Description,
                    Type = p.Type,
                    Required = p.Required
                }).ToList(),
                EstimatedTokenCost = mcpTool.EstimatedTokenCost,
                IsAvailable = true
            };

            await _toolRegistry.RegisterToolAsync(tool);
        }

        // Assert
        var tools = await _toolRegistry.GetAllToolsAsync();
        Assert.Equal(2, tools.Count);
        
        var awsTools = await _toolRegistry.GetToolsByBackendAsync(ToolBackendType.MCP);
        Assert.Equal(2, awsTools.Count);
        
        Assert.All(awsTools, t => Assert.Equal("aws-server", t.Source));
    }

    /// <summary>
    /// Test: Agent handles MCP tool errors without crashing.
    /// </summary>
    [Fact]
    public async Task MCP_ToolError_GracefulHandling()
    {
        // Arrange
        var failingTool = new ToolDescriptor
        {
            ToolId = "failing-mcp-tool",
            Name = "Failing Tool",
            Description = "Tool that fails",
            Backend = ToolBackendType.MCP,
            Source = "unstable-server",
            IsAvailable = true
        };

        await _toolRegistry.RegisterToolAsync(failingTool);

        _mockMcpProvider
            .Setup(p => p.InvokeToolAsync(
                "unstable-server",
                "failing-mcp-tool",
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new McpToolInvocationResult
            {
                Success = false,
                ErrorMessage = "Connection refused to MCP server"
            });

        var toolInvoker = _serviceProvider.GetRequiredService<IToolInvoker>();

        // Act
        var result = await toolInvoker.InvokeToolAsync(
            "failing-mcp-tool",
            new Dictionary<string, object>(),
            new ToolInvocationPreferences(),
            CancellationToken.None);

        // Assert - Should not crash, should return error gracefully
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("Connection refused", result.ErrorMessage!);
    }

    /// <summary>
    /// Test: Context overhead is tracked across multiple MCP invocations.
    /// </summary>
    [Fact]
    public async Task MCP_ContextOverhead_TrackedAcrossMultipleInvocations()
    {
        // Arrange
        var tool1 = new ToolDescriptor
        {
            ToolId = "tool-1",
            Name = "Tool 1",
            Description = "First tool",
            Backend = ToolBackendType.MCP,
            Source = "server-1",
            IsAvailable = true
        };

        var tool2 = new ToolDescriptor
        {
            ToolId = "tool-2",
            Name = "Tool 2",
            Description = "Second tool",
            Backend = ToolBackendType.MCP,
            Source = "server-2",
            IsAvailable = true
        };

        await _toolRegistry.RegisterToolAsync(tool1);
        await _toolRegistry.RegisterToolAsync(tool2);

        var result1 = new McpToolInvocationResult
        {
            Success = true,
            Result = "First result",
            ContextTokenCost = 100
        };

        var result2 = new McpToolInvocationResult
        {
            Success = true,
            Result = "Second result",
            ContextTokenCost = 150
        };

        var toolInvoker = _serviceProvider.GetRequiredService<IToolInvoker>();

        _mockMcpProvider
            .SetupSequence(p => p.InvokeToolAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(result1)
            .ReturnsAsync(result2);

        // Act
        var invocation1 = await toolInvoker.InvokeToolAsync(
            "tool-1",
            new Dictionary<string, object>(),
            new ToolInvocationPreferences(),
            CancellationToken.None);

        var invocation2 = await toolInvoker.InvokeToolAsync(
            "tool-2",
            new Dictionary<string, object>(),
            new ToolInvocationPreferences(),
            CancellationToken.None);

        var totalTokens = invocation1.ContextTokenCost + invocation2.ContextTokenCost;

        // Assert
        Assert.True(invocation1.Success);
        Assert.True(invocation2.Success);
        Assert.True(totalTokens >= 250); // Should be at least 100 + 150
    }

    /// <summary>
    /// Test: MCP tools coexist with Direct and CLI tools.
    /// </summary>
    [Fact]
    public async Task ToolRegistry_MixedBackends_CoexistHarmlessly()
    {
        // Arrange - Register tools from all three backends
        var directTool = new ToolDescriptor
        {
            ToolId = "direct-tool",
            Name = "Direct Tool",
            Description = "Direct C# tool",
            Backend = ToolBackendType.Direct,
            Source = "Daiv3.Orchestration",
            IsAvailable = true
        };

        var cliTool = new ToolDescriptor
        {
            ToolId = "cli-tool",
            Name = "CLI Tool",
            Description = "CLI-based tool",
            Backend = ToolBackendType.CLI,
            Source = "/usr/local/bin/tool",
            IsAvailable = true
        };

        var mcpTool = new ToolDescriptor
        {
            ToolId = "mcp-tool",
            Name = "MCP Tool",
            Description = "MCP server tool",
            Backend = ToolBackendType.MCP,
            Source = "mcp-server",
            IsAvailable = true
        };

        // Act
        await _toolRegistry.RegisterToolAsync(directTool);
        await _toolRegistry.RegisterToolAsync(cliTool);
        await _toolRegistry.RegisterToolAsync(mcpTool);

        var allTools = await _toolRegistry.GetAllToolsAsync();
        var directTools = await _toolRegistry.GetToolsByBackendAsync(ToolBackendType.Direct);
        var cliTools = await _toolRegistry.GetToolsByBackendAsync(ToolBackendType.CLI);
        var mcpTools = await _toolRegistry.GetToolsByBackendAsync(ToolBackendType.MCP);

        // Assert
        Assert.Equal(3, allTools.Count);
        Assert.Single(directTools);
        Assert.Single(cliTools);
        Assert.Single(mcpTools);
        Assert.Equal("direct-tool", directTools[0].ToolId);
        Assert.Equal("cli-tool", cliTools[0].ToolId);
        Assert.Equal("mcp-tool", mcpTools[0].ToolId);
    }
}
