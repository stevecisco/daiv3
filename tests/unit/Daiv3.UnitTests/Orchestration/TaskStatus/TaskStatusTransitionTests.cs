using Xunit;

namespace Daiv3.UnitTests.Orchestration.TaskStatusState;

public class TaskStatusTransitionTests
{
    [Fact]
    public void ValidateTransition_PendingToQueued_IsValid()
    {
        var transition = new Daiv3.Orchestration.Models.TaskStatusTransition
        {
            CurrentStatus = Daiv3.Orchestration.Models.TaskStatus.Pending,
            RequestedStatus = Daiv3.Orchestration.Models.TaskStatus.Queued,
        };

        var isValid = transition.Validate();

        Assert.True(isValid);
        Assert.Null(transition.InvalidReason);
    }

    [Fact]
    public void ValidateTransition_PendingToInProgress_IsInvalid()
    {
        var transition = new Daiv3.Orchestration.Models.TaskStatusTransition
        {
            CurrentStatus = Daiv3.Orchestration.Models.TaskStatus.Pending,
            RequestedStatus = Daiv3.Orchestration.Models.TaskStatus.InProgress,
        };

        var isValid = transition.Validate();

        Assert.False(isValid);
        Assert.NotNull(transition.InvalidReason);
        Assert.Contains("Pending", transition.InvalidReason);
        Assert.Contains("InProgress", transition.InvalidReason);
    }

    [Fact]
    public void ValidateTransition_QueuedToInProgress_IsValid()
    {
        var transition = new Daiv3.Orchestration.Models.TaskStatusTransition
        {
            CurrentStatus = Daiv3.Orchestration.Models.TaskStatus.Queued,
            RequestedStatus = Daiv3.Orchestration.Models.TaskStatus.InProgress,
        };

        var isValid = transition.Validate();

        Assert.True(isValid);
        Assert.Null(transition.InvalidReason);
    }

    [Fact]
    public void ValidateTransition_QueuedToBlocked_IsValid()
    {
        var transition = new Daiv3.Orchestration.Models.TaskStatusTransition
        {
            CurrentStatus = Daiv3.Orchestration.Models.TaskStatus.Queued,
            RequestedStatus = Daiv3.Orchestration.Models.TaskStatus.Blocked,
        };

        var isValid = transition.Validate();

        Assert.True(isValid);
        Assert.Null(transition.InvalidReason);
    }

    [Fact]
    public void ValidateTransition_InProgressToComplete_IsValid()
    {
        var transition = new Daiv3.Orchestration.Models.TaskStatusTransition
        {
            CurrentStatus = Daiv3.Orchestration.Models.TaskStatus.InProgress,
            RequestedStatus = Daiv3.Orchestration.Models.TaskStatus.Complete,
        };

        var isValid = transition.Validate();

        Assert.True(isValid);
        Assert.Null(transition.InvalidReason);
    }

    [Fact]
    public void ValidateTransition_InProgressToFailed_IsValid()
    {
        var transition = new Daiv3.Orchestration.Models.TaskStatusTransition
        {
            CurrentStatus = Daiv3.Orchestration.Models.TaskStatus.InProgress,
            RequestedStatus = Daiv3.Orchestration.Models.TaskStatus.Failed,
        };

        var isValid = transition.Validate();

        Assert.True(isValid);
        Assert.Null(transition.InvalidReason);
    }

    [Fact]
    public void ValidateTransition_InProgressToBlocked_IsValid()
    {
        var transition = new Daiv3.Orchestration.Models.TaskStatusTransition
        {
            CurrentStatus = Daiv3.Orchestration.Models.TaskStatus.InProgress,
            RequestedStatus = Daiv3.Orchestration.Models.TaskStatus.Blocked,
        };

        var isValid = transition.Validate();

        Assert.True(isValid);
        Assert.Null(transition.InvalidReason);
    }

    [Fact]
    public void ValidateTransition_CompleteToQueued_IsInvalid()
    {
        var transition = new Daiv3.Orchestration.Models.TaskStatusTransition
        {
            CurrentStatus = Daiv3.Orchestration.Models.TaskStatus.Complete,
            RequestedStatus = Daiv3.Orchestration.Models.TaskStatus.Queued,
        };

        var isValid = transition.Validate();

        Assert.False(isValid);
        Assert.NotNull(transition.InvalidReason);
    }

    [Fact]
    public void ValidateTransition_FailedToQueued_IsInvalid()
    {
        var transition = new Daiv3.Orchestration.Models.TaskStatusTransition
        {
            CurrentStatus = Daiv3.Orchestration.Models.TaskStatus.Failed,
            RequestedStatus = Daiv3.Orchestration.Models.TaskStatus.Queued,
        };

        var isValid = transition.Validate();

        Assert.False(isValid);
        Assert.NotNull(transition.InvalidReason);
    }

