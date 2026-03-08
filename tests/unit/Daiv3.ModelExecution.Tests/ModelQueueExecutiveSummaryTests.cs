using Daiv3.ModelExecution;
using Daiv3.ModelExecution.Interfaces;
using Daiv3.ModelExecution.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Daiv3.ModelExecution.Tests;

/// <summary>
/// Requirement-traceability tests for ES-REQ-006.
/// ES-REQ-006: The system SHALL maintain a model request queue that batches tasks by model affinity.
/// </summary>
public class ModelQueueExecutiveSummaryTests
{
    [Fact]
    public async Task ES_REQ_006_NormalPriority_BatchesByModelAffinity()
    {
        // Arrange
        var options = CreateOptions();
        options.ChatModelId = "phi-3-mini";
        options.CodeModelId = "phi-4-mini";

        var mockFoundryBridge = new Mock<IFoundryBridge>();
        var mockModelLifecycleManager = new Mock<IModelLifecycleManager>();
        var mockOnlineRouter = new Mock<IOnlineProviderRouter>();
        var mockLogger = new Mock<ILogger<ModelQueue>>();
        var executedModels = new List<string>();

        mockModelLifecycleManager.Setup(x => x.GetLoadedModelAsync())
            .ReturnsAsync("phi-3-mini");

        mockModelLifecycleManager.Setup(x => x.GetLastModelSwitchAsync())
            .ReturnsAsync(DateTimeOffset.UtcNow);

        mockModelLifecycleManager.Setup(x => x.SwitchModelAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        mockFoundryBridge.Setup(x => x.ExecuteAsync(It.IsAny<ExecutionRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExecutionRequest request, string model, CancellationToken ct) =>
            {
                executedModels.Add(model);
                return new ExecutionResult
                {
                    RequestId = request.Id,
                    Content = "ok",
                    Status = ExecutionStatus.Completed,
                    TokenUsage = new TokenUsage { InputTokens = 5, OutputTokens = 5 }
                };
            });

        var queue = new ModelQueue(
            mockFoundryBridge.Object,
            mockModelLifecycleManager.Object,
            mockOnlineRouter.Object,
            Options.Create(options),
            Options.Create(new LocalFirstRouteOptions()),
            mockLogger.Object);

        var chat1 = new ExecutionRequest { TaskType = "chat", Content = "chat-1" };
        var code1 = new ExecutionRequest { TaskType = "code", Content = "code-1" };
        var chat2 = new ExecutionRequest { TaskType = "chat", Content = "chat-2" };

        // Act
        var chatId1 = await queue.EnqueueAsync(chat1, ExecutionPriority.Normal);
        var codeId1 = await queue.EnqueueAsync(code1, ExecutionPriority.Normal);
        var chatId2 = await queue.EnqueueAsync(chat2, ExecutionPriority.Normal);

        await Task.WhenAll(
            queue.ProcessAsync(chatId1),
            queue.ProcessAsync(codeId1),
            queue.ProcessAsync(chatId2)).WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        Assert.True(executedModels.Count >= 3);

        // With affinity batching enabled, chat requests are grouped before switching to code.
        var firstCodeIndex = executedModels.FindIndex(m => m == options.CodeModelId);
        var lastChatIndex = executedModels.FindLastIndex(m => m == options.ChatModelId);

        Assert.True(firstCodeIndex >= 0, "Expected at least one code-model execution");
        Assert.True(lastChatIndex >= 0, "Expected at least one chat-model execution");
        Assert.True(lastChatIndex <= firstCodeIndex,
            $"Expected chat-model executions to be batched before switching. Executed models: {string.Join(",", executedModels)}");
    }

    [Fact]
    public async Task ES_REQ_006_WhenCurrentModelHasNoPendingRequests_SelectsDominantPendingModel()
    {
        // Arrange
        var options = CreateOptions();
        options.ChatModelId = "phi-3-mini";
        options.CodeModelId = "phi-4-mini";

        var mockFoundryBridge = new Mock<IFoundryBridge>();
        var mockModelLifecycleManager = new Mock<IModelLifecycleManager>();
        var mockOnlineRouter = new Mock<IOnlineProviderRouter>();
        var mockLogger = new Mock<ILogger<ModelQueue>>();
        var executedModels = new List<string>();

        mockModelLifecycleManager.Setup(x => x.GetLoadedModelAsync())
            .ReturnsAsync("phi-vision");

        mockModelLifecycleManager.Setup(x => x.GetLastModelSwitchAsync())
            .ReturnsAsync(DateTimeOffset.UtcNow);

        mockModelLifecycleManager.Setup(x => x.SwitchModelAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        mockFoundryBridge.Setup(x => x.ExecuteAsync(It.IsAny<ExecutionRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExecutionRequest request, string model, CancellationToken ct) =>
            {
                executedModels.Add(model);
                return new ExecutionResult
                {
                    RequestId = request.Id,
                    Content = "ok",
                    Status = ExecutionStatus.Completed,
                    TokenUsage = new TokenUsage { InputTokens = 5, OutputTokens = 5 }
                };
            });

        var queue = new ModelQueue(
            mockFoundryBridge.Object,
            mockModelLifecycleManager.Object,
            mockOnlineRouter.Object,
            Options.Create(options),
            Options.Create(new LocalFirstRouteOptions()),
            mockLogger.Object);

        var chatRequest = new ExecutionRequest { TaskType = "chat", Content = "chat" };
        var codeRequest1 = new ExecutionRequest { TaskType = "code", Content = "code-1" };
        var codeRequest2 = new ExecutionRequest { TaskType = "code", Content = "code-2" };

        // Act
        var chatId = await queue.EnqueueAsync(chatRequest, ExecutionPriority.Normal);
        var codeId1 = await queue.EnqueueAsync(codeRequest1, ExecutionPriority.Normal);
        var codeId2 = await queue.EnqueueAsync(codeRequest2, ExecutionPriority.Normal);

        await Task.WhenAll(
            queue.ProcessAsync(chatId),
            queue.ProcessAsync(codeId1),
            queue.ProcessAsync(codeId2)).WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        Assert.NotEmpty(executedModels);
        Assert.Equal(options.CodeModelId, executedModels[0]);
    }

    private static ModelQueueOptions CreateOptions()
    {
        return new ModelQueueOptions
        {
            DefaultModelId = "phi-3-mini",
            ChatModelId = "phi-3-mini",
            CodeModelId = "phi-3-mini",
            SummarizeModelId = "phi-3-mini"
        };
    }
}
