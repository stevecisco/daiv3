using Daiv3.Orchestration.Interfaces;
using Daiv3.Persistence.Repositories;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Daiv3.Orchestration;

/// <summary>
/// Resolves task dependencies and determines execution order before enqueueing model requests.
/// </summary>
public class DependencyResolver : IDependencyResolver
{
    private readonly TaskRepository _taskRepository;
    private readonly ILogger<DependencyResolver> _logger;

    public DependencyResolver(TaskRepository taskRepository, ILogger<DependencyResolver> logger)
    {
        _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DependencyResolvedTask>> ResolveDependenciesAsync(
        string taskId,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);

        _logger.LogInformation("Resolving dependencies for task {TaskId}", taskId);

        var task = await _taskRepository.GetByIdAsync(taskId, ct).ConfigureAwait(false);
        if (task == null)
        {
            _logger.LogError("Task {TaskId} not found", taskId);
            throw new ArgumentException($"Task {taskId} not found", nameof(taskId));
        }

        var resolvedTasks = new List<DependencyResolvedTask>();
        var visited = new HashSet<string>();
        var visitStack = new HashSet<string>(); // For cycle detection

        await AddTaskAndDependenciesAsync(task, resolvedTasks, visited, visitStack, ct).ConfigureAwait(false);

        // Remove the main task from results - only return its dependencies
        resolvedTasks = resolvedTasks.Where(t => t.TaskId != taskId).ToList();

        // Sort by execution order
        resolvedTasks = resolvedTasks.OrderBy(t => t.ExecutionOrder).ToList();

        _logger.LogInformation(
            "Resolved {Count} dependencies for task {TaskId}",
            resolvedTasks.Count, taskId);

        return resolvedTasks.AsReadOnly();
    }

    /// <inheritdoc />
    public async Task<bool> AreDependenciesSatisfiedAsync(string taskId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);

        var task = await _taskRepository.GetByIdAsync(taskId, ct).ConfigureAwait(false);
        if (task == null)
        {
            _logger.LogWarning("Task {TaskId} not found when checking dependencies", taskId);
            return false;
        }

        // No dependencies = satisfied
        if (string.IsNullOrWhiteSpace(task.DependenciesJson))
        {
            _logger.LogDebug("Task {TaskId} has no dependencies", taskId);
            return true;
        }

        var dependencyIds = ParseDependencies(task.DependenciesJson);
        if (dependencyIds.Count == 0)
        {
            return true;
        }

        // Check if all dependencies are complete
        foreach (var depId in dependencyIds)
        {
            var depTask = await _taskRepository.GetByIdAsync(depId, ct).ConfigureAwait(false);
            if (depTask == null)
            {
                _logger.LogWarning(
                    "Dependency task {DepTaskId} for task {TaskId} not found",
                    depId, taskId);
                return false;
            }

            if (depTask.Status != "complete" && depTask.Status != "completed")
            {
                _logger.LogDebug(
                    "Dependency {DepTaskId} for task {TaskId} not satisfied (status: {Status})",
                    depId, taskId, depTask.Status);
                return false;
            }
        }

