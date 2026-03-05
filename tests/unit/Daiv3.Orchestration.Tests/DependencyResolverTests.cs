using Daiv3.Orchestration;
using Daiv3.Orchestration.Interfaces;
using Daiv3.Persistence;
using Daiv3.Persistence.Entities;
using Daiv3.Persistence.Repositories;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;

namespace Daiv3.Orchestration.Tests;

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

    #region Determinism Tests (PTS-NFR-001)

    [Fact]
    public async Task ResolveDependenciesAsync_WithSameInput_ProducesIdenticalOutputMultipleTimes()
    {
        // Arrange
        const string task1 = "task-a";
        const string task2 = "task-b";
        const string task3 = "task-c";
        const string mainTask = "task-main";

        var t1 = new ProjectTask { TaskId = task1, Title = "Task A", Status = "pending", Priority = 5, DependenciesJson = null };
        var t2 = new ProjectTask { TaskId = task2, Title = "Task B", Status = "pending", Priority = 5, DependenciesJson = null };
        var t3 = new ProjectTask { TaskId = task3, Title = "Task C", Status = "pending", Priority = 5, DependenciesJson = JsonSerializer.Serialize(new[] { task1, task2 }) };
        var tMain = new ProjectTask { TaskId = mainTask, Title = "Main Task", Status = "pending", Priority = 5, DependenciesJson = JsonSerializer.Serialize(new[] { task3 }) };

        _mockTaskRepository.Setup(r => r.GetByIdAsync(It.Is<string>(id => id == mainTask), It.IsAny<CancellationToken>())).ReturnsAsync(tMain);
        _mockTaskRepository.Setup(r => r.GetByIdAsync(It.Is<string>(id => id == task1), It.IsAny<CancellationToken>())).ReturnsAsync(t1);
        _mockTaskRepository.Setup(r => r.GetByIdAsync(It.Is<string>(id => id == task2), It.IsAny<CancellationToken>())).ReturnsAsync(t2);
        _mockTaskRepository.Setup(r => r.GetByIdAsync(It.Is<string>(id => id == task3), It.IsAny<CancellationToken>())).ReturnsAsync(t3);

        // Act - Run multiple times
        var result1 = await _resolver.ResolveDependenciesAsync(mainTask);
        var result2 = await _resolver.ResolveDependenciesAsync(mainTask);
        var result3 = await _resolver.ResolveDependenciesAsync(mainTask);

        // Assert - All results should be identical
        Assert.Equal(3, result1.Count);
        Assert.Equal(3, result2.Count);
        Assert.Equal(3, result3.Count);

        for (int i = 0; i < result1.Count; i++)
        {
            Assert.Equal(result1[i].TaskId, result2[i].TaskId);
            Assert.Equal(result1[i].TaskId, result3[i].TaskId);
            Assert.Equal(result1[i].ExecutionOrder, result2[i].ExecutionOrder);
            Assert.Equal(result1[i].ExecutionOrder, result3[i].ExecutionOrder);
        }
    }

    [Fact]
    public async Task ResolveDependenciesAsync_WithTasksSameExecutionOrder_SortsAlphabeticallyByTaskId()
    {
        // Arrange - Create tasks with IDs that would NOT be in alphabetical order if unsorted
        const string taskZ = "task-z";
        const string taskA = "task-a";
        const string taskM = "task-m";
        const string mainTask = "task-main";

        var tZ = new ProjectTask { TaskId = taskZ, Title = "Task Z", Status = "pending", Priority = 5, DependenciesJson = null };
        var tA = new ProjectTask { TaskId = taskA, Title = "Task A", Status = "pending", Priority = 5, DependenciesJson = null };
        var tM = new ProjectTask { TaskId = taskM, Title = "Task M", Status = "pending", Priority = 5, DependenciesJson = null };
        var tMain = new ProjectTask
        {
            TaskId = mainTask,
            Title = "Main Task",
            Status = "pending",
            Priority = 5,
            // JSON array in non-alphabetical order
            DependenciesJson = JsonSerializer.Serialize(new[] { taskZ, taskA, taskM })
        };

        _mockTaskRepository.Setup(r => r.GetByIdAsync(It.Is<string>(id => id == mainTask), It.IsAny<CancellationToken>())).ReturnsAsync(tMain);
        _mockTaskRepository.Setup(r => r.GetByIdAsync(It.Is<string>(id => id == taskZ), It.IsAny<CancellationToken>())).ReturnsAsync(tZ);
        _mockTaskRepository.Setup(r => r.GetByIdAsync(It.Is<string>(id => id == taskA), It.IsAny<CancellationToken>())).ReturnsAsync(tA);
        _mockTaskRepository.Setup(r => r.GetByIdAsync(It.Is<string>(id => id == taskM), It.IsAny<CancellationToken>())).ReturnsAsync(tM);

        // Act
        var result = await _resolver.ResolveDependenciesAsync(mainTask);

        // Assert - Tasks should be sorted alphabetically (task-a, task-m, task-z)
        Assert.Equal(3, result.Count);
        Assert.Equal(taskA, result[0].TaskId); // task-a
        Assert.Equal(taskM, result[1].TaskId); // task-m
        Assert.Equal(taskZ, result[2].TaskId); // task-z
        Assert.All(result, r => Assert.Equal(0, r.ExecutionOrder)); // All same execution order
    }

    [Fact]
    public async Task ResolveDependenciesAsync_WithDependenciesInDifferentJsonOrder_ProducesSameResult()
    {
        // Arrange - Create two resolvers with same dependencies but different JSON order
        const string task1 = "task-1";
        const string task2 = "task-2";
        const string task3 = "task-3";
        const string mainTask1 = "task-main-1";
        const string mainTask2 = "task-main-2";

        var t1 = new ProjectTask { TaskId = task1, Title = "Task 1", Status = "pending", Priority = 5, DependenciesJson = null };
        var t2 = new ProjectTask { TaskId = task2, Title = "Task 2", Status = "pending", Priority = 5, DependenciesJson = null };
        var t3 = new ProjectTask { TaskId = task3, Title = "Task 3", Status = "pending", Priority = 5, DependenciesJson = null };

        // Main task 1: dependencies in order [task-1, task-2, task-3]
        var tMain1 = new ProjectTask
        {
            TaskId = mainTask1,
            Title = "Main Task 1",
            Status = "pending",
            Priority = 5,
            DependenciesJson = JsonSerializer.Serialize(new[] { task1, task2, task3 })
        };

        // Main task 2: dependencies in DIFFERENT order [task-3, task-1, task-2]
        var tMain2 = new ProjectTask
        {
            TaskId = mainTask2,
            Title = "Main Task 2",
            Status = "pending",
            Priority = 5,
            DependenciesJson = JsonSerializer.Serialize(new[] { task3, task1, task2 })
        };

        _mockTaskRepository.Setup(r => r.GetByIdAsync(It.Is<string>(id => id == mainTask1), It.IsAny<CancellationToken>())).ReturnsAsync(tMain1);
        _mockTaskRepository.Setup(r => r.GetByIdAsync(It.Is<string>(id => id == mainTask2), It.IsAny<CancellationToken>())).ReturnsAsync(tMain2);
        _mockTaskRepository.Setup(r => r.GetByIdAsync(It.Is<string>(id => id == task1), It.IsAny<CancellationToken>())).ReturnsAsync(t1);
        _mockTaskRepository.Setup(r => r.GetByIdAsync(It.Is<string>(id => id == task2), It.IsAny<CancellationToken>())).ReturnsAsync(t2);
        _mockTaskRepository.Setup(r => r.GetByIdAsync(It.Is<string>(id => id == task3), It.IsAny<CancellationToken>())).ReturnsAsync(t3);

        // Act
        var result1 = await _resolver.ResolveDependenciesAsync(mainTask1);
        var result2 = await _resolver.ResolveDependenciesAsync(mainTask2);

        // Assert - Both should produce the same order despite different JSON order
        Assert.Equal(result1.Count, result2.Count);
        for (int i = 0; i < result1.Count; i++)
        {
            Assert.Equal(result1[i].TaskId, result2[i].TaskId);
            Assert.Equal(result1[i].ExecutionOrder, result2[i].ExecutionOrder);
        }

        // Verify they're in alphabetical order by TaskId
        Assert.Equal(task1, result1[0].TaskId);
        Assert.Equal(task2, result1[1].TaskId);
        Assert.Equal(task3, result1[2].TaskId);
    }

    [Fact]
    public async Task ResolveDependenciesAsync_WithComplexDependencyGraph_ProducesDeterministicOrder()
    {
        // Arrange - Complex graph with multiple levels and branches
        //    task-main
        //       |
        //    task-c  (depends on task-a, task-b)
        //      / \
        //  task-a task-b   (no dependencies)
        const string taskA = "task-a";
        const string taskB = "task-b";
        const string taskC = "task-c";
        const string mainTask = "task-main";

        var tA = new ProjectTask { TaskId = taskA, Title = "Task A", Status = "pending", Priority = 5, DependenciesJson = null };
        var tB = new ProjectTask { TaskId = taskB, Title = "Task B", Status = "pending", Priority = 5, DependenciesJson = null };
        var tC = new ProjectTask { TaskId = taskC, Title = "Task C", Status = "pending", Priority = 5, DependenciesJson = JsonSerializer.Serialize(new[] { taskB, taskA }) }; // note: B then A
        var tMain = new ProjectTask { TaskId = mainTask, Title = "Main Task", Status = "pending", Priority = 5, DependenciesJson = JsonSerializer.Serialize(new[] { taskC }) };

        _mockTaskRepository.Setup(r => r.GetByIdAsync(It.Is<string>(id => id == mainTask), It.IsAny<CancellationToken>())).ReturnsAsync(tMain);
        _mockTaskRepository.Setup(r => r.GetByIdAsync(It.Is<string>(id => id == taskA), It.IsAny<CancellationToken>())).ReturnsAsync(tA);
        _mockTaskRepository.Setup(r => r.GetByIdAsync(It.Is<string>(id => id == taskB), It.IsAny<CancellationToken>())).ReturnsAsync(tB);
        _mockTaskRepository.Setup(r => r.GetByIdAsync(It.Is<string>(id => id == taskC), It.IsAny<CancellationToken>())).ReturnsAsync(tC);

        // Act - Run multiple times
        var result1 = await _resolver.ResolveDependenciesAsync(mainTask);
        var result2 = await _resolver.ResolveDependenciesAsync(mainTask);
        var result3 = await _resolver.ResolveDependenciesAsync(mainTask);

        // Assert - All runs produce identical results
        Assert.Equal(3, result1.Count);
        for (int i = 0; i < result1.Count; i++)
        {
            Assert.Equal(result1[i].TaskId, result2[i].TaskId);
            Assert.Equal(result1[i].TaskId, result3[i].TaskId);
        }

        // Verify correct execution order
        Assert.Equal(taskA, result1[0].TaskId); // task-a: execution order 0
        Assert.Equal(taskB, result1[1].TaskId); // task-b: execution order 0
        Assert.Equal(taskC, result1[2].TaskId); // task-c: execution order 1
        Assert.Equal(0, result1[0].ExecutionOrder);
        Assert.Equal(0, result1[1].ExecutionOrder);
        Assert.Equal(1, result1[2].ExecutionOrder);
    }

    [Fact]
    public async Task AreDependenciesSatisfiedAsync_ChecksDependenciesInDeterministicOrder()
    {
        // Arrange
        const string task1 = "task-1";
        const string task2 = "task-2";
        const string task3 = "task-3";
        const string mainTask = "task-main";

        var t1 = new ProjectTask { TaskId = task1, Title = "Task 1", Status = "completed", Priority = 5, DependenciesJson = null };
        var t2 = new ProjectTask { TaskId = task2, Title = "Task 2", Status = "completed", Priority = 5, DependenciesJson = null };
        var t3 = new ProjectTask { TaskId = task3, Title = "Task 3", Status = "pending", Priority = 5, DependenciesJson = null };
        var tMain = new ProjectTask
        {
            TaskId = mainTask,
            Title = "Main Task",
            Status = "pending",
            Priority = 5,
            // Dependencies in non-alphabetical order
            DependenciesJson = JsonSerializer.Serialize(new[] { task3, task1, task2 })
        };

        _mockTaskRepository.Setup(r => r.GetByIdAsync(It.Is<string>(id => id == mainTask), It.IsAny<CancellationToken>())).ReturnsAsync(tMain);
        _mockTaskRepository.Setup(r => r.GetByIdAsync(It.Is<string>(id => id == task1), It.IsAny<CancellationToken>())).ReturnsAsync(t1);
        _mockTaskRepository.Setup(r => r.GetByIdAsync(It.Is<string>(id => id == task2), It.IsAny<CancellationToken>())).ReturnsAsync(t2);
        _mockTaskRepository.Setup(r => r.GetByIdAsync(It.Is<string>(id => id == task3), It.IsAny<CancellationToken>())).ReturnsAsync(t3);

        // Act
        var satisfied = await _resolver.AreDependenciesSatisfiedAsync(mainTask);

        // Assert
        Assert.False(satisfied); // task-3 is pending, so dependencies not satisfied

        // Verify GetByIdAsync was called in deterministic (sorted) order: task-1, task-2, task-3
        // Note: The implementation will check task-1 first (alphabetically), find it complete,
        // then task-2 (complete), then task-3 (pending) and return false
        _mockTaskRepository.Verify(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(4)); // main + 3 deps
    }

    #endregion
}
