namespace Daiv3.Orchestration.Interfaces;

/// <summary>
/// Coordinates system-wide operations and orchestrates complex multi-step tasks.
/// </summary>
public interface ITaskOrchestrator
{
    /// <summary>
    /// Executes a user request by resolving intent, decomposing into tasks, and orchestrating execution.
    /// </summary>
    /// <param name="request">The user request to execute.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The orchestration result containing all task results.</returns>
    Task<OrchestrationResult> ExecuteAsync(UserRequest request, CancellationToken ct = default);
    
    /// <summary>
    /// Resolves user input into a list of tasks with dependencies.
    /// </summary>
    /// <param name="userInput">The user's input text.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of resolved tasks with dependencies.</returns>
    Task<List<ResolvedTask>> ResolveIntentAsync(string userInput, CancellationToken ct = default);

    /// <summary>
    /// Checks if task dependencies are satisfied before enqueueing model requests.
    /// </summary>
    /// <remarks>
    /// Used to validate that all task dependencies are complete before execution.
    /// </remarks>
    /// <param name="taskId">The task ID to check dependencies for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if all dependencies are satisfied, false otherwise.</returns>
    Task<bool> CanEnqueueTaskAsync(string taskId, CancellationToken ct = default);
}

/// <summary>
/// Represents a user request to the orchestration layer.
/// </summary>
public class UserRequest
{
    /// <summary>
    /// The user's input text.
    /// </summary>
    public required string Input { get; set; }
    
    /// <summary>
    /// The project context for the request.
    /// </summary>
    public Guid ProjectId { get; set; }
    
    /// <summary>
    /// Additional context parameters.
    /// </summary>
    public Dictionary<string, string> Context { get; set; } = new();
}

/// <summary>
/// Represents a resolved task with dependencies.
/// </summary>
public class ResolvedTask
{
    /// <summary>
    /// The type of task (e.g., "chat", "search", "analyze").
    /// </summary>
    public required string TaskType { get; set; }
    
    /// <summary>
    /// Task-specific parameters.
    /// </summary>
    public Dictionary<string, string> Parameters { get; set; } = new();
    
    /// <summary>
    /// Execution order (0-based).
    /// </summary>
    public int ExecutionOrder { get; set; }
    
    /// <summary>
    /// Task IDs this task depends on.
    /// </summary>
    public List<Guid> Dependencies { get; set; } = new();
}

/// <summary>
/// Result of orchestrating a user request.
/// </summary>
public class OrchestrationResult
{
    /// <summary>
    /// The session ID for this orchestration.
    /// </summary>
    public Guid SessionId { get; set; }
    
    /// <summary>
    /// Results from all executed tasks.
    /// </summary>
    public List<TaskExecutionResult> TaskResults { get; set; } = new();
    
    /// <summary>
    /// Whether the orchestration succeeded overall.
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Error message if the orchestration failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Result of a single task execution.
/// </summary>
public class TaskExecutionResult
{
    /// <summary>
    /// Task ID.
    /// </summary>
    public Guid TaskId { get; set; }
    
    /// <summary>
    /// Task type.
    /// </summary>
    public required string TaskType { get; set; }
    
    /// <summary>
    /// Result content.
    /// </summary>
    public string? Content { get; set; }
    
    /// <summary>
    /// Whether the task succeeded.
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Error message if task failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// When the task completed.
    /// </summary>
    public DateTimeOffset CompletedAt { get; set; }
}
