namespace Daiv3.Scheduler;

/// <summary>
/// Represents an event that can trigger scheduled jobs.
/// 
/// Events are used to trigger jobs based on application-level or system-level occurrences
/// rather than time-based schedules. Examples include:
/// - File system changes (file created, modified, deleted)
/// - Database changes (record inserted, updated)
/// - External API notifications
/// - Application state changes
/// - Custom business events
/// </summary>
public interface ISchedulerEvent
{
    /// <summary>
    /// Gets the unique identifier for this event type.
    /// 
    /// Event types should be globally unique within the application.
    /// Recommended format: "namespace.eventname" (e.g., "filesystem.file_created", "database.record_updated").
    /// </summary>
    string EventType { get; }

    /// <summary>
    /// Gets the timestamp when this event occurred in UTC.
    /// </summary>
    DateTime OccurredAtUtc { get; }

    /// <summary>
    /// Gets optional metadata about the event.
    /// 
    /// This can contain event-specific data such as:
    /// - File path for file system events
    /// - Record ID for database events
    /// - User ID for user-triggered events
    /// - Any other contextual information
    /// </summary>
    IReadOnlyDictionary<string, object>? Metadata { get; }
}

/// <summary>
/// Default implementation of ISchedulerEvent.
/// </summary>
public class SchedulerEvent : ISchedulerEvent
{
    /// <inheritdoc />
    public required string EventType { get; init; }

    /// <inheritdoc />
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;

    /// <inheritdoc />
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }
}
