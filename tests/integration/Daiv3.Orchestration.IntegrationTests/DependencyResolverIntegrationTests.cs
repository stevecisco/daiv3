using Daiv3.Orchestration;
using Daiv3.Orchestration.Interfaces;
using Daiv3.Persistence;
using Daiv3.Persistence.Entities;
using Daiv3.Persistence.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using System.Text.Json;

namespace Daiv3.Orchestration.IntegrationTests;

/// <summary>
/// Integration tests for DependencyResolver with real database context.
/// </summary>
public class DependencyResolverIntegrationTests : IAsyncLifetime
{
    private readonly DatabaseContext _databaseContext;
    private readonly TaskRepository _taskRepository;
    private readonly DependencyResolver _resolver;
    private readonly ILogger<DependencyResolver> _logger;

    public DependencyResolverIntegrationTests()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<DependencyResolver>();

        var dbPath = Path.Combine(
            Path.GetTempPath(),
            $"daiv3_depresolver_test_{Guid.NewGuid():N}.db");

        var persistenceOptions = Options.Create(new PersistenceOptions { DatabasePath = dbPath });
        _databaseContext = new DatabaseContext(
            loggerFactory.CreateLogger<DatabaseContext>(),
            persistenceOptions);

        var taskLogger = loggerFactory.CreateLogger<TaskRepository>();
        _taskRepository = new TaskRepository(_databaseContext, taskLogger);

