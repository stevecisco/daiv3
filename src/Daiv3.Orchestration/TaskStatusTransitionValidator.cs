namespace Daiv3.Orchestration;

using Interfaces;
using Microsoft.Extensions.Logging;
using Models;

/// <summary>
/// Service for validating and enforcing task status transitions.
/// Implements PTS-REQ-006: Task status state machine with allowed transitions.
/// </summary>
public class TaskStatusTransitionValidator : ITaskStatusTransitionValidator
{
    private readonly ILogger<TaskStatusTransitionValidator> _logger;

    /// <summary>
    /// Terminal states where no further transitions are allowed.
    /// </summary>
    private static readonly HashSet<TaskStatus> TerminalStates = new()
    {
        TaskStatus.Complete,
        TaskStatus.Failed,
    };

    /// <summary>
    /// Recoverable states that may transition to queue or failure.
    /// </summary>
    private static readonly HashSet<TaskStatus> RecoverableStates = new()
    {
        TaskStatus.Blocked,
        TaskStatus.Pending,
    };

    public TaskStatusTransitionValidator(ILogger<TaskStatusTransitionValidator> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public TaskStatusTransition ValidateTransition(TaskStatus currentStatus, TaskStatus requestedStatus)
    {
        var transition = new TaskStatusTransition
        {
            CurrentStatus = currentStatus,
            RequestedStatus = requestedStatus,
        };

        var isValid = transition.Validate();

        if (!isValid)
        {
            _logger.LogWarning(
                "Invalid task status transition: {CurrentStatus} → {RequestedStatus}. Reason: {Reason}",
                currentStatus,
                requestedStatus,
                transition.InvalidReason);
        }
        else
        {
            _logger.LogDebug(
                "Valid task status transition: {CurrentStatus} → {RequestedStatus}",
                currentStatus,
                requestedStatus);
        }

        return transition;
    }

    /// <inheritdoc />
    public IEnumerable<TaskStatus> GetValidTransitions(TaskStatus currentStatus)
    {
        return TaskStatusTransition.GetValidTransitions(currentStatus);
    }

    /// <inheritdoc />
    public bool IsTerminalState(TaskStatus status)
    {
        return TerminalStates.Contains(status);
    }

    /// <inheritdoc />
    public bool IsRecoverableState(TaskStatus status)
    {
        return RecoverableStates.Contains(status);
    }
}
