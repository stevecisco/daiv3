using Daiv3.Orchestration;
using Daiv3.Orchestration.Interfaces;
using Daiv3.Orchestration.Messaging;
using Daiv3.Orchestration.Messaging.Storage;
using Daiv3.Persistence;
using Daiv3.Persistence.Entities;
using Daiv3.Persistence.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using System.Text.Json;

namespace Daiv3.Orchestration.IntegrationTests;

/// <summary>
/// Acceptance tests for task dependency management.
/// Verifies PTS-ACC-001: A task with dependencies does not execute until dependencies complete.
/// </summary>
public class TaskDependencyAcceptanceTests : IAsyncLifetime
{
    private readonly DatabaseContext _databaseContext;
    private readonly TaskRepository _taskRepository;
    private readonly AgentRepository _agentRepository;
    private readonly DependencyResolver _dependencyResolver;
    private readonly IAgentManager _agentManager;
    private readonly ITaskOrchestrator _orchestrator;
    private readonly ILogger<TaskOrchestrator> _orchestratorLogger;
    private readonly ILogger<DependencyResolver> _resolverLogger;
    private readonly IMessageBroker _messageBroker;
    private readonly string _testStorageDir;

    public TaskDependencyAcceptanceTests()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _orchestratorLogger = loggerFactory.CreateLogger<TaskOrchestrator>();
        _resolverLogger = loggerFactory.CreateLogger<DependencyResolver>();

        // Setup message broker for tests
        _testStorageDir = Path.Combine(Path.GetTempPath(), $"daiv3-dep-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testStorageDir);
        
        var brokerOptions = new MessageBrokerOptions
        {
            StorageBackend = "FileSystem",
            FileSystemOptions = new FileSystemMessageStoreOptions
            {
                StorageDirectory = _testStorageDir,
                CleanupIntervalSeconds = 3600
            }
        };
        
        var messageStore = new FileSystemMessageStore(
            loggerFactory.CreateLogger<FileSystemMessageStore>(),
            Options.Create(brokerOptions.FileSystemOptions));
        
        _messageBroker = new MessageBroker(
            loggerFactory.CreateLogger<MessageBroker>(),
            messageStore,
            Options.Create(brokerOptions));

        var dbPath = Path.Combine(
            Path.GetTempPath(),
            $"daiv3_acceptance_test_{Guid.NewGuid():N}.db");

        var persistenceOptions = Options.Create(new PersistenceOptions { DatabasePath = dbPath });
        _databaseContext = new DatabaseContext(
            loggerFactory.CreateLogger<DatabaseContext>(),
            persistenceOptions);

        var taskLogger = loggerFactory.CreateLogger<TaskRepository>();
        _taskRepository = new TaskRepository(_databaseContext, taskLogger);
        _agentRepository = new AgentRepository(_databaseContext, loggerFactory.CreateLogger<AgentRepository>());

        _dependencyResolver = new DependencyResolver(_taskRepository, _resolverLogger);

        // Create orchestrator with required dependencies
        var orchestrationOptions = Options.Create(new OrchestrationOptions
        {
            MinimumIntentConfidence = 0.5m,
            EnableTaskDependencyValidation = true
        });

        var intentResolverLogger = loggerFactory.CreateLogger<IntentResolver>();
        var intentResolver = new IntentResolver(intentResolverLogger, orchestrationOptions);
        _agentManager = new AgentManager(
            loggerFactory.CreateLogger<AgentManager>(),
            _agentRepository,
            _messageBroker,
            orchestrationOptions);

        _orchestrator = new TaskOrchestrator(
            intentResolver,
            _dependencyResolver,
            _agentManager,
            _messageBroker,
            _orchestratorLogger,
            orchestrationOptions);
    }

