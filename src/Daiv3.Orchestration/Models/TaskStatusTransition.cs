namespace Daiv3.Orchestration.Models;

/// <summary>
/// Represents a valid state transition for tasks.
/// Implements PTS-REQ-006 state machine constraints.
/// </summary>
public class TaskStatusTransition
{
    /// <summary>
    /// Current task status.
    /// </summary>
    public required TaskStatus CurrentStatus { get; set; }

    /// <summary>
    /// Requested new status.
    /// </summary>
    public required TaskStatus RequestedStatus { get; set; }

    /// <summary>
    /// Whether the transition is valid according to the state machine.
    /// </summary>
    public bool IsValid { get; internal set; }

    /// <summary>
    /// Reason why the transition is invalid (if applicable).
    /// </summary>
    public string? InvalidReason { get; internal set; }

    /// <summary>
    /// State machine rules defining valid transitions.
    /// PTS-REQ-006: Pending → Queued → In Progress → Complete/Failed/Blocked
    /// 
    /// Detailed transition rules:
    /// - Pending: Can only transition to Queued
    /// - Queued: Can transition to InProgress or Blocked
    /// - InProgress: Can transition to Complete, Failed, or Blocked
    /// - Complete: Terminal state, no transitions
    /// - Failed: Terminal state, no transitions
    /// - Blocked: Can transition to Queued (if unblocked) or Failed (if permanent)
    /// </summary>
    private static readonly Dictionary<TaskStatus, HashSet<TaskStatus>> ValidTransitions = new()
    {
        { TaskStatus.Pending, new() { TaskStatus.Queued } },
        { TaskStatus.Queued, new() { TaskStatus.InProgress, TaskStatus.Blocked } },
        { TaskStatus.InProgress, new() { TaskStatus.Complete, TaskStatus.Failed, TaskStatus.Blocked } },
        { TaskStatus.Complete, new() },
        { TaskStatus.Failed, new() },
        { TaskStatus.Blocked, new() { TaskStatus.Queued, TaskStatus.Failed } },
    };

    /// <summary>
    /// Validates the transition according to the state machine rules.
    /// </summary>
    /// <returns>True if the transition is valid; false otherwise.</returns>
    public bool Validate()
    {
        // Check if current status has any valid transitions
        if (!ValidTransitions.TryGetValue(CurrentStatus, out var validTargets))
        {
            IsValid = false;
            InvalidReason = $"State '{CurrentStatus}' is not recognized.";
            return false;
        }

        // Check if requested transition is in the valid set
        if (!validTargets.Contains(RequestedStatus))
        {
            IsValid = false;
            InvalidReason = $"Cannot transition from '{CurrentStatus}' to '{RequestedStatus}'. Valid transitions: {string.Join(", ", validTargets.OrderBy(s => s))}";
            return false;
        }

        IsValid = true;
        InvalidReason = null;
        return true;
    }

    /// <summary>
    /// Gets all valid target states from a given current state.
    /// </summary>
    /// <param name="currentStatus">The current task status.</param>
    /// <returns>A collection of valid target statuses.</returns>
    public static IEnumerable<TaskStatus> GetValidTransitions(TaskStatus currentStatus)
    {
        if (ValidTransitions.TryGetValue(currentStatus, out var targets))
        {
            return targets.OrderBy(s => s);
        }
        return Enumerable.Empty<TaskStatus>();
    }

    /// <summary>
    /// Determines if a transition is valid without creating an instance.
    /// </summary>
    /// <param name="currentStatus">The current task status.</param>
    /// <param name="requestedStatus">The requested new status.</param>
    /// <returns>True if the transition is valid; false otherwise.</returns>
    public static bool IsTransitionValid(TaskStatus currentStatus, TaskStatus requestedStatus)
    {
        if (!ValidTransitions.TryGetValue(currentStatus, out var validTargets))
        {
            return false;
        }
        return validTargets.Contains(requestedStatus);
    }
}
