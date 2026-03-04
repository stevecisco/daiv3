using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Daiv3.Orchestration.Messaging.Storage;

/// <summary>
/// File system-based message store for local agent communication.
/// Persists messages as JSON files in a configurable directory structure.
/// </summary>
public class FileSystemMessageStore : IMessageStore
{
    private readonly ILogger<FileSystemMessageStore> _logger;
    private readonly FileSystemMessageStoreOptions _options;
    private readonly JsonSerializerOptions _jsonOptions;

    public FileSystemMessageStore(
        ILogger<FileSystemMessageStore> logger,
        IOptions<FileSystemMessageStoreOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true,
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
                new DateTimeOffsetConverter()
            }
        };

        _options.Validate();
        EnsureStorageDirectory();
    }

    /// <inheritdoc />
    public async Task SaveMessageAsync(IAgentMessage message, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        try
        {
            var topicDir = GetTopicDirectory(message.Topic);
            Directory.CreateDirectory(topicDir);

            var filePath = Path.Combine(topicDir, $"{message.MessageId:N}.json");
            var json = JsonSerializer.Serialize(ConvertToSerializable(message), _jsonOptions);

            await File.WriteAllTextAsync(filePath, json, ct).ConfigureAwait(false);

            _logger.LogDebug(
                "Message {MessageId} saved to {FilePath}",
                message.MessageId, filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to save message {MessageId} to file system",
                message.MessageId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IAgentMessage?> LoadMessageAsync(Guid messageId, CancellationToken ct = default)
    {
        try
        {
            var baseDir = _options.StorageDirectory;
            var topicDirs = Directory.EnumerateDirectories(baseDir, "*", SearchOption.TopDirectoryOnly);

            foreach (var topicDir in topicDirs)
            {
                var filePath = Path.Combine(topicDir, $"{messageId:N}.json");
                if (!File.Exists(filePath))
                    continue;

                var json = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);
                var serializable = JsonSerializer.Deserialize<SerializableAgentMessage>(json, _jsonOptions);

                if (serializable != null)
                {
                    _logger.LogDebug("Message {MessageId} loaded from {FilePath}", messageId, filePath);
                    return DeserializeMessage(serializable);
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to load message {MessageId} from file system",
                messageId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IAgentMessage>> LoadMessagesByTopicAsync(
        string topic,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        try
        {
            var topicDir = GetTopicDirectory(topic);
            if (!Directory.Exists(topicDir))
                return Array.Empty<IAgentMessage>();

            var messages = new List<IAgentMessage>();
            var files = Directory.GetFiles(topicDir, "*.json");

            foreach (var file in files)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    var json = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
                    var serializable = JsonSerializer.Deserialize<SerializableAgentMessage>(json, _jsonOptions);
                    if (serializable != null)
                    {
                        messages.Add(DeserializeMessage(serializable));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize message from {File}", file);
                }
            }

            _logger.LogDebug("Loaded {Count} messages from topic {Topic}", messages.Count, topic);
            return messages.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load messages from topic {Topic}", topic);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task UpdateMessageStatusAsync(
        Guid messageId,
        MessageStatus newStatus,
        Dictionary<string, object>? metadata = null,
        CancellationToken ct = default)
    {
        try
        {
            var message = await LoadMessageAsync(messageId, ct).ConfigureAwait(false);
            if (message == null)
            {
                _logger.LogWarning("Message {MessageId} not found for status update", messageId);
                return;
            }

            // Cast to allow modification
            if (message is AgentMessage untypedMsg)
            {
                untypedMsg.SetStatus(newStatus);
            }
            else if (message is IAgentMessage)
            {
                // For generic messages, we need to update the file directly
                var topicDir = GetTopicDirectory(message.Topic);
                var filePath = Path.Combine(topicDir, $"{messageId:N}.json");

                if (File.Exists(filePath))
                {
                    var json = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);
                    var serializable = JsonSerializer.Deserialize<SerializableAgentMessage>(json, _jsonOptions);
                    if (serializable != null)
                    {
                        serializable.Status = newStatus;
                        if (metadata != null)
                        {
                            serializable.Metadata ??= new();
                            foreach (var kvp in metadata)
                            {
                                serializable.Metadata[kvp.Key] = kvp.Value;
                            }
                        }

                        var updatedJson = JsonSerializer.Serialize(serializable, _jsonOptions);
                        await File.WriteAllTextAsync(filePath, updatedJson, ct).ConfigureAwait(false);

                        _logger.LogDebug(
                            "Message {MessageId} status updated to {Status}",
                            messageId, newStatus);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to update message {MessageId} status",
                messageId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IAgentMessage>> QueryMessagesAsync(
        PendingMessageQuery query,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        try
        {
            var baseDir = _options.StorageDirectory;
            var matching = new List<IAgentMessage>();

            if (!Directory.Exists(baseDir))
                return matching.AsReadOnly();

            var topicDirs = Directory.EnumerateDirectories(baseDir);
            var topicPattern = query.TopicPattern ?? "*";

            foreach (var topicDir in topicDirs)
            {
                ct.ThrowIfCancellationRequested();

                var topicName = Path.GetFileName(topicDir);
                if (!MatchesTopic(topicName, topicPattern))
                    continue;

                var messages = await LoadMessagesByTopicAsync(topicName, ct).ConfigureAwait(false);

                foreach (var msg in messages)
                {
                    if (query.SenderAgentId != null && msg.SenderAgentId != query.SenderAgentId)
                        continue;

                    if (msg.Status != MessageStatus.Pending)
                        continue;

                    if (query.MaxAgeHours.HasValue)
                    {
                        var age = DateTimeOffset.UtcNow - msg.Metadata.PublishedAt;
                        if (age.TotalHours > query.MaxAgeHours.Value)
                            continue;
                    }

                    matching.Add(msg);
                }
            }

            // Sort by priority (if enabled) and timestamp
            if (query.SortByPriority)
            {
                matching.Sort((a, b) =>
                {
                    var priorityCmp = b.Metadata.Priority.CompareTo(a.Metadata.Priority);
                    return priorityCmp != 0 ? priorityCmp : a.Metadata.PublishedAt.CompareTo(b.Metadata.PublishedAt);
                });
            }
            else
            {
                matching.Sort((a, b) => a.Metadata.PublishedAt.CompareTo(b.Metadata.PublishedAt));
            }

            return matching.Take(query.PageSize).ToList().AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query messages");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> CleanupExpiredAsync(
        int retentionPolicyCompleted,
        int retentionPolicyFailed,
        CancellationToken ct = default)
    {
        int cleaned = 0;

        try
        {
            var baseDir = _options.StorageDirectory;
            if (!Directory.Exists(baseDir))
                return 0;

            var cutoffCompleted = DateTimeOffset.UtcNow.AddDays(-retentionPolicyCompleted);
            var cutoffFailed = DateTimeOffset.UtcNow.AddDays(-retentionPolicyFailed);

            var topicDirs = Directory.EnumerateDirectories(baseDir);
            foreach (var topicDir in topicDirs)
            {
                ct.ThrowIfCancellationRequested();

                var files = Directory.GetFiles(topicDir, "*.json");
                foreach (var file in files)
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
                        var obj = JsonSerializer.Deserialize<SerializableAgentMessage>(json, _jsonOptions);

                        if (obj == null)
                            continue;

                        bool shouldDelete = obj.Status switch
                        {
                            MessageStatus.Expired => true,
                            MessageStatus.Completed when obj.PublishedAt <= cutoffCompleted => true,
                            MessageStatus.Failed when obj.PublishedAt <= cutoffFailed => true,
                            _ => false
                        };

                        if (shouldDelete)
                        {
                            File.Delete(file);
                            cleaned++;
                            _logger.LogDebug("Deleted expired message {MessageId}", obj.MessageId);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to process file {File} during cleanup", file);
                    }
                }
            }

            _logger.LogInformation("Cleaned up {Count} expired messages", cleaned);
            return cleaned;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup expired messages");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> GetMessageCountAsync(string topic, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        try
        {
            var topicDir = GetTopicDirectory(topic);
            if (!Directory.Exists(topicDir))
                return 0;

            var messageCount = Directory.GetFiles(topicDir, "*.json").Length;
            return await Task.FromResult(messageCount).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get message count for topic {Topic}", topic);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<(bool IsHealthy, string DiagnosticMessage)> ValidateHealthAsync(CancellationToken ct = default)
    {
        try
        {
            var testDir = GetTopicDirectory("__health_check__");
            Directory.CreateDirectory(testDir);

            var testFile = Path.Combine(testDir, $"health-{Guid.NewGuid():N}.txt");
            await File.WriteAllTextAsync(testFile, "OK", ct).ConfigureAwait(false);
            File.Delete(testFile);

            Directory.Delete(testDir, false);

            return (true, $"File system message store healthy. Storage directory: {_options.StorageDirectory}");
        }
        catch (Exception ex)
        {
            return (false, $"File system message store unhealthy: {ex.Message}");
        }
    }

    private string GetTopicDirectory(string topic) =>
        Path.Combine(_options.StorageDirectory, topic.Replace("/", "\\"));

    private void EnsureStorageDirectory()
    {
        try
        {
            Directory.CreateDirectory(_options.StorageDirectory);
            _logger.LogInformation(
                "Message store configured at {StorageDirectory}",
                _options.StorageDirectory);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to create message store directory {StorageDirectory}",
                _options.StorageDirectory);
            throw;
        }
    }

    private static bool MatchesTopic(string topicName, string pattern)
    {
        if (pattern == "*")
            return true;

        if (pattern.EndsWith("/*"))
        {
            var prefix = pattern.Substring(0, pattern.Length - 2);
            return topicName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        return topicName.Equals(pattern, StringComparison.OrdinalIgnoreCase);
    }

    private static SerializableAgentMessage ConvertToSerializable(IAgentMessage message) =>
        new()
        {
            MessageId = message.MessageId,
            Topic = message.Topic,
            SenderAgentId = message.SenderAgentId,
            Status = message.Status,
            PublishedAt = message.Metadata.PublishedAt,
            CorrelationId = message.Metadata.CorrelationId,
            ReplyToTopic = message.Metadata.ReplyToTopic,
            Priority = message.Metadata.Priority,
            ExpiresAt = message.Metadata.ExpiresAt,
            Tags = message.Metadata.Tags as Dictionary<string, string> ??
                   new Dictionary<string, string>(message.Metadata.Tags),
            Payload = message.Payload
        };

    private static IAgentMessage DeserializeMessage(SerializableAgentMessage obj)
    {
        var metadata = new MessageMetadata
        {
            PublishedAt = obj.PublishedAt,
            CorrelationId = obj.CorrelationId ?? Guid.NewGuid().ToString(),
            ReplyToTopic = obj.ReplyToTopic,
            Priority = obj.Priority,
            ExpiresAt = obj.ExpiresAt,
            Tags = obj.Tags ?? new()
        };

        var topic = obj.Topic ?? throw new InvalidOperationException("Message topic cannot be null");
        var senderAgentId = obj.SenderAgentId ?? throw new InvalidOperationException("Message sender cannot be null");

        var message = new AgentMessage(topic, senderAgentId, obj.Payload, metadata);
        message.SetStatus(obj.Status);
        return message;
    }

    /// <summary>
    /// Serializable representation of IAgentMessage for JSON persistence.
    /// </summary>
    private class SerializableAgentMessage
    {
        public Guid MessageId { get; set; }
        public string? Topic { get; set; }
        public string? SenderAgentId { get; set; }
        public MessageStatus Status { get; set; }
        public DateTimeOffset PublishedAt { get; set; }
        public string? CorrelationId { get; set; }
        public string? ReplyToTopic { get; set; }
        public int Priority { get; set; }
        public DateTimeOffset? ExpiresAt { get; set; }
        public Dictionary<string, string>? Tags { get; set; }
        public object? Payload { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
    }
}

/// <summary>
/// Custom JSON converter for DateTimeOffset to ensure consistent serialization.
/// </summary>
internal class DateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return value != null ? DateTimeOffset.Parse(value) : DateTimeOffset.MinValue;
    }

    public override void Write(
        Utf8JsonWriter writer,
        DateTimeOffset value,
        JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString("O"));
    }
}
