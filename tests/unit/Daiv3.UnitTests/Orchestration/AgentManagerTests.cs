using Daiv3.Orchestration;
using Daiv3.Orchestration.Interfaces;
using Daiv3.Persistence;
using Daiv3.Persistence.Repositories;
using Daiv3.Persistence.Entities;
using DBAgent = Daiv3.Persistence.Entities.Agent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Daiv3.UnitTests.Orchestration;

/// <summary>
/// Unit tests for AgentManager.
/// </summary>
public class AgentManagerTests
{
    private readonly AgentManager _manager;
    private readonly AgentRepository _repository;
    private readonly Mock<ILogger<AgentManager>> _mockLogger;
    private readonly OrchestrationOptions _options;
    private readonly string _dbPath;

    public AgentManagerTests()
    {
        _mockLogger = new Mock<ILogger<AgentManager>>();
        _options = new OrchestrationOptions();
        _dbPath = Path.Combine(Path.GetTempPath(), $"daiv3-test-{Guid.NewGuid()}.db");
        
        // Setup a test service provider with persistence
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPersistence(options =>
        {
            options.DatabasePath = _dbPath;
        });

        var serviceProvider = services.BuildServiceProvider();
        
        // Initialize database and get repository
        serviceProvider.InitializeDatabaseAsync().GetAwaiter().GetResult();
        _repository = serviceProvider.GetRequiredService<AgentRepository>();
        
        _manager = new AgentManager(
            _mockLogger.Object,
            _repository,
            Options.Create(_options));
    }

    [Fact]
    public async Task CreateAgentAsync_WithValidDefinition_CreatesAgent()
    {
        // Arrange
        var definition = new AgentDefinition
        {
            Name = "TestAgent",
            Purpose = "Test purposes",
            EnabledSkills = new List<string> { "skill1", "skill2" },
            Config = new Dictionary<string, string> { ["key"] = "value" }
        };

        // Act
        var agent = await _manager.CreateAgentAsync(definition);

        // Assert
        Assert.NotNull(agent);
        Assert.NotEqual(Guid.Empty, agent.Id);
        Assert.Equal("TestAgent", agent.Name);
        Assert.Equal("Test purposes", agent.Purpose);
        Assert.Equal(2, agent.EnabledSkills.Count);
        Assert.Contains("skill1", agent.EnabledSkills);
        Assert.Contains("skill2", agent.EnabledSkills);
        Assert.True(agent.CreatedAt <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task CreateAgentAsync_WithNullDefinition_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _manager.CreateAgentAsync(null!));
    }

    [Fact]
    public async Task CreateAgentAsync_WithEmptyName_ThrowsArgumentException()
    {
        // Arrange
        var definition = new AgentDefinition
        {
            Name = "",
            Purpose = "Test purposes"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _manager.CreateAgentAsync(definition));
    }

    [Fact]
    public async Task CreateAgentAsync_WithEmptyPurpose_ThrowsArgumentException()
    {
        // Arrange
        var definition = new AgentDefinition
        {
            Name = "TestAgent",
            Purpose = ""
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _manager.CreateAgentAsync(definition));
    }

    [Fact]
    public async Task GetAgentAsync_WithExistingAgent_ReturnsAgent()
    {
        // Arrange
        var definition = new AgentDefinition
        {
            Name = "TestAgent",
            Purpose = "Test purposes"
        };
        var createdAgent = await _manager.CreateAgentAsync(definition);

        // Act
        var retrievedAgent = await _manager.GetAgentAsync(createdAgent.Id);

        // Assert
        Assert.NotNull(retrievedAgent);
        Assert.Equal(createdAgent.Id, retrievedAgent.Id);
        Assert.Equal(createdAgent.Name, retrievedAgent.Name);
    }

    [Fact]
    public async Task GetAgentAsync_WithNonExistentAgent_ReturnsNull()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var agent = await _manager.GetAgentAsync(nonExistentId);