        _logger.LogInformation("All dependencies for task {TaskId} are satisfied", taskId);
        return true;
    }

    /// <inheritdoc />
    public async Task<DependencyValidationResult> ValidateDependenciesAsync(
        string taskId,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);

        var task = await _taskRepository.GetByIdAsync(taskId, ct).ConfigureAwait(false);
        if (task == null)
        {
            _logger.LogWarning("Task {TaskId} not found during validation", taskId);
            return new DependencyValidationResult
            {
                IsValid = false,
                ErrorMessage = $"Task {taskId} not found",
                ErrorType = ValidationErrorType.TaskNotFound,
                InvolvedTaskIds = new List<string> { taskId }
            };
        }

        // No dependencies = valid
        if (string.IsNullOrWhiteSpace(task.DependenciesJson))
        {
            _logger.LogDebug("Task {TaskId} has no dependencies to validate", taskId);
            return new DependencyValidationResult { IsValid = true };
        }

        var dependencyIds = ParseDependencies(task.DependenciesJson);
        if (dependencyIds.Count == 0)
        {
            return new DependencyValidationResult { IsValid = true };
        }

        // Validate all dependencies exist
        var involvedTasks = new List<string> { taskId };
        foreach (var depId in dependencyIds)
        {
            var depTask = await _taskRepository.GetByIdAsync(depId, ct).ConfigureAwait(false);
            if (depTask == null)
            {
                _logger.LogWarning(
                    "Dependency task {DepTaskId} for task {TaskId} does not exist",
                    depId, taskId);
                return new DependencyValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"Dependency task {depId} does not exist",
                    ErrorType = ValidationErrorType.MissingDependency,
                    InvolvedTaskIds = new List<string> { taskId, depId }
                };
            }
            involvedTasks.Add(depId);
        }

        // Check for circular dependencies
        var cycleResult = DetectCircularDependency(taskId, dependencyIds);
        if (cycleResult.HasCycle)
        {
            _logger.LogWarning(
                "Circular dependency detected for task {TaskId}: {Cycle}",
                taskId, string.Join(" -> ", cycleResult.Cycle));
            return new DependencyValidationResult
            {
                IsValid = false,
                ErrorMessage = $"Circular dependency detected: {string.Join(" -> ", cycleResult.Cycle)}",
                ErrorType = ValidationErrorType.CircularDependency,
                InvolvedTaskIds = cycleResult.Cycle
            };
        }

        _logger.LogInformation("Task {TaskId} dependencies validated successfully", taskId);
        return new DependencyValidationResult { IsValid = true };
    }

    private async Task AddTaskAndDependenciesAsync(
        Persistence.Entities.ProjectTask task,
        List<DependencyResolvedTask> resolvedTasks,
        HashSet<string> visited,
        HashSet<string> visitStack,
        CancellationToken ct)
    {
        if (visited.Contains(task.TaskId))
        {
            return;
        }

        if (visitStack.Contains(task.TaskId))
        {
            _logger.LogError("Circular dependency detected for task {TaskId}", task.TaskId);
            throw new InvalidOperationException($"Circular dependency detected for task {task.TaskId}");
        }

        visitStack.Add(task.TaskId);

        // Add dependencies first (they have lower execution order)
        if (!string.IsNullOrWhiteSpace(task.DependenciesJson))
        {
            var dependencyIds = ParseDependencies(task.DependenciesJson);
            foreach (var depId in dependencyIds)
            {
                var depTask = await _taskRepository.GetByIdAsync(depId, ct).ConfigureAwait(false);
                if (depTask != null)
                {
                    await AddTaskAndDependenciesAsync(depTask, resolvedTasks, visited, visitStack, ct)
                        .ConfigureAwait(false);
                }
            }
        }

        visitStack.Remove(task.TaskId);
        visited.Add(task.TaskId);

        // Calculate execution order
        var maxDependencyOrder = -1;
        if (!string.IsNullOrWhiteSpace(task.DependenciesJson))
        {
            var dependencyIds = ParseDependencies(task.DependenciesJson);
            maxDependencyOrder = resolvedTasks
                .Where(t => dependencyIds.Contains(t.TaskId))
                .Select(t => t.ExecutionOrder)
                .DefaultIfEmpty(-1)
                .Max();
        }

        var resolvedTask = new DependencyResolvedTask
        {
            TaskId = task.TaskId,
            Title = task.Title,
            Status = task.Status,
            Priority = task.Priority,
            ExecutionOrder = maxDependencyOrder + 1
        };

        resolvedTasks.Add(resolvedTask);
    }

    private (bool HasCycle, List<string> Cycle) DetectCircularDependency(string taskId, List<string> directDependencies)
    {
        // Simplified cycle detection - does not perform full graph traversal
        // checking only direct dependencies for now
        if (directDependencies.Contains(taskId))
        {
            return (true, new List<string> { taskId, taskId });
        }

        return (false, new List<string>());
    }

    private List<string> ParseDependencies(string? dependenciesJson)
    {
        if (string.IsNullOrWhiteSpace(dependenciesJson))
        {
            return new List<string>();
        }

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var parsed = JsonSerializer.Deserialize<List<string>>(dependenciesJson, options);
            return parsed ?? new List<string>();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning("Failed to parse dependencies JSON: {Error}", ex.Message);
            return new List<string>();
        }
    }
}
