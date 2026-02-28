namespace Daiv3.Orchestration.Messaging.Storage;

/// <summary>
/// Base interface for message storage backends (file system, blob storage, etc.).
/// </summary>
public interface IMessageStore
{
    /// <summary>
    /// Saves a message to storage.
    /// </summary>
    /// <param name="message">The message to save.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous save operation.</returns>
    Task SaveMessageAsync(IAgentMessage message, CancellationToken ct = default);

    /// <summary>
    /// Loads a message from storage by ID.
    /// </summary>
    /// <param name="messageId">The message ID to load.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>The message if found; null otherwise.</returns>
    Task<IAgentMessage?> LoadMessageAsync(Guid messageId, CancellationToken ct = default);

    /// <summary>
    /// Loads all messages in a specific topic.
    /// </summary>
    /// <param name="topic">The topic to query.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>A collection of messages in the topic.</returns>
    Task<IReadOnlyList<IAgentMessage>> LoadMessagesByTopicAsync(
        string topic,
        CancellationToken ct = default);

    /// <summary>
    /// Updates the status of a message.
    /// </summary>
    /// <param name="messageId">The message ID to update.</param>
    /// <param name="newStatus">The new status.</param>
    /// <param name="metadata">Optional additional metadata to store (e.g., error reason).</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous update operation.</returns>
    Task UpdateMessageStatusAsync(
        Guid messageId,
        MessageStatus newStatus,
        Dictionary<string, object>? metadata = null,
        CancellationToken ct = default);

    /// <summary>
    /// Queries for messages matching the specified criteria.
    /// </summary>
    /// <param name="query">The query criteria.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>A collection of matching messages.</returns>
    Task<IReadOnlyList<IAgentMessage>> QueryMessagesAsync(
        PendingMessageQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes expired messages and archives completed/failed messages.
    /// </summary>
    /// <param name="retentionPolicyCompleted">Days to retain completed messages.</param>
    /// <param name="retentionPolicyFailed">Days to retain failed messages.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>The number of messages cleaned up.</returns>
    Task<int> CleanupExpiredAsync(
        int retentionPolicyCompleted,
        int retentionPolicyFailed,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the total count of messages in a topic.
    /// </summary>
    /// <param name="topic">The topic to query.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>The count of messages in the topic.</returns>
    Task<int> GetMessageCountAsync(string topic, CancellationToken ct = default);

    /// <summary>
    /// Validates that the message store is accessible and functional.
    /// </summary>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>A tuple indicating health (isHealthy, diagnosticMessage).</returns>
    Task<(bool IsHealthy, string DiagnosticMessage)> ValidateHealthAsync(CancellationToken ct = default);
}
