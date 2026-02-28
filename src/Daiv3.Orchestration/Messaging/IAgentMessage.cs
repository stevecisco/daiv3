namespace Daiv3.Orchestration.Messaging;

/// <summary>
/// Base interface for all messages exchanged between agents in the system.
/// </summary>
public interface IAgentMessage
{
    /// <summary>
    /// Gets the unique message identifier.
    /// </summary>
    Guid MessageId { get; }

    /// <summary>
    /// Gets the topic/channel to which this message was published.
    /// Topics organize messages by domain or agent (e.g., "task-execution/agent-1", "knowledge-update").
    /// </summary>
    string Topic { get; }

    /// <summary>
    /// Gets the identifier of the agent that published this message.
    /// </summary>
    string SenderAgentId { get; }

    /// <summary>
    /// Gets the current status of the message in its lifecycle.
    /// </summary>
    MessageStatus Status { get; }

    /// <summary>
    /// Gets the metadata associated with this message (timing, correlation, priority, tags).
    /// </summary>
    MessageMetadata Metadata { get; }

    /// <summary>
    /// Gets the message payload (optional).
    /// The specific type depends on the message topic and sender context.
    /// </summary>
    object? Payload { get; }
}