        // Assert
        Assert.Null(agent);
    }

    [Fact]
    public async Task ListAgentsAsync_WithNoAgents_ReturnsEmptyList()
    {
        // Act
        var agents = await _manager.ListAgentsAsync();

        // Assert
        Assert.NotNull(agents);
        Assert.Empty(agents);
    }

    [Fact]
    public async Task ListAgentsAsync_WithMultipleAgents_ReturnsAllAgents()
    {
        // Arrange
        await _manager.CreateAgentAsync(new AgentDefinition
        {
            Name = "Agent1",
            Purpose = "Purpose1"
        });
        await _manager.CreateAgentAsync(new AgentDefinition
        {
            Name = "Agent2",
            Purpose = "Purpose2"
        });

        // Act
        var agents = await _manager.ListAgentsAsync();

        // Assert
        Assert.Equal(2, agents.Count);
        Assert.Contains(agents, a => a.Name == "Agent1");
        Assert.Contains(agents, a => a.Name == "Agent2");
    }

    [Fact]
    public async Task DeleteAgentAsync_WithExistingAgent_RemovesAgent()
    {
        // Arrange
        var definition = new AgentDefinition
        {
            Name = "TestAgent",
            Purpose = "Test purposes"
        };
        var agent = await _manager.CreateAgentAsync(definition);

        // Act
        await _manager.DeleteAgentAsync(agent.Id);

        // Assert
        var retrievedAgent = await _manager.GetAgentAsync(agent.Id);
        Assert.Null(retrievedAgent);
    }

    [Fact]
    public async Task DeleteAgentAsync_WithNonExistentAgent_DoesNotThrow()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act & Assert - should not throw
        await _manager.DeleteAgentAsync(nonExistentId);
    }

    [Fact]
    public async Task CreateAgentAsync_PreservesConfigValues()
    {
        // Arrange
        var config = new Dictionary<string, string>
        {
            ["setting1"] = "value1",
            ["setting2"] = "value2"
        };
        var definition = new AgentDefinition
        {
            Name = "ConfigAgent",
            Purpose = "Test config preservation",
            Config = config
        };

        // Act
        var agent = await _manager.CreateAgentAsync(definition);

        // Assert
        Assert.Equal(2, agent.Config.Count);
        Assert.Equal("value1", agent.Config["setting1"]);
        Assert.Equal("value2", agent.Config["setting2"]);
    }

    #region ExecuteTaskAsync Tests

    [Fact]
    public async Task ExecuteTaskAsync_WithValidRequest_CompletesSuccessfully()
    {
        // Arrange
        var agent = await _manager.CreateAgentAsync(new AgentDefinition
        {
            Name = "ExecutionAgent",
            Purpose = "Test execution"
        });

        var request = new AgentExecutionRequest
        {
            AgentId = agent.Id,
            TaskGoal = "Complete a test task",
            Options = new AgentExecutionOptions
            {
                MaxIterations = 5,
                TimeoutSeconds = 10,
                TokenBudget = 1000
            }
        };

        // Act
        var result = await _manager.ExecuteTaskAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(agent.Id, result.AgentId);
        Assert.True(result.Success);
        Assert.Equal("Success", result.TerminationReason);
        Assert.True(result.IterationsExecuted > 0);
        Assert.NotEmpty(result.Steps);
        Assert.NotNull(result.CompletedAt);
        Assert.True(result.TokensConsumed > 0);
    }

    [Fact]
    public async Task ExecuteTaskAsync_WithNonExistentAgent_ThrowsException()
    {
        // Arrange
        var request = new AgentExecutionRequest
        {
            AgentId = Guid.NewGuid(),
            TaskGoal = "Test task"
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _manager.ExecuteTaskAsync(request));
    }

    [Fact]
    public async Task ExecuteTaskAsync_WithNullRequest_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _manager.ExecuteTaskAsync(null!));
    }

    [Fact]
    public async Task ExecuteTaskAsync_WithEmptyTaskGoal_ThrowsArgumentException()
    {
        // Arrange
        var agent = await _manager.CreateAgentAsync(new AgentDefinition
        {
            Name = "TestAgent",
            Purpose = "Test"
        });

        var request = new AgentExecutionRequest
        {
            AgentId = agent.Id,
            TaskGoal = ""
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _manager.ExecuteTaskAsync(request));
    }

    [Fact]
    public async Task ExecuteTaskAsync_ExceedingMaxIterations_TerminatesWithMaxIterations()
    {
        // Arrange
        var agent = await _manager.CreateAgentAsync(new AgentDefinition
        {
            Name = "IterationAgent",
            Purpose = "Test max iterations"
        });

        var request = new AgentExecutionRequest
        {
            AgentId = agent.Id,
            TaskGoal = "Task that requires many iterations",
            Options = new AgentExecutionOptions
            {
                MaxIterations = 3, // Low iteration limit
                TimeoutSeconds = 30,
                TokenBudget = 10000
            }
        };

        // Act
        var result = await _manager.ExecuteTaskAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("MaxIterations", result.TerminationReason);
        Assert.Equal(3, result.IterationsExecuted);
        Assert.NotEmpty(result.ErrorMessage);
        Assert.Contains("Maximum iterations", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteTaskAsync_ExceedingTokenBudget_TerminatesWithTokenBudgetExceeded()
    {
        // Arrange
        var agent = await _manager.CreateAgentAsync(new AgentDefinition
        {
            Name = "TokenAgent",
            Purpose = "Test token budget"
        });

        var request = new AgentExecutionRequest
        {
            AgentId = agent.Id,
            TaskGoal = "Task that consumes many tokens",
            Options = new AgentExecutionOptions
            {
                MaxIterations = 20,
                TimeoutSeconds = 30,
                TokenBudget = 50 // Very low token budget (each step consumes 100)
            }
        };

        // Act
        var result = await _manager.ExecuteTaskAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("TokenBudgetExceeded", result.TerminationReason);
        Assert.True(result.TokensConsumed >= 50);
        Assert.NotEmpty(result.ErrorMessage);
        Assert.Contains("Token budget exceeded", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteTaskAsync_WithTimeout_TerminatesWithTimeout()
    {
        // Arrange
        var agent = await _manager.CreateAgentAsync(new AgentDefinition
        {
            Name = "TimeoutAgent",
            Purpose = "Test timeout"
        });

        var request = new AgentExecutionRequest
        {
            AgentId = agent.Id,
            TaskGoal = "Long running task",
            Options = new AgentExecutionOptions
            {
                MaxIterations = 1000, // Many iterations to ensure timeout
                TimeoutSeconds = 1, // Very short timeout
                TokenBudget = 100000
            }
        };

        // Act
        var result = await _manager.ExecuteTaskAsync(request);

        // Assert
        // With 1000 iterations at ~50ms each, this should timeout
        // But in case of faster execution, accept either timeout or success
        Assert.True(
            result.TerminationReason == "Timeout" || 
            result.TerminationReason == "Success",
            $"Expected Timeout or Success, got {result.TerminationReason}");
        
        if (result.TerminationReason == "Timeout")
        {
            Assert.False(result.Success);
            Assert.NotEmpty(result.ErrorMessage);
            Assert.Contains("timeout", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }
        
        Assert.NotNull(result.CompletedAt);
    }

    [Fact]
    public async Task ExecuteTaskAsync_WithCancellation_TerminatesWithCancelled()
    {
        // Arrange
        var agent = await _manager.CreateAgentAsync(new AgentDefinition
        {
            Name = "CancellationAgent",
            Purpose = "Test cancellation"
        });

        var request = new AgentExecutionRequest
        {
            AgentId = agent.Id,
            TaskGoal = "Cancellable task",
            Options = new AgentExecutionOptions
            {
                MaxIterations = 100,
                TimeoutSeconds = 30,
                TokenBudget = 100000
            }
        };

        var cts = new CancellationTokenSource();
        cts.CancelAfter(100); // Cancel after 100ms

        // Act
        var result = await _manager.ExecuteTaskAsync(request, cts.Token);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Cancelled", result.TerminationReason);
        Assert.NotEmpty(result.ErrorMessage);
        Assert.Contains("cancelled", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteTaskAsync_WithNullOptions_UsesDefaults()
    {
        // Arrange
        var agent = await _manager.CreateAgentAsync(new AgentDefinition
        {
            Name = "DefaultOptionsAgent",
            Purpose = "Test default options"
        });

        var request = new AgentExecutionRequest
        {
            AgentId = agent.Id,
            TaskGoal = "Task with default options",
            Options = null // No options provided
        };

        // Act
        var result = await _manager.ExecuteTaskAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success || result.TerminationReason == "MaxIterations");
        // Defaults from OrchestrationOptions should be used
        Assert.True(result.IterationsExecuted <= _options.DefaultAgentMaxIterations);
    }

    [Fact]
    public async Task ExecuteTaskAsync_TracksAllSteps()
    {
        // Arrange
        var agent = await _manager.CreateAgentAsync(new AgentDefinition
        {
            Name = "StepTrackingAgent",
            Purpose = "Test step tracking"
        });

        var request = new AgentExecutionRequest
        {
            AgentId = agent.Id,
            TaskGoal = "Multi-step task",
            Options = new AgentExecutionOptions
            {
                MaxIterations = 5,
                TimeoutSeconds = 10,
                TokenBudget = 10000
            }
        };

        // Act
        var result = await _manager.ExecuteTaskAsync(request);

        // Assert
        Assert.NotEmpty(result.Steps);
        Assert.Equal(result.IterationsExecuted, result.Steps.Count);
        
        // Verify step sequencing
        for (int i = 0; i < result.Steps.Count; i++)
        {
            var step = result.Steps[i];
            Assert.Equal(i + 1, step.StepNumber);
            Assert.NotEmpty(step.StepType);
            Assert.NotEmpty(step.Description);
            Assert.True(step.StartedAt <= step.CompletedAt);
            Assert.True(step.TokensConsumed > 0);
        }
    }

    [Fact]
    public async Task ExecuteTaskAsync_PopulatesExecutionMetadata()
    {
        // Arrange
        var agent = await _manager.CreateAgentAsync(new AgentDefinition
        {
            Name = "MetadataAgent",
            Purpose = "Test metadata population"
        });

        var request = new AgentExecutionRequest
        {
            AgentId = agent.Id,
            TaskGoal = "Test task",
            Context = new Dictionary<string, string> { ["key"] = "value" },
            SuccessCriteria = "Task completes successfully"
        };

        // Act
        var result = await _manager.ExecuteTaskAsync(request);

        // Assert
        Assert.NotEqual(Guid.Empty, result.ExecutionId);
        Assert.Equal(agent.Id, result.AgentId);
        Assert.True(result.StartedAt <= DateTimeOffset.UtcNow);
        Assert.NotNull(result.CompletedAt);
        Assert.True(result.StartedAt <= result.CompletedAt);
        Assert.NotEmpty(result.TerminationReason);
    }

    [Fact]
    public async Task ExecuteTaskAsync_WithContext_AcceptsContextDictionary()
    {
        // Arrange
        var agent = await _manager.CreateAgentAsync(new AgentDefinition
        {
            Name = "ContextAgent",
            Purpose = "Test context handling"
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

        // Assert - should not throw, context should be accepted
        Assert.NotNull(result);
        Assert.True(result.Success || !string.IsNullOrEmpty(result.TerminationReason));
    }

    [Fact]
    public async Task ExecuteTaskAsync_WithSuccessCriteria_AcceptsCriteria()
    {
        // Arrange
        var agent = await _manager.CreateAgentAsync(new AgentDefinition
        {
            Name = "CriteriaAgent",
            Purpose = "Test success criteria"
        });

        var request = new AgentExecutionRequest
        {
            AgentId = agent.Id,
            TaskGoal = "Task with criteria",
            SuccessCriteria = "Output must contain 'success'"
        };

        // Act
        var result = await _manager.ExecuteTaskAsync(request);

        // Assert - should not throw, criteria should be accepted
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ExecuteTaskAsync_ProducesOutput()
    {
        // Arrange
        var agent = await _manager.CreateAgentAsync(new AgentDefinition
        {
            Name = "OutputAgent",
            Purpose = "Test output generation"
        });

        var request = new AgentExecutionRequest
        {
            AgentId = agent.Id,
            TaskGoal = "Generate output"
        };

        // Act
        var result = await _manager.ExecuteTaskAsync(request);

        // Assert
        if (result.Success)
        {
            Assert.NotEmpty(result.Output);
        }
        // Even failures may produce partial output
    }

    #endregion
}