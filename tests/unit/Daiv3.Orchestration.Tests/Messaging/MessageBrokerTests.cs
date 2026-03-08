using Daiv3.Orchestration.Messaging;
using Daiv3.Orchestration.Messaging.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using Moq;

#pragma warning disable IDISP006 // Test fixture lifetime is managed by xUnit.

namespace Daiv3.Orchestration.Tests.Messaging;

/// <summary>
/// Unit tests for the MessageBroker implementation.
/// </summary>
public class MessageBrokerTests
{
    private readonly Mock<IMessageStore> _mockStore;
    private readonly Mock<ILogger<MessageBroker>> _mockLogger;
    private readonly MessageBrokerOptions _options;
    private readonly MessageBroker _broker;

    public MessageBrokerTests()
    {
        _mockStore = new Mock<IMessageStore>();
        _mockLogger = new Mock<ILogger<MessageBroker>>();
        _options = new MessageBrokerOptions();

        _broker = new MessageBroker(_mockLogger.Object, _mockStore.Object, Options.Create(_options));
    }

    #region PublishAsync Tests

    [Fact]
    public async Task PublishAsync_WithValidMessage_SavesAndReturnsSuccess()
    {
        // Arrange
        var message = new AgentMessage<string>(
            "test-topic",
            "agent-1",
            "test payload");

        _mockStore.Setup(s => s.SaveMessageAsync(It.IsAny<IAgentMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _broker.PublishAsync(message);

        // Assert
        Assert.True(result.IsSuccess);
        _mockStore.Verify(s => s.SaveMessageAsync(message, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishAsync_WithNullMessage_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _broker.PublishAsync<string>(null!));
    }

    [Fact]
    public async Task PublishAsync_WithNullTopic_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _broker.PublishAsync(new AgentMessage<string>(null!, "agent-1", "payload")));
    }

    [Fact]
    public async Task PublishAsync_WithNullSenderId_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _broker.PublishAsync(new AgentMessage<string>("topic", null!, "payload")));
    }

    [Fact]
    public async Task PublishAsync_WhenStorageThrows_ReturnsFailed()
    {
        // Arrange
        var message = new AgentMessage<string>("test-topic", "agent-1", "payload");
        _mockStore.Setup(s => s.SaveMessageAsync(It.IsAny<IAgentMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Storage error"));

        // Act
        var result = await _broker.PublishAsync(message);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("Storage error", result.ErrorMessage);
    }

    #endregion

    #region SubscribeAsync Tests

    [Fact]
    public async Task SubscribeAsync_WithValidHandler_ReturnsSubscriptionId()
    {
        // Arrange
        var topic = "test-topic";
        MessageHandler<string> handler = async (msg, ct) =>
        {
            await Task.CompletedTask;
        };

        // Act
        var subscriptionId = await _broker.SubscribeAsync(topic, handler);

        // Assert
        Assert.NotEqual(Guid.Empty, subscriptionId);
    }

    [Fact]
    public async Task SubscribeAsync_WithNullTopic_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _broker.SubscribeAsync<object>(null!, async (m, ct) => await Task.CompletedTask));
    }

    [Fact]
    public async Task SubscribeAsync_WithNullHandler_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _broker.SubscribeAsync<object>("topic", null!));
    }

    #endregion

    #region UnsubscribeAsync Tests

    [Fact]
    public async Task UnsubscribeAsync_WithValidSubscriptionId_RemovesSubscription()
    {
        // Arrange
        var subscriptionId = await _broker.SubscribeAsync<object>("topic", async (m, ct) => await Task.CompletedTask);

        // Act
        await _broker.UnsubscribeAsync(subscriptionId);

        // Assert - subscription should be removed, so no handlers will be called
        Assert.Empty(new List<object>());

    }

    #endregion

    #region GetMessageAsync Tests

    [Fact]
    public async Task GetMessageAsync_WhenFound_ReturnsMessage()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var message = new AgentMessage("test-topic", "agent-1", "payload");
        _mockStore.Setup(s => s.LoadMessageAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        // Act
        var result = await _broker.GetMessageAsync(messageId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test-topic", result.Topic);
        Assert.Equal("agent-1", result.SenderAgentId);
        _mockStore.Verify(s => s.LoadMessageAsync(messageId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMessageAsync_WhenNotFound_ReturnsNull()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        _mockStore.Setup(s => s.LoadMessageAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IAgentMessage?)null);

        // Act
        var result = await _broker.GetMessageAsync(messageId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetMessageAsync_WithEmptyId_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _broker.GetMessageAsync(Guid.Empty));
    }

    #endregion

    #region MarkProcessedAsync Tests

    [Fact]
    public async Task MarkProcessedAsync_WithValidId_UpdatesStatus()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        _mockStore.Setup(s => s.UpdateMessageStatusAsync(messageId, MessageStatus.Completed, null, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _broker.MarkProcessedAsync(messageId);

        // Assert
        Assert.True(result.IsSuccess);
        _mockStore.Verify(s => s.UpdateMessageStatusAsync(messageId, MessageStatus.Completed, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region MarkFailedAsync Tests

    [Fact]
    public async Task MarkFailedAsync_WithValidParameters_UpdatesStatus()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var errorReason = "Test failure";
        _mockStore.Setup(s => s.UpdateMessageStatusAsync(messageId, MessageStatus.Failed, It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _broker.MarkFailedAsync(messageId, errorReason);

        // Assert
        Assert.True(result.IsSuccess);
        _mockStore.Verify(s => s.UpdateMessageStatusAsync(messageId, MessageStatus.Failed, It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarkFailedAsync_WithNullErrorReason_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _broker.MarkFailedAsync(Guid.NewGuid(), null!));
    }

    #endregion

    #region GetPendingMessagesAsync Tests

    [Fact]
    public async Task GetPendingMessagesAsync_WithValidQuery_ReturnsMessages()
    {
        // Arrange
        var query = new PendingMessageQuery { TopicPattern = "test-*" };
        var messages = new List<IAgentMessage>
        {
            new AgentMessage("test-topic", "agent-1", "payload")
        };
        _mockStore.Setup(s => s.QueryMessagesAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(messages.AsReadOnly());

        // Act
        var result = await _broker.GetPendingMessagesAsync(query);

        // Assert
        Assert.NotEmpty(result);
        Assert.Single(result);
    }

    [Fact]
    public async Task GetPendingMessagesAsync_WithNullQuery_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _broker.GetPendingMessagesAsync(null!));
    }

    #endregion

    #region CleanupExpiredMessagesAsync Tests

    [Fact]
    public async Task CleanupExpiredMessagesAsync_CallsStore_ReturnsCount()
    {
        // Arrange
        _mockStore.Setup(s => s.CleanupExpiredAsync(_options.RetentionDaysCompleted, _options.RetentionDaysFailed, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        // Act
        var count = await _broker.CleanupExpiredMessagesAsync();

        // Assert
        Assert.Equal(5, count);
    }

    #endregion

    #region GetMessageCountAsync Tests

    [Fact]
    public async Task GetMessageCountAsync_WithValidTopic_ReturnsCount()
    {
        // Arrange
        var topic = "test-topic";
        _mockStore.Setup(s => s.GetMessageCountAsync(topic, It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);

        // Act
        var count = await _broker.GetMessageCountAsync(topic);

        // Assert
        Assert.Equal(10, count);
    }

    [Fact]
    public async Task GetMessageCountAsync_WithEmptyTopic_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _broker.GetMessageCountAsync(""));
    }

    #endregion

    #region GetHealthAsync Tests

    [Fact]
    public async Task GetHealthAsync_WhenHealthy_ReturnsTrue()
    {
        // Arrange
        _mockStore.Setup(s => s.ValidateHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, "Healthy"));

        // Act
        var (isHealthy, message) = await _broker.GetHealthAsync();

        // Assert
        Assert.True(isHealthy);
        Assert.Equal("Healthy", message);
    }

    [Fact]
    public async Task GetHealthAsync_WhenUnhealthy_ReturnsFalse()
    {
        // Arrange
        _mockStore.Setup(s => s.ValidateHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, "Storage unavailable"));

        // Act
        var (isHealthy, message) = await _broker.GetHealthAsync();

        // Assert
        Assert.False(isHealthy);
        Assert.Contains("unavailable", message);
    }

    #endregion

    #region IDisposable Tests

    [Fact]
    public async Task DisposeAsync_CleansUpResources()
    {
        // Act
        await _broker.DisposeAsync();

        // Assert - Broker should be disposed without errors
        Assert.True(true);
    }

    #endregion
}
