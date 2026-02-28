using System.Collections.ObjectModel;

namespace Daiv3.Orchestration.Messaging;

/// <summary>
/// Metadata associated with an agent message.
/// Includes timing, correlation tracking, priority, and expiration information.
/// </summary>
public class MessageMetadata
{
    /// <summary>
    /// Gets the timestamp when the message was published.
    /// </summary>
    public DateTimeOffset PublishedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the correlation ID for tracking related messages.
    /// Enables request/reply patterns and multi-agent workflows.
    /// </summary>
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets the topic to which replies to this message should be published (optional).
    /// Enables request/reply messaging patterns.
    /// </summary>
    public string? ReplyToTopic { get; init; }

    /// <summary>
    /// Gets the priority level of the message.
    /// Higher values indicate higher priority for processing.
    /// Default: 0 (normal priority). Range: -10 to 100.
    /// </summary>
    public int Priority { get; init; } = 0;

    /// <summary>
    /// Gets the date and time when the message expires and should be purged.
    /// If null, message never expires.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>
    /// Gets the collection of arbitrary tags for flexible message filtering and organization.
    /// Example: { "projectId": "proj-123", "domain": "knowledge-update" }
    /// </summary>
    public IReadOnlyDictionary<string, string> Tags { get; init; } = 
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());

    /// <summary>
    /// Validates metadata consistency.
    /// Throws if correlation ID is null/empty or priority is out of range.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(CorrelationId))
            throw new ArgumentException("CorrelationId must not be null or empty", nameof(CorrelationId));

        if (Priority < -10 || Priority > 100)
            throw new ArgumentOutOfRangeException(nameof(Priority), "Priority must be between -10 and 100");

        if (ExpiresAt.HasValue && ExpiresAt <= PublishedAt)
            throw new ArgumentException("ExpiresAt must be after PublishedAt", nameof(ExpiresAt));
    }
}
