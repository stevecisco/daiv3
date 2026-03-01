using Daiv3.Orchestration;
using Daiv3.Orchestration.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Daiv3.IntegrationTests.Orchestration;

/// <summary>
/// Integration tests for agent execution observability and metrics collection.
/// Tests that metrics are properly captured during actual agent execution.
/// </summary>
public class AgentExecutionObservabilityIntegrationTests : IAsyncLifetime
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IAgentManager _agentManager;
    private readonly AgentExecutionMetricsCollector _metricsCollector;
    private readonly ILogger<AgentExecutionObservabilityIntegrationTests> _logger;
    private Guid _testAgentId;

    public AgentExecutionObservabilityIntegrationTests()
    {
        var services = new ServiceCollection();
        
        // Add logging
        services.AddLogging(builder => builder.AddConsole());

        // Add orchestration services
        services.AddOrchestrationServices();

        // Add persistence services
        services.AddSingleton<IDatabaseContextFactory>(new InMemoryDatabaseContextFactory());

        _serviceProvider = services.BuildServiceProvider();
        _agentManager = _serviceProvider.GetRequiredService<IAgentManager>();
        _metricsCollector = _serviceProvider.GetRequiredService<AgentExecutionMetricsCollector>();
        _logger = _serviceProvider.GetRequiredService<ILogger<AgentExecutionObservabilityIntegrationTests>>();
    }

    public async Task InitializeAsync()
    {
        // Create a test agent
        var definition = new AgentDefinition
        {
            Name = "MetricsTestAgent",
            Purpose = "Test agent for metrics collection",
            EnabledSkills = new List<string>(),
            Config = new Dictionary<string, string>()
        };

        var agent = await _agentManager.CreateAgentAsync(definition);
        _testAgentId = agent.Id;
    }

    public Task DisposeAsync()
    {
        _serviceProvider?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ExecuteTask_CollectsMetrics()
    {
        // Arrange
        var request = new AgentExecutionRequest
        {
            AgentId = _testAgentId,
            TaskGoal = "test task",
            Options = new AgentExecutionOptions { MaxIterations = 1, TimeoutSeconds = 10 }
        };

        var observerMock = new Mock<IAgentExecutionObserver>();
        observerMock.Setup(o => o.OnExecutionStartedAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        observerMock.Setup(o => o.OnIterationStartedAsync(It.IsAny<Guid>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);
        observerMock.Setup(o => o.OnIterationCompletedAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<AgentExecutionMetricsSnapshot>()))
            .Returns(Task.CompletedTask);
        observerMock.Setup(o => o.OnStepCompletedAsync(It.IsAny<Guid>(), It.IsAny<AgentExecutionMetrics.StepMetrics>()))
            .Returns(Task.CompletedTask);
        observerMock.Setup(o => o.OnExecutionCompletedAsync(It.IsAny<Guid>(), It.IsAny<AgentExecutionMetricsSnapshot>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        observerMock.Setup(o => o.OnMetricsSnapshotAsync(It.IsAny<Guid>(), It.IsAny<AgentExecutionMetricsSnapshot>()))
            .Returns(Task.CompletedTask);
        observerMock.Setup(o => o.OnPerformanceWarningAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _metricsCollector.Subscribe(observerMock.Object);

        // Act
        var result = await _agentManager.ExecuteTaskAsync(request);

        // Assert
        Assert.NotNull(result);
        var activeMetrics = _metricsCollector.GetMetrics(result.ExecutionId);
        
        Assert.NotNull(activeMetrics);
        Assert.Equal(_testAgentId, activeMetrics.AgentId);
        Assert.Equal(result.ExecutionId, activeMetrics.ExecutionId);
        
        // Verify observer was notified of start and completion
        observerMock.Verify(
            o => o.OnExecutionStartedAsync(It.IsAny<Guid>(), _testAgentId, "test task"),
            Times.Once);
        
        observerMock.Verify(
            o => o.OnExecutionCompletedAsync(It.IsAny<Guid>(), It.IsAny<AgentExecutionMetricsSnapshot>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteTask_TracksPauseResumeMetrics()
    {
        // Arrange
        var request = new AgentExecutionRequest
        {
            AgentId = _testAgentId,
            TaskGoal = "test task with pause",
            Options = new AgentExecutionOptions { MaxIterations = 3, TimeoutSeconds = 10 }
        };

        var executionStarted = false;
        var executionPaused = false;
        var executionResumed = false;

        var observerMock = new Mock<IAgentExecutionObserver>();
        observerMock.Setup(o => o.OnExecutionStartedAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>()))
            .Callback(() => executionStarted = true)
            .Returns(Task.CompletedTask);
        observerMock.Setup(o => o.OnExecutionPausedAsync(It.IsAny<Guid>(), It.IsAny<AgentExecutionMetricsSnapshot>()))
            .Callback(() => executionPaused = true)
            .Returns(Task.CompletedTask);
        observerMock.Setup(o => o.OnExecutionResumedAsync(It.IsAny<Guid>(), It.IsAny<AgentExecutionMetricsSnapshot>()))
            .Callback(() => executionResumed = true)
            .Returns(Task.CompletedTask);
        observerMock.Setup(o => o.OnIterationStartedAsync(It.IsAny<Guid>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);
        observerMock.Setup(o => o.OnIterationCompletedAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<AgentExecutionMetricsSnapshot>()))
            .Returns(Task.CompletedTask);
        observerMock.Setup(o => o.OnStepCompletedAsync(It.IsAny<Guid>(), It.IsAny<AgentExecutionMetrics.StepMetrics>()))
            .Returns(Task.CompletedTask);
        observerMock.Setup(o => o.OnExecutionCompletedAsync(It.IsAny<Guid>(), It.IsAny<AgentExecutionMetricsSnapshot>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        observerMock.Setup(o => o.OnMetricsSnapshotAsync(It.IsAny<Guid>(), It.IsAny<AgentExecutionMetricsSnapshot>()))
            .Returns(Task.CompletedTask);
        observerMock.Setup(o => o.OnPerformanceWarningAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _metricsCollector.Subscribe(observerMock.Object);

        // Act - Execute with pause/resume control
        var (control, executionTask) = _agentManager.StartExecutionWithControl(request);

        // Pause after a short delay
        await Task.Delay(100);
        control.Pause();
        
        // Resume after another short delay
        await Task.Delay(100);
        control.Resume();

        var result = await executionTask;

        // Assert
        Assert.True(executionStarted, "Execution should have started");
        
        var metrics = _metricsCollector.GetMetrics(result.ExecutionId);
        Assert.NotNull(metrics);
        Assert.True(metrics.TotalPausedDuration.TotalMilliseconds > 0, "Should have pause duration");
        
        control.Dispose();
    }

    [Fact]
    public async Task ExecuteTask_CalculatesTokenMetrics()
    {
        // Arrange
        var request = new AgentExecutionRequest
        {
            AgentId = _testAgentId,
            TaskGoal = "test task",
            Options = new AgentExecutionOptions { MaxIterations = 2, TimeoutSeconds = 10, TokenBudget = 10000 }
        };

        var observerMock = new Mock<IAgentExecutionObserver>();
        observerMock.Setup(o => o.OnExecutionStartedAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        observerMock.Setup(o => o.OnIterationStartedAsync(It.IsAny<Guid>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);
        observerMock.Setup(o => o.OnIterationCompletedAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<AgentExecutionMetricsSnapshot>()))
            .Returns(Task.CompletedTask);
        observerMock.Setup(o => o.OnStepCompletedAsync(It.IsAny<Guid>(), It.IsAny<AgentExecutionMetrics.StepMetrics>()))
            .Returns(Task.CompletedTask);
        observerMock.Setup(o => o.OnExecutionCompletedAsync(It.IsAny<Guid>(), It.IsAny<AgentExecutionMetricsSnapshot>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        observerMock.Setup(o => o.OnMetricsSnapshotAsync(It.IsAny<Guid>(), It.IsAny<AgentExecutionMetricsSnapshot>()))
            .Returns(Task.CompletedTask);
        observerMock.Setup(o => o.OnPerformanceWarningAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _metricsCollector.Subscribe(observerMock.Object);

        // Act
        var result = await _agentManager.ExecuteTaskAsync(request);

        // Assert
        var metrics = _metricsCollector.GetMetrics(result.ExecutionId);
        Assert.NotNull(metrics);
        
        // Metrics should have token information
        Assert.True(metrics.TotalTokensConsumed >= 0, "Token count should be non-negative");
        Assert.Equal(result.TokensConsumed, metrics.TotalTokensConsumed, "Metrics should match result tokens");
        
        // Check calculations
        if (metrics.TotalIterations > 0)
        {
            Assert.True(metrics.AverageTokensPerIteration >= 0, "Average tokens per iteration should be calculated");
        }
    }

    [Fact]
    public async Task ExecuteTask_TracksIterationMetrics()
    {
        // Arrange
        var request = new AgentExecutionRequest
        {
            AgentId = _testAgentId,
            TaskGoal = "test task",
            Options = new AgentExecutionOptions { MaxIterations = 2, TimeoutSeconds = 10 }
        };

        var iterationCompleteCount = 0;
        var observerMock = new Mock<IAgentExecutionObserver>();
        
        observerMock.Setup(o => o.OnExecutionStartedAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        observerMock.Setup(o => o.OnIterationStartedAsync(It.IsAny<Guid>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);
        observerMock.Setup(o => o.OnIterationCompletedAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<AgentExecutionMetricsSnapshot>()))
            .Callback(() => iterationCompleteCount++)
            .Returns(Task.CompletedTask);
        observerMock.Setup(o => o.OnStepCompletedAsync(It.IsAny<Guid>(), It.IsAny<AgentExecutionMetrics.StepMetrics>()))
            .Returns(Task.CompletedTask);
        observerMock.Setup(o => o.OnExecutionCompletedAsync(It.IsAny<Guid>(), It.IsAny<AgentExecutionMetricsSnapshot>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        observerMock.Setup(o => o.OnMetricsSnapshotAsync(It.IsAny<Guid>(), It.IsAny<AgentExecutionMetricsSnapshot>()))
            .Returns(Task.CompletedTask);
        observerMock.Setup(o => o.OnPerformanceWarningAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _metricsCollector.Subscribe(observerMock.Object);

        // Act
        var result = await _agentManager.ExecuteTaskAsync(request);

        // Assert
        Assert.True(iterationCompleteCount > 0, "Should have recorded iteration completions");
        
        var metrics = _metricsCollector.GetMetrics(result.ExecutionId);
        Assert.NotNull(metrics);
        Assert.Equal(result.IterationsExecuted, metrics.TotalIterations, "Iteration count should match");
    }
}

/// <summary>
/// In-memory database context factory for testing.
/// </summary>
internal class InMemoryDatabaseContextFactory : IDatabaseContextFactory
{
    public ValueTask<IDatabaseContext> CreateContextAsync(CancellationToken ct = default)
    {
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new DatabaseContext(options);
        context.Database.EnsureCreated();
        return ValueTask.FromResult<IDatabaseContext>(context);
    }
}
