namespace Daiv3.Orchestration.Messaging;

/// <summary>
/// Message broker for agent-to-agent communication using storage-based persistence.
/// Supports publish/subscribe patterns with file system or blob storage backends.
/// </summary>
public interface IMessageBroker
{
    /// <summary>
    /// Publishes a message to a specific topic.
    /// </summary>
    /// <typeparam name="TPayload">The payload type.</typeparam>
    /// <param name="message">The message to publish.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous publish operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown if message is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if message size exceeds limit or storage operation fails.</exception>
    Task<MessageBrokerResult> PublishAsync<TPayload>(
        AgentMessage<TPayload> message,
        CancellationToken ct = default);

    /// <summary>
    /// Publishes an untyped message to a specific topic.
    /// </summary>
    /// <param name="message">The message to publish.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous publish operation.</returns>
    Task<MessageBrokerResult> PublishAsync(
        AgentMessage message,
        CancellationToken ct = default);

    /// <summary>
    /// Subscribes to messages on a specific topic with a handler.
    /// Topic can include wildcards (e.g., "task-execution/*" matches "task-execution/agent-1").
    /// </summary>
    /// <typeparam name="TPayload">The payload type to handle.</typeparam>
    /// <param name="topic">The topic pattern to subscribe to.</param>
    /// <param name="handler">The handler to invoke for each matching message.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>A subscription ID that can be used to unsubscribe later.</returns>
    /// <exception cref="ArgumentException">Thrown if topic is null/empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown if handler is null.</exception>
    Task<Guid> SubscribeAsync<TPayload>(
        string topic,
        MessageHandler<TPayload> handler,
        CancellationToken ct = default);

    /// <summary>
    /// Unsubscribes from a previous subscription.
    /// </summary>
    /// <param name="subscriptionId">The subscription ID returned by SubscribeAsync.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous unsubscribe operation.</returns>
    Task UnsubscribeAsync(Guid subscriptionId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a specific message by ID.
    /// </summary>
    /// <param name="messageId">The message ID to retrieve.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>The message if found; null otherwise.</returns>
    Task<IAgentMessage?> GetMessageAsync(
        Guid messageId,
        CancellationToken ct = default);

    /// <summary>
    /// Marks a message as processed.
    /// Updates the status and records processing timestamp.
    /// </summary>
    /// <param name="messageId">The message ID to mark as processed.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<MessageBrokerResult> MarkProcessedAsync(
        Guid messageId,
        CancellationToken ct = default);

    /// <summary>
    /// Marks a message as failed with an error reason.
    /// </summary>
    /// <param name="messageId">The message ID to mark as failed.</param>
    /// <param name="errorReason">Description of the failure reason.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<MessageBrokerResult> MarkFailedAsync(
        Guid messageId,
        string errorReason,
        CancellationToken ct = default);

    /// <summary>
    /// Queries for pending messages matching the specified criteria.
    /// </summary>
    /// <param name="query">Query options for filtering and pagination.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>A collection of pending messages matching the query criteria.</returns>
    Task<IReadOnlyList<IAgentMessage>> GetPendingMessagesAsync(
        PendingMessageQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Cleanups expired messages based on retention policy.
    /// Also archives messages that have completed processing.
    /// </summary>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>The number of messages cleaned up/archived.</returns>
    Task<int> CleanupExpiredMessagesAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the total number of messages in a specific topic.
    /// </summary>
    /// <param name="topic">The topic to query.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>The count of messages in the topic.</returns>
    Task<int> GetMessageCountAsync(
        string topic,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the current health status of the message broker.
    /// </summary>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>A tuple indicating health (isHealthy, diagnosticMessage).</returns>
    Task<(bool IsHealthy, string DiagnosticMessage)> GetHealthAsync(CancellationToken ct = default);
}