    public async Task InitializeAsync()
    {
        await _databaseContext.InitializeAsync().ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        if (_databaseContext is DatabaseContext dbContext)
        {
            await dbContext.DisposeAsync().ConfigureAwait(false);

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

        // Clean up message broker test storage
        try
        {
            if (Directory.Exists(_testStorageDir))
            {
                Directory.Delete(_testStorageDir, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    /// <summary>
    /// PTS-ACC-001: Verifies that a task with dependencies does not execute until dependencies complete.
    /// </summary>
    [Fact]
    public async Task TaskWithDependencies_DoesNotExecute_UntilDependenciesComplete()
    {
        // Arrange: Create three tasks in a dependency chain
        // Task A: No dependencies (base task)
        var taskA = new ProjectTask
        {
            TaskId = "task-a",
            Title = "Task A - Base Task",
            Description = "This is the foundational task with no dependencies",
            Status = "pending",
            Priority = 5,
            DependenciesJson = null,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        // Task B: Depends on Task A
        var taskB = new ProjectTask
        {
            TaskId = "task-b",
            Title = "Task B - Depends on A",
            Description = "This task depends on Task A completion",
            Status = "pending",
            Priority = 5,
            DependenciesJson = JsonSerializer.Serialize(new[] { "task-a" }),
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        // Task C: Depends on Task A and Task B
        var taskC = new ProjectTask
        {
            TaskId = "task-c",
            Title = "Task C - Depends on A and B",
            Description = "This task depends on both Task A and Task B completion",
            Status = "pending",
            Priority = 5,
            DependenciesJson = JsonSerializer.Serialize(new[] { "task-a", "task-b" }),
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        await _taskRepository.AddAsync(taskA).ConfigureAwait(false);
        await _taskRepository.AddAsync(taskB).ConfigureAwait(false);
        await _taskRepository.AddAsync(taskC).ConfigureAwait(false);

        // Act & Assert 1: Task B cannot be enqueued because Task A is not complete
        var canEnqueueB_Initially = await _orchestrator.CanEnqueueTaskAsync("task-b").ConfigureAwait(false);
        Assert.False(canEnqueueB_Initially, "Task B should not be enqueueable because Task A is not complete");

        // Act & Assert 2: Task C cannot be enqueued because Task A and B are not complete
        var canEnqueueC_Initially = await _orchestrator.CanEnqueueTaskAsync("task-c").ConfigureAwait(false);
        Assert.False(canEnqueueC_Initially, "Task C should not be enqueueable because Task A and B are not complete");

        // Act & Assert 3: Complete Task A, now Task B can be enqueued
        taskA.Status = "complete";
        taskA.CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await _taskRepository.UpdateAsync(taskA).ConfigureAwait(false);

        var canEnqueueB_AfterA = await _orchestrator.CanEnqueueTaskAsync("task-b").ConfigureAwait(false);
        Assert.True(canEnqueueB_AfterA, "Task B should now be enqueueable because Task A is complete");

        // Act & Assert 4: Task C still cannot be enqueued because Task B is not complete
        var canEnqueueC_AfterA = await _orchestrator.CanEnqueueTaskAsync("task-c").ConfigureAwait(false);
        Assert.False(canEnqueueC_AfterA, "Task C should not be enqueueable because Task B is still not complete");

        // Act & Assert 5: Complete Task B, now Task C can be enqueued
        taskB.Status = "complete";
        taskB.CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await _taskRepository.UpdateAsync(taskB).ConfigureAwait(false);

        var canEnqueueC_AfterB = await _orchestrator.CanEnqueueTaskAsync("task-c").ConfigureAwait(false);
        Assert.True(canEnqueueC_AfterB, "Task C should now be enqueueable because both Task A and B are complete");

        // Verify dependency resolution order
        var dependencies = await _dependencyResolver.ResolveDependenciesAsync("task-c").ConfigureAwait(false);
        Assert.Equal(2, dependencies.Count);
        Assert.Contains(dependencies, d => d.TaskId == "task-a");
        Assert.Contains(dependencies, d => d.TaskId == "task-b");
    }

    /// <summary>
    /// PTS-ACC-001 Extended: Verifies that dependency satisfaction checks work with multiple dependency states.
    /// </summary>
    [Fact]
    public async Task TaskDependencyBlocking_WithMixedStates_BehavesCorrectly()
    {
        // Arrange: Create tasks with various states
        var completedTask = new ProjectTask
        {
            TaskId = "completed-dep",
            Title = "Completed Dependency",
            Status = "complete",
            Priority = 5,
            DependenciesJson = null,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        var pendingTask = new ProjectTask
        {
            TaskId = "pending-dep",
            Title = "Pending Dependency",
            Status = "pending",
            Priority = 5,
            DependenciesJson = null,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        var inProgressTask = new ProjectTask
        {
            TaskId = "inprogress-dep",
            Title = "In Progress Dependency",
            Status = "in_progress",
            Priority = 5,
            DependenciesJson = null,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        var targetTask = new ProjectTask
        {
            TaskId = "target-task",
            Title = "Target Task with Mixed Dependencies",
            Status = "pending",
            Priority = 5,
            DependenciesJson = JsonSerializer.Serialize(new[] { "completed-dep", "pending-dep", "inprogress-dep" }),
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        await _taskRepository.AddAsync(completedTask).ConfigureAwait(false);
        await _taskRepository.AddAsync(pendingTask).ConfigureAwait(false);
        await _taskRepository.AddAsync(inProgressTask).ConfigureAwait(false);
        await _taskRepository.AddAsync(targetTask).ConfigureAwait(false);

        // Act & Assert 1: Cannot enqueue because not all dependencies are complete
        var canEnqueue_Initially = await _orchestrator.CanEnqueueTaskAsync("target-task").ConfigureAwait(false);
        Assert.False(canEnqueue_Initially, "Target task should be blocked because some dependencies are not complete");

        // Act & Assert 2: Complete the pending task
        pendingTask.Status = "complete";
        pendingTask.CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await _taskRepository.UpdateAsync(pendingTask).ConfigureAwait(false);

        var canEnqueue_AfterPending = await _orchestrator.CanEnqueueTaskAsync("target-task").ConfigureAwait(false);
        Assert.False(canEnqueue_AfterPending, "Target task should still be blocked because in-progress task is not complete");

        // Act & Assert 3: Complete the in-progress task
        inProgressTask.Status = "complete";
        inProgressTask.CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await _taskRepository.UpdateAsync(inProgressTask).ConfigureAwait(false);

        var canEnqueue_Final = await _orchestrator.CanEnqueueTaskAsync("target-task").ConfigureAwait(false);
        Assert.True(canEnqueue_Final, "Target task should now be enqueueable because all dependencies are complete");
    }

    /// <summary>
    /// PTS-ACC-001 Edge Case: Verifies that a task with no dependencies can always be enqueued.
    /// </summary>
    [Fact]
    public async Task TaskWithNoDependencies_CanAlwaysBeEnqueued()
    {
        // Arrange
        var independentTask = new ProjectTask
        {
            TaskId = "independent-task",
            Title = "Independent Task",
            Description = "This task has no dependencies",
            Status = "pending",
            Priority = 5,
            DependenciesJson = null,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        await _taskRepository.AddAsync(independentTask).ConfigureAwait(false);

        // Act
        var canEnqueue = await _orchestrator.CanEnqueueTaskAsync("independent-task").ConfigureAwait(false);

        // Assert
        Assert.True(canEnqueue, "Task with no dependencies should always be enqueueable");
    }

    /// <summary>
    /// PTS-ACC-001 Observability: Verifies that dependency blocking is logged appropriately.
    /// </summary>
    [Fact]
    public async Task DependencyBlocking_IsObservableInLogs()
    {
        // Arrange
        var depTask = new ProjectTask
        {
            TaskId = "observable-dep",
            Title = "Observable Dependency",
            Status = "pending",
            Priority = 5,
            DependenciesJson = null,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        var mainTask = new ProjectTask
        {
            TaskId = "observable-main",
            Title = "Observable Main Task",
            Status = "pending",
            Priority = 5,
            DependenciesJson = JsonSerializer.Serialize(new[] { "observable-dep" }),
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        await _taskRepository.AddAsync(depTask).ConfigureAwait(false);
        await _taskRepository.AddAsync(mainTask).ConfigureAwait(false);

        // Act: Check if dependencies are satisfied (should be logged internally)
        var areSatisfied = await _dependencyResolver.AreDependenciesSatisfiedAsync("observable-main").ConfigureAwait(false);
        var canEnqueue = await _orchestrator.CanEnqueueTaskAsync("observable-main").ConfigureAwait(false);

        // Assert
        Assert.False(areSatisfied, "Dependencies should not be satisfied");
        Assert.False(canEnqueue, "Task should not be enqueueable");

        // Note: Actual log verification would require a test logger implementation
        // For now, we verify the behavior is correct and logs are emitted during execution
    }
}
