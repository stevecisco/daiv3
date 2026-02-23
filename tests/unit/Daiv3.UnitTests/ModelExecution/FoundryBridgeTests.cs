using Daiv3.ModelExecution;
using Daiv3.ModelExecution.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Daiv3.UnitTests.ModelExecution;

public class FoundryBridgeTests
{
    private readonly Mock<ILogger<FoundryBridge>> _mockLogger;

    public FoundryBridgeTests()
    {
        _mockLogger = new Mock<ILogger<FoundryBridge>>();
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_ReturnsResult()
    {
        // Arrange
        var bridge = new FoundryBridge(_mockLogger.Object);
        var request = new ExecutionRequest
        {
            Id = Guid.NewGuid(),
            TaskType = "chat",
            Content = "Hello, world!"
        };

        // Act
        var result = await bridge.ExecuteAsync(request, "phi-3-mini");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(request.Id, result.RequestId);
        Assert.Equal(ExecutionStatus.Completed, result.Status);
        Assert.NotEmpty(result.Content);
    }

    [Fact]
    public async Task ExecuteAsync_NullRequest_ThrowsArgumentNullException()
    {
        // Arrange
        var bridge = new FoundryBridge(_mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => bridge.ExecuteAsync(null!, "phi-3-mini"));
    }

    [Fact]
    public async Task ExecuteAsync_EmptyModelId_ThrowsArgumentException()
    {
        // Arrange
        var bridge = new FoundryBridge(_mockLogger.Object);
        var request = new ExecutionRequest { TaskType = "chat", Content = "Test" };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => bridge.ExecuteAsync(request, ""));
    }

    [Fact]
    public async Task GetLoadedModelAsync_AfterExecution_ReturnsModelId()
    {
        // Arrange
        var bridge = new FoundryBridge(_mockLogger.Object);
        var request = new ExecutionRequest { TaskType = "chat", Content = "Test" };
        var modelId = "phi-3-mini";

        // Act
        await bridge.ExecuteAsync(request, modelId);
        var loadedModel = await bridge.GetLoadedModelAsync();

        // Assert
        Assert.Equal(modelId, loadedModel);
    }

    [Fact]
    public async Task GetLoadedModelAsync_NoExecution_ReturnsNull()
    {
        // Arrange
        var bridge = new FoundryBridge(_mockLogger.Object);

        // Act
        var loadedModel = await bridge.GetLoadedModelAsync();

        // Assert
        Assert.Null(loadedModel);
    }

    [Fact]
    public async Task ListAvailableModelsAsync_ReturnsModels()
    {
        // Arrange
        var bridge = new FoundryBridge(_mockLogger.Object);

        // Act
        var models = await bridge.ListAvailableModelsAsync();

        // Assert
        Assert.NotEmpty(models);
        Assert.Contains("phi-3-mini", models);
    }

    [Fact]
    public async Task ExecuteAsync_TracksTokenUsage()
    {
        // Arrange
        var bridge = new FoundryBridge(_mockLogger.Object);
        var request = new ExecutionRequest { TaskType = "chat", Content = "Hello" };

        // Act
        var result = await bridge.ExecuteAsync(request, "phi-3-mini");

        // Assert
        Assert.NotNull(result.TokenUsage);
        Assert.True(result.TokenUsage.InputTokens > 0);
        Assert.True(result.TokenUsage.OutputTokens > 0);
        Assert.Equal(result.TokenUsage.InputTokens + result.TokenUsage.OutputTokens, result.TokenUsage.TotalTokens);
    }

    [Fact]
    public async Task ExecuteAsync_DifferentModels_UpdatesLoadedModel()
    {
        // Arrange
        var bridge = new FoundryBridge(_mockLogger.Object);
        var request = new ExecutionRequest { TaskType = "chat", Content = "Test" };

        // Act
        await bridge.ExecuteAsync(request, "phi-3-mini");
        var model1 = await bridge.GetLoadedModelAsync();

        await bridge.ExecuteAsync(request, "phi-3.5-mini");
        var model2 = await bridge.GetLoadedModelAsync();

        // Assert
        Assert.Equal("phi-3-mini", model1);
        Assert.Equal("phi-3.5-mini", model2);
    }

    [Fact]
    public async Task ExecuteAsync_CompletesWithinReasonableTime()
    {
        // Arrange
        var bridge = new FoundryBridge(_mockLogger.Object);
        var request = new ExecutionRequest { TaskType = "chat", Content = "Quick test" };
        var startTime = DateTimeOffset.UtcNow;

        // Act
        await bridge.ExecuteAsync(request, "phi-3-mini");
        var duration = DateTimeOffset.UtcNow - startTime;

        // Assert
        Assert.True(duration.TotalSeconds < 5, "Execution took longer than expected");
    }

    [Fact]
    public async Task ExecuteAsync_PreservesRequestId()
    {
        // Arrange
        var bridge = new FoundryBridge(_mockLogger.Object);
        var requestId = Guid.NewGuid();
        var request = new ExecutionRequest
        {
            Id = requestId,
            TaskType = "chat",
            Content = "Test"
        };

        // Act
        var result = await bridge.ExecuteAsync(request, "phi-3-mini");

        // Assert
        Assert.Equal(requestId, result.RequestId);
    }
}
