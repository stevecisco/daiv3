using Daiv3.Orchestration;
using Daiv3.Orchestration.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Daiv3.UnitTests.Orchestration;

/// <summary>
/// Unit tests for TaskOrchestrator.
/// </summary>
public class TaskOrchestratorTests
{
    private readonly Mock<IIntentResolver> _mockIntentResolver;
    private readonly Mock<IDependencyResolver> _mockDependencyResolver;
    private readonly Mock<ILogger<TaskOrchestrator>> _mockLogger;
    private readonly OrchestrationOptions _options;
    private readonly TaskOrchestrator _orchestrator;

    public TaskOrchestratorTests()
    {
        _mockIntentResolver = new Mock<IIntentResolver>();
        _mockDependencyResolver = new Mock<IDependencyResolver>();
        _mockLogger = new Mock<ILogger<TaskOrchestrator>>();
        _options = new OrchestrationOptions
        {
            MinimumIntentConfidence = 0.5m,
            MaxConcurrentTasks = 4,
            TaskTimeoutSeconds = 600
        };

        _orchestrator = new TaskOrchestrator(
            _mockIntentResolver.Object,
            _mockDependencyResolver.Object,
            _mockLogger.Object,
            Options.Create(_options));
    }

    [Fact]
    public async Task ExecuteAsync_WithValidRequest_ReturnsSuccessfulResult()
    {
        // Arrange
        var request = new UserRequest
        {
            Input = "search for documentation",
            ProjectId = Guid.NewGuid()
        };

        var intent = new Intent
        {
            Type = "search",
            Confidence = 0.9m,
            Entities = new Dictionary<string, string> { ["query"] = "documentation" }
        };

        _mockIntentResolver
            .Setup(x => x.ResolveAsync(request.Input, request.Context, It.IsAny<CancellationToken>()))
            .ReturnsAsync(intent);

        // Act
        var result = await _orchestrator.ExecuteAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.SessionId);
        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
        Assert.NotEmpty(result.TaskResults);
    }

    [Fact]
    public async Task ExecuteAsync_WithLowConfidence_ReturnsFailure()
    {
        // Arrange
        var request = new UserRequest
        {
            Input = "unclear gibberish",
            ProjectId = Guid.NewGuid()
        };

        var intent = new Intent
        {
            Type = "unknown",
            Confidence = 0.3m
        };

        _mockIntentResolver
            .Setup(x => x.ResolveAsync(request.Input, request.Context, It.IsAny<CancellationToken>()))
            .ReturnsAsync(intent);

        // Act
        var result = await _orchestrator.ExecuteAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("confidence", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullRequest_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _orchestrator.ExecuteAsync(null!));
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyInput_ThrowsArgumentException()
    {
        // Arrange
        var request = new UserRequest
        {
            Input = "",
            ProjectId = Guid.NewGuid()
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _orchestrator.ExecuteAsync(request));
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        var request = new UserRequest
        {
            Input = "test request",
            ProjectId = Guid.NewGuid()
        };

        var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockIntentResolver
            .Setup(x => x.ResolveAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _orchestrator.ExecuteAsync(request, cts.Token));
    }

    [Fact]
    public async Task ResolveIntentAsync_WithValidInput_ReturnsResolvedTasks()
    {
        // Arrange
        var input = "create a new document";
        var intent = new Intent
        {
            Type = "create",
            Confidence = 0.85m,
            Entities = new Dictionary<string, string> { ["type"] = "document" }
        };

        _mockIntentResolver
            .Setup(x => x.ResolveAsync(input, It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(intent);

        // Act
        var tasks = await _orchestrator.ResolveIntentAsync(input);

        // Assert
        Assert.NotEmpty(tasks);
        Assert.Equal("create", tasks[0].TaskType);
        Assert.Equal(0, tasks[0].ExecutionOrder);
    }

    [Fact]
    public async Task ResolveIntentAsync_WithEmptyInput_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _orchestrator.ResolveIntentAsync(""));
    }

    [Fact]
    public async Task CanEnqueueTaskAsync_WithSatisfiedDependencies_ReturnsTrue()
    {
        // Arrange
        const string taskId = "task-1";

        _mockDependencyResolver
            .Setup(r => r.AreDependenciesSatisfiedAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _orchestrator.CanEnqueueTaskAsync(taskId);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task CanEnqueueTaskAsync_WithUnsatisfiedDependencies_ReturnsFalse()
    {
        // Arrange
        const string taskId = "task-1";

        _mockDependencyResolver
            .Setup(r => r.AreDependenciesSatisfiedAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _orchestrator.CanEnqueueTaskAsync(taskId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task CanEnqueueTaskAsync_WithDependencyResolverException_ReturnsFalse()
    {
        // Arrange
        const string taskId = "task-1";

        _mockDependencyResolver
            .Setup(r => r.AreDependenciesSatisfiedAsync(taskId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Dependency resolution failed"));

        // Act
        var result = await _orchestrator.CanEnqueueTaskAsync(taskId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task CanEnqueueTaskAsync_WithNullTaskId_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _orchestrator.CanEnqueueTaskAsync(null!));
    }
}
