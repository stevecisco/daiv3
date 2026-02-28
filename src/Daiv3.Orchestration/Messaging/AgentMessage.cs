using System.Diagnostics;

namespace Daiv3.Orchestration.Messaging;

/// <summary>
/// Generic envelope for agent messages carrying a strongly-typed payload.
/// </summary>
/// <typeparam name="TPayload">The type of payload carried by this message.</typeparam>
public class AgentMessage<TPayload> : IAgentMessage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AgentMessage{TPayload}"/> class.
    /// </summary>
    /// <param name="topic">The topic to which the message is published.</param>
    /// <param name="senderAgentId">The ID of the sending agent.</param>
    /// <param name="payload">The message payload.</param>
    /// <param name="metadata">Message metadata (timing, correlation, priority). Optional; defaults will be provided.</param>
    /// <exception cref="ArgumentException">Thrown if topic or senderAgentId is null/empty.</exception>
    public AgentMessage(
        string topic,
        string senderAgentId,
        TPayload? payload,
        MessageMetadata? metadata = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic, nameof(topic));
        ArgumentException.ThrowIfNullOrWhiteSpace(senderAgentId, nameof(senderAgentId));

        MessageId = Guid.NewGuid();
        Topic = topic;
        SenderAgentId = senderAgentId;
        Payload = payload;
        Status = MessageStatus.Pending;

        // Use provided metadata or create defaults
        if (metadata != null)
        {
            metadata.Validate();
            Metadata = metadata;
        }
        else
        {
            Metadata = new MessageMetadata();
        }
    }

    /// <inheritdoc />
    public Guid MessageId { get; }

    /// <inheritdoc />
    public string Topic { get; }

    /// <inheritdoc />
    public string SenderAgentId { get; }

    /// <inheritdoc />
    public MessageStatus Status { get; private set; }

    /// <inheritdoc />
    public MessageMetadata Metadata { get; }

    /// <inheritdoc />
    public object? Payload { get; }

    /// <summary>
    /// Updates the message status.
    /// </summary>
    /// <param name="status">The new status.</param>
    /// <remarks>
    /// Used internally when message storage persists status updates.
    /// </remarks>
    internal void SetStatus(MessageStatus status) => Status = status;

    /// <summary>
    /// Gets the strongly-typed payload.
    /// </summary>
    /// <returns>The payload cast to TPayload.</returns>
    /// <exception cref="InvalidCastException">Thrown if payload is null or wrong type.</exception>
    public TPayload GetPayload()
    {
        if (Payload is not TPayload typed)
            throw new InvalidCastException(
                $"Cannot cast payload to {typeof(TPayload).Name}. Actual type: {Payload?.GetType().Name ?? "null"}");

        return typed;
    }

    /// <summary>
    /// Returns a human-readable representation of the message.
    /// </summary>
    public override string ToString() =>
        $"AgentMessage(Id={MessageId:N}, Topic={Topic}, Sender={SenderAgentId}, Status={Status}, CorrelationId={Metadata.CorrelationId})";
}

/// <summary>
/// Non-generic agent message variant for cases where the payload type isn't known at compile time.
/// </summary>
public class AgentMessage : IAgentMessage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AgentMessage"/> class.
    /// </summary>
    public AgentMessage(
        string topic,
        string senderAgentId,
        object? payload,
        MessageMetadata? metadata = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic, nameof(topic));
        ArgumentException.ThrowIfNullOrWhiteSpace(senderAgentId, nameof(senderAgentId));

        MessageId = Guid.NewGuid();
        Topic = topic;
        SenderAgentId = senderAgentId;
        Payload = payload;
        Status = MessageStatus.Pending;

        if (metadata != null)
        {
            metadata.Validate();
            Metadata = metadata;
        }
        else
        {
            Metadata = new MessageMetadata();
        }
    }

    /// <inheritdoc />
    public Guid MessageId { get; }

    /// <inheritdoc />
    public string Topic { get; }

    /// <inheritdoc />
    public string SenderAgentId { get; }

    /// <inheritdoc />
    public MessageStatus Status { get; private set; }

    /// <inheritdoc />
    public MessageMetadata Metadata { get; }

    /// <inheritdoc />
    public object? Payload { get; }

    /// <summary>
    /// Updates the message status.
    /// </summary>
    internal void SetStatus(MessageStatus status) => Status = status;

    /// <summary>
    /// Returns a human-readable representation of the message.
    /// </summary>
    public override string ToString() =>
        $"AgentMessage(Id={MessageId:N}, Topic={Topic}, Sender={SenderAgentId}, Status={Status}, CorrelationId={Metadata.CorrelationId})";
}
