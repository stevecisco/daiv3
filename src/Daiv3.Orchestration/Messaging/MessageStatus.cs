namespace Daiv3.Orchestration.Messaging;

/// <summary>
/// Represents the lifecycle status of an agent message.
/// </summary>
public enum MessageStatus
{
    /// <summary>
    /// Message has been published and awaits processing.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Message is currently being processed by a handler.
    /// </summary>
    Processing = 1,

    /// <summary>
    /// Message has been successfully processed.
    /// </summary>
    Completed = 2,

    /// <summary>
    /// Message processing failed with an error.
    /// </summary>
    Failed = 3,

    /// <summary>
    /// Message Time-To-Live (TTL) exceeded; message is expired.
    /// </summary>
    Expired = 4,

    /// <summary>
    /// Message has been archived; deleted from active storage but record kept.
    /// </summary>
    Archived = 5
}
