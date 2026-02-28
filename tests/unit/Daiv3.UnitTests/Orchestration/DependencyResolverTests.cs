using Daiv3.Orchestration;
using Daiv3.Orchestration.Interfaces;
using Daiv3.Persistence;
using Daiv3.Persistence.Entities;
using Daiv3.Persistence.Repositories;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;

namespace Daiv3.UnitTests.Orchestration;

/// <summary>
/// Unit tests for DependencyResolver.
/// </summary>
public class DependencyResolverTests
{
    private readonly Mock<TaskRepository> _mockTaskRepository;
    private readonly Mock<ILogger<DependencyResolver>> _mockLogger;
    private readonly DependencyResolver _resolver;

    public DependencyResolverTests()
    {
        _mockTaskRepository = new Mock<TaskRepository>(
            Mock.Of<IDatabaseContext>(),
            Mock.Of<ILogger<TaskRepository>>());
        _mockLogger = new Mock<ILogger<DependencyResolver>>();
        _resolver = new DependencyResolver(_mockTaskRepository.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task ResolveDependenciesAsync_WithTaskWithoutDependencies_ReturnsEmptyList()
    {
        // Arrange
        var taskId = "task-1";
        var task = new ProjectTask
        {
            TaskId = taskId,
            Title = "Task 1",
            Status = "pending",
            Priority = 5,
            DependenciesJson = null
        };

        _mockTaskRepository.Setup(r => r.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        // Act
        var result = await _resolver.ResolveDependenciesAsync(taskId);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task ResolveDependenciesAsync_WithSingleDependency_ReturnsTaskInOrder()
    {
        // Arrange
        const string depTaskId = "task-dep";
        const string mainTaskId = "task-main";

        var depTask = new ProjectTask
        {
            TaskId = depTaskId,
            Title = "Dependency Task",
            Status = "pending",
            Priority = 5,
            DependenciesJson = null
        };

        var mainTask = new ProjectTask
        {
            TaskId = mainTaskId,
            Title = "Main Task",
            Status = "pending",
            Priority = 5,
            DependenciesJson = JsonSerializer.Serialize(new[] { depTaskId })
        };

        _mockTaskRepository.Setup(r =>
                r.GetByIdAsync(It.Is<string>(id => id == mainTaskId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mainTask);

        _mockTaskRepository.Setup(r =>
                r.GetByIdAsync(It.Is<string>(id => id == depTaskId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(depTask);

        // Act
        var result = await _resolver.ResolveDependenciesAsync(mainTaskId);

        // Assert
        Assert.Single(result);
        Assert.Equal(depTaskId, result[0].TaskId);
        Assert.Equal(0, result[0].ExecutionOrder);
    }

    [Fact]
    public async Task ResolveDependenciesAsync_WithChainedDependencies_ReturnsTasksInCorrectOrder()
    {
        // Arrange
        const string task1 = "task-1";
        const string task2 = "task-2";
        const string task3 = "task-3";

        var t1 = new ProjectTask
        {
            TaskId = task1,
            Title = "Task 1",
            Status = "pending",
            Priority = 5,
            DependenciesJson = null
        };

        var t2 = new ProjectTask
        {
            TaskId = task2,
            Title = "Task 2",
            Status = "pending",
            Priority = 5,
            DependenciesJson = JsonSerializer.Serialize(new[] { task1 })
        };

        var t3 = new ProjectTask
        {
            TaskId = task3,
            Title = "Task 3",
            Status = "pending",
            Priority = 5,
            DependenciesJson = JsonSerializer.Serialize(new[] { task2 })
        };

        _mockTaskRepository.Setup(r =>
                r.GetByIdAsync(It.Is<string>(id => id == task3), It.IsAny<CancellationToken>()))
            .ReturnsAsync(t3);

        _mockTaskRepository.Setup(r =>
                r.GetByIdAsync(It.Is<string>(id => id == task2), It.IsAny<CancellationToken>()))
            .ReturnsAsync(t2);

        _mockTaskRepository.Setup(r =>
                r.GetByIdAsync(It.Is<string>(id => id == task1), It.IsAny<CancellationToken>()))
            .ReturnsAsync(t1);

        // Act
        var result = await _resolver.ResolveDependenciesAsync(task3);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(task1, result[0].TaskId);
        Assert.Equal(task2, result[1].TaskId);
        Assert.Equal(0, result[0].ExecutionOrder);
        Assert.Equal(1, result[1].ExecutionOrder);
    }

    [Fact]
    public async Task ResolveDependenciesAsync_WithTaskNotFound_ThrowsArgumentException()
    {
        // Arrange
        const string taskId = "nonexistent";

        _mockTaskRepository.Setup(r =>
                r.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProjectTask?)null);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _resolver.ResolveDependenciesAsync(taskId));
    }

    [Fact]
    public async Task ResolveDependenciesAsync_WithMissingDependency_IgnoresMissingDep()
    {
        // Arrange
        const string taskId = "task-main";
        const string missingDepId = "task-missing";

        var task = new ProjectTask
        {
            TaskId = taskId,
            Title = "Main Task",
            Status = "pending",
            Priority = 5,
            DependenciesJson = JsonSerializer.Serialize(new[] { missingDepId })
        };

        _mockTaskRepository.Setup(r =>
                r.GetByIdAsync(It.Is<string>(id => id == taskId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        _mockTaskRepository.Setup(r =>
                r.GetByIdAsync(It.Is<string>(id => id == missingDepId), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProjectTask?)null);

        // Act - Should handle missing dependency gracefully
        var result = await _resolver.ResolveDependenciesAsync(taskId);

        // Assert - Returns empty list since missing dep is skipped
        Assert.Empty(result);
    }

    [Fact]
    public async Task AreDependenciesSatisfiedAsync_WithNoSatisfiedDependencies_ReturnsFalse()
    {
        // Arrange
        const string taskId = "task-main";
        const string depId = "task-dep";

        var depTask = new ProjectTask
        {
            TaskId = depId,
            Title = "Dependency",
            Status = "pending",
            Priority = 5,
            DependenciesJson = null
        };

        var mainTask = new ProjectTask
        {
            TaskId = taskId,
            Title = "Main Task",
            Status = "pending",
            Priority = 5,
            DependenciesJson = JsonSerializer.Serialize(new[] { depId })
        };

        _mockTaskRepository.Setup(r =>
                r.GetByIdAsync(It.Is<string>(id => id == taskId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mainTask);

        _mockTaskRepository.Setup(r =>
                r.GetByIdAsync(It.Is<string>(id => id == depId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(depTask);

        // Act
        var result = await _resolver.AreDependenciesSatisfiedAsync(taskId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task AreDependenciesSatisfiedAsync_WithCompletedDependencies_ReturnsTrue()
    {
        // Arrange
        const string taskId = "task-main";
        const string depId = "task-dep";

        var depTask = new ProjectTask
        {
            TaskId = depId,
            Title = "Dependency",
            Status = "complete",
            Priority = 5,
            DependenciesJson = null
        };

        var mainTask = new ProjectTask
        {
            TaskId = taskId,
            Title = "Main Task",
            Status = "pending",
            Priority = 5,
            DependenciesJson = JsonSerializer.Serialize(new[] { depId })
        };

        _mockTaskRepository.Setup(r =>
                r.GetByIdAsync(It.Is<string>(id => id == taskId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mainTask);

        _mockTaskRepository.Setup(r =>
                r.GetByIdAsync(It.Is<string>(id => id == depId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(depTask);

        // Act
        var result = await _resolver.AreDependenciesSatisfiedAsync(taskId);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task AreDependenciesSatisfiedAsync_WithNullDependencies_ReturnsTrue()
    {
        // Arrange
        const string taskId = "task-main";

        var mainTask = new ProjectTask
        {
            TaskId = taskId,
            Title = "Main Task",
            Status = "pending",
            Priority = 5,
            DependenciesJson = null
        };

        _mockTaskRepository.Setup(r =>
                r.GetByIdAsync(It.Is<string>(id => id == taskId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mainTask);

        // Act
        var result = await _resolver.AreDependenciesSatisfiedAsync(taskId);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ValidateDependenciesAsync_WithValidDependencies_ReturnsValid()
    {
        // Arrange
        const string taskId = "task-main";
        const string depId = "task-dep";

        var depTask = new ProjectTask
        {
            TaskId = depId,
            Title = "Dependency",
            Status = "pending",
            Priority = 5,
            DependenciesJson = null
        };

        var mainTask = new ProjectTask
        {
            TaskId = taskId,
            Title = "Main Task",
            Status = "pending",
            Priority = 5,
            DependenciesJson = JsonSerializer.Serialize(new[] { depId })
        };

        _mockTaskRepository.Setup(r =>
                r.GetByIdAsync(It.Is<string>(id => id == taskId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mainTask);

        _mockTaskRepository.Setup(r =>
                r.GetByIdAsync(It.Is<string>(id => id == depId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(depTask);

        // Act
        var result = await _resolver.ValidateDependenciesAsync(taskId);

        // Assert
        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateDependenciesAsync_WithMissingTask_ReturnsMissingDependencyError()
    {
        // Arrange
        const string taskId = "task-main";
        const string depId = "task-missing";

        var mainTask = new ProjectTask
        {
            TaskId = taskId,
            Title = "Main Task",
            Status = "pending",
            Priority = 5,
            DependenciesJson = JsonSerializer.Serialize(new[] { depId })
        };

        _mockTaskRepository.Setup(r =>
                r.GetByIdAsync(It.Is<string>(id => id == taskId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mainTask);

        _mockTaskRepository.Setup(r =>
                r.GetByIdAsync(It.Is<string>(id => id == depId), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProjectTask?)null);

        // Act
        var result = await _resolver.ValidateDependenciesAsync(taskId);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("does not exist", result.ErrorMessage);
        Assert.Equal(ValidationErrorType.MissingDependency, result.ErrorType);
    }

    [Fact]
    public async Task ValidateDependenciesAsync_WithSelfDependency_ReturnsCircularError()
    {
        // Arrange
        const string taskId = "task-main";

        var mainTask = new ProjectTask
        {
            TaskId = taskId,
            Title = "Main Task",
            Status = "pending",
            Priority = 5,
            DependenciesJson = JsonSerializer.Serialize(new[] { taskId })
        };

        _mockTaskRepository.Setup(r =>
                r.GetByIdAsync(It.Is<string>(id => id == taskId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mainTask);

        // Act
        var result = await _resolver.ValidateDependenciesAsync(taskId);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("Circular", result.ErrorMessage);
        Assert.Equal(ValidationErrorType.CircularDependency, result.ErrorType);
    }

    [Fact]
    public async Task ValidateDependenciesAsync_WithNoTask_ReturnsTaskNotFoundError()
    {
        // Arrange
        const string taskId = "task-nonexistent";

        _mockTaskRepository.Setup(r =>
                r.GetByIdAsync(It.Is<string>(id => id == taskId), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProjectTask?)null);

        // Act
        var result = await _resolver.ValidateDependenciesAsync(taskId);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
        Assert.Equal(ValidationErrorType.TaskNotFound, result.ErrorType);
    }

    [Fact]
    public async Task ResolveDependenciesAsync_WithMultipleDependencies_OrdersCorrectly()
    {
        // Arrange
        const string task1 = "task-1";
        const string task2 = "task-2";
        const string mainTask = "task-main";

        var t1 = new ProjectTask
        {
            TaskId = task1,
            Title = "Task 1",
            Status = "pending",
            Priority = 5,
            DependenciesJson = null
        };

        var t2 = new ProjectTask
        {
            TaskId = task2,
            Title = "Task 2",
            Status = "pending",
            Priority = 5,
            DependenciesJson = null
        };

        var tMain = new ProjectTask
        {
            TaskId = mainTask,
            Title = "Main Task",
            Status = "pending",
            Priority = 5,
            DependenciesJson = JsonSerializer.Serialize(new[] { task1, task2 })
        };

        _mockTaskRepository.Setup(r =>
                r.GetByIdAsync(It.Is<string>(id => id == mainTask), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tMain);

        _mockTaskRepository.Setup(r =>
                r.GetByIdAsync(It.Is<string>(id => id == task1), It.IsAny<CancellationToken>()))
            .ReturnsAsync(t1);

        _mockTaskRepository.Setup(r =>
                r.GetByIdAsync(It.Is<string>(id => id == task2), It.IsAny<CancellationToken>()))
            .ReturnsAsync(t2);

        // Act
        var result = await _resolver.ResolveDependenciesAsync(mainTask);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Equal(0, r.ExecutionOrder));
    }
}
