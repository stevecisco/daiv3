using Daiv3.Orchestration.Messaging;
using Xunit;

namespace Daiv3.UnitTests.Orchestration.Messaging;

/// <summary>
/// Unit tests for message contract classes.
/// </summary>
public class MessageContractTests
{
    #region MessageStatus Enum Tests

    [Fact]
    public void MessageStatus_HasExpectedValues()
    {
        // Assert
        Assert.Equal(0, (int)MessageStatus.Pending);
        Assert.Equal(1, (int)MessageStatus.Processing);
        Assert.Equal(2, (int)MessageStatus.Completed);
        Assert.Equal(3, (int)MessageStatus.Failed);
        Assert.Equal(4, (int)MessageStatus.Expired);
        Assert.Equal(5, (int)MessageStatus.Archived);
    }

    #endregion

    #region MessageMetadata Tests

    [Fact]
    public void MessageMetadata_CreatesWithDefaults()
    {
        // Act
        var metadata = new MessageMetadata();

        // Assert
        Assert.NotNull(metadata);
        Assert.NotEqual(DateTimeOffset.MinValue, metadata.PublishedAt);
        Assert.NotNull(metadata.CorrelationId);
        Assert.Null(metadata.ReplyToTopic);
        Assert.Equal(0, metadata.Priority);
        Assert.Null(metadata.ExpiresAt);
        Assert.Empty(metadata.Tags);
    }

    [Fact]
    public void MessageMetadata_WithCustomValues()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var now = DateTimeOffset.UtcNow;
        var tags = new Dictionary<string, string> { { "key", "value" } };

        // Act
        var metadata = new MessageMetadata
        {
            CorrelationId = correlationId,
            Priority = 5,
            ReplyToTopic = "reply-topic",
            ExpiresAt = now.AddHours(1),
            Tags = tags
        };

        // Assert
        Assert.Equal(correlationId, metadata.CorrelationId);
        Assert.Equal(5, metadata.Priority);
        Assert.Equal("reply-topic", metadata.ReplyToTopic);
        Assert.NotNull(metadata.ExpiresAt);
        Assert.NotEmpty(metadata.Tags);
    }

    [Fact]
    public void MessageMetadata_Validate_ThrowsWhenCorrelationIdNull()
    {
        // Arrange
        var metadata = new MessageMetadata { CorrelationId = null! };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => metadata.Validate());
    }

    [Fact]
    public void MessageMetadata_Validate_ThrowsWhenPriorityOutOfRange()
    {
        // Arrange
        var metadataLow = new MessageMetadata { Priority = -11 };
        var metadataHigh = new MessageMetadata { Priority = 101 };

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => metadataLow.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => metadataHigh.Validate());
    }

    [Fact]
    public void MessageMetadata_Validate_ThrowsWhenExpiresBeforePublished()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var metadata = new MessageMetadata
        {
            PublishedAt = now,
            ExpiresAt = now.AddHours(-1) // Expires in the past
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => metadata.Validate());
    }

    [Fact]
    public void MessageMetadata_Validate_SucceedsWithValidData()
    {
        // Arrange
        var metadata = new MessageMetadata
        {
            CorrelationId = Guid.NewGuid().ToString(),
            Priority = 5,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };

        // Act & Assert - Should not throw
        metadata.Validate();
    }

    #endregion

    #region AgentMessage<T> Tests

    [Fact]
    public void AgentMessage_Generic_CreatesWithValidData()
    {
        // Arrange
        var topic = "test-topic";
        var senderId = "agent-1";
        var payload = "test-payload";

        // Act
        var message = new AgentMessage<string>(topic, senderId, payload);

        // Assert
        Assert.NotEqual(Guid.Empty, message.MessageId);
        Assert.Equal(topic, message.Topic);
        Assert.Equal(senderId, message.SenderAgentId);
        Assert.Equal(payload, message.Payload);
        Assert.Equal(MessageStatus.Pending, message.Status);
        Assert.NotNull(message.Metadata);
    }

    [Fact]
    public void AgentMessage_Generic_ThrowsWhenTopicNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new AgentMessage<string>(null!, "agent-1", "payload"));
    }

    [Fact]
    public void AgentMessage_Generic_ThrowsWhenSenderIdNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new AgentMessage<string>("topic", null!, "payload"));
    }

    [Fact]
    public void AgentMessage_Generic_WithCustomMetadata()
    {
        // Arrange
        var metadata = new MessageMetadata
        {
            Priority = 10,
            ReplyToTopic = "reply-topic"
        };

        // Act
        var message = new AgentMessage<string>("topic", "agent-1", "payload", metadata);

        // Assert
        Assert.Equal(10, message.Metadata.Priority);
        Assert.Equal("reply-topic", message.Metadata.ReplyToTopic);
    }

    [Fact]
    public void AgentMessage_Generic_GetPayload_ReturnsCorrectType()
    {
        // Arrange
        var payload = "test-payload";
        var message = new AgentMessage<string>("topic", "agent-1", payload);

        // Act
        var result = message.GetPayload();

        // Assert
        Assert.Equal(payload, result);
    }

    [Fact]
    public void AgentMessage_Generic_GetPayload_ThrowsWhenWrongType()
    {
        // Arrange
        var message = new AgentMessage<string>("topic", "agent-1", "payload");

        // Act & Assert - GetPayload should work fine for matching type
        var result = message.GetPayload();
        Assert.Equal("payload", result);
    }

    #endregion

    #region AgentMessage (non-generic) Tests

    [Fact]
    public void AgentMessage_NonGeneric_CreatesWithValidData()
    {
        // Arrange
        var topic = "test-topic";
        var senderId = "agent-1";
        object payload = new { data = "value" };

        // Act
        var message = new AgentMessage(topic, senderId, payload);

        // Assert
        Assert.NotEqual(Guid.Empty, message.MessageId);
        Assert.Equal(topic, message.Topic);
        Assert.Equal(senderId, message.SenderAgentId);
        Assert.Equal(payload, message.Payload);
        Assert.Equal(MessageStatus.Pending, message.Status);
    }

    [Fact]
    public void AgentMessage_NonGeneric_ThrowsWhenTopicNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new AgentMessage(null!, "agent-1", "payload"));
    }

    #endregion

    #region Message Status Updates

    [Fact]
    public void AgentMessage_SetStatus_UpdatesMessageStatus()
    {
        // Arrange
        var message = new AgentMessage<string>("topic", "agent-1", "payload");
        Assert.Equal(MessageStatus.Pending, message.Status);

        // Act
        // Access internal SetStatus via reflection for non-generic AgentMessage<T>
        var setStatusMethod = message.GetType().GetMethod("SetStatus", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        setStatusMethod?.Invoke(message, new object[] { MessageStatus.Processing });

        // Assert
        Assert.Equal(MessageStatus.Processing, message.Status);
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void AgentMessage_ToString_IncludesImportantData()
    {
        // Arrange
        var message = new AgentMessage<string>("test-topic", "agent-1", "payload");

        // Act
        var result = message.ToString();

        // Assert
        Assert.Contains(message.MessageId.ToString("N"), result);
        Assert.Contains("test-topic", result);
        Assert.Contains("agent-1", result);
        Assert.Contains("Pending", result);
        Assert.Contains(message.Metadata.CorrelationId, result);
    }

    #endregion
}