    [Fact]
    public void ValidateTransition_BlockedToQueued_IsValid()
    {
        var transition = new Daiv3.Orchestration.Models.TaskStatusTransition
        {
            CurrentStatus = Daiv3.Orchestration.Models.TaskStatus.Blocked,
            RequestedStatus = Daiv3.Orchestration.Models.TaskStatus.Queued,
        };

        var isValid = transition.Validate();

        Assert.True(isValid);
        Assert.Null(transition.InvalidReason);
    }

    [Fact]
    public void ValidateTransition_BlockedToFailed_IsValid()
    {
        var transition = new Daiv3.Orchestration.Models.TaskStatusTransition
        {
            CurrentStatus = Daiv3.Orchestration.Models.TaskStatus.Blocked,
            RequestedStatus = Daiv3.Orchestration.Models.TaskStatus.Failed,
        };

        var isValid = transition.Validate();

        Assert.True(isValid);
        Assert.Null(transition.InvalidReason);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    [InlineData(1, 5)]
    [InlineData(2, 3)]
    [InlineData(2, 4)]
    [InlineData(2, 5)]
    [InlineData(5, 1)]
    [InlineData(5, 4)]
    public void IsTransitionValid_StaticMethod_ReturnsCorrectly(
        int currentStatusValue,
        int requestedStatusValue)
    {
        var current = (Daiv3.Orchestration.Models.TaskStatus)currentStatusValue;
        var requested = (Daiv3.Orchestration.Models.TaskStatus)requestedStatusValue;
        var isValid = Daiv3.Orchestration.Models.TaskStatusTransition.IsTransitionValid(current, requested);

        Assert.True(isValid);
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(3, 1)]
    [InlineData(4, 0)]
    public void IsTransitionValid_InvalidTransitions_ReturnsFalse(
        int currentStatusValue,
        int requestedStatusValue)
    {
        var current = (Daiv3.Orchestration.Models.TaskStatus)currentStatusValue;
        var requested = (Daiv3.Orchestration.Models.TaskStatus)requestedStatusValue;
        var isValid = Daiv3.Orchestration.Models.TaskStatusTransition.IsTransitionValid(current, requested);

        Assert.False(isValid);
    }

    [Fact]
    public void GetValidTransitions_FromPending_ReturnsQueued()
    {
        var validTransitions = Daiv3.Orchestration.Models.TaskStatusTransition
            .GetValidTransitions(Daiv3.Orchestration.Models.TaskStatus.Pending)
            .ToList();

        Assert.Single(validTransitions);
        Assert.Contains(Daiv3.Orchestration.Models.TaskStatus.Queued, validTransitions);
    }

    [Fact]
    public void GetValidTransitions_FromQueued_ReturnsInProgressAndBlocked()
    {
        var validTransitions = Daiv3.Orchestration.Models.TaskStatusTransition
            .GetValidTransitions(Daiv3.Orchestration.Models.TaskStatus.Queued)
            .ToList();

        Assert.Equal(2, validTransitions.Count);
        Assert.Contains(Daiv3.Orchestration.Models.TaskStatus.InProgress, validTransitions);
        Assert.Contains(Daiv3.Orchestration.Models.TaskStatus.Blocked, validTransitions);
    }

    [Fact]
    public void GetValidTransitions_FromInProgress_ReturnsCompleteFailedBlocked()
    {
        var validTransitions = Daiv3.Orchestration.Models.TaskStatusTransition
            .GetValidTransitions(Daiv3.Orchestration.Models.TaskStatus.InProgress)
            .ToList();

        Assert.Equal(3, validTransitions.Count);
        Assert.Contains(Daiv3.Orchestration.Models.TaskStatus.Complete, validTransitions);
        Assert.Contains(Daiv3.Orchestration.Models.TaskStatus.Failed, validTransitions);
        Assert.Contains(Daiv3.Orchestration.Models.TaskStatus.Blocked, validTransitions);
    }

    [Fact]
    public void GetValidTransitions_FromComplete_ReturnsEmpty()
    {
        var validTransitions = Daiv3.Orchestration.Models.TaskStatusTransition
            .GetValidTransitions(Daiv3.Orchestration.Models.TaskStatus.Complete)
            .ToList();

        Assert.Empty(validTransitions);
    }

    [Fact]
    public void GetValidTransitions_FromFailed_ReturnsEmpty()
    {
        var validTransitions = Daiv3.Orchestration.Models.TaskStatusTransition
            .GetValidTransitions(Daiv3.Orchestration.Models.TaskStatus.Failed)
            .ToList();

        Assert.Empty(validTransitions);
    }

    [Fact]
    public void GetValidTransitions_FromBlocked_ReturnsQueuedAndFailed()
    {
        var validTransitions = Daiv3.Orchestration.Models.TaskStatusTransition
            .GetValidTransitions(Daiv3.Orchestration.Models.TaskStatus.Blocked)
            .ToList();

        Assert.Equal(2, validTransitions.Count);
        Assert.Contains(Daiv3.Orchestration.Models.TaskStatus.Queued, validTransitions);
        Assert.Contains(Daiv3.Orchestration.Models.TaskStatus.Failed, validTransitions);
    }
}
