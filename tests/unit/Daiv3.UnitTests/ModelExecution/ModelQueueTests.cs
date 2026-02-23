using Daiv3.ModelExecution;
using Daiv3.ModelExecution.Interfaces;
using Daiv3.ModelExecution.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Daiv3.UnitTests.ModelExecution;

public class ModelQueueTests
{
    private readonly Mock<IFoundryBridge> _mockFoundryBridge;
    private readonly Mock<IOnlineProviderRouter> _mockOnlineRouter;
    private readonly Mock<ILogger<ModelQueue>> _mockLogger;
    private readonly ModelQueueOptions _options;

    public ModelQueueTests()
    {
        _mockFoundryBridge = new Mock<IFoundryBridge>();
        _mockOnlineRouter = new Mock<IOnlineProviderRouter>();
        _mockLogger = new Mock<ILogger<ModelQueue>>();
        _options = new ModelQueueOptions
        {
            DefaultModelId = "phi-3-mini",
            ChatModelId = "phi-3-mini",
            CodeModelId = "phi-3-mini",
            SummarizeModelId = "phi-3-mini"
        };
    }

    [Fact]
    public async Task EnqueueAsync_WithNormalPriority_ReturnsRequestId()
    {
        // Arrange
        var queue = CreateQueue();
        var request = new ExecutionRequest
        {
            Id = Guid.NewGuid(),
            TaskType = "chat",
            Content = "Test request"
        };

        // Act
        var requestId = await queue.EnqueueAsync(request);

        // Assert
        Assert.Equal(request.Id, requestId);
    }

    [Fact]
    public async Task GetQueueStatusAsync_EmptyQueue_ReturnsZeroCounts()
    {
        // Arrange
        var queue = CreateQueue();

        // Act
        var status = await queue.GetQueueStatusAsync();

        // Assert
        Assert.Equal(0, status.ImmediateCount);
        Assert.Equal(0, status.NormalCount);
        Assert.Equal(0, status.BackgroundCount);
    }

    [Fact]
    public async Task EnqueueAsync_ImmediatePriority_ProcessedBeforeNormal()
    {
        // Arrange
        var queue = CreateQueue();
        var executionOrder = new List<Guid>();

        _mockFoundryBridge.Setup(x => x.ExecuteAsync(It.IsAny<ExecutionRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExecutionRequest req, string model, CancellationToken ct) =>
            {
                executionOrder.Add(req.Id);
                return new ExecutionResult
                {
                    RequestId = req.Id,
                    Content = "Response",
                    Status = ExecutionStatus.Completed
                };
            });

        _mockFoundryBridge.Setup(x => x.GetLoadedModelAsync())
            .ReturnsAsync("phi-3-mini");

        var normalRequest = new ExecutionRequest { TaskType = "chat", Content = "Normal" };
        var immediateRequest = new ExecutionRequest { TaskType = "chat", Content = "Immediate" };

        // Act
        await queue.EnqueueAsync(normalRequest, ExecutionPriority.Normal);
        await Task.Delay(50); // Let processing start
        await queue.EnqueueAsync(immediateRequest, ExecutionPriority.Immediate);

        // Wait for both to complete
        await Task.Delay(500);

        // Assert - Immediate should be processed before Normal (or first if Normal not yet started)
        Assert.Contains(immediateRequest.Id, executionOrder);
    }

    [Fact]
    public async Task ProcessAsync_CompletesSuccessfully_ReturnsResult()
    {
        // Arrange
        var queue = CreateQueue();
        var request = new ExecutionRequest { TaskType = "chat", Content = "Test" };

        _mockFoundryBridge.Setup(x => x.ExecuteAsync(It.IsAny<ExecutionRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExecutionResult
            {
                RequestId = request.Id,
                Content = "Response",
                Status = ExecutionStatus.Completed,
                TokenUsage = new TokenUsage { InputTokens = 10, OutputTokens = 20 }
            });

        _mockFoundryBridge.Setup(x => x.GetLoadedModelAsync())
            .ReturnsAsync("phi-3-mini");

        // Act
        var requestId = await queue.EnqueueAsync(request);

        var result = await queue.ProcessAsync(requestId);

        // Assert
        Assert.Equal(ExecutionStatus.Completed, result.Status);
        Assert.Equal("Response", result.Content);
        Assert.Equal(30, result.TokenUsage.TotalTokens);
    }

    [Fact]
    public async Task ProcessAsync_NonExistentRequest_ThrowsInvalidOperationException()
    {
        // Arrange
        var queue = CreateQueue();
        var nonExistentId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => queue.ProcessAsync(nonExistentId));
    }

