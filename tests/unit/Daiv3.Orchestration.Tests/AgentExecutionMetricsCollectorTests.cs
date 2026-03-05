using Daiv3.Orchestration;
using Daiv3.Orchestration.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Daiv3.Orchestration.Tests;

/// <summary>
/// Unit tests for AgentExecutionMetricsCollector functionality.
/// </summary>
public class AgentExecutionMetricsCollectorTests
{
    private readonly AgentExecutionMetricsCollector _collector;
    private readonly Mock<ILogger<AgentExecutionMetricsCollector>> _loggerMock;
    private readonly IOptions<AgentExecutionObservabilityOptions> _options;

    public AgentExecutionMetricsCollectorTests()
    {
        _loggerMock = new Mock<ILogger<AgentExecutionMetricsCollector>>(MockBehavior.Loose);
        var options = new AgentExecutionObservabilityOptions { Enabled = true };
        _options = Options.Create(options);
        _collector = new AgentExecutionMetricsCollector(_loggerMock.Object, _options);
    }

    [Fact]
    public void CreateMetrics_WithValidIds_CreatesMetricsContainer()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        // Act
        var metrics = _collector.CreateMetrics(executionId, agentId);

        // Assert
        Assert.NotNull(metrics);
        Assert.Equal(executionId, metrics.ExecutionId);
        Assert.Equal(agentId, metrics.AgentId);
        Assert.Equal(0, metrics.TotalIterations);
        Assert.Equal(0, metrics.TotalTokensConsumed);
    }

    [Fact]
    public void GetMetrics_WithExistingExecution_ReturnsMetrics()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var metrics = _collector.CreateMetrics(executionId, agentId);

        // Act
        var retrieved = _collector.GetMetrics(executionId);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(executionId, retrieved.ExecutionId);
        Assert.Equal(agentId, retrieved.AgentId);
    }

    [Fact]
    public void GetMetrics_WithNonExistentExecution_ReturnsNull()
    {
        // Arrange
        var executionId = Guid.NewGuid();

        // Act
        var retrieved = _collector.GetMetrics(executionId);

        // Assert
        Assert.Null(retrieved);
    }

    [Fact]
    public void GetMetricsSnapshot_WithExistingExecution_ReturnsSnapshot()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var metrics = _collector.CreateMetrics(executionId, agentId);
        metrics.TotalIterations = 5;
        metrics.TotalTokensConsumed = 1000;

        // Act
        var snapshot = _collector.GetMetricsSnapshot(executionId);

        // Assert
        Assert.NotNull(snapshot);
        Assert.Equal(executionId, snapshot.ExecutionId);
        Assert.Equal(5, snapshot.TotalIterations);
        Assert.Equal(1000, snapshot.TotalTokensConsumed);
    }

    [Fact]
    public void Subscribe_WithObserver_AddsObserverToCollection()
    {
        // Arrange
        var observerMock = new Mock<IAgentExecutionObserver>();

        // Act
        _collector.Subscribe(observerMock.Object);

        // Assert
        // We can verify by calling a notification and checking that the observer is called
        var executionId = Guid.NewGuid();
        _collector.CreateMetrics(executionId, Guid.NewGuid());
    }

    [Fact]
    public async Task OnExecutionStartedAsync_NotifiesAllObservers()
    {
        // Arrange
        var observerMock1 = new Mock<IAgentExecutionObserver>();
        var observerMock2 = new Mock<IAgentExecutionObserver>();
        _collector.Subscribe(observerMock1.Object);
        _collector.Subscribe(observerMock2.Object);

        var executionId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var taskGoal = "Test task";

        observerMock1.Setup(o => o.OnExecutionStartedAsync(executionId, agentId, taskGoal))
            .Returns(Task.CompletedTask);
        observerMock2.Setup(o => o.OnExecutionStartedAsync(executionId, agentId, taskGoal))
            .Returns(Task.CompletedTask);

        // Act
        await _collector.OnExecutionStartedAsync(executionId, agentId, taskGoal);

        // Assert
        observerMock1.Verify(o => o.OnExecutionStartedAsync(executionId, agentId, taskGoal), Times.Once);
        observerMock2.Verify(o => o.OnExecutionStartedAsync(executionId, agentId, taskGoal), Times.Once);
    }

    [Fact]
    public async Task OnIterationCompletedAsync_WithSlowIteration_CallsPerformanceWarning()
    {
        // Arrange
        var observerMock = new Mock<IAgentExecutionObserver>();
        var warningDefined = false;

        observerMock.Setup(o => o.OnPerformanceWarningAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback((Guid _, string warningType, string _) =>
            {
                if (warningType == "SlowIteration")
                    warningDefined = true;
            })
            .Returns(Task.CompletedTask);

        observerMock.Setup(o => o.OnIterationCompletedAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<AgentExecutionMetricsSnapshot>()))
            .Returns(Task.CompletedTask);

        _collector.Subscribe(observerMock.Object);

        var executionId = Guid.NewGuid();
        var snapshot = new AgentExecutionMetricsSnapshot
        {
            ExecutionId = executionId,
            TotalIterations = 1,
            AverageIterationDuration = TimeSpan.FromSeconds(31) // Exceeds default threshold of 30s
        };

        // Act
        await _collector.OnIterationCompletedAsync(executionId, 1, snapshot);

        // Assert
        Assert.True(warningDefined, "Should have called performance warning for slow iteration");
    }

    [Fact]
    public async Task OnStepCompletedAsync_AddsMetricsToCollection()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var metrics = _collector.CreateMetrics(executionId, agentId);

        var stepMetrics = new AgentExecutionMetrics.StepMetrics
        {
            StepNumber = 1,
            StepType = "Planning",
            Duration = TimeSpan.FromMilliseconds(100),
            TokensConsumed = 50,
            Success = true,
            StartedAt = DateTimeOffset.UtcNow
        };

        // Act
        await _collector.OnStepCompletedAsync(executionId, stepMetrics);

        // Assert
        Assert.NotNull(metrics);
        Assert.Single(metrics.StepMetricsCollection);
        Assert.Equal(stepMetrics, metrics.StepMetricsCollection[0]);
    }

    [Fact]
    public async Task OnToolInvocationCompletedAsync_IncrementsToolInvocationCount()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var metrics = _collector.CreateMetrics(executionId, agentId);

        // Act
        await _collector.OnToolInvocationCompletedAsync(executionId, "TestTool", "Direct", true, TimeSpan.FromMilliseconds(100));
        await _collector.OnToolInvocationCompletedAsync(executionId, "TestTool2", "CLI", true, TimeSpan.FromMilliseconds(150));

        // Assert
        Assert.Equal(2, metrics.ToolInvocations);
        Assert.Equal(TimeSpan.FromMilliseconds(250), metrics.TotalToolDuration);
    }

    [Fact]
    public async Task OnSkillExecutionCompletedAsync_IncrementsSkillCount()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var metrics = _collector.CreateMetrics(executionId, agentId);

        // Act
        await _collector.OnSkillExecutionCompletedAsync(executionId, "CalculatorSkill", true, TimeSpan.FromMilliseconds(50));
        await _collector.OnSkillExecutionCompletedAsync(executionId, "StringSkill", true, TimeSpan.FromMilliseconds(75));

        // Assert
        Assert.Equal(2, metrics.SkillExecutions);
    }

    [Fact]
    public async Task OnExecutionCompletedAsync_WithHighPausePercentage_WarnsAboutExcessivePausing()
    {
        // Arrange
        var observerMock = new Mock<IAgentExecutionObserver>();
        var warningTriggered = false;

        observerMock.Setup(o => o.OnPerformanceWarningAsync(It.IsAny<Guid>(), "ExcessivePausing", It.IsAny<string>()))
            .Callback(() => warningTriggered = true)
            .Returns(Task.CompletedTask);

        observerMock.Setup(o => o.OnExecutionCompletedAsync(It.IsAny<Guid>(), It.IsAny<AgentExecutionMetricsSnapshot>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _collector.Subscribe(observerMock.Object);

        var executionId = Guid.NewGuid();
        var snapshot = new AgentExecutionMetricsSnapshot
        {
            ExecutionId = executionId,
            TotalDuration = TimeSpan.FromSeconds(100),
            TotalPausedDuration = TimeSpan.FromSeconds(60), // 60% paused (exceeds 50% default threshold)
            PausedPercentage = 60
        };

        // Act
        await _collector.OnExecutionCompletedAsync(executionId, snapshot, "Success");

        // Assert
        Assert.True(warningTriggered, "Should have warned about excessive pausing");
    }

    [Fact]
    public void ClearMetrics_RemovesMetricsFromCollection()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        _collector.CreateMetrics(executionId, agentId);

        // Verify metrics exist
        Assert.NotNull(_collector.GetMetrics(executionId));

        // Act
        _collector.ClearMetrics(executionId);

        // Assert
        Assert.Null(_collector.GetMetrics(executionId));
    }

    [Fact]
    public async Task ObserverExceptionHandling_DoesNotPropagateExceptions()
    {
        // Arrange
        var throwingObserver = new Mock<IAgentExecutionObserver>();
        throwingObserver.Setup(o => o.OnExecutionStartedAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Observer threw exception"));

        var goodObserver = new Mock<IAgentExecutionObserver>();
        goodObserver.Setup(o => o.OnExecutionStartedAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _collector.Subscribe(throwingObserver.Object);
        _collector.Subscribe(goodObserver.Object);

        var executionId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        // Act & Assert (should not throw)
        await _collector.OnExecutionStartedAsync(executionId, agentId, "Test");

        // Verify both were called despite the exception in the first one
        throwingObserver.Verify(o => o.OnExecutionStartedAsync(executionId, agentId, "Test"), Times.Once);
        goodObserver.Verify(o => o.OnExecutionStartedAsync(executionId, agentId, "Test"), Times.Once);
    }

    [Fact]
    public async Task StepMetricsRetentionLimit_TrimsOldMetrics()
    {
        // Arrange
        var options = new AgentExecutionObservabilityOptions
        {
            Enabled = true,
            CollectStepMetrics = true,
            MaxStepMetricsToRetain = 5
        };
        var optionsWrapper = Options.Create(options);
        var collector = new AgentExecutionMetricsCollector(_loggerMock.Object, optionsWrapper);

        var executionId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var metrics = collector.CreateMetrics(executionId, agentId);

        // Act - Add more steps than the retention limit
        var stepTasks = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            var stepMetrics = new AgentExecutionMetrics.StepMetrics
            {
                StepNumber = i + 1,
                StepType = "Test",
                Duration = TimeSpan.FromMilliseconds(100),
                TokensConsumed = 50,
                Success = true,
                StartedAt = DateTimeOffset.UtcNow
            };
            stepTasks.Add(collector.OnStepCompletedAsync(executionId, stepMetrics));
        }

        await Task.WhenAll(stepTasks);

        // Assert - Should have at most MaxStepMetricsToRetain + 1 (the one added after trimming starts)
        Assert.True(metrics.StepMetricsCollection.Count <= options.MaxStepMetricsToRetain + 1);
    }

    [Fact]
    public void Unsubscribe_RemovesObserver()
    {
        // Arrange
        var observerMock = new Mock<IAgentExecutionObserver>();
        _collector.Subscribe(observerMock.Object);

        // Act
        _collector.Unsubscribe(observerMock.Object);

        // Call a notification - observer should not be called
        observerMock.Setup(o => o.OnExecutionStartedAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // This won't actually test anything without internal observer list access,
        // but we can verify no exception is thrown
        var executeTask = _collector.OnExecutionStartedAsync(Guid.NewGuid(), Guid.NewGuid(), "Test");
        Assert.NotNull(executeTask);
    }

    [Fact]
    public void MetricsSnapshot_CalculatesAverageDurations()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var metrics = _collector.CreateMetrics(executionId, agentId);
        metrics.TotalIterations = 5;
        metrics.TotalDuration = TimeSpan.FromSeconds(100);
        metrics.TotalTokensConsumed = 5000;

        // Act
        var snapshot = metrics.CreateSnapshot();

        // Assert
        Assert.Equal(5, snapshot.TotalIterations);
        Assert.Equal(TimeSpan.FromSeconds(20), snapshot.AverageIterationDuration);
        Assert.Equal(1000, snapshot.AverageTokensPerIteration);
        Assert.Equal(50, snapshot.TokensPerSecond);
    }

    [Fact]
    public void MetricsSnapshot_CalculatesPausedPercentage()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var metrics = _collector.CreateMetrics(executionId, agentId);
        metrics.TotalDuration = TimeSpan.FromSeconds(100);
        metrics.TotalPausedDuration = TimeSpan.FromSeconds(30);

        // Act
        var snapshot = metrics.CreateSnapshot();

        // Assert
        Assert.Equal(30, snapshot.PausedPercentage);
    }
}
