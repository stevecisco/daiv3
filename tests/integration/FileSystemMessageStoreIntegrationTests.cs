using Daiv3.Orchestration.Messaging;
using Daiv3.Orchestration.Messaging.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Daiv3.UnitTests.Orchestration.Messaging;

/// <summary>
/// Integration tests for FileSystemMessageStore with real file I/O.
/// </summary>
public class FileSystemMessageStoreTests : IDisposable
{
    private readonly string _testStorageDir;
    private readonly IMessageStore _store;
    private readonly ILoggerFactory _loggerFactory;

    public FileSystemMessageStoreTests()
    {
        _testStorageDir = Path.Combine(Path.GetTempPath(), $"daiv3-msg-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testStorageDir);

        _loggerFactory = LoggerFactory.Create(b => b.AddConsole());

        var options = new FileSystemMessageStoreOptions { StorageDirectory = _testStorageDir };
        _store = new FileSystemMessageStore(
            _loggerFactory.CreateLogger<FileSystemMessageStore>(),
            Options.Create(options));
    }

    #region SaveMessage Tests

    [Fact]
    public async Task SaveMessageAsync_WithValidMessage_CreatesFile()
    {
        // Arrange
        var message = new AgentMessage<string>("test-topic", "agent-1", "test payload");

        // Act
        await _store.SaveMessageAsync(message);

        // Assert
        var topicDir = Path.Combine(_testStorageDir, "test-topic");
        Assert.True(Directory.Exists(topicDir));

        var file = Path.Combine(topicDir, $"{message.MessageId:N}.json");
        Assert.True(File.Exists(file));
    }

    [Fact]
    public async Task SaveMessageAsync_WithNullMessage_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _store.SaveMessageAsync(null!));
    }

    #endregion

    #region LoadMessage Tests

    [Fact]
    public async Task LoadMessageAsync_WhenExists_ReturnsMessage()
    {
        // Arrange
        var original = new AgentMessage<string>("test-topic", "agent-1", "test payload");
        await _store.SaveMessageAsync(original);

        // Act
        var loaded = await _store.LoadMessageAsync(original.MessageId);

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal(original.MessageId, loaded.MessageId);
        Assert.Equal("test-topic", loaded.Topic);
        Assert.Equal("agent-1", loaded.SenderAgentId);
    }

    [Fact]
    public async Task LoadMessageAsync_WhenNotExists_ReturnsNull()
    {
        // Act
        var loaded = await _store.LoadMessageAsync(Guid.NewGuid());

        // Assert
        Assert.Null(loaded);
    }

    #endregion

    #region LoadByTopic Tests

    [Fact]
    public async Task LoadMessagesByTopicAsync_WithExistingTopic_ReturnsMessages()
    {
        // Arrange
        var msg1 = new AgentMessage<string>("topic-1", "agent-1", "payload1");
        var msg2 = new AgentMessage<string>("topic-1", "agent-2", "payload2");
        await _store.SaveMessageAsync(msg1);
        await _store.SaveMessageAsync(msg2);

        // Act
        var messages = await _store.LoadMessagesByTopicAsync("topic-1");

        // Assert
        Assert.Equal(2, messages.Count);
    }

    [Fact]
    public async Task LoadMessagesByTopicAsync_WithNonExistingTopic_ReturnsEmpty()
    {
        // Act
        var messages = await _store.LoadMessagesByTopicAsync("non-existent");

        // Assert
        Assert.Empty(messages);
    }

    #endregion

    #region UpdateStatus Tests

    [Fact]
    public async Task UpdateMessageStatusAsync_WithExistingMessage_UpdatesStatus()
    {
        // Arrange
        var message = new AgentMessage<string>("test-topic", "agent-1", "payload");
        await _store.SaveMessageAsync(message);

        // Act
        await _store.UpdateMessageStatusAsync(message.MessageId, MessageStatus.Processing);

        // Assert
        var loaded = await _store.LoadMessageAsync(message.MessageId);
        Assert.NotNull(loaded);
        Assert.Equal(MessageStatus.Processing, loaded.Status);
    }

    [Fact]
    public async Task UpdateMessageStatusAsync_WithMetadata_StoresMetadata()
    {
        // Arrange
        var message = new AgentMessage<string>("test-topic", "agent-1", "payload");
        await _store.SaveMessageAsync(message);
        var metadata = new Dictionary<string, object> { { "error", "test error" } };

        // Act
        await _store.UpdateMessageStatusAsync(message.MessageId, MessageStatus.Failed, metadata);

        // Assert
        var loaded = await _store.LoadMessageAsync(message.MessageId);
        Assert.NotNull(loaded);
        Assert.Equal(MessageStatus.Failed, loaded.Status);
    }

    #endregion

    #region QueryMessages Tests

    [Fact]
    public async Task QueryMessagesAsync_WithTopicPattern_ReturnsMatchingMessages()
    {
        // Arrange
        var msg1 = new AgentMessage<string>("task-execution/agent-1", "agent-1", "payload1");
        var msg2 = new AgentMessage<string>("task-execution/agent-2", "agent-2", "payload2");
        var msg3 = new AgentMessage<string>("knowledge-update", "agent-1", "payload3");
        
        await _store.SaveMessageAsync(msg1);
        await _store.SaveMessageAsync(msg2);
        await _store.SaveMessageAsync(msg3);

        // Act
        var query = new PendingMessageQuery { TopicPattern = "task-execution/*" };
        var messages = await _store.QueryMessagesAsync(query);

        // Assert
        Assert.Equal(2, messages.Count);
    }

    [Fact]
    public async Task QueryMessagesAsync_WithSenderFilter_ReturnsMatchingMessages()
    {
        // Arrange
        var msg1 = new AgentMessage<string>("test-topic", "agent-1", "payload1");
        var msg2 = new AgentMessage<string>("test-topic", "agent-2", "payload2");
        
        await _store.SaveMessageAsync(msg1);
        await _store.SaveMessageAsync(msg2);

        // Act
        var query = new PendingMessageQuery { SenderAgentId = "agent-1" };
        var messages = await _store.QueryMessagesAsync(query);

        // Assert
        Assert.Single(messages);
        Assert.Equal("agent-1", messages.First().SenderAgentId);
    }

    [Fact]
    public async Task QueryMessagesAsync_SortsByPriority()
    {
        // Arrange
        var lowPriority = new AgentMessage<string>(
            "test-topic", "agent-1", "payload",
            new MessageMetadata { Priority = 0 });
        var highPriority = new AgentMessage<string>(
            "test-topic", "agent-1", "payload",
            new MessageMetadata { Priority = 10 });
        
        await _store.SaveMessageAsync(lowPriority);
        await _store.SaveMessageAsync(highPriority);

        // Act
        var query = new PendingMessageQuery { SortByPriority = true, PageSize = 10 };
        var messages = await _store.QueryMessagesAsync(query);

        // Assert
        Assert.Equal(2, messages.Count);
        Assert.Equal(10, messages.First().Metadata.Priority);
        Assert.Equal(0, messages.Last().Metadata.Priority);
    }

    #endregion

    #region Cleanup Tests

    [Fact]
    public async Task CleanupExpiredAsync_RemovesExpiredMessages()
    {
        // Arrange
        var expiredTime = DateTimeOffset.UtcNow.AddDays(-8);
        var expiredMessage = new AgentMessage<string>(
            "test-topic", "agent-1", "payload",
            new MessageMetadata { ExpiresAt = expiredTime });
        
        // Update status to mark it as completed earlier
        await _store.SaveMessageAsync(expiredMessage);
        await _store.UpdateMessageStatusAsync(
            expiredMessage.MessageId, 
            MessageStatus.Completed);

        var fresh = new AgentMessage<string>("test-topic", "agent-1", "payload");
        await _store.SaveMessageAsync(fresh);

        // Act
        var cleaned = await _store.CleanupExpiredAsync(retentionPolicyCompleted: 7, retentionPolicyFailed: 30);

        // Assert
        Assert.Equal(1, cleaned);
        var stillExists = await _store.LoadMessageAsync(fresh.MessageId);
        Assert.NotNull(stillExists);
    }

    #endregion

    #region GetMessageCount Tests

    [Fact]
    public async Task GetMessageCountAsync_ReturnsCorrectCount()
    {
        // Arrange
        var msg1 = new AgentMessage<string>("test-topic", "agent-1", "payload1");
        var msg2 = new AgentMessage<string>("test-topic", "agent-2", "payload2");
        await _store.SaveMessageAsync(msg1);
        await _store.SaveMessageAsync(msg2);

        // Act
        var count = await _store.GetMessageCountAsync("test-topic");

        // Assert
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task GetMessageCountAsync_WithNonExistentTopic_ReturnsZero()
    {
        // Act
        var count = await _store.GetMessageCountAsync("non-existent");

        // Assert
        Assert.Equal(0, count);
    }

    #endregion

    #region Health Tests

    [Fact]
    public async Task ValidateHealthAsync_WhenAccessible_ReturnsHealthy()
    {
        // Act
        var (isHealthy, message) = await _store.ValidateHealthAsync();

        // Assert
        Assert.True(isHealthy);
        Assert.Contains("healthy", message.ToLower());
    }

    #endregion

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testStorageDir))
                Directory.Delete(_testStorageDir, recursive: true);
        }
        catch
        {
            // Ignore cleanup errors
        }

        _loggerFactory?.Dispose();
    }
}
