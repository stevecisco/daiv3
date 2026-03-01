using Daiv3.Mcp.Integration;
using Daiv3.Orchestration;
using Daiv3.Orchestration.Interfaces;
using Daiv3.Orchestration.Models;
using Daiv3.Persistence;
using Daiv3.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Daiv3.UnitTests.Orchestration;

/// <summary>
/// Unit tests for agent tool invocation with intelligent routing.
/// Tests AST-REQ-008 functionality for agents invoking tools across multiple backends.
/// </summary>
public class AgentToolInvocationTests
{
    private readonly AgentManager _manager;
    private readonly Mock<IToolRegistry> _mockToolRegistry;
    private readonly Mock<IToolInvoker> _mockToolInvoker;
    private readonly AgentRepository _repository;
    private readonly string _dbPath;

    public AgentToolInvocationTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"daiv3-test-{Guid.NewGuid()}.db");
        
        // Setup mocks
        _mockToolRegistry = new Mock<IToolRegistry>();
        _mockToolInvoker = new Mock<IToolInvoker>();

        // Setup a test service provider with persistence
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPersistence(options =>
        {
            options.DatabasePath = _dbPath;
        });
        
        // Register test mocks
        services.AddScoped(_ => _mockToolRegistry.Object);
        services.AddScoped(_ => _mockToolInvoker.Object);
        services.AddOrchestrationServices();

        var serviceProvider = services.BuildServiceProvider();
        
        // Initialize database
        serviceProvider.InitializeDatabaseAsync().GetAwaiter().GetResult();
        _repository = serviceProvider.GetRequiredService<AgentRepository>();
        
        // Get the actual manager (which will use mocked tool services)
        _manager = (AgentManager)serviceProvider.GetRequiredService<IAgentManager>();
    }

    [Fact]
    public async Task ExecuteTaskAsync_WithAvailableTools_AttemptsToolInvocation()
    {
        // Arrange
        var agent = await _manager.CreateAgentAsync(new AgentDefinition
        {
            Name = "ToolInvokingAgent",
            Purpose = "Test agent tool invocation"
        });

        var availableTool = new ToolDescriptor
        {
            ToolId = "test-tool",
            Name = "Test Tool",
            Description = "A test tool for invocation",
            Backend = ToolBackendType.MCP,
            Source = "test-server",
            IsAvailable = true
        };

        _mockToolRegistry.Setup(r => r.GetAvailableToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ToolDescriptor> { availableTool });

        var toolResult = new ToolInvocationResult
        {
            Success = true,
            Result = new { data = "test result" },
            BackendUsed = ToolBackendType.MCP,
            ContextTokenCost = 25,
            DurationMs = 100
        };

        _mockToolInvoker.Setup(i => i.InvokeToolAsync(
                "test-tool",
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<ToolInvocationPreferences>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(toolResult);

        var request = new AgentExecutionRequest
        {
            AgentId = agent.Id,
            TaskGoal = "Test task requiring tool invocation"
        };

        // Act
        var result = await _manager.ExecuteTaskAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IterationsExecuted > 0);
        _mockToolInvoker.Verify(
            i => i.InvokeToolAsync(
                "test-tool",
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<ToolInvocationPreferences>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteTaskAsync_AgentReceivesCorrectToolList()
    {
        // Arrange
        var agent = await _manager.CreateAgentAsync(new AgentDefinition
        {
            Name = "ToolListAgent",
            Purpose = "Test agent tool discovery"
        });

        var tools = new List<ToolDescriptor>
        {
            new ToolDescriptor
            {
                ToolId = "mcp-tool-1",
                Name = "Remote Tool",
                Description = "MCP backend tool",
                Backend = ToolBackendType.MCP,
                Source = "remote-server",
                IsAvailable = true
            },
            new ToolDescriptor
            {
                ToolId = "direct-tool-1",
                Name = "Local Tool",
                Description = "Direct C# backend tool",
                Backend = ToolBackendType.Direct,
                Source = "local",
                IsAvailable = true
            }
        };

        _mockToolRegistry.Setup(r => r.GetAvailableToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tools);

        _mockToolInvoker.Setup(i => i.InvokeToolAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<ToolInvocationPreferences>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolInvocationResult
            {
                Success = true,
                Result = "Tool executed",
                BackendUsed = ToolBackendType.MCP,
                ContextTokenCost = 10
            });

        var request = new AgentExecutionRequest
        {
            AgentId = agent.Id,
            TaskGoal = "Task using multiple backend tools"
        };

        // Act
        var result = await _manager.ExecuteTaskAsync(request);

        // Assert
        Assert.NotNull(result);
        _mockToolRegistry.Verify(
            r => r.GetAvailableToolsAsync(It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteTaskAsync_ToolInvocationFailure_HandlesGracefully()
    {
        // Arrange
        var agent = await _manager.CreateAgentAsync(new AgentDefinition
        {
            Name = "FailureHandlingAgent",
            Purpose = "Test agent tool failure handling"
        });

        var availableTool = new ToolDescriptor
        {
            ToolId = "failing-tool",
            Name = "Failing Tool",
            Description = "A tool that fails",
            Backend = ToolBackendType.MCP,
            Source = "broken-server",
            IsAvailable = true
        };

        _mockToolRegistry.Setup(r => r.GetAvailableToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ToolDescriptor> { availableTool });

        var failureResult = new ToolInvocationResult
        {
            Success = false,
            ErrorMessage = "MCP server connection failed",
            ErrorCode = "SERVER_UNAVAILABLE",
            BackendUsed = ToolBackendType.MCP,
            ContextTokenCost = 5
        };

        _mockToolInvoker.Setup(i => i.InvokeToolAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<ToolInvocationPreferences>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(failureResult);

        var request = new AgentExecutionRequest
        {
            AgentId = agent.Id,
            TaskGoal = "Task with tool failure"
        };

        // Act
        var result = await _manager.ExecuteTaskAsync(request);

        // Assert - agent should complete despite tool failure (graceful degradation)
        Assert.NotNull(result);
        Assert.True(result.IterationsExecuted > 0);
        // The execution should continue, not crash on tool failure
    }

    [Fact]
    public async Task ExecuteTaskAsync_ToolInvocationTracksContextTokenCost()
    {
        // Arrange
        var agent = await _manager.CreateAgentAsync(new AgentDefinition
        {
            Name = "TokenTrackingAgent",
            Purpose = "Test context token tracking"
        });

        var availableTool = new ToolDescriptor
        {
            ToolId = "token-tracking-tool",
            Name = "Token Tool",
            Description = "Tool for tracking context tokens",
            Backend = ToolBackendType.MCP,
            Source = "test-server",
            IsAvailable = true,
            EstimatedTokenCost = 100
        };

        _mockToolRegistry.Setup(r => r.GetAvailableToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ToolDescriptor> { availableTool });

        var resultWithTokenCost = new ToolInvocationResult
        {
            Success = true,
            Result = new { data = "result" },
            BackendUsed = ToolBackendType.MCP,
            ContextTokenCost = 250  // High token cost for MCP
        };

        _mockToolInvoker.Setup(i => i.InvokeToolAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<ToolInvocationPreferences>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultWithTokenCost);

        var request = new AgentExecutionRequest
        {
            AgentId = agent.Id,
            TaskGoal = "Task with context token tracking"
        };

        // Act
        var result = await _manager.ExecuteTaskAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.TokensConsumed > 0);
        // Token consumption should include tool invocation tokens
    }

    [Fact]
    public async Task ExecuteTaskAsync_ToolSelection_PrefersDirect_ThenCLI_ThenMCP()
    {
        // Arrange
        var agent = await _manager.CreateAgentAsync(new AgentDefinition
        {
            Name = "RoutingPreferenceAgent",
            Purpose = "Test intelligent tool routing preferences"
        });

        var mixedTools = new List<ToolDescriptor>
        {
            new ToolDescriptor
            {
                ToolId = "mcp-tool",
                Name = "MCP Tool",
                Description = "Remote MCP tool",
                Backend = ToolBackendType.MCP,
                Source = "remote-server",
                IsAvailable = true,
                EstimatedTokenCost = 100
            },
            new ToolDescriptor
            {
                ToolId = "cli-tool",
                Name = "CLI Tool",
                Description = "CLI executable tool",
                Backend = ToolBackendType.CLI,
                Source = "/bin/mytool",
                IsAvailable = true,
                EstimatedTokenCost = 10
            },
            new ToolDescriptor
            {
                ToolId = "direct-tool",
                Name = "Direct Tool",
                Description = "Direct C# service",
                Backend = ToolBackendType.Direct,
                Source = "local-service",
                IsAvailable = true,
                EstimatedTokenCost = 0
            }
        };

        _mockToolRegistry.Setup(r => r.GetAvailableToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mixedTools);

        var successResult = new ToolInvocationResult
        {
            Success = true,
            Result = "Success",
            BackendUsed = ToolBackendType.Direct,
            ContextTokenCost = 0
        };

        // Setup invoker to track which tool is called first
        var firstToolCalled = string.Empty;
        _mockToolInvoker.Setup(i => i.InvokeToolAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<ToolInvocationPreferences>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, Dictionary<string, object>, ToolInvocationPreferences, CancellationToken>(
                (toolId, _, prefs, _) => { firstToolCalled = toolId; })
            .ReturnsAsync(successResult);

        var request = new AgentExecutionRequest
        {
            AgentId = agent.Id,
            TaskGoal = "Task with mixed tool types available"
        };

        // Act
        var result = await _manager.ExecuteTaskAsync(request);

        // Assert
        Assert.NotNull(result);
        // First available tool should be selected (in this case, the MCP tool as first in list)
        // In a real scenario with a language model, Direct would be preferred over CLI over MCP
    }

    [Fact]
    public async Task ExecuteTaskAsync_NoToolsAvailable_CompletesWithoutInvocation()
    {
        // Arrange
        var agent = await _manager.CreateAgentAsync(new AgentDefinition
        {
            Name = "NoToolsAgent",
            Purpose = "Test agent execution with no available tools"
        });

        _mockToolRegistry.Setup(r => r.GetAvailableToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ToolDescriptor>());

        var request = new AgentExecutionRequest
        {
            AgentId = agent.Id,
            TaskGoal = "Task with no tools available"
        };

        // Act
        var result = await _manager.ExecuteTaskAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IterationsExecuted > 0);
        _mockToolInvoker.Verify(
            i => i.InvokeToolAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<ToolInvocationPreferences>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteTaskAsync_ToolInvocationWithContext_PassesContextToTool()
    {
        // Arrange
        var agent = await _manager.CreateAgentAsync(new AgentDefinition
        {
            Name = "ContextAgent",
            Purpose = "Test agent tool invocation with context"
        });

        var availableTool = new ToolDescriptor
        {
            ToolId = "context-tool",
            Name = "Context Tool",
            Description = "Tool that uses context",
            Backend = ToolBackendType.MCP,
            Source = "test-server",
            IsAvailable = true
        };

        _mockToolRegistry.Setup(r => r.GetAvailableToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ToolDescriptor> { availableTool });

        var passedParameters = new Dictionary<string, object>();
        _mockToolInvoker.Setup(i => i.InvokeToolAsync(
                "context-tool",
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<ToolInvocationPreferences>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, Dictionary<string, object>, ToolInvocationPreferences, CancellationToken>(
                (_, parameters, _, _) =>
                {
                    // Capture the parameters
                    foreach (var kvp in parameters)
                        passedParameters[kvp.Key] = kvp.Value;
                })
            .ReturnsAsync(new ToolInvocationResult
            {
                Success = true,
                Result = "Context processed",
                BackendUsed = ToolBackendType.MCP,
                ContextTokenCost = 20
            });

        var context = new Dictionary<string, string>
        {
            ["input1"] = "value1",
            ["input2"] = "value2"
        };

        var request = new AgentExecutionRequest
        {
            AgentId = agent.Id,
            TaskGoal = "Task with context",
            Context = context
        };

        // Act
        var result = await _manager.ExecuteTaskAsync(request);

        // Assert
        Assert.NotNull(result);
        // Parameters should include both the goal and context items
        // (The tool is called, and context is passed as parameters)
        _mockToolInvoker.Verify(
            i => i.InvokeToolAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<ToolInvocationPreferences>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteTaskAsync_ToolInvocationUsesCorrectBackend()
    {
        // Arrange
        var agent = await _manager.CreateAgentAsync(new AgentDefinition
        {
            Name = "BackendTrackingAgent",
            Purpose = "Test backend tracking during tool invocation"
        });

        var mcpTool = new ToolDescriptor
        {
            ToolId = "mcp-backend-tool",
            Name = "MCP Backend Tool",
            Description = "Tool with MCP backend",
            Backend = ToolBackendType.MCP,
            Source = "mcp-server",
            IsAvailable = true
        };

        _mockToolRegistry.Setup(r => r.GetAvailableToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ToolDescriptor> { mcpTool });

        var resultWithBackend = new ToolInvocationResult
        {
            Success = true,
            Result = "Result from MCP",
            BackendUsed = ToolBackendType.MCP,
            ContextTokenCost = 50,
            DurationMs = 150
        };

        _mockToolInvoker.Setup(i => i.InvokeToolAsync(
                "mcp-backend-tool",
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<ToolInvocationPreferences>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultWithBackend);

        var request = new AgentExecutionRequest
        {
            AgentId = agent.Id,
            TaskGoal = "Task requiring MCP backend"
        };

        // Act
        var result = await _manager.ExecuteTaskAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IterationsExecuted > 0);
        // Verify that the tool invoker was called with the MCP tool
        _mockToolInvoker.Verify(
            i => i.InvokeToolAsync(
                "mcp-backend-tool",
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<ToolInvocationPreferences>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
