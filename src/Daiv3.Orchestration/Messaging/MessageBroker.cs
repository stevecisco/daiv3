using System.Collections.Concurrent;
using System.Diagnostics;
using Daiv3.Orchestration.Messaging.Correlation;
using Daiv3.Orchestration.Messaging.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Daiv3.Orchestration.Messaging;

/// <summary>
/// Core message broker implementation coordinating storage-based agent communication.
/// Manages pub/sub, message persistence, and automated message watchers.
/// </summary>
public class MessageBroker : IMessageBroker, IAsyncDisposable
{
    private readonly ILogger<MessageBroker> _logger;
    private readonly IMessageStore _messageStore;
    private readonly MessageBrokerOptions _options;
    private readonly MessageCorrelationContext _correlationContext;

    /// <summary>
    /// Subscription registrations keyed by (topic, handler).
    /// </summary>
    private readonly ConcurrentDictionary<Guid, Subscription> _subscriptions = new();

    /// <summary>
    /// Message watchers keyed by topic pattern.
    /// </summary>
    private readonly ConcurrentDictionary<string, Task> _watchers = new();

    /// <summary>
    /// Cancellation token source for coordinating watcher shutdown.
    /// </summary>
    private CancellationTokenSource _watcherCts = new();

    public MessageBroker(
        ILogger<MessageBroker> logger,
        IMessageStore messageStore,
        IOptions<MessageBrokerOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _messageStore = messageStore ?? throw new ArgumentNullException(nameof(messageStore));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _correlationContext = new MessageCorrelationContext();

        _options.Validate();

        _logger.LogInformation(
            "Message broker initialized with {Backend} backend. Retention: {CompleteD}d completed, {FailedD}d failed",
            _options.StorageBackend,
            _options.RetentionDaysCompleted,
            _options.RetentionDaysFailed);
    }

