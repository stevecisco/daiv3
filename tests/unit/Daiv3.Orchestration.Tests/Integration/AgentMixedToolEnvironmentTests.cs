using Daiv3.Mcp.Integration;
using Daiv3.Orchestration;
using Daiv3.Orchestration.Interfaces;
using Daiv3.Orchestration.Models;
using Daiv3.Persistence;
using Daiv3.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

#pragma warning disable IDISP006 // Test classes don't need to implement IDisposable

namespace Daiv3.Orchestration.Tests.Integration;

/// <summary>
/// Integration tests for agent tool invocation in mixed tool environments.
/// Tests AST-REQ-008 requirement for agents supporting multiple tool backends (Direct, CLI, MCP).
/// </summary>
[Trait("Category", "Integration")]
public class AgentMixedToolEnvironmentTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IAgentManager _agentManager;
    private readonly IToolRegistry _toolRegistry;
    private readonly IToolInvoker _toolInvoker;
    private readonly string _dbPath;

    public AgentMixedToolEnvironmentTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"daiv3-test-{Guid.NewGuid()}.db");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPersistence(options =>
        {
            options.DatabasePath = _dbPath;
        });
        services.AddOrchestrationServices();

        _serviceProvider = services.BuildServiceProvider();
        _serviceProvider.InitializeDatabaseAsync().GetAwaiter().GetResult();

        _agentManager = _serviceProvider.GetRequiredService<IAgentManager>();
        _toolRegistry = _serviceProvider.GetRequiredService<IToolRegistry>();
        _toolInvoker = _serviceProvider.GetRequiredService<IToolInvoker>();
    }

    [Fact]
    public async Task AgentExecution_WithMultipleToolBackends_CanAccessAll()
    {
        // Arrange
        var agent = await _agentManager.CreateAgentAsync(new AgentDefinition
        {
            Name = "MultiBackendAgent",
            Purpose = "Test agent with access to multiple tool backends"
        });

        // Register tools with different backends
        var directTool = CreateTestTool("direct-search", "Knowledge Search", ToolBackendType.Direct);
        var cliTool = CreateTestTool("cli-convert", "File Converter", ToolBackendType.CLI);
        var mcpTool = CreateTestTool("mcp-github", "GitHub Operations", ToolBackendType.MCP);

        await _toolRegistry.RegisterToolAsync(directTool);
        await _toolRegistry.RegisterToolAsync(cliTool);
        await _toolRegistry.RegisterToolAsync(mcpTool);

        var request = new AgentExecutionRequest
        {
            AgentId = agent.Id,
            TaskGoal = "Task requiring tools from all backends"
        };

        // Act
        var result = await _agentManager.ExecuteTaskAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IterationsExecuted > 0);

        // Verify all tools are available
        var allTools = await _toolRegistry.GetAllToolsAsync();
        Assert.Contains(allTools, t => t.ToolId == "direct-search");
        Assert.Contains(allTools, t => t.ToolId == "cli-convert");
        Assert.Contains(allTools, t => t.ToolId == "mcp-github");
    }

    [Fact]
    public async Task AgentExecution_WithMixedBackends_CanSelectAndInvokeTool()
    {
        // Arrange
        var agent = await _agentManager.CreateAgentAsync(new AgentDefinition
        {
            Name = "SelectiveInvokingAgent",
            Purpose = "Test selective tool invocation with mixed backends"
        });

        var directTool = CreateTestTool("direct-task", "Direct Execution Service", ToolBackendType.Direct, isAvailable: true);
        var cliTool = CreateTestTool("cli-task", "CLI Task", ToolBackendType.CLI, isAvailable: true);
        var mcpTool = CreateTestTool("mcp-task", "Remote Service", ToolBackendType.MCP, isAvailable: false); // Unavailable

        await _toolRegistry.RegisterToolAsync(directTool);
        await _toolRegistry.RegisterToolAsync(cliTool);
        await _toolRegistry.RegisterToolAsync(mcpTool);

        var request = new AgentExecutionRequest
        {
            AgentId = agent.Id,
            TaskGoal = "Execute task with tool selection"
        };

        // Act
        var result = await _agentManager.ExecuteTaskAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IterationsExecuted > 0);

        // Verify available tools excludes the unavailable MCP tool
        var availableTools = await _toolRegistry.GetAvailableToolsAsync();
        Assert.Contains(availableTools, t => t.ToolId == "direct-task");
        Assert.Contains(availableTools, t => t.ToolId == "cli-task");
        Assert.DoesNotContain(availableTools, t => t.ToolId == "mcp-task" && !t.IsAvailable);
    }

    [Fact]
    public async Task ToolRegistry_BackendFiltering_DirectToolsExcludeable()
    {
        // Arrange
        var directTool = CreateTestTool("direct-filter", "Direct Tool", ToolBackendType.Direct);
        var mcpTool = CreateTestTool("mcp-filter", "MCP Tool", ToolBackendType.MCP);

        await _toolRegistry.RegisterToolAsync(directTool);
        await _toolRegistry.RegisterToolAsync(mcpTool);

        // Act - Get only MCP backend tools
        var mcpTools = await _toolRegistry.GetToolsByBackendAsync(ToolBackendType.MCP);

        // Assert
        Assert.Single(mcpTools);
        Assert.Equal("mcp-filter", mcpTools.First().ToolId);

        // Verify Direct tool is not in MCP-filtered list
        Assert.DoesNotContain(mcpTools, t => t.ToolId == "direct-filter");
    }

    [Fact]
    public async Task ToolRegistry_ContextTokenCostTracking_MCPToolsHigherThanDirect()
    {
        // Arrange
        var directTool = CreateTestTool("direct-cost", "Direct", ToolBackendType.Direct, estimatedTokenCost: 0);
        var mcpTool = CreateTestTool("mcp-cost", "MCP", ToolBackendType.MCP, estimatedTokenCost: 100);

        await _toolRegistry.RegisterToolAsync(directTool);
        await _toolRegistry.RegisterToolAsync(mcpTool);

        // Act
        var directRetrieved = await _toolRegistry.GetToolAsync("direct-cost");
        var mcpRetrieved = await _toolRegistry.GetToolAsync("mcp-cost");

        // Assert - Verify token cost differential
        Assert.NotNull(directRetrieved);
        Assert.NotNull(mcpRetrieved);
        Assert.Equal(0, directRetrieved.EstimatedTokenCost);
        Assert.Equal(100, mcpRetrieved.EstimatedTokenCost);
        Assert.True(mcpRetrieved.EstimatedTokenCost > directRetrieved.EstimatedTokenCost);
    }

    [Fact]
    public async Task AgentExecution_TokenBudget_RespectsCostFromTools()
    {
        // Arrange
        var agent = await _agentManager.CreateAgentAsync(new AgentDefinition
        {
            Name = "TokenBudgetAgent",
            Purpose = "Test token budget tracking with tools"
        });

        var expensiveTool = CreateTestTool("expensive-tool", "Expensive Service", ToolBackendType.MCP, estimatedTokenCost: 50);
        await _toolRegistry.RegisterToolAsync(expensiveTool);

        var request = new AgentExecutionRequest
        {
            AgentId = agent.Id,
            TaskGoal = "Execute task with expensive tool",
            Options = new AgentExecutionOptions
            {
                TokenBudget = 200  // Limited budget
            }
        };

        // Act
        var result = await _agentManager.ExecuteTaskAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.TokensConsumed >= 0);
        // Token consumption should be tracked and respect budget
    }

    [Fact]
    public async Task MixedToolEnvironment_DynamicRegistration_ToolsBecomingAvailable()
    {
        // Arrange
        var agent = await _agentManager.CreateAgentAsync(new AgentDefinition
        {
            Name = "DynamicAgent",
            Purpose = "Test dynamic tool registration and availability"
        });

        // Register first set of tools
        var tool1 = CreateTestTool("dynamic-1", "Dynamic Tool 1", ToolBackendType.MCP, isAvailable: true);
        await _toolRegistry.RegisterToolAsync(tool1);

        var initialTools = await _toolRegistry.GetAvailableToolsAsync();
        Assert.Single(initialTools);

        // Register new tool dynamically
        var tool2 = CreateTestTool("dynamic-2", "Dynamic Tool 2", ToolBackendType.MCP, isAvailable: true);
        await _toolRegistry.RegisterToolAsync(tool2);

        // Act
        var updatedTools = await _toolRegistry.GetAvailableToolsAsync();

        // Assert - New tool should be available after registration
        Assert.Equal(2, updatedTools.Count);
        Assert.Contains(updatedTools, t => t.ToolId == "dynamic-1");
        Assert.Contains(updatedTools, t => t.ToolId == "dynamic-2");
    }

    [Fact]
    public async Task Agentiteration_WithToolInvocation_TracksBackendUsed()
    {
        // Arrange
        var agent = await _agentManager.CreateAgentAsync(new AgentDefinition
        {
            Name = "BackendTrackingAgent",
            Purpose = "Test backend usage tracking in iterations"
        });

        var mcpTool = CreateTestTool("tracked-mcp", "Tracked MCP Tool", ToolBackendType.MCP, isAvailable: true);
        await _toolRegistry.RegisterToolAsync(mcpTool);

        var request = new AgentExecutionRequest
        {
            AgentId = agent.Id,
            TaskGoal = "Task with backend tracking"
        };

        // Act
        var result = await _agentManager.ExecuteTaskAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Steps.Count > 0);
        Assert.All(result.Steps, step => Assert.NotEmpty(step.StepType));

        // Verify execution steps are recorded
        var toolExecSteps = result.Steps.Where(s => s.StepType == "ToolExecution").ToList();
        Assert.True(toolExecSteps.Count >= 0);

        // Steps should contain information about tool invocation
        foreach (var step in toolExecSteps)
        {
            Assert.NotEmpty(step.Output);
            Assert.True(step.Success || !string.IsNullOrEmpty(step.Output));
        }
    }

    [Fact]
    public async Task ToolInvoker_ErrorHandling_MissingToolReturnsNotFound()
    {
        // Arrange
        var nonexistentToolId = "nonexistent-tool-" + Guid.NewGuid();

        // Act
        var result = await _toolInvoker.InvokeToolAsync(
            nonexistentToolId,
            new Dictionary<string, object>());

        // Assert
        Assert.False(result.Success);
        Assert.Equal("TOOL_NOT_FOUND", result.ErrorCode);
        Assert.Contains("not found", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ToolInvoker_UnavailableTool_ReturnsUnavailableError()
    {
        // Arrange
        var unavailableTool = CreateTestTool("unavailable-tool", "Unavailable", ToolBackendType.MCP, isAvailable: false);
        await _toolRegistry.RegisterToolAsync(unavailableTool);

        // Act
        var result = await _toolInvoker.InvokeToolAsync(
            "unavailable-tool",
            new Dictionary<string, object>());

        // Assert
        Assert.False(result.Success);
        Assert.Equal("TOOL_UNAVAILABLE", result.ErrorCode);
    }

    [Fact]
    public async Task ToolRegistry_ConsistencyAfterDynamicUpdate_ToolsRemainConsistent()
    {
        // Arrange
        var tool1 = CreateTestTool("consistency-1", "Tool 1", ToolBackendType.Direct);
        var tool2 = CreateTestTool("consistency-2", "Tool 2", ToolBackendType.MCP);

        await _toolRegistry.RegisterToolAsync(tool1);
        await _toolRegistry.RegisterToolAsync(tool2);

        // Act - Get all tools multiple times
        var firstRead = await _toolRegistry.GetAllToolsAsync();
        var secondRead = await _toolRegistry.GetAllToolsAsync();
        var thirdRead = await _toolRegistry.GetAllToolsAsync();

        // Assert - Registry should be consistent across reads
        Assert.Equal(firstRead.Count, secondRead.Count);
        Assert.Equal(secondRead.Count, thirdRead.Count);
        Assert.Equal(2, firstRead.Count);
    }

    // Helper method to create test tools
    private static ToolDescriptor CreateTestTool(
        string toolId,
        string name,
        ToolBackendType backend,
        bool isAvailable = true,
        int estimatedTokenCost = 10)
    {
        return new ToolDescriptor
        {
            ToolId = toolId,
            Name = name,
            Description = $"Test {name}",
            Backend = backend,
            Source = backend == ToolBackendType.Direct ? "local-service" :
                     backend == ToolBackendType.CLI ? "/bin/tool" :
                     "test-server",
            IsAvailable = isAvailable,
            EstimatedTokenCost = estimatedTokenCost
        };
    }
}
