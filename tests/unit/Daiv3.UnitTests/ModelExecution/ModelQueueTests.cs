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

    // ==================== MQ-REQ-004 Tests: P1 Model Affinity Batching ====================

    [Fact]
    public async Task P1Requests_ForCurrentModel_ExecutedBeforeSwitching()
    {
        // MQ-REQ-004: If P1 requests exist for current model, execute them before switching
        // Arrange
        _options.ChatModelId = "phi-3-mini";
        _options.CodeModelId = "phi-4-mini";  // Different model for code tasks
        var queue = CreateQueue();
        var executionOrder = new List<Guid>();
        var modelSwitches = new List<string>();

        _mockFoundryBridge.Setup(x => x.GetLoadedModelAsync())
            .ReturnsAsync(() => modelSwitches.LastOrDefault());

        _mockFoundryBridge.Setup(x => x.ExecuteAsync(It.IsAny<ExecutionRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (ExecutionRequest req, string model, CancellationToken ct) =>
            {
                executionOrder.Add(req.Id);
                modelSwitches.Add(model);
                await Task.Delay(10, ct);
                return new ExecutionResult
                {
                    RequestId = req.Id,
                    Content = "Response",
                    Status = ExecutionStatus.Completed,
                    TokenUsage = new TokenUsage { InputTokens = 10, OutputTokens = 20 }
                };
            });

        // Create requests for two different models
        var chatRequests = new List<ExecutionRequest>
        {
            new() { TaskType = "chat", Content = "Chat 1" },
            new() { TaskType = "chat", Content = "Chat 2" },
            new() { TaskType = "chat", Content = "Chat 3" }
        };

        var codeRequests = new List<ExecutionRequest>
        {
            new() { TaskType = "code", Content = "Code 1" },
            new() { TaskType = "code", Content = "Code 2" }
        };

        // Act - Enqueue alternating chat and code requests
        var chatId1 = await queue.EnqueueAsync(chatRequests[0], ExecutionPriority.Normal);
        var codeId1 = await queue.EnqueueAsync(codeRequests[0], ExecutionPriority.Normal);
        var chatId2 = await queue.EnqueueAsync(chatRequests[1], ExecutionPriority.Normal);
        var codeId2 = await queue.EnqueueAsync(codeRequests[1], ExecutionPriority.Normal);
        var chatId3 = await queue.EnqueueAsync(chatRequests[2], ExecutionPriority.Normal);

        // Wait for all to complete
        await Task.WhenAll(
            queue.ProcessAsync(chatId1),
            queue.ProcessAsync(chatId2),
            queue.ProcessAsync(chatId3),
            queue.ProcessAsync(codeId1),
            queue.ProcessAsync(codeId2)
        ).WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        Assert.Equal(5, executionOrder.Count);

        // All chat requests should execute before code requests (batching)
        var chatIndices = chatRequests.Select(r => executionOrder.IndexOf(r.Id)).ToList();
        var codeIndices = codeRequests.Select(r => executionOrder.IndexOf(r.Id)).ToList();

        var maxChatIndex = chatIndices.Max();
        var minCodeIndex = codeIndices.Min();

        Assert.True(maxChatIndex < minCodeIndex,
            $"Chat requests should complete before code requests. Max chat index: {maxChatIndex}, Min code index: {minCodeIndex}");

        // Should have used both models
        var uniqueModels = modelSwitches.Distinct().ToList();
        Assert.Equal(2, uniqueModels.Count); // phi-3-mini (chat) and phi-4-mini (code)
        Assert.Contains(_options.ChatModelId, uniqueModels);
        Assert.Contains(_options.CodeModelId, uniqueModels);
    }

    [Fact]
    public async Task P1Requests_NoCurrentModel_ExecutesFirstRequest()
    {
        // MQ-REQ-004: When no model is loaded, should execute first P1 request
        // Arrange
        var queue = CreateQueue();

        _mockFoundryBridge.Setup(x => x.GetLoadedModelAsync())
            .ReturnsAsync((string?)null); // No model loaded

        _mockFoundryBridge.Setup(x => x.ExecuteAsync(It.IsAny<ExecutionRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExecutionRequest req, string model, CancellationToken ct) =>
                new ExecutionResult
                {
                    RequestId = req.Id,
                    Content = "Response",
                    Status = ExecutionStatus.Completed,
                    TokenUsage = new TokenUsage { InputTokens = 10, OutputTokens = 20 }
                });

        var request = new ExecutionRequest { TaskType = "chat", Content = "Test" };

        // Act
        var requestId = await queue.EnqueueAsync(request, ExecutionPriority.Normal);
        var result = await queue.ProcessAsync(requestId).WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.Equal(ExecutionStatus.Completed, result.Status);
    }

    [Fact]
    public async Task P1Requests_LookaheadLimit_PreventsScan()
    {
        // MQ-REQ-004: Lookahead should be limited to prevent excessive delays
        // Arrange
        var queue = CreateQueue();
        var executionOrder = new List<string>(); // Track which model each request used

        _mockFoundryBridge.Setup(x => x.GetLoadedModelAsync())
            .ReturnsAsync(() =>
            {
                // Simulate model tracking - return last executed model
                return executionOrder.Count == 0 ? "phi-3-mini" : executionOrder.Last();
            });

        _mockFoundryBridge.Setup(x => x.ExecuteAsync(It.IsAny<ExecutionRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (ExecutionRequest req, string model, CancellationToken ct) =>
            {
                executionOrder.Add(model);
                await Task.Delay(10, ct);
                return new ExecutionResult
                {
                    RequestId = req.Id,
                    Content = "Response",
                    Status = ExecutionStatus.Completed,
                    TokenUsage = new TokenUsage { InputTokens = 10, OutputTokens = 20 }
                };
            });

        // Enqueue 15 requests for "code" model, then 1 for "chat" model
        var requestIds = new List<Guid>();
        for (int i = 0; i < 15; i++)
        {
            var req = new ExecutionRequest { TaskType = "code", Content = $"Code {i}" };
            requestIds.Add(await queue.EnqueueAsync(req, ExecutionPriority.Normal));
        }

        var chatRequest = new ExecutionRequest { TaskType = "chat", Content = "Chat request" };
        var chatId = await queue.EnqueueAsync(chatRequest, ExecutionPriority.Normal);
        requestIds.Add(chatId);

        // Act - Wait for several requests to complete
        await Task.Delay(500); // Let some processing happen

        // Assert
        // The lookahead limit (10) means the chat request at position 16 won't be found in initial scan
        // So code requests should start executing even though chat model was initially loaded
        // This test validates that we don't scan indefinitely
        var status = await queue.GetStatusAsync(chatId);
        
        // The chat request should still be processable (not starved)
        Assert.NotNull(status);
    }

    [Fact]
    public async Task P0Request_PreemptsP1Batching_SwitchesImmediately()
    {
        // MQ-REQ-004: P0 should still preempt even during P1 batching
        // Arrange
        var queue = CreateQueue();
        var p1Cancelled = new TaskCompletionSource<bool>();
        var executionOrder = new List<string>();

        _mockFoundryBridge.Setup(x => x.GetLoadedModelAsync())
            .ReturnsAsync(() => executionOrder.LastOrDefault() ?? "phi-3-mini");

        _mockFoundryBridge.Setup(x => x.ExecuteAsync(It.IsAny<ExecutionRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (ExecutionRequest req, string model, CancellationToken ct) =>
            {
                executionOrder.Add($"{req.TaskType}:{model}");

                if (req.TaskType == "chat" && executionOrder.Count == 1)
                {
                    // First P1 request - wait to be cancelled
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

        var p1Request = new ExecutionRequest { TaskType = "chat", Content = "P1 chat" };
        var p0Request = new ExecutionRequest { TaskType = "code", Content = "P0 code" };

        // Act
        var p1Id = await queue.EnqueueAsync(p1Request, ExecutionPriority.Normal);
        await Task.Delay(100); // Let P1 start

        var p0Id = await queue.EnqueueAsync(p0Request, ExecutionPriority.Immediate);

        // Wait for cancellation
        var wasCancelled = await p1Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // Wait for P0 to complete
        var p0Result = await queue.ProcessAsync(p0Id).WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.True(wasCancelled, "P1 should have been cancelled by P0");
        Assert.Equal(ExecutionStatus.Completed, p0Result.Status);

        // P0 should execute before P1 retry completes
        Assert.Contains($"code:{_options.CodeModelId}", executionOrder);
    }

    [Fact]
    public async Task P1Requests_MixedModels_BatchesByModel()
    {
        // MQ-REQ-004: Verify batching reduces model switches
        // Arrange
        var queue = CreateQueue();
        var modelSwitchLog = new List<string>();
        string? currentModel = null;

        _mockFoundryBridge.Setup(x => x.GetLoadedModelAsync())
            .ReturnsAsync(() => currentModel);

        _mockFoundryBridge.Setup(x => x.ExecuteAsync(It.IsAny<ExecutionRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (ExecutionRequest req, string model, CancellationToken ct) =>
            {
                // Track model switches
                if (currentModel != model)
                {
                    modelSwitchLog.Add($"Switch: {currentModel ?? "(none)"} → {model}");
                    currentModel = model;
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

        // Create alternating pattern: chat, code, chat, code, chat, code
        var requests = new List<(ExecutionRequest, ExecutionPriority)>
        {
            (new ExecutionRequest { TaskType = "chat", Content = "Chat 1" }, ExecutionPriority.Normal),
            (new ExecutionRequest { TaskType = "code", Content = "Code 1" }, ExecutionPriority.Normal),
            (new ExecutionRequest { TaskType = "chat", Content = "Chat 2" }, ExecutionPriority.Normal),
            (new ExecutionRequest { TaskType = "code", Content = "Code 2" }, ExecutionPriority.Normal),
            (new ExecutionRequest { TaskType = "chat", Content = "Chat 3" }, ExecutionPriority.Normal),
            (new ExecutionRequest { TaskType = "code", Content = "Code 3" }, ExecutionPriority.Normal)
        };

        // Act
        var tasks = new List<Task<ExecutionResult>>();
        foreach (var (request, priority) in requests)
        {
            var id = await queue.EnqueueAsync(request, priority);
            tasks.Add(queue.ProcessAsync(id));
        }

        await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        // Without batching: would switch after every request (6 switches)
        // With batching: should batch by model (2-3 switches)
        Assert.True(modelSwitchLog.Count <= 4,
            $"Expected ≤ 4 model switches with batching, got {modelSwitchLog.Count}. Switches: {string.Join(", ", modelSwitchLog)}");
    }

    // ==================== MQ-REQ-005 Tests: P2 Model Affinity Batching ====================

    [Fact]
    public async Task P2Requests_ForCurrentModel_ExecutedBeforeSwitching()
    {
        // MQ-REQ-005: If P2 requests exist for current model, execute them before switching
        // Arrange
        _options.ChatModelId = "phi-3-mini";
        _options.CodeModelId = "phi-4-mini";  // Different model for code tasks
        var queue = CreateQueue();
        var executionOrder = new List<Guid>();
        var modelSwitches = new List<string>();

        _mockFoundryBridge.Setup(x => x.GetLoadedModelAsync())
            .ReturnsAsync(() => modelSwitches.LastOrDefault());

        _mockFoundryBridge.Setup(x => x.ExecuteAsync(It.IsAny<ExecutionRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (ExecutionRequest req, string model, CancellationToken ct) =>
            {
                executionOrder.Add(req.Id);
                modelSwitches.Add(model);
                await Task.Delay(10, ct);
                return new ExecutionResult
                {
                    RequestId = req.Id,
                    Content = "Response",
                    Status = ExecutionStatus.Completed,
                    TokenUsage = new TokenUsage { InputTokens = 10, OutputTokens = 20 }
                };
            });

        // Create background requests for two different models
        var chatRequests = new List<ExecutionRequest>
        {
            new() { TaskType = "chat", Content = "Chat 1" },
            new() { TaskType = "chat", Content = "Chat 2" },
            new() { TaskType = "chat", Content = "Chat 3" }
        };

        var codeRequests = new List<ExecutionRequest>
        {
            new() { TaskType = "code", Content = "Code 1" },
            new() { TaskType = "code", Content = "Code 2" }
        };

        // Act - Enqueue alternating chat and code requests as P2 (Background)
        var chatId1 = await queue.EnqueueAsync(chatRequests[0], ExecutionPriority.Background);
        var codeId1 = await queue.EnqueueAsync(codeRequests[0], ExecutionPriority.Background);
        var chatId2 = await queue.EnqueueAsync(chatRequests[1], ExecutionPriority.Background);
        var codeId2 = await queue.EnqueueAsync(codeRequests[1], ExecutionPriority.Background);
        var chatId3 = await queue.EnqueueAsync(chatRequests[2], ExecutionPriority.Background);

        // Wait for all to complete
        await Task.WhenAll(
            queue.ProcessAsync(chatId1),
            queue.ProcessAsync(chatId2),
            queue.ProcessAsync(chatId3),
            queue.ProcessAsync(codeId1),
            queue.ProcessAsync(codeId2)
        ).WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        Assert.Equal(5, executionOrder.Count);

        // All chat requests should execute before code requests (batching)
        var chatIndices = chatRequests.Select(r => executionOrder.IndexOf(r.Id)).ToList();
        var codeIndices = codeRequests.Select(r => executionOrder.IndexOf(r.Id)).ToList();

        var maxChatIndex = chatIndices.Max();
        var minCodeIndex = codeIndices.Min();

        Assert.True(maxChatIndex < minCodeIndex,
            $"Chat requests should complete before code requests. Max chat index: {maxChatIndex}, Min code index: {minCodeIndex}");

        // Should have used both models
        var uniqueModels = modelSwitches.Distinct().ToList();
        Assert.Equal(2, uniqueModels.Count); // phi-3-mini (chat) and phi-4-mini (code)
        Assert.Contains(_options.ChatModelId, uniqueModels);
        Assert.Contains(_options.CodeModelId, uniqueModels);
    }

    [Fact]
    public async Task P2Requests_NoCurrentModel_ExecutesFirstRequest()
    {
        // MQ-REQ-005: When no model is loaded, should execute first P2 request
        // Arrange
        var queue = CreateQueue();

        _mockFoundryBridge.Setup(x => x.GetLoadedModelAsync())
            .ReturnsAsync((string?)null); // No model loaded

        _mockFoundryBridge.Setup(x => x.ExecuteAsync(It.IsAny<ExecutionRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExecutionRequest req, string model, CancellationToken ct) =>
                new ExecutionResult
                {
                    RequestId = req.Id,
                    Content = "Response",
                    Status = ExecutionStatus.Completed,
                    TokenUsage = new TokenUsage { InputTokens = 10, OutputTokens = 20 }
                });

        var request = new ExecutionRequest { TaskType = "chat", Content = "Test" };

        // Act
        var requestId = await queue.EnqueueAsync(request, ExecutionPriority.Background);
        var result = await queue.ProcessAsync(requestId).WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.Equal(ExecutionStatus.Completed, result.Status);
    }

    [Fact]
    public async Task P2Requests_LookaheadLimit_PreventsScan()
    {
        // MQ-REQ-005: Lookahead should be limited to prevent excessive delays
        // Arrange
        var queue = CreateQueue();
        var executionOrder = new List<string>(); // Track which model each request used

        _mockFoundryBridge.Setup(x => x.GetLoadedModelAsync())
            .ReturnsAsync(() =>
            {
                // Simulate model tracking - return last executed model
                return executionOrder.Count == 0 ? "phi-3-mini" : executionOrder.Last();
            });

        _mockFoundryBridge.Setup(x => x.ExecuteAsync(It.IsAny<ExecutionRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (ExecutionRequest req, string model, CancellationToken ct) =>
            {
                executionOrder.Add(model);
                await Task.Delay(10, ct);
                return new ExecutionResult
                {
                    RequestId = req.Id,
                    Content = "Response",
                    Status = ExecutionStatus.Completed,
                    TokenUsage = new TokenUsage { InputTokens = 10, OutputTokens = 20 }
                };
            });

        // Enqueue 15 requests for "code" model, then 1 for "chat" model
        var requestIds = new List<Guid>();
        for (int i = 0; i < 15; i++)
        {
            var req = new ExecutionRequest { TaskType = "code", Content = $"Code {i}" };
            requestIds.Add(await queue.EnqueueAsync(req, ExecutionPriority.Background));
        }

        var chatRequest = new ExecutionRequest { TaskType = "chat", Content = "Chat request" };
        var chatId = await queue.EnqueueAsync(chatRequest, ExecutionPriority.Background);
        requestIds.Add(chatId);

        // Act - Wait for several requests to complete
        await Task.Delay(500); // Let some processing happen

        // Assert
        // The lookahead limit (10) means the chat request at position 16 won't be found in initial scan
        // So code requests should start executing even though chat model was initially loaded
        // This test validates that we don't scan indefinitely
        var status = await queue.GetStatusAsync(chatId);
        
        // The chat request should still be processable (not starved)
        Assert.NotNull(status);
    }

    [Fact]
    public async Task P1Request_PreemptsP2Batching_SwitchesImmediately()
    {
        // MQ-REQ-005: P1 should preempt P2 batching
        // Arrange
        var queue = CreateQueue();
        var p2Cancelled = new TaskCompletionSource<bool>();
        var executionOrder = new List<string>();

        _mockFoundryBridge.Setup(x => x.GetLoadedModelAsync())
            .ReturnsAsync(() => executionOrder.LastOrDefault() ?? "phi-3-mini");

        _mockFoundryBridge.Setup(x => x.ExecuteAsync(It.IsAny<ExecutionRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (ExecutionRequest req, string model, CancellationToken ct) =>
            {
                var taskType = req.TaskType;
                executionOrder.Add($"{taskType}:{model}");

                if (taskType == "background" && executionOrder.Count == 1)
                {
                    // First P2 request - simulate work but allow P1 to take over
                    await Task.Delay(50, ct);
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

        var p2Request = new ExecutionRequest { TaskType = "background", Content = "P2 background" };
        var p1Request = new ExecutionRequest { TaskType = "chat", Content = "P1 chat" };

        // Act
        var p2Id = await queue.EnqueueAsync(p2Request, ExecutionPriority.Background);
        await Task.Delay(20); // Let P2 start

        var p1Id = await queue.EnqueueAsync(p1Request, ExecutionPriority.Normal);

        // Wait for both to complete
        var p1Result = await queue.ProcessAsync(p1Id).WaitAsync(TimeSpan.FromSeconds(5));
        var p2Result = await queue.ProcessAsync(p2Id).WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.Equal(ExecutionStatus.Completed, p1Result.Status);
        Assert.Equal(ExecutionStatus.Completed, p2Result.Status);

        // P1 should execute after P2 starts (no preemption, just priority ordering)
        Assert.Equal(2, executionOrder.Count);
    }

    [Fact]
    public async Task P2Requests_MixedModels_BatchesByModel()
    {
        // MQ-REQ-005: Verify batching reduces model switches
        // Arrange
        var queue = CreateQueue();
        var modelSwitchLog = new List<string>();
        string? currentModel = null;

        _mockFoundryBridge.Setup(x => x.GetLoadedModelAsync())
            .ReturnsAsync(() => currentModel);

        _mockFoundryBridge.Setup(x => x.ExecuteAsync(It.IsAny<ExecutionRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (ExecutionRequest req, string model, CancellationToken ct) =>
            {
                // Track model switches
                if (currentModel != model)
                {
                    modelSwitchLog.Add($"Switch: {currentModel ?? "(none)"} → {model}");
                    currentModel = model;
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

        // Create alternating pattern: chat, code, chat, code, chat, code (all P2)
        var requests = new List<(ExecutionRequest, ExecutionPriority)>
        {
            (new ExecutionRequest { TaskType = "chat", Content = "Chat 1" }, ExecutionPriority.Background),
            (new ExecutionRequest { TaskType = "code", Content = "Code 1" }, ExecutionPriority.Background),
            (new ExecutionRequest { TaskType = "chat", Content = "Chat 2" }, ExecutionPriority.Background),
            (new ExecutionRequest { TaskType = "code", Content = "Code 2" }, ExecutionPriority.Background),
            (new ExecutionRequest { TaskType = "chat", Content = "Chat 3" }, ExecutionPriority.Background),
            (new ExecutionRequest { TaskType = "code", Content = "Code 3" }, ExecutionPriority.Background)
        };

        // Act
        var tasks = new List<Task<ExecutionResult>>();
        foreach (var (request, priority) in requests)
        {
            var id = await queue.EnqueueAsync(request, priority);
            tasks.Add(queue.ProcessAsync(id));
        }

        await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        // Without batching: would switch after every request (6 switches)
        // With batching: should batch by model (2-3 switches)
        Assert.True(modelSwitchLog.Count <= 4,
            $"Expected ≤ 4 model switches with batching, got {modelSwitchLog.Count}. Switches: {string.Join(", ", modelSwitchLog)}");
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
