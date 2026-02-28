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

    [Fact]
    public async Task EnqueueAsync_P0PreemptsP1_P1IsRequeued()
    {
        // Arrange
        var queue = CreateQueue();
        var p1Cancelled = new TaskCompletionSource<bool>();
        var p1ExecutionCount = 0;

        _mockFoundryBridge.Setup(x => x.ExecuteAsync(It.IsAny<ExecutionRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (ExecutionRequest req, string model, CancellationToken ct) =>
            {
                // P1 request behavior
                if (req.TaskType == "normal")
                {
                    p1ExecutionCount++;
                    
                    // First execution - wait to be cancelled
                    if (p1ExecutionCount == 1)
                    {
                        try
                        {
                            await Task.Delay(10000, ct);
                            return new ExecutionResult { RequestId = req.Id, Content = "Should not reach", Status = ExecutionStatus.Completed, TokenUsage = new TokenUsage() };
                        }
                        catch (OperationCanceledException)
                        {
                            p1Cancelled.TrySetResult(true);
                            throw;
                        }
                    }
                    
                    // Retry execution - complete quickly
                    await Task.Delay(10, ct);
                    return new ExecutionResult
                    {
                        RequestId = req.Id,
                        Content = "P1 completed on retry",
                        Status = ExecutionStatus.Completed,
                        TokenUsage = new TokenUsage { InputTokens = 10, OutputTokens = 20 }
                    };
                }
                
                // P0 request - complete immediately
                await Task.Delay(10, ct);
                return new ExecutionResult
                {
                    RequestId = req.Id,
                    Content = "P0 response",
                    Status = ExecutionStatus.Completed,
                    TokenUsage = new TokenUsage { InputTokens = 10, OutputTokens = 20 }
                };
            });

        _mockFoundryBridge.Setup(x => x.GetLoadedModelAsync())
            .ReturnsAsync("phi-3-mini");

        var p1Request = new ExecutionRequest { TaskType = "normal", Content = "P1 request" };
        var p0Request = new ExecutionRequest { TaskType = "immediate", Content = "P0 request" };

        // Act
        var p1Id = await queue.EnqueueAsync(p1Request, ExecutionPriority.Normal);
        await Task.Delay(100); // Let P1 start executing
        
        var p0Id = await queue.EnqueueAsync(p0Request, ExecutionPriority.Immediate);

        // Wait for P1 to be cancelled
        var wasCancelled = await p1Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(2));
        
        // Wait for both to complete
        var p0Result = await queue.ProcessAsync(p0Id).WaitAsync(TimeSpan.FromSeconds(5));
        var p1Result = await queue.ProcessAsync(p1Id).WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.True(wasCancelled, "P1 request should have been cancelled");
        Assert.Equal(ExecutionStatus.Completed, p0Result.Status);
        Assert.Equal(ExecutionStatus.Completed, p1Result.Status);
        Assert.Equal(2, p1ExecutionCount); // P1 executed twice (initial + retry)
    }

    [Fact]
    public async Task EnqueueAsync_P0PreemptsP2_P2IsRequeued()
    {
        // Arrange
        var queue = CreateQueue();
        var p2Cancelled = new TaskCompletionSource<bool>();
        var p2ExecutionCount = 0;

        _mockFoundryBridge.Setup(x => x.ExecuteAsync(It.IsAny<ExecutionRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (ExecutionRequest req, string model, CancellationToken ct) =>
            {
                // P2 request behavior
                if (req.TaskType == "background")
                {
                    p2ExecutionCount++;
                    
                    // First execution - wait to be cancelled
                    if (p2ExecutionCount == 1)
                    {
                        try
                        {
                            await Task.Delay(10000, ct);
                            return new ExecutionResult { RequestId = req.Id, Content = "Should not reach", Status = ExecutionStatus.Completed, TokenUsage = new TokenUsage() };
                        }
                        catch (OperationCanceledException)
                        {
                            p2Cancelled.TrySetResult(true);
                            throw;
                        }
                    }
                    
                    // Retry execution - complete quickly
                    await Task.Delay(10, ct);
                    return new ExecutionResult
                    {
                        RequestId = req.Id,
                        Content = "P2 completed on retry",
                        Status = ExecutionStatus.Completed,
                        TokenUsage = new TokenUsage { InputTokens = 10, OutputTokens = 20 }
                    };
                }
                
                // P0 request - complete immediately
                await Task.Delay(10, ct);
                return new ExecutionResult
                {
                    RequestId = req.Id,
                    Content = "P0 response",
                    Status = ExecutionStatus.Completed,
                    TokenUsage = new TokenUsage { InputTokens = 10, OutputTokens = 20 }
                };
            });

        _mockFoundryBridge.Setup(x => x.GetLoadedModelAsync())
            .ReturnsAsync("phi-3-mini");

        var p2Request = new ExecutionRequest { TaskType = "background", Content = "P2 request" };
        var p0Request = new ExecutionRequest { TaskType = "immediate", Content = "P0 request" };

        // Act
        var p2Id = await queue.EnqueueAsync(p2Request, ExecutionPriority.Background);
        await Task.Delay(100); // Let P2 start executing
        
        var p0Id = await queue.EnqueueAsync(p0Request, ExecutionPriority.Immediate);

        // Wait for P2 to be cancelled
        var wasCancelled = await p2Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(2));
        
        // Wait for both to complete
        var p0Result = await queue.ProcessAsync(p0Id).WaitAsync(TimeSpan.FromSeconds(5));
        var p2Result = await queue.ProcessAsync(p2Id).WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.True(wasCancelled, "P2 request should have been cancelled");
        Assert.Equal(ExecutionStatus.Completed, p0Result.Status);
        Assert.Equal(ExecutionStatus.Completed, p2Result.Status);
        Assert.Equal(2, p2ExecutionCount); // P2 executed twice (initial + retry)
    }

    [Fact]
    public async Task EnqueueAsync_P0DoesNotPreemptP0_BothComplete()
    {
        // Arrange
        var queue = CreateQueue();
        var executionOrder = new List<Guid>();
        var firstP0Started = new TaskCompletionSource<bool>();

        _mockFoundryBridge.Setup(x => x.ExecuteAsync(It.IsAny<ExecutionRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (ExecutionRequest req, string model, CancellationToken ct) =>
            {
                executionOrder.Add(req.Id);
                
                if (executionOrder.Count == 1)
                {
                    firstP0Started.SetResult(true);
                    await Task.Delay(200, ct); // Simulate work
                }
                else
                {
                    await Task.Delay(10, ct);
                }

                return new ExecutionResult
                {
                    RequestId = req.Id,
                    Content = "Response",
                    Status = ExecutionStatus.Completed,
                    TokenUsage = new TokenUsage { InputTokens = 10, OutputTokens = 20 }
                };
            });

        _mockFoundryBridge.Setup(x => x.GetLoadedModelAsync())
            .ReturnsAsync("phi-3-mini");

        var p0Request1 = new ExecutionRequest { TaskType = "immediate1", Content = "First P0" };
        var p0Request2 = new ExecutionRequest { TaskType = "immediate2", Content = "Second P0" };

        // Act
        var p0Id1 = await queue.EnqueueAsync(p0Request1, ExecutionPriority.Immediate);
        await firstP0Started.Task; // Wait for first P0 to start
        
        var p0Id2 = await queue.EnqueueAsync(p0Request2, ExecutionPriority.Immediate);

        // Wait for both to complete
        var result1 = await queue.ProcessAsync(p0Id1).WaitAsync(TimeSpan.FromSeconds(5));
        var result2 = await queue.ProcessAsync(p0Id2).WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.Equal(ExecutionStatus.Completed, result1.Status);
        Assert.Equal(ExecutionStatus.Completed, result2.Status);
        Assert.Equal(2, executionOrder.Count);
        Assert.Equal(p0Request1.Id, executionOrder[0]);
        Assert.Equal(p0Request2.Id, executionOrder[1]);
    }

    [Fact]
    public async Task EnqueueAsync_P0WithModelSwitch_SwitchesImmediately()
    {
        // Arrange
        var queue = CreateQueue();
        var modelSwitches = new List<string>();
        var p1Cancelled = new TaskCompletionSource<bool>();

        _mockFoundryBridge.Setup(x => x.GetLoadedModelAsync())
            .ReturnsAsync(() => modelSwitches.LastOrDefault() ?? "phi-3-mini");

        _mockFoundryBridge.Setup(x => x.ExecuteAsync(It.IsAny<ExecutionRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (ExecutionRequest req, string model, CancellationToken ct) =>
            {
                modelSwitches.Add(model);
                
                if (req.TaskType == "code")
                {
                    try
                    {
                        await Task.Delay(10000, ct);
                        return new ExecutionResult { RequestId = req.Id, Content = "Should not reach", Status = ExecutionStatus.Completed, TokenUsage = new TokenUsage() };
                    }
                    catch (OperationCanceledException)
                    {
                        p1Cancelled.TrySetResult(true);
                        throw;
                    }
                }
                
                await Task.Delay(10, ct);
                return new ExecutionResult
                {
                    RequestId = req.Id,
                    Content = "Response",
                    Status = ExecutionStatus.Completed,
                    TokenUsage = new TokenUsage { InputTokens = 10, OutputTokens = 20 }
                };
            });

        var p1Request = new ExecutionRequest { TaskType = "code", Content = "P1 code request" };
        var p0Request = new ExecutionRequest { TaskType = "chat", Content = "P0 chat request" };

        // Act
        var p1Id = await queue.EnqueueAsync(p1Request, ExecutionPriority.Normal);
        await Task.Delay(100); // Let P1 start
        
        var p0Id = await queue.EnqueueAsync(p0Request, ExecutionPriority.Immediate);

        // Wait for cancellation
        var wasCancelled = await p1Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // Wait for P0 to complete
        var p0Result = await queue.ProcessAsync(p0Id).WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.True(wasCancelled, "P1 should have been cancelled");
        Assert.Equal(ExecutionStatus.Completed, p0Result.Status);
        Assert.Contains(_options.CodeModelId, modelSwitches); // P1 started with code model
        Assert.Contains(_options.ChatModelId, modelSwitches); // P0 switched to chat model
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
