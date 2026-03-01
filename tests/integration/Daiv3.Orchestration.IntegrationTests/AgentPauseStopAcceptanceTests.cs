using Daiv3.Orchestration;
using Daiv3.Orchestration.Interfaces;
using Daiv3.Orchestration.Messaging;
using Daiv3.Orchestration.Models;
using Daiv3.Mcp.Integration;
using Daiv3.Persistence;
using Daiv3.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Daiv3.Orchestration.IntegrationTests;

/// <summary>
/// Acceptance tests for agent pause and stop functionality.
/// Verifies AST-ACC-003: Agents can be paused or stopped by the user.
/// </summary>
public class AgentPauseStopAcceptanceTests : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IAgentManager _agentManager;
    private readonly ILogger<AgentPauseStopAcceptanceTests> _logger;
    private readonly string _dbPath;

    public AgentPauseStopAcceptanceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"daiv3-pause-stop-test-{Guid.NewGuid()}.db");

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

        // Register persistence and orchestration services
        services.AddPersistence(options => options.DatabasePath = _dbPath);
        services.AddOrchestrationServices();

        _serviceProvider = services.BuildServiceProvider();
        
        // Initialize database
        _serviceProvider.InitializeDatabaseAsync().GetAwaiter().GetResult();

        _agentManager = _serviceProvider.GetRequiredService<IAgentManager>();
        _logger = _serviceProvider.GetRequiredService<ILogger<AgentPauseStopAcceptanceTests>>();
    }

    /// <summary>
    /// Acceptance Test 1: Agent execution can be stopped mid-execution.
    /// </summary>
    [Fact]
    public async Task AcceptanceTest_AgentCanBeStopped_MidExecution()
    {
        // Arrange
        _logger.LogInformation("=== Acceptance Test 1: Agent can be stopped mid-execution ===");

        var agent = await CreateTestAgentAsync("TestStopAgent", "Agent for testing stop functionality");
        
        var request = new AgentExecutionRequest
        {
            AgentId = agent.Id,
            TaskGoal = "Execute a long-running task that will be stopped",
            Options = new AgentExecutionOptions
            {
                MaxIterations = 20, // Long enough to allow stop
                TimeoutSeconds = 60
            }
        };

        // Act - Start execution with control
        var (control, executionTask) = _agentManager.StartExecutionWithControl(request);
        Assert.NotNull(control);
        Assert.Equal(agent.Id, control.AgentId);

        _logger.LogInformation("Execution started with ExecutionId: {ExecutionId}", control.ExecutionId);

        // Wait briefly for execution to start
        await Task.Delay(500);

        // Stop the execution
        _logger.LogInformation("Stopping execution...");
        control.Stop();
        Assert.True(control.IsStopped);

        // Wait for execution to complete
        var result = await executionTask;

        // Assert - Execution should have terminated with "Cancelled" reason
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("Cancelled", result.TerminationReason);
        Assert.Contains("cancelled", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.CompletedAt.HasValue);

        _logger.LogInformation(
            "✓ Execution stopped successfully. Iterations: {Iterations}, Reason: {Reason}",
            result.IterationsExecuted, result.TerminationReason);
    }

    /// <summary>
    /// Acceptance Test 2: Agent execution can be paused and resumed.
    /// </summary>
    [Fact]
    public async Task AcceptanceTest_AgentCanBePausedAndResumed()
    {
        // Arrange
        _logger.LogInformation("=== Acceptance Test 2: Agent can be paused and resumed ===");

        var agent = await CreateTestAgentAsync("TestPauseAgent", "Agent for testing pause functionality");
        
        var request = new AgentExecutionRequest
        {
            AgentId = agent.Id,
            TaskGoal = "Execute a task that will be paused and resumed",
            Options = new AgentExecutionOptions
            {
                MaxIterations = 10,
                TimeoutSeconds = 60
            }
        };

        // Act - Start execution with control
        var (control, executionTask) = _agentManager.StartExecutionWithControl(request);
        _logger.LogInformation("Execution started with ExecutionId: {ExecutionId}", control.ExecutionId);

        // Wait briefly for execution to start
        await Task.Delay(500);

        // Pause the execution
        _logger.LogInformation("Pausing execution...");
        control.Pause();
        Assert.True(control.IsPaused);
        Assert.False(control.IsStopped);

        // Wait while paused
        var pauseDuration = TimeSpan.FromSeconds(2);
        _logger.LogInformation("Execution paused for {Duration}s", pauseDuration.TotalSeconds);
        await Task.Delay(pauseDuration);

        // Verify still paused
        Assert.True(control.IsPaused);

        // Resume the execution
        _logger.LogInformation("Resuming execution...");
        control.Resume();
        Assert.False(control.IsPaused);
        Assert.False(control.IsStopped);

        // Wait for execution to complete
        var result = await executionTask;

        // Assert - Execution should complete successfully
        Assert.NotNull(result);
        Assert.True(result.Success || result.TerminationReason == "MaxIterations");
        Assert.True(result.CompletedAt.HasValue);

        // Verify pause duration was tracked
        var pausedMs = result.PausedDuration.TotalMilliseconds;
        _logger.LogInformation("Total paused duration: {PausedMs}ms", pausedMs);
        Assert.True(pausedMs >= 1500, $"Expected paused duration >= 1500ms, got {pausedMs}ms");

        _logger.LogInformation(
            "✓ Execution paused and resumed successfully. Iterations: {Iterations}, PausedDuration: {PausedMs}ms",
            result.IterationsExecuted, pausedMs);
    }

    /// <summary>
    /// Acceptance Test 3: Paused agent can be stopped without resuming.
    /// </summary>
    [Fact]
    public async Task AcceptanceTest_PausedAgentCanBeStopped()
    {
        // Arrange
        _logger.LogInformation("=== Acceptance Test 3: Paused agent can be stopped ===");

        var agent = await CreateTestAgentAsync("TestPauseStopAgent", "Agent for testing pause-stop sequence");
        
        var request = new AgentExecutionRequest
        {
            AgentId = agent.Id,
            TaskGoal = "Execute a task that will be paused then stopped",
            Options = new AgentExecutionOptions
            {
                MaxIterations = 20,
                TimeoutSeconds = 60
            }
        };

        // Act - Start execution with control
        var (control, executionTask) = _agentManager.StartExecutionWithControl(request);
        _logger.LogInformation("Execution started with ExecutionId: {ExecutionId}", control.ExecutionId);

        // Wait briefly for execution to start
        await Task.Delay(500);

        // Pause the execution
        _logger.LogInformation("Pausing execution...");
        control.Pause();
        Assert.True(control.IsPaused);

        // Wait a bit while paused
        await Task.Delay(1000);

        // Stop while paused (without resuming)
        _logger.LogInformation("Stopping paused execution...");
        control.Stop();
        Assert.True(control.IsStopped);
        Assert.False(control.IsPaused); // Pause should be cleared after stop

        // Wait for execution to complete
        var result = await executionTask;

        // Assert - Execution should have terminated with "Cancelled" reason
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("Cancelled", result.TerminationReason);
        Assert.True(result.CompletedAt.HasValue);

        // Verify pause duration was tracked before stop
        var pausedMs = result.PausedDuration.TotalMilliseconds;
        _logger.LogInformation("Paused duration before stop: {PausedMs}ms", pausedMs);
        Assert.True(pausedMs >= 500, $"Expected paused duration >= 500ms, got {pausedMs}ms");

        _logger.LogInformation(
            "✓ Paused execution stopped successfully. Iterations: {Iterations}, PausedDuration: {PausedMs}ms",
            result.IterationsExecuted, pausedMs);
    }

    /// <summary>
    /// Acceptance Test 4: Can retrieve execution control by execution ID.
    /// </summary>
    [Fact]
    public async Task AcceptanceTest_CanRetrieveExecutionControlById()
    {
        // Arrange
        _logger.LogInformation("=== Acceptance Test 4: Can retrieve execution control by ID ===");

        var agent = await CreateTestAgentAsync("TestControlRetrievalAgent", "Agent for testing control retrieval");
        
        var request = new AgentExecutionRequest
        {
            AgentId = agent.Id,
            TaskGoal = "Execute a task where control is retrieved by ID",
            Options = new AgentExecutionOptions
            {
                MaxIterations = 10,
                TimeoutSeconds = 60
            }
        };

        // Act - Start execution with control
        var (control, executionTask) = _agentManager.StartExecutionWithControl(request);
        var executionId = control.ExecutionId;
        
        _logger.LogInformation("Execution started with ExecutionId: {ExecutionId}", executionId);

        // Retrieve control by ID
        var retrievedControl = _agentManager.GetExecutionControl(executionId);

        // Assert - Should retrieve the same control object
        Assert.NotNull(retrievedControl);
        Assert.Equal(executionId, retrievedControl.ExecutionId);
        Assert.Equal(agent.Id, retrievedControl.AgentId);

        _logger.LogInformation("✓ Successfully retrieved execution control by ID");

        // Pause using retrieved control
        retrievedControl.Pause();
        Assert.True(retrievedControl.IsPaused);

        // Wait briefly
        await Task.Delay(500);

        // Resume and let complete
        retrievedControl.Resume();
        var result = await executionTask;

        Assert.NotNull(result);
        _logger.LogInformation("✓ Execution completed after control retrieval test");
    }

    /// <summary>
    /// Acceptance Test 5: Can list all active executions.
    /// </summary>
    [Fact]
    public async Task AcceptanceTest_CanListActiveExecutions()
    {
        // Arrange
        _logger.LogInformation("=== Acceptance Test 5: Can list all active executions ===");

        var agent1 = await CreateTestAgentAsync("TestListAgent1", "First agent");
        var agent2 = await CreateTestAgentAsync("TestListAgent2", "Second agent");
        
        var request1 = new AgentExecutionRequest
        {
            AgentId = agent1.Id,
            TaskGoal = "First execution",
            Options = new AgentExecutionOptions { MaxIterations = 15, TimeoutSeconds = 60 }
        };

        var request2 = new AgentExecutionRequest
        {
            AgentId = agent2.Id,
            TaskGoal = "Second execution",
            Options = new AgentExecutionOptions { MaxIterations = 15, TimeoutSeconds = 60 }
        };

        // Act - Start two executions
        var (control1, task1) = _agentManager.StartExecutionWithControl(request1);
        var (control2, task2) = _agentManager.StartExecutionWithControl(request2);

        _logger.LogInformation("Started two executions: {Id1}, {Id2}", control1.ExecutionId, control2.ExecutionId);

        // Wait for executions to start
        await Task.Delay(500);

        // Get active executions
        var activeExecutions = _agentManager.GetActiveExecutions();

        // Assert - Should have at least 2 active executions
        Assert.NotNull(activeExecutions);
        Assert.True(activeExecutions.Count >= 2, $"Expected >= 2 active executions, got {activeExecutions.Count}");

        var found1 = activeExecutions.Any(e => e.ExecutionId == control1.ExecutionId);
        var found2 = activeExecutions.Any(e => e.ExecutionId == control2.ExecutionId);
        Assert.True(found1, "First execution not found in active list");
        Assert.True(found2, "Second execution not found in active list");

        _logger.LogInformation("✓ Found both executions in active list");

        // Stop both executions
        control1.Stop();
        control2.Stop();

        await task1;
        await task2;

        _logger.LogInformation("✓ Active execution listing test completed");
    }

    /// <summary>
    /// Acceptance Test 6: Backward compatibility - ExecuteTaskAsync still works without control.
    /// </summary>
    [Fact]
    public async Task AcceptanceTest_BackwardCompatibility_ExecuteTaskAsyncWorksWithoutControl()
    {
        // Arrange
        _logger.LogInformation("=== Acceptance Test 6: Backward compatibility test ===");

        var agent = await CreateTestAgentAsync("TestBackwardCompatAgent", "Agent for backward compatibility");
        
        var request = new AgentExecutionRequest
        {
            AgentId = agent.Id,
            TaskGoal = "Execute task using original ExecuteTaskAsync method",
            Options = new AgentExecutionOptions
            {
                MaxIterations = 5,
                TimeoutSeconds = 30
            }
        };

        // Act - Use original ExecuteTaskAsync method (no control object)
        var result = await _agentManager.ExecuteTaskAsync(request);

        // Assert - Should complete successfully
        Assert.NotNull(result);
        Assert.True(result.Success || result.TerminationReason == "MaxIterations");
        Assert.True(result.CompletedAt.HasValue);
        Assert.Equal(TimeSpan.Zero, result.PausedDuration); // No pause/resume used

        _logger.LogInformation(
            "✓ Backward compatibility verified. Iterations: {Iterations}, Success: {Success}",
            result.IterationsExecuted, result.Success);
    }

    /// <summary>
    /// Acceptance Test 7: Execution control with external cancellation token.
    /// </summary>
    [Fact]
    public async Task AcceptanceTest_ExecutionControlWithExternalCancellation()
    {
        // Arrange
        _logger.LogInformation("=== Acceptance Test 7: Execution control with external cancellation ===");

        var agent = await CreateTestAgentAsync("TestExternalCancelAgent", "Agent for external cancellation test");
        
        var request = new AgentExecutionRequest
        {
            AgentId = agent.Id,
            TaskGoal = "Execute task that will be cancelled externally",
            Options = new AgentExecutionOptions
            {
                MaxIterations = 20,
                TimeoutSeconds = 60
            }
        };

        var cts = new CancellationTokenSource();

        // Act - Start execution with external cancellation token
        var (control, executionTask) = _agentManager.StartExecutionWithControl(request, cts.Token);
        _logger.LogInformation("Execution started with external cancellation token");

        // Wait briefly
        await Task.Delay(500);

        // Cancel via external token
        _logger.LogInformation("Cancelling via external CancellationToken...");
        cts.Cancel();

        // Wait for execution to complete
        var result = await executionTask;

        // Assert - Should be cancelled
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("Cancelled", result.TerminationReason);

        _logger.LogInformation("✓ External cancellation worked correctly");
    }

    private async Task<Agent> CreateTestAgentAsync(string name, string purpose)
    {
        var definition = new AgentDefinition
        {
            Name = name,
            Purpose = purpose,
            EnabledSkills = new List<string> { "TestSkill" }
        };

        return await _agentManager.CreateAgentAsync(definition);
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
        catch
        {
            // Ignore cleanup errors in tests
        }
    }
}
