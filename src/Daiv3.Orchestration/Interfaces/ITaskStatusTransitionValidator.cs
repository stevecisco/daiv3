namespace Daiv3.Orchestration.Interfaces;

using Models;

/// <summary>
/// Service for validating and executing task status transitions.
/// Implements PTS-REQ-006: Task status state machine enforcement.
/// </summary>
public interface ITaskStatusTransitionValidator
{
    /// <summary>
    /// Validates if a status transition is allowed according to the state machine rules.
    /// </summary>
    /// <param name="currentStatus">Current task status.</param>
    /// <param name="requestedStatus">Requested new task status.</param>
    /// <returns>Validation result with status and any error message.</returns>
    TaskStatusTransition ValidateTransition(TaskStatus currentStatus, TaskStatus requestedStatus);

    /// <summary>
    /// Gets all valid target statuses from a given current status.
    /// </summary>
    /// <param name="currentStatus">Current task status.</param>
    /// <returns>Collection of valid target statuses.</returns>
    IEnumerable<TaskStatus> GetValidTransitions(TaskStatus currentStatus);

    /// <summary>
    /// Determines if transitioning to a terminal state (Complete, Failed).
    /// </summary>
    /// <param name="status">Task status to check.</param>
    /// <returns>True if the status is terminal.</returns>
    bool IsTerminalState(TaskStatus status);

    /// <summary>
    /// Determines if transitioning to a blocked state requires re-evaluation.
    /// </summary>
    /// <param name="status">Task status to check.</param>
    /// <returns>True if the status may be recoverable.</returns>
    bool IsRecoverableState(TaskStatus status);
}
