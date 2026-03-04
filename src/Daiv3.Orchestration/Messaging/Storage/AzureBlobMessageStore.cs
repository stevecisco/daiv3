using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Daiv3.Orchestration.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Daiv3.Orchestration.Messaging.Storage;

/// <summary>
/// Azure Blob Storage-based message store for distributed agent communication.
/// Persists messages as JSON blobs in an Azure Blob Storage container.
/// Tracks message status using blob metadata for scalable cloud deployments.
/// </summary>
public class AzureBlobMessageStore : IMessageStore
{
    private readonly ILogger<AzureBlobMessageStore> _logger;
    private readonly BlobContainerClient _containerClient;
    private readonly AzureBlobMessageStoreOptions _options;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _messageCache = new();

    public AzureBlobMessageStore(
        ILogger<AzureBlobMessageStore> logger,
        BlobContainerClient containerClient,
        IOptions<AzureBlobMessageStoreOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _containerClient = containerClient ?? throw new ArgumentNullException(nameof(containerClient));
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

        _logger.LogInformation(
            "Azure Blob Message Store initialized for container {ContainerName}",
            _containerClient.Name);
    }

    /// <inheritdoc />
    public async Task SaveMessageAsync(IAgentMessage message, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        try
        {
            var blobPath = GetBlobPath(message.Topic, message.MessageId);
            var blobClient = _containerClient.GetBlobClient(blobPath);

            var serializable = ConvertToSerializable(message);
            var json = JsonSerializer.Serialize(serializable, _jsonOptions);
            var data = System.Text.Encoding.UTF8.GetBytes(json);

            // Set metadata for efficient querying
            var metadata = new Dictionary<string, string>
            {
                { "topic", message.Topic },
                { "senderAgentId", message.SenderAgentId },
                { "status", message.Status.ToString() },
                { "priority", message.Metadata.Priority.ToString() },
                { "publishedAt", message.Metadata.PublishedAt.UtcTicks.ToString() },
                { "correlationId", message.Metadata.CorrelationId }
            };

            if (!string.IsNullOrWhiteSpace(message.Metadata.ReplyToTopic))
                metadata["replyToTopic"] = message.Metadata.ReplyToTopic;

            if (message.Metadata.ExpiresAt.HasValue)
                metadata["expiresAt"] = message.Metadata.ExpiresAt.Value.UtcTicks.ToString();

            // Upload blob with metadata
            using var stream = new MemoryStream(data);
            await blobClient.UploadAsync(stream, overwrite: true, ct).ConfigureAwait(false);

            // Set blob properties/metadata
            await blobClient.SetMetadataAsync(metadata, cancellationToken: ct).ConfigureAwait(false);

            _logger.LogDebug(
                "Message {MessageId} saved to blob {BlobPath}",
                message.MessageId, blobPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to save message {MessageId} to Azure Blob Storage",
                message.MessageId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IAgentMessage?> LoadMessageAsync(Guid messageId, CancellationToken ct = default)
    {
        try
        {
            // Search all topics for the message (inefficient but necessary without index)
            // In production, consider storing metadata in a separate index blob
            foreach (var blobHierarchyItem in _containerClient.GetBlobsByHierarchy(delimiter: "/", cancellationToken: ct))
            {
                if (!blobHierarchyItem.IsPrefix)
                    continue;

                var topicPrefix = blobHierarchyItem.Prefix;
                var blobName = $"{topicPrefix}{messageId:N}.json";

                try
                {
                    var blobClient = _containerClient.GetBlobClient(blobName);
                    var download = await blobClient.DownloadAsync(ct).ConfigureAwait(false);

                    using var streamReader = new StreamReader(download.Value.Content);
                    var json = await streamReader.ReadToEndAsync().ConfigureAwait(false);
                    var serializable = JsonSerializer.Deserialize<SerializableAgentMessage>(json, _jsonOptions);

                    if (serializable != null)
                    {
                        return DeserializeMessage(serializable);
                    }
                }
                catch (Azure.RequestFailedException ex) when (ex.Status == 404)
                {
                    // Blob not found, continue searching
                    continue;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to load message {MessageId} from Azure Blob Storage",
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
            var messages = new List<IAgentMessage>();
            var topicPath = $"{topic}/";

            await foreach (var blobItem in _containerClient.GetBlobsAsync(
                prefix: topicPath,
                states: BlobStates.All,
                cancellationToken: ct))
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    var blobClient = _containerClient.GetBlobClient(blobItem.Name);
                    var download = await blobClient.DownloadAsync(ct).ConfigureAwait(false);

                    using var streamReader = new StreamReader(download.Value.Content);
                    var json = await streamReader.ReadToEndAsync().ConfigureAwait(false);
                    var serializable = JsonSerializer.Deserialize<SerializableAgentMessage>(json, _jsonOptions);

                    if (serializable != null)
                    {
                        messages.Add(DeserializeMessage(serializable));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to deserialize blob {BlobName}",
                        blobItem.Name);
                }
            }

            return messages.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to load messages from topic {Topic}",
                topic);
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
            // Find the message blob
            var blobName = await FindBlobNameByIdAsync(messageId, ct).ConfigureAwait(false);
            if (blobName == null)
            {
                _logger.LogWarning(
                    "Cannot update status for message {MessageId}: message not found",
                    messageId);
                return;
            }

            var blobClient = _containerClient.GetBlobClient(blobName);

            // Load existing metadata
            var properties = await blobClient.GetPropertiesAsync(cancellationToken: ct).ConfigureAwait(false);
            var blobMetadata = new Dictionary<string, string>(properties.Value.Metadata)
            {
                { "status", newStatus.ToString() },
                { "lastUpdatedAt", DateTimeOffset.UtcNow.UtcTicks.ToString() }
            };

            // Add custom metadata if provided
            if (metadata != null)
            {
                foreach (var kvp in metadata)
                {
                    blobMetadata[$"meta_{kvp.Key}"] = kvp.Value?.ToString() ?? string.Empty;
                }
            }

            await blobClient.SetMetadataAsync(blobMetadata, cancellationToken: ct).ConfigureAwait(false);

            _logger.LogDebug(
                "Updated message {MessageId} status to {Status}",
                messageId, newStatus);
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
            var matching = new List<IAgentMessage>();
            var topicPattern = query.TopicPattern ?? "*";

            await foreach (var blobItem in _containerClient.GetBlobsAsync(
                states: BlobStates.All,
                cancellationToken: ct))
            {
                ct.ThrowIfCancellationRequested();

                // Skip non-JSON files and parse topic from path
                if (!blobItem.Name.EndsWith(".json"))
                    continue;

                var pathParts = blobItem.Name.Split('/');
                if (pathParts.Length < 2)
                    continue;

                var topic = pathParts[0];
                if (!MatchesTopic(topic, topicPattern))
                    continue;

                try
                {
                    // Check metadata filters first (efficient)
                    if (!MatchesMetadataFilters(blobItem.Metadata, query))
                        continue;

                    // Load and deserialize message for further filtering
                    var blobClient = _containerClient.GetBlobClient(blobItem.Name);
                    var download = await blobClient.DownloadAsync(ct).ConfigureAwait(false);

                    using var streamReader = new StreamReader(download.Value.Content);
                    var json = await streamReader.ReadToEndAsync().ConfigureAwait(false);
                    var serializable = JsonSerializer.Deserialize<SerializableAgentMessage>(json, _jsonOptions);

                    if (serializable != null)
                    {
                        var message = DeserializeMessage(serializable);

                        // Check expiration
                        if (message.Metadata.ExpiresAt.HasValue &&
                            DateTimeOffset.UtcNow > message.Metadata.ExpiresAt.Value)
                            continue;

                        // Check age filter
                        if (query.MaxAgeHours.HasValue)
                        {
                            var age = DateTimeOffset.UtcNow - message.Metadata.PublishedAt;
                            if (age.TotalHours > query.MaxAgeHours.Value)
                                continue;
                        }

                        // Only include Pending messages
                        if (message.Status != MessageStatus.Pending)
                            continue;

                        matching.Add(message);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to deserialize message from blob {BlobName}",
                        blobItem.Name);
                }
            }

            // Sort and paginate
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

            return matching
                .Take(query.PageSize)
                .ToList()
                .AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query messages in Azure Blob Storage");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> CleanupExpiredAsync(
        int retentionPolicyCompleted,
        int retentionPolicyFailed,
        CancellationToken ct = default)
    {
        int deletedCount = 0;

        try
        {
            var now = DateTimeOffset.UtcNow;
            var completedCutoff = now.AddDays(-retentionPolicyCompleted);
            var failedCutoff = now.AddDays(-retentionPolicyFailed);

            await foreach (var blobItem in _containerClient.GetBlobsAsync(
                states: BlobStates.All,
                cancellationToken: ct))
            {
                ct.ThrowIfCancellationRequested();

                if (!blobItem.Name.EndsWith(".json"))
                    continue;

                try
                {
                    var metadata = blobItem.Metadata;
                    if (!metadata.TryGetValue("status", out var statusStr) ||
                        !metadata.TryGetValue("publishedAt", out var publishedAtStr))
                        continue;

                    if (!Enum.TryParse<MessageStatus>(statusStr, out var status))
                        continue;

                    if (!long.TryParse(publishedAtStr, out var ticks))
                        continue;

                    var publishedAt = new DateTimeOffset(new DateTime(ticks, DateTimeKind.Utc));

                    bool shouldDelete = (status == MessageStatus.Completed && publishedAt < completedCutoff) ||
                                       (status == MessageStatus.Failed && publishedAt < failedCutoff) ||
                                       (status == MessageStatus.Expired);

                    if (shouldDelete)
                    {
                        var blobClient = _containerClient.GetBlobClient(blobItem.Name);
                        await blobClient.DeleteAsync(cancellationToken: ct).ConfigureAwait(false);
                        deletedCount++;

                        _logger.LogDebug(
                            "Cleaned up expired message blob {BlobName}",
                            blobItem.Name);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to cleanup message blob {BlobName}",
                        blobItem.Name);
                }
            }

            _logger.LogInformation(
                "Cleanup completed: {DeletedCount} expired/failed messages removed",
                deletedCount);

            return deletedCount;
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
            int count = 0;
            var topicPath = $"{topic}/";

            await foreach (var blobItem in _containerClient.GetBlobsAsync(
                prefix: topicPath,
                states: BlobStates.All,
                cancellationToken: ct))
            {
                if (blobItem.Name.EndsWith(".json"))
                    count++;
            }

            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to get message count for topic {Topic}",
                topic);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<(bool IsHealthy, string DiagnosticMessage)> ValidateHealthAsync(
        CancellationToken ct = default)
    {
        try
        {
            // Try to get container properties as a health check
            var properties = await _containerClient.GetPropertiesAsync(cancellationToken: ct)
                .ConfigureAwait(false);

            return (true, $"Azure Blob Storage healthy. Container: {_containerClient.Name}");
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return (false, $"Container {_containerClient.Name} not found (404)");
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 401 || ex.Status == 403)
        {
            return (false, $"Authorization failed: {ex.ErrorCode}");
        }
        catch (Exception ex)
        {
            return (false, $"Azure Blob Storage health check failed: {ex.Message}");
        }
    }

    // === Private Helpers ===

    private static string GetBlobPath(string topic, Guid messageId)
        => $"{topic}/{messageId:N}.json";

    private async Task<string?> FindBlobNameByIdAsync(Guid messageId, CancellationToken ct)
    {
        var targetName = $"{messageId:N}.json";

        await foreach (var blobItem in _containerClient.GetBlobsAsync(
            states: BlobStates.All,
            cancellationToken: ct))
        {
            if (blobItem.Name.EndsWith(targetName))
                return blobItem.Name;
        }

        return null;
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

    private static bool MatchesMetadataFilters(IDictionary<string, string> metadata, PendingMessageQuery query)
    {
        // Check sender filter
        if (!string.IsNullOrWhiteSpace(query.SenderAgentId))
        {
            if (!metadata.TryGetValue("senderAgentId", out var sender) ||
                !sender.Equals(query.SenderAgentId, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    private static SerializableAgentMessage ConvertToSerializable(IAgentMessage message)
        => new()
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
        [JsonPropertyName("messageId")]
        public Guid MessageId { get; set; }

        [JsonPropertyName("topic")]
        public string? Topic { get; set; }

        [JsonPropertyName("senderAgentId")]
        public string? SenderAgentId { get; set; }

        [JsonPropertyName("status")]
        public MessageStatus Status { get; set; }

        [JsonPropertyName("publishedAt")]
        public DateTimeOffset PublishedAt { get; set; }

        [JsonPropertyName("correlationId")]
        public string? CorrelationId { get; set; }

        [JsonPropertyName("replyToTopic")]
        public string? ReplyToTopic { get; set; }

        [JsonPropertyName("priority")]
        public int Priority { get; set; }

        [JsonPropertyName("expiresAt")]
        public DateTimeOffset? ExpiresAt { get; set; }

        [JsonPropertyName("tags")]
        public Dictionary<string, string>? Tags { get; set; }

        [JsonPropertyName("payload")]
        public object? Payload { get; set; }

        [JsonPropertyName("metadata")]
        public Dictionary<string, object>? Metadata { get; set; }
    }
}
