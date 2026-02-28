namespace Daiv3.Orchestration.Models;

/// <summary>
/// Enumeration of task execution statuses.
/// Implements PTS-REQ-006: Task status SHALL follow Pending → Queued → In Progress → Complete/Failed/Blocked.
/// </summary>
public enum TaskStatus
{
    /// <summary>
    /// Task has been created but not yet queued for execution.
    /// Valid transitions: → Queued
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Task is waiting in the execution queue.
    /// Valid transitions: → InProgress or → Blocked (if dependencies unmet)
    /// </summary>
    Queued = 1,

    /// <summary>
    /// Task is currently being executed.
    /// Valid transitions: → Complete, → Failed, or → Blocked (if error encountered)
    /// </summary>
    InProgress = 2,

    /// <summary>
    /// Task completed successfully.
    /// Valid transitions: None (terminal state)
    /// </summary>
    Complete = 3,

    /// <summary>
    /// Task execution failed with an error.
    /// Valid transitions: None (terminal state)
    /// </summary>
    Failed = 4,

    /// <summary>
    /// Task execution is blocked (e.g., dependencies unmet, resource unavailable).
    /// Valid transitions: → Queued (if dependencies later satisfied) or → Failed (if permanently blocked)
    /// </summary>
    Blocked = 5,
}