        _resolver = new DependencyResolver(_taskRepository, _logger);
    }

    public async Task InitializeAsync()
    {
        await _databaseContext.InitializeAsync().ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        // Clean up database
        if (_databaseContext is DatabaseContext dbContext)
        {
            // Close connection
            await dbContext.DisposeAsync().ConfigureAwait(false);

            // Delete temp database file if it exists
            try
            {
                if (File.Exists(dbContext.DatabasePath))
                {
                    File.Delete(dbContext.DatabasePath);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public async Task ResolveDependencies_WithStoredTasks_ResolvesCorrectly()
    {
        // Arrange
        var task1 = new ProjectTask
        {
            TaskId = "task-1",
            Title = "Task 1",
            Status = "pending",
            Priority = 5,
            DependenciesJson = null,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        var task2 = new ProjectTask
        {
            TaskId = "task-2",
            Title = "Task 2",
            Status = "pending",
            Priority = 5,
            DependenciesJson = JsonSerializer.Serialize(new[] { "task-1" }),
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        await _taskRepository.AddAsync(task1).ConfigureAwait(false);
        await _taskRepository.AddAsync(task2).ConfigureAwait(false);

        // Act
        var result = await _resolver.ResolveDependenciesAsync("task-2").ConfigureAwait(false);

        // Assert
        Assert.Single(result);
        Assert.Equal("task-1", result[0].TaskId);
        Assert.Equal(0, result[0].ExecutionOrder);
    }

    [Fact]
    public async Task AreDependenciesSatisfied_WithCompletedDependencies_ReturnsFalse()
    {
        // Arrange
        var depTask = new ProjectTask
        {
            TaskId = "dep-task",
            Title = "Dependency Task",
            Status = "pending",
            Priority = 5,
            DependenciesJson = null,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        var mainTask = new ProjectTask
        {
            TaskId = "main-task",
            Title = "Main Task",
            Status = "pending",
            Priority = 5,
            DependenciesJson = JsonSerializer.Serialize(new[] { "dep-task" }),
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        await _taskRepository.AddAsync(depTask).ConfigureAwait(false);
        await _taskRepository.AddAsync(mainTask).ConfigureAwait(false);

        // Act
        var result = await _resolver.AreDependenciesSatisfiedAsync("main-task").ConfigureAwait(false);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task AreDependenciesSatisfied_WithCompletedDependencies_ReturnsTrue()
    {
        // Arrange
        var depTask = new ProjectTask
        {
            TaskId = "completed-task",
            Title = "Completed Task",
            Status = "complete",
            Priority = 5,
            DependenciesJson = null,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        var mainTask = new ProjectTask
        {
            TaskId = "task-waiting",
            Title = "Waiting Task",
            Status = "pending",
            Priority = 5,
            DependenciesJson = JsonSerializer.Serialize(new[] { "completed-task" }),
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        await _taskRepository.AddAsync(depTask).ConfigureAwait(false);
        await _taskRepository.AddAsync(mainTask).ConfigureAwait(false);

        // Act
        var result = await _resolver.AreDependenciesSatisfiedAsync("task-waiting").ConfigureAwait(false);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ValidateDependencies_WithMultipleDependencies_ValidatesAllCorrectly()
    {
        // Arrange
        var task1 = new ProjectTask
        {
            TaskId = "t1",
            Title = "Task 1",
            Status = "pending",
            Priority = 5,
            DependenciesJson = null,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        var task2 = new ProjectTask
        {
            TaskId = "t2",
            Title = "Task 2",
            Status = "pending",
            Priority = 5,
            DependenciesJson = null,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        var mainTask = new ProjectTask
        {
            TaskId = "tmain",
            Title = "Main Task",
            Status = "pending",
            Priority = 5,
            DependenciesJson = JsonSerializer.Serialize(new[] { "t1", "t2" }),
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        await _taskRepository.AddAsync(task1).ConfigureAwait(false);
        await _taskRepository.AddAsync(task2).ConfigureAwait(false);
        await _taskRepository.AddAsync(mainTask).ConfigureAwait(false);

        // Act
        var result = await _resolver.ValidateDependenciesAsync("tmain").ConfigureAwait(false);

        // Assert
        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateDependencies_WithMissingTask_ReturnsError()
    {
        // Arrange
        var mainTask = new ProjectTask
        {
            TaskId = "main",
            Title = "Main Task",
            Status = "pending",
            Priority = 5,
            DependenciesJson = JsonSerializer.Serialize(new[] { "missing-task-id" }),
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        await _taskRepository.AddAsync(mainTask).ConfigureAwait(false);

        // Act
        var result = await _resolver.ValidateDependenciesAsync("main").ConfigureAwait(false);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
        Assert.Equal(ValidationErrorType.MissingDependency, result.ErrorType);
    }

    [Fact]
    public async Task ChainedDependencies_ResolvesCorrectly()
    {
        // Arrange
        var task1 = new ProjectTask
        {
            TaskId = "base",
            Title = "Base Task",
            Status = "pending",
            Priority = 5,
            DependenciesJson = null,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        var task2 = new ProjectTask
        {
            TaskId = "middle",
            Title = "Middle Task",
            Status = "pending",
            Priority = 5,
            DependenciesJson = JsonSerializer.Serialize(new[] { "base" }),
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        var task3 = new ProjectTask
        {
            TaskId = "top",
            Title = "Top Task",
            Status = "pending",
            Priority = 5,
            DependenciesJson = JsonSerializer.Serialize(new[] { "middle" }),
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        await _taskRepository.AddAsync(task1).ConfigureAwait(false);
        await _taskRepository.AddAsync(task2).ConfigureAwait(false);
        await _taskRepository.AddAsync(task3).ConfigureAwait(false);

        // Act
        var result = await _resolver.ResolveDependenciesAsync("top").ConfigureAwait(false);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("base", result[0].TaskId);
        Assert.Equal("middle", result[1].TaskId);
        Assert.Equal(0, result[0].ExecutionOrder);
        Assert.Equal(1, result[1].ExecutionOrder);
    }

    [Fact]
    public async Task EmptyDependencies_HandledCorrectly()
    {
        // Arrange
        var task = new ProjectTask
        {
            TaskId = "standalone",
            Title = "Standalone Task",
            Status = "pending",
            Priority = 5,
            DependenciesJson = JsonSerializer.Serialize(new string[] { }),
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        await _taskRepository.AddAsync(task).ConfigureAwait(false);

        // Act
        var satisfiedResult = await _resolver.AreDependenciesSatisfiedAsync("standalone").ConfigureAwait(false);
        var validateResult = await _resolver.ValidateDependenciesAsync("standalone").ConfigureAwait(false);
        var resolveResult = await _resolver.ResolveDependenciesAsync("standalone").ConfigureAwait(false);

        // Assert
        Assert.True(satisfiedResult);
        Assert.True(validateResult.IsValid);
        Assert.Empty(resolveResult);
    }
}