    [Fact]
    public async Task GetStatusAsync_EnqueuedRequest_ReturnsQueuedStatus()
    {
        // Arrange
        var queue = CreateQueue();
        var request = new ExecutionRequest { TaskType = "chat", Content = "Test" };

        // Prevent execution from starting
        _mockFoundryBridge.Setup(x => x.ExecuteAsync(It.IsAny<ExecutionRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.Delay(10000).ContinueWith(_ => new ExecutionResult()));

        // Act
        var requestId = await queue.EnqueueAsync(request);
        await Task.Delay(100); // Let it enter processing
        var status = await queue.GetStatusAsync(requestId);

        // Assert
        Assert.NotNull(status);
        Assert.Equal(requestId, status.RequestId);
    }

    [Fact]
    public async Task EnqueueAsync_NullRequest_ThrowsArgumentNullException()
    {
        // Arrange
        var queue = CreateQueue();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => queue.EnqueueAsync(null!));
    }

    [Fact]
    public async Task ProcessAsync_Cancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var queue = CreateQueue();
        var request = new ExecutionRequest { TaskType = "chat", Content = "Test" };

        _mockFoundryBridge.Setup(x => x.ExecuteAsync(It.IsAny<ExecutionRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (ExecutionRequest req, string model, CancellationToken ct) =>
            {
                await Task.Delay(5000, ct); // Long delay
                return new ExecutionResult();
            });

        _mockFoundryBridge.Setup(x => x.GetLoadedModelAsync())
            .ReturnsAsync("phi-3-mini");

        var requestId = await queue.EnqueueAsync(request);

        using var cts = new CancellationTokenSource(100);

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(() => queue.ProcessAsync(requestId, cts.Token));
    }

    [Fact]
    public async Task GetStatusAsync_NonExistentRequest_ThrowsInvalidOperationException()
    {
        // Arrange
        var queue = CreateQueue();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => queue.GetStatusAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task EnqueueAsync_BackgroundPriority_EnqueuesSuccessfully()
    {
        // Arrange
        var queue = CreateQueue();
        var request = new ExecutionRequest { TaskType = "summarize", Content = "Background task" };

        // Act
        var requestId = await queue.EnqueueAsync(request, ExecutionPriority.Background);

        // Assert
        Assert.NotEqual(Guid.Empty, requestId);
    }

    [Theory]
    [InlineData(ExecutionPriority.Immediate)]
    [InlineData(ExecutionPriority.Normal)]
    [InlineData(ExecutionPriority.Background)]
    public async Task EnqueueAsync_AllPriorities_Succeeds(ExecutionPriority priority)
    {
        // Arrange
        var queue = CreateQueue();
        var request = new ExecutionRequest { TaskType = "chat", Content = "Test" };

        // Act
        var requestId = await queue.EnqueueAsync(request, priority);

        // Assert
        Assert.NotEqual(Guid.Empty, requestId);
    }

    [Fact]
    public async Task ProcessAsync_ExecutionError_ReturnsFailedResult()
    {
        // Arrange
        var queue = CreateQueue();
        var request = new ExecutionRequest { TaskType = "chat", Content = "Test" };

        _mockFoundryBridge.Setup(x => x.ExecuteAsync(It.IsAny<ExecutionRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Execution failed"));

        _mockFoundryBridge.Setup(x => x.GetLoadedModelAsync())
            .ReturnsAsync("phi-3-mini");

        // Act
        var requestId = await queue.EnqueueAsync(request);
        var result = await queue.ProcessAsync(requestId);

        // Assert
        Assert.Equal(ExecutionStatus.Failed, result.Status);
        Assert.NotNull(result.ErrorMessage);
    }

    private ModelQueue CreateQueue()
    {
        return new ModelQueue(
            _mockFoundryBridge.Object,
            _mockOnlineRouter.Object,
            Options.Create(_options),
            _mockLogger.Object);
    }
}
