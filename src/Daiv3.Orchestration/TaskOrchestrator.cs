using Daiv3.Orchestration.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Daiv3.Orchestration;

/// <summary>
/// Coordinates system-wide operations and orchestrates complex multi-step tasks.
/// </summary>
public class TaskOrchestrator : ITaskOrchestrator
{
    private readonly IIntentResolver _intentResolver;
    private readonly IDependencyResolver _dependencyResolver;
    private readonly ILogger<TaskOrchestrator> _logger;
    private readonly OrchestrationOptions _options;

    public TaskOrchestrator(
        IIntentResolver intentResolver,
        IDependencyResolver dependencyResolver,
        ILogger<TaskOrchestrator> logger,
        IOptions<OrchestrationOptions> options)
    {
        _intentResolver = intentResolver ?? throw new ArgumentNullException(nameof(intentResolver));
        _dependencyResolver = dependencyResolver ?? throw new ArgumentNullException(nameof(dependencyResolver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async Task<OrchestrationResult> ExecuteAsync(UserRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Input);

        var sessionId = Guid.NewGuid();
        _logger.LogInformation(
            "Starting orchestration for session {SessionId}, ProjectId: {ProjectId}",
            sessionId, request.ProjectId);

        try
        {
            // Resolve intent
            var intent = await _intentResolver.ResolveAsync(request.Input, request.Context, ct);
            
            if (intent.Confidence < _options.MinimumIntentConfidence)
            {
                _logger.LogWarning(
                    "Intent confidence {Confidence} below threshold {Threshold}",
                    intent.Confidence, _options.MinimumIntentConfidence);
                
                return new OrchestrationResult
                {
                    SessionId = sessionId,
                    Success = false,
                    ErrorMessage = $"Unable to understand request with sufficient confidence (confidence: {intent.Confidence:P0})"
                };
            }

            _logger.LogInformation(
                "Resolved intent: {IntentType} with confidence {Confidence:P2}",
                intent.Type, intent.Confidence);

            // Decompose into tasks
            var resolvedTasks = await ResolveIntentAsync(request.Input, ct);

            // Validate dependencies if enabled
            if (_options.EnableTaskDependencyValidation && !ValidateTaskDependencies(resolvedTasks))
            {
                _logger.LogError("Task dependency validation failed");
                return new OrchestrationResult
                {
                    SessionId = sessionId,
                    Success = false,
                    ErrorMessage = "Task dependencies are invalid or contain cycles"
                };
            }

            // Execute tasks in dependency order
            var results = await ExecuteTasksAsync(resolvedTasks, ct);

            _logger.LogInformation(
                "Orchestration completed for session {SessionId}. Tasks executed: {Count}",
                sessionId, results.Count);

            return new OrchestrationResult
            {
                SessionId = sessionId,
                TaskResults = results,
                Success = results.All(r => r.Success),
                ErrorMessage = results.All(r => r.Success) 
                    ? null 
                    : "One or more tasks failed"
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Orchestration cancelled for session {SessionId}", sessionId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Orchestration failed for session {SessionId}", sessionId);
            return new OrchestrationResult
            {
                SessionId = sessionId,
                Success = false,
                ErrorMessage = $"Orchestration failed: {ex.Message}"
            };
        }
    }

    /// <inheritdoc />
    public async Task<List<ResolvedTask>> ResolveIntentAsync(string userInput, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userInput);

        _logger.LogInformation("Resolving intent for input: {Input}", userInput);

        // Resolve intent
        var intent = await _intentResolver.ResolveAsync(userInput, new Dictionary<string, string>(), ct);

        // For now, create a single task based on the intent
        // TODO: Implement more sophisticated task decomposition based on intent complexity
        var task = new ResolvedTask
        {
            TaskType = intent.Type,
            Parameters = intent.Entities,
            ExecutionOrder = 0,
            Dependencies = new List<Guid>()
        };

        _logger.LogInformation(
            "Resolved to single task of type: {TaskType}",
            task.TaskType);

        return new List<ResolvedTask> { task };
    }

    private bool ValidateTaskDependencies(List<ResolvedTask> tasks)
    {
        if (tasks.Count == 0)
            return true;

        // Check for duplicate execution orders
        var orderGroups = tasks.GroupBy(t => t.ExecutionOrder);
        if (orderGroups.Any(g => g.Count() > 1))
        {
            _logger.LogWarning("Duplicate execution orders detected");
        }

        // For now, accept tasks with duplicate orders (parallel execution)
        // TODO: Implement cycle detection for task dependencies
        return true;
    }

    private async Task<List<TaskExecutionResult>> ExecuteTasksAsync(
        List<ResolvedTask> tasks, 
        CancellationToken ct)
    {
        var results = new List<TaskExecutionResult>();
        var taskGroups = tasks.GroupBy(t => t.ExecutionOrder).OrderBy(g => g.Key);

        foreach (var group in taskGroups)
        {
            var groupTasks = group.ToList();
            _logger.LogInformation(
                "Executing {Count} task(s) at order {Order}",
                groupTasks.Count, group.Key);

            // Execute tasks in the same order group concurrently
            var groupResults = await Task.WhenAll(
                groupTasks.Select(task => ExecuteTaskAsync(task, ct)));
            
            results.AddRange(groupResults);

            // Stop if any task in the group failed
            if (groupResults.Any(r => !r.Success))
            {
                _logger.LogWarning(
                    "Task execution stopped at order {Order} due to failures",
                    group.Key);
                break;
            }
        }

        return results;
    }

    private Task<TaskExecutionResult> ExecuteTaskAsync(ResolvedTask task, CancellationToken ct)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.TaskTimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        try
        {
            _logger.LogInformation(
                "Executing task type: {TaskType}",
                task.TaskType);

            // TODO: Integrate with Model Execution Layer to actually execute tasks
            // For now, return a placeholder result
            var result = new TaskExecutionResult
            {
                TaskId = Guid.NewGuid(),
                TaskType = task.TaskType,
                Content = $"Task {task.TaskType} executed successfully (placeholder)",
                Success = true,
                CompletedAt = DateTimeOffset.UtcNow
            };

            _logger.LogInformation(
                "Task {TaskId} completed successfully",
                result.TaskId);

            return Task.FromResult(result);
        }
        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
        {
            _logger.LogError(
                "Task {TaskType} timed out after {Timeout} seconds",
                task.TaskType, _options.TaskTimeoutSeconds);

            return Task.FromResult(new TaskExecutionResult
            {
                TaskId = Guid.NewGuid(),
                TaskType = task.TaskType,
                Success = false,
                ErrorMessage = $"Task timed out after {_options.TaskTimeoutSeconds} seconds",
                CompletedAt = DateTimeOffset.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Task {TaskType} failed", task.TaskType);

            return Task.FromResult(new TaskExecutionResult
            {
                TaskId = Guid.NewGuid(),
                TaskType = task.TaskType,
                Success = false,
                ErrorMessage = ex.Message,
                CompletedAt = DateTimeOffset.UtcNow
            });
        }
    }

    /// <inheritdoc />
    public async Task<bool> CanEnqueueTaskAsync(string taskId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);

        _logger.LogInformation("Checking if task {TaskId} can be enqueued", taskId);

        try
        {
            var dependenciesSatisfied = await _dependencyResolver.AreDependenciesSatisfiedAsync(taskId, ct);

            if (!dependenciesSatisfied)
            {
                _logger.LogWarning(
                    "Task {TaskId} cannot be enqueued because dependencies are not satisfied",
                    taskId);
                return false;
            }

            _logger.LogInformation(
                "Task {TaskId} dependencies satisfied - ready for enqueueing",
                taskId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error checking dependencies for task {TaskId}",
                taskId);
            return false;
        }
    }
}
