using Daiv3.Persistence;
using Daiv3.Persistence.Entities;
using Daiv3.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Daiv3.IntegrationTests.Persistence;

[Collection("Database")]
public class TaskRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly string _testDbPath;
    private readonly ILoggerFactory _loggerFactory;
    private DatabaseContext? _databaseContext;

    public TaskRepositoryIntegrationTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"daiv3_task_repo_test_{Guid.NewGuid():N}.db");
        _loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Warning));
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_databaseContext != null)
        {
            await _databaseContext.DisposeAsync();
            _databaseContext = null;
        }

        SqliteConnection.ClearAllPools();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        await Task.Delay(100);

        if (File.Exists(_testDbPath))
        {
            var remainingAttempts = 10;
            while (remainingAttempts > 0)
            {
                try
                {
                    File.Delete(_testDbPath);
                    break;
                }
                catch (IOException) when (remainingAttempts > 1)
                {
                    remainingAttempts--;
                    await Task.Delay(100);
                }
            }
        }

        _loggerFactory.Dispose();
    }

    [Fact]
    public async Task AddAndGetById_PersistsTaskWithDependencyMetadata()
    {
        var databaseContext = await CreateInitializedContextAsync();
        var projectRepository = new ProjectRepository(databaseContext, _loggerFactory.CreateLogger<ProjectRepository>());
        var taskRepository = new TaskRepository(databaseContext, _loggerFactory.CreateLogger<TaskRepository>());

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var projectId = Guid.NewGuid().ToString();
        var taskId = Guid.NewGuid().ToString();

        await projectRepository.AddAsync(new Project
        {
            ProjectId = projectId,
            Name = "PTS Project",
            Description = "Task persistence test",
            RootPaths = "C:/workspace",
            CreatedAt = now,
            UpdatedAt = now,
            Status = "active"
        });

        var dependenciesJson = "[\"dep-task-1\",\"dep-task-2\"]";

        await taskRepository.AddAsync(new ProjectTask
        {
            TaskId = taskId,
            ProjectId = projectId,
            Title = "Implement persistence",
            Description = "Store dependency metadata",
            Status = "pending",
            Priority = 2,
            ScheduledAt = now + 300,
            NextRunAt = now + 600,
            LastRunAt = now - 120,
            DependenciesJson = dependenciesJson,
            ResultJson = "{\"state\":\"not-started\"}",
            CreatedAt = now,
            UpdatedAt = now
        });

        var task = await taskRepository.GetByIdAsync(taskId);
        var projectTasks = await taskRepository.GetByProjectIdAsync(projectId);

        Assert.NotNull(task);
        Assert.Equal(taskId, task!.TaskId);
        Assert.Equal(projectId, task.ProjectId);
        Assert.Equal("pending", task.Status);
        Assert.Equal(2, task.Priority);
        Assert.Equal(now + 600, task.NextRunAt);
        Assert.Equal(now - 120, task.LastRunAt);
        Assert.Equal(dependenciesJson, task.DependenciesJson);
        Assert.Single(projectTasks);
        Assert.Equal(taskId, projectTasks[0].TaskId);
    }

    [Fact]
    public async Task ExistingTaskWithoutDependencies_CanBeUpdatedWithDependencyMetadata()
    {
        var databaseContext = await CreateInitializedContextAsync();
        var taskRepository = new TaskRepository(databaseContext, _loggerFactory.CreateLogger<TaskRepository>());
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var taskId = Guid.NewGuid().ToString();

        await using (var connection = await databaseContext.GetConnectionAsync())
        {
            await using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO tasks (task_id, project_id, title, description, status, priority, created_at, updated_at)
                VALUES ($task_id, NULL, $title, $description, $status, $priority, $created_at, $updated_at)";
            command.Parameters.AddWithValue("$task_id", taskId);
            command.Parameters.AddWithValue("$title", "Legacy task");
            command.Parameters.AddWithValue("$description", "Inserted without dependency metadata");
            command.Parameters.AddWithValue("$status", "pending");
            command.Parameters.AddWithValue("$priority", 1);
            command.Parameters.AddWithValue("$created_at", now);
            command.Parameters.AddWithValue("$updated_at", now);
            await command.ExecuteNonQueryAsync();
        }

        var task = await taskRepository.GetByIdAsync(taskId);
        Assert.NotNull(task);
        Assert.Null(task!.DependenciesJson);
        Assert.Null(task.NextRunAt);
        Assert.Null(task.LastRunAt);

        task.DependenciesJson = "[\"legacy-dep\"]";
        task.NextRunAt = now + 600;
        task.LastRunAt = now + 10;
        task.UpdatedAt = now + 5;
        await taskRepository.UpdateAsync(task);

        var updatedTask = await taskRepository.GetByIdAsync(taskId);
        Assert.NotNull(updatedTask);
        Assert.Equal("[\"legacy-dep\"]", updatedTask!.DependenciesJson);
        Assert.Equal(now + 600, updatedTask.NextRunAt);
        Assert.Equal(now + 10, updatedTask.LastRunAt);
    }

    [Fact]
    public async Task DeletingProject_PreservesTaskAndDependencyMetadata()
    {
        var databaseContext = await CreateInitializedContextAsync();
        var projectRepository = new ProjectRepository(databaseContext, _loggerFactory.CreateLogger<ProjectRepository>());
        var taskRepository = new TaskRepository(databaseContext, _loggerFactory.CreateLogger<TaskRepository>());

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var projectId = Guid.NewGuid().ToString();
        var taskId = Guid.NewGuid().ToString();

        await projectRepository.AddAsync(new Project
        {
            ProjectId = projectId,
            Name = "Deletable project",
            RootPaths = "C:/workspace",
            CreatedAt = now,
            UpdatedAt = now,
            Status = "active"
        });

        await taskRepository.AddAsync(new ProjectTask
        {
            TaskId = taskId,
            ProjectId = projectId,
            Title = "Task with dependency metadata",
            Status = "pending",
            Priority = 1,
            DependenciesJson = "[\"dep-after-delete\"]",
            CreatedAt = now,
            UpdatedAt = now
        });

        await projectRepository.DeleteAsync(projectId);

        var taskAfterProjectDelete = await taskRepository.GetByIdAsync(taskId);

        Assert.NotNull(taskAfterProjectDelete);
        Assert.Null(taskAfterProjectDelete!.ProjectId);
        Assert.Equal("[\"dep-after-delete\"]", taskAfterProjectDelete.DependenciesJson);
    }

    private async Task<DatabaseContext> CreateInitializedContextAsync()
    {
        var options = Options.Create(new PersistenceOptions
        {
            DatabasePath = _testDbPath,
            EnableWAL = true,
            BusyTimeout = 5000,
            MaxPoolSize = 10
        });

        _databaseContext = new DatabaseContext(_loggerFactory.CreateLogger<DatabaseContext>(), options);
        await _databaseContext.InitializeAsync();
        await _databaseContext.MigrateToLatestAsync();
        return _databaseContext;
    }
}