    /// <inheritdoc />
    public async Task<MessageBrokerResult> PublishAsync<TPayload>(
        AgentMessage<TPayload> message,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        try
        {
            // Validate message size
            var jsonSize = EstimateJsonSize(message);
            if (jsonSize > _options.MaxMessageSizeBytes)
            {
                var err = $"Message {message.MessageId} exceeds size limit: {jsonSize} > {_options.MaxMessageSizeBytes}";
                _logger.LogError(err);
                return MessageBrokerResult.Failure(err);
            }

            // Save to storage
            await _messageStore.SaveMessageAsync(message, ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Published message {MessageId} to topic {Topic} with correlation {CorrelationId}",
                message.MessageId, message.Topic, message.Metadata.CorrelationId);

            // Trigger watchers for this topic
            _ = Task.Run(() => DeliverToSubscribersAsync(message), ct);

            return MessageBrokerResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to publish message {MessageId} to topic {Topic}",
                message.MessageId, message.Topic);
            return MessageBrokerResult.Failure($"Publish failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public Task<MessageBrokerResult> PublishAsync(
        AgentMessage message,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        return PublishAsync<object?>(
            new AgentMessage<object?>(message.Topic, message.SenderAgentId, message.Payload),
            ct);
    }

    /// <inheritdoc />
    public async Task<Guid> SubscribeAsync<TPayload>(
        string topic,
        MessageHandler<TPayload> handler,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentNullException.ThrowIfNull(handler);

        var subscriptionId = Guid.NewGuid();
        var subscription = new Subscription
        {
            Id = subscriptionId,
            Topic = topic,
            PayloadType = typeof(TPayload),
            Handler = async (msg) => await handler((AgentMessage<TPayload>)msg, ct).ConfigureAwait(false)
        };

        if (!_subscriptions.TryAdd(subscriptionId, subscription))
        {
            throw new InvalidOperationException(
                $"Failed to register subscription {subscriptionId}");
        }

        _logger.LogInformation(
            "Subscription {SubscriptionId} registered for topic {Topic} (payload type: {PayloadType})",
            subscriptionId, topic, typeof(TPayload).Name);

        // Start watcher for this topic if not already watching
        _ = EnsureWatcherAsync(topic, ct);

        return subscriptionId;
    }

    /// <inheritdoc />
    public Task UnsubscribeAsync(Guid subscriptionId, CancellationToken ct = default)
    {
        if (_subscriptions.TryRemove(subscriptionId, out var sub))
        {
            _logger.LogInformation(
                "Subscription {SubscriptionId} unregistered from topic {Topic}",
                subscriptionId, sub.Topic);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IAgentMessage?> GetMessageAsync(Guid messageId, CancellationToken ct = default)
    {
        if (messageId == Guid.Empty)
            throw new ArgumentException("messageId cannot be empty", nameof(messageId));
        return _messageStore.LoadMessageAsync(messageId, ct);
    }

    /// <summary>
    /// Waits for a reply message matching the correlation ID.
    /// Used for request/reply patterns.
    /// </summary>
    /// <param name="correlationId">The correlation ID to wait for.</param>
    /// <param name="timeout">Maximum time to wait for a reply.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The reply message if received within timeout.</returns>
    public Task<IAgentMessage> WaitForReplyAsync(
        string correlationId,
        TimeSpan timeout,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        return _correlationContext.WaitForReplyAsync(correlationId, timeout);
    }

    /// <inheritdoc />
    public Task<MessageBrokerResult> MarkProcessedAsync(Guid messageId, CancellationToken ct = default)
    {
        if (messageId == Guid.Empty)
            throw new ArgumentException("messageId cannot be empty", nameof(messageId));

        try
        {
            _logger.LogDebug("Marking message {MessageId} as processed", messageId);
            return _messageStore.UpdateMessageStatusAsync(messageId, MessageStatus.Completed, null, ct)
                .ContinueWith(_ => MessageBrokerResult.Success(), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark message {MessageId} as processed", messageId);
            return Task.FromResult(MessageBrokerResult.Failure($"Update failed: {ex.Message}"));
        }
    }

    /// <inheritdoc />
    public Task<MessageBrokerResult> MarkFailedAsync(Guid messageId, string errorReason, CancellationToken ct = default)
    {
        if (messageId == Guid.Empty)
            throw new ArgumentException("messageId cannot be empty", nameof(messageId));
        ArgumentException.ThrowIfNullOrWhiteSpace(errorReason);

        try
        {
            var metadata = new Dictionary<string, object> { { "errorReason", errorReason } };
            _logger.LogWarning(
                "Marking message {MessageId} as failed: {ErrorReason}",
                messageId, errorReason);

            return _messageStore.UpdateMessageStatusAsync(messageId, MessageStatus.Failed, metadata, ct)
                .ContinueWith(_ => MessageBrokerResult.Success(), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark message {MessageId} as failed", messageId);
            return Task.FromResult(MessageBrokerResult.Failure($"Update failed: {ex.Message}"));
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<IAgentMessage>> GetPendingMessagesAsync(
        PendingMessageQuery query,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return _messageStore.QueryMessagesAsync(query, ct);
    }

    /// <inheritdoc />
    public Task<int> CleanupExpiredMessagesAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Starting cleanup of expired messages");
        return _messageStore.CleanupExpiredAsync(
            _options.RetentionDaysCompleted,
            _options.RetentionDaysFailed,
            ct);
    }

    /// <inheritdoc />
    public Task<int> GetMessageCountAsync(string topic, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic, nameof(topic));
        return _messageStore.GetMessageCountAsync(topic, ct);
    }

    /// <inheritdoc />
    public Task<(bool IsHealthy, string DiagnosticMessage)> GetHealthAsync(CancellationToken ct = default)
    {
        return _messageStore.ValidateHealthAsync(ct);
    }

    public async ValueTask DisposeAsync()
    {
        _logger.LogInformation("Shutting down message broker");

        // Stop all watchers
        _watcherCts.Cancel();
        _watcherCts.Dispose();

        // Clear subscriptions
        _subscriptions.Clear();
        _watchers.Clear();

        // Clear correlation context
        _correlationContext.Dispose();

        await Task.CompletedTask.ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Ensures a watcher is running for the specified topic.
    /// </summary>
    private async Task EnsureWatcherAsync(string topic, CancellationToken ct)
    {
        // For now, on-demand delivery via DeliverToSubscribersAsync.
        // In future, this will integrate with FileSystemWatcherService for real-time updates.
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Delivers a message to all matching subscribers.
    /// </summary>
    private async Task DeliverToSubscribersAsync(IAgentMessage message)
    {
        // Check if this is a reply to a waiting correlation
        if (!string.IsNullOrWhiteSpace(message.Metadata?.CorrelationId))
        {
            if (_correlationContext.DeliverReply(message.Metadata.CorrelationId, message))
            {
                _logger.LogDebug(
                    "Message {MessageId} delivered to waiting correlator via correlation context",
                    message.MessageId);
                return; // Don't broadcast replies to subscribers
            }
        }

        if (_subscriptions.IsEmpty)
            return;

        var sw = Stopwatch.StartNew();
        var successCount = 0;
        var failureCount = 0;

        foreach (var sub in _subscriptions.Values)
        {
            if (sub.Topic == null || !MatchesTopic(message.Topic, sub.Topic))
                continue;

            if (sub.Handler == null)
                continue;

            try
            {
                await sub.Handler(message).ConfigureAwait(false);
                successCount++;
            }
            catch (Exception ex)
            {
                failureCount++;
                _logger.LogError(ex,
                    "Handler failed for subscription {SubscriptionId} on message {MessageId}",
                    sub.Id, message.MessageId);
            }
        }

        sw.Stop();
        _logger.LogDebug(
            "Message {MessageId} delivered to {Success} subscribers ({Failures} failures) in {ElapsedMs}ms",
            message.MessageId, successCount, failureCount, sw.ElapsedMilliseconds);
    }

    /// <summary>
    /// Checks if a message topic matches a subscription topic pattern.
    /// </summary>
    private static bool MatchesTopic(string messageTopic, string pattern)
    {
        if (pattern == "*")
            return true;

        if (pattern.EndsWith("/*"))
        {
            var prefix = pattern.Substring(0, pattern.Length - 2);
            return messageTopic.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        return messageTopic.Equals(pattern, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Estimates the JSON serialized size of a message.
    /// </summary>
    private static long EstimateJsonSize(object message)
    {
        // Rough estimate: serialize and check actual size
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(message);
            return System.Text.Encoding.UTF8.GetByteCount(json);
        }
        catch
        {
            // Fallback estimate if serialization fails
            return 1024;
        }
    }

    /// <summary>
    /// Internal representation of a subscription.
    /// </summary>
    /// <summary>
    /// Internal representation of a subscription.
    /// </summary>
    private class Subscription
    {
        public Guid Id { get; init; }
        public string? Topic { get; init; }
        public Type? PayloadType { get; init; }

        /// <summary>
        /// Handler function that accepts the message.
        /// </summary>
        public Func<IAgentMessage, Task>? Handler { get; init; }
    }
}