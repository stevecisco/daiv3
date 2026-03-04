namespace Daiv3.Orchestration.Messaging;

/// <summary>
/// Delegate for handling messages of a specific type.
/// Note: TPayload is invariant (not contravariant) due to the parameter type constraint.
/// </summary>
/// <typeparam name="TPayload">The payload type handled by this delegate.</typeparam>
/// <param name="message">The message to handle.</param>
/// <param name="ct">Cancellation token for the operation.</param>
/// <returns>A task representing the asynchronous handler execution.</returns>
public delegate Task MessageHandler<TPayload>(
    AgentMessage<TPayload> message,
    CancellationToken ct = default);

/// <summary>
/// Delegate for handling any agent message.
/// </summary>
/// <param name="message">The message to handle.</param>
/// <param name="ct">Cancellation token for the operation.</param>
/// <returns>A task representing the asynchronous handler execution.</returns>
public delegate Task GenericMessageHandler(
    IAgentMessage message,
    CancellationToken ct = default);

/// <summary>
/// Result of a message broker operation.
/// </summary>
public class MessageBrokerResult
{
    /// <summary>
    /// Initializes a successful result.
    /// </summary>
    public static MessageBrokerResult Success() => new() { IsSuccess = true };

    /// <summary>
    /// Initializes a failed result with an error message.
    /// </summary>
    public static MessageBrokerResult Failure(string error) =>
        new() { IsSuccess = false, ErrorMessage = error };

    /// <summary>
    /// Gets a value indicating whether the operation succeeded.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Gets the error message if the operation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Throws an exception if the operation failed.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if not successful.</exception>
    public void ThrowIfFailed()
    {
        if (!IsSuccess)
            throw new InvalidOperationException(ErrorMessage ?? "Message broker operation failed");
    }
}

/// <summary>
/// Query options for retrieving pending messages.
/// </summary>
public class PendingMessageQuery
{
    /// <summary>
    /// Gets or sets the topic filter. If null, matches all topics.
    /// Supports wildcard patterns (e.g., "task-execution/*").
    /// </summary>
    public string? TopicPattern { get; set; }

    /// <summary>
    /// Gets or sets the sender agent ID filter. If null, matches all senders.
    /// </summary>
    public string? SenderAgentId { get; set; }

    /// <summary>
    /// Gets or sets the maximum age of messages to retrieve (in hours).
    /// If null, no age filter is applied.
    /// </summary>
    public int? MaxAgeHours { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of messages to retrieve.
    /// Default: 100.
    /// </summary>
    public int PageSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets whether to sort by priority (highest first) in addition to timestamp.
    /// Default: true.
    /// </summary>
    public bool SortByPriority { get; set; } = true;
}
