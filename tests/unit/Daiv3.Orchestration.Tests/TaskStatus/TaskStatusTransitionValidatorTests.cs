using Daiv3.Orchestration;
using Daiv3.Orchestration.Interfaces;
using Daiv3.Orchestration.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Daiv3.Orchestration.Tests.TaskStatus;

public class TaskStatusTransitionValidatorTests
{
    private readonly Mock<ILogger<TaskStatusTransitionValidator>> _mockLogger = new();
    private readonly ITaskStatusTransitionValidator _validator;

    public TaskStatusTransitionValidatorTests()
    {
        _validator = new TaskStatusTransitionValidator(_mockLogger.Object);
    }

    [Fact]
    public void ValidateTransition_ValidTransition_ReturnsValidResult()
    {
        var result = _validator.ValidateTransition(
            Daiv3.Orchestration.Models.TaskStatus.Pending,
            Daiv3.Orchestration.Models.TaskStatus.Queued);

        Assert.NotNull(result);
        Assert.True(result.IsValid);
        Assert.Null(result.InvalidReason);
        Assert.Equal(Daiv3.Orchestration.Models.TaskStatus.Pending, result.CurrentStatus);
        Assert.Equal(Daiv3.Orchestration.Models.TaskStatus.Queued, result.RequestedStatus);
    }

    [Fact]
    public void ValidateTransition_InvalidTransition_ReturnsInvalidResult()
    {
        var result = _validator.ValidateTransition(
            Daiv3.Orchestration.Models.TaskStatus.Pending,
            Daiv3.Orchestration.Models.TaskStatus.InProgress);

        Assert.NotNull(result);
        Assert.False(result.IsValid);
        Assert.NotNull(result.InvalidReason);
    }

    [Fact]
    public void ValidateTransition_InvalidTransition_LogsWarning()
    {
        _validator.ValidateTransition(
            Daiv3.Orchestration.Models.TaskStatus.Complete,
            Daiv3.Orchestration.Models.TaskStatus.Queued);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Invalid task status transition")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void ValidateTransition_ValidTransition_LogsDebug()
    {
        _validator.ValidateTransition(
            Daiv3.Orchestration.Models.TaskStatus.Pending,
            Daiv3.Orchestration.Models.TaskStatus.Queued);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Valid task status transition")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void GetValidTransitions_ValidStates_ReturnsExpectedStates()
    {
        var validTransitions = _validator
            .GetValidTransitions(Daiv3.Orchestration.Models.TaskStatus.Queued)
            .ToList();

        Assert.Equal(2, validTransitions.Count);
        Assert.Contains(Daiv3.Orchestration.Models.TaskStatus.InProgress, validTransitions);
        Assert.Contains(Daiv3.Orchestration.Models.TaskStatus.Blocked, validTransitions);
    }

    [Fact]
    public void GetValidTransitions_TerminalState_ReturnsEmpty()
    {
        var validTransitions = _validator
            .GetValidTransitions(Daiv3.Orchestration.Models.TaskStatus.Complete)
            .ToList();

        Assert.Empty(validTransitions);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void IsTerminalState_TerminalStates_ReturnsTrue(int status)
    {
        var statusValue = (Daiv3.Orchestration.Models.TaskStatus)status;
        var isTerminal = _validator.IsTerminalState(statusValue);

        Assert.True(isTerminal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public void IsTerminalState_NonTerminalStates_ReturnsFalse(int status)
    {
        var statusValue = (Daiv3.Orchestration.Models.TaskStatus)status;
        var isTerminal = _validator.IsTerminalState(statusValue);

        Assert.False(isTerminal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void IsRecoverableState_RecoverableStates_ReturnsTrue(int status)
    {
        var statusValue = (Daiv3.Orchestration.Models.TaskStatus)status;
        var isRecoverable = _validator.IsRecoverableState(statusValue);

        Assert.True(isRecoverable);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void IsRecoverableState_NonRecoverableStates_ReturnsFalse(int status)
    {
        var statusValue = (Daiv3.Orchestration.Models.TaskStatus)status;
        var isRecoverable = _validator.IsRecoverableState(statusValue);

        Assert.False(isRecoverable);
    }

    [Fact]
    public void ValidateTransition_MultipleTransitions_EachLogged()
    {
        var transitions = new[]
        {
            (Daiv3.Orchestration.Models.TaskStatus.Pending, Daiv3.Orchestration.Models.TaskStatus.Queued),
            (Daiv3.Orchestration.Models.TaskStatus.Queued, Daiv3.Orchestration.Models.TaskStatus.InProgress),
            (Daiv3.Orchestration.Models.TaskStatus.InProgress, Daiv3.Orchestration.Models.TaskStatus.Complete),
        };

        foreach (var (current, requested) in transitions)
        {
            _validator.ValidateTransition(current, requested);
        }

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Exactly(3));
    }

    [Fact]
    public void ValidateTransition_InvalidTransition_PopulatesInvalidReason()
    {
        var result = _validator.ValidateTransition(
            Daiv3.Orchestration.Models.TaskStatus.Blocked,
            Daiv3.Orchestration.Models.TaskStatus.Complete);

        Assert.False(result.IsValid);
        Assert.False(string.IsNullOrWhiteSpace(result.InvalidReason));
    }

    [Fact]
    public void ITaskStatusTransitionValidator_CanBeMocked_ForConsumerTests()
    {
        var validatorMock = new Mock<ITaskStatusTransitionValidator>();
        var expected = new TaskStatusTransition
        {
            CurrentStatus = Daiv3.Orchestration.Models.TaskStatus.Pending,
            RequestedStatus = Daiv3.Orchestration.Models.TaskStatus.Queued,
        };
        expected.Validate();

        validatorMock
            .Setup(x => x.ValidateTransition(
                Daiv3.Orchestration.Models.TaskStatus.Pending,
                Daiv3.Orchestration.Models.TaskStatus.Queued))
            .Returns(expected);

        var result = validatorMock.Object.ValidateTransition(
            Daiv3.Orchestration.Models.TaskStatus.Pending,
            Daiv3.Orchestration.Models.TaskStatus.Queued);

        Assert.True(result.IsValid);
        Assert.Equal(Daiv3.Orchestration.Models.TaskStatus.Pending, result.CurrentStatus);
        Assert.Equal(Daiv3.Orchestration.Models.TaskStatus.Queued, result.RequestedStatus);
        validatorMock.Verify(
            x => x.ValidateTransition(
                Daiv3.Orchestration.Models.TaskStatus.Pending,
                Daiv3.Orchestration.Models.TaskStatus.Queued),
            Times.Once);
    }
}
