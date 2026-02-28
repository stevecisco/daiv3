namespace Daiv3.Orchestration.Interfaces;

/// <summary>
/// Resolves task dependencies and determines execution order before enqueueing model requests.
/// </summary>
public interface IDependencyResolver
{
    /// <summary>
    /// Resolves dependencies for a task and returns execution order.
    /// </summary>
    /// <remarks>
    /// - Validates all dependencies exist in the database
    /// - Checks for circular dependencies
    /// - Returns tasks in execution order: dependencies before dependents
    /// - Only includes tasks in non-complete status
    /// </remarks>
    /// <param name="taskId">The task ID to resolve dependencies for</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Ordered list of tasks that must execute before the specified task</returns>
    /// <exception cref="ArgumentException">If task not found or circular dependency detected</exception>
    Task<IReadOnlyList<DependencyResolvedTask>> ResolveDependenciesAsync(
        string taskId,
        CancellationToken ct = default);

    /// <summary>
    /// Checks if a task's dependencies are satisfied (all completed).
    /// </summary>
    /// <param name="taskId">The task ID to check</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if all dependencies are complete or task has no dependencies</returns>
    Task<bool> AreDependenciesSatisfiedAsync(string taskId, CancellationToken ct = default);

    /// <summary>
    /// Validates task dependencies without resolving execution order.
    /// </summary>
    /// <remarks>
    /// - Checks for circular dependencies
    /// - Validates all referenced tasks exist
    /// - Does not require tasks to be complete
    /// </remarks>
    /// <param name="taskId">The task ID to validate</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Validation result with error message if validation fails</returns>
    Task<DependencyValidationResult> ValidateDependenciesAsync(
        string taskId,
        CancellationToken ct = default);
}

/// <summary>
/// Represents a resolved task with dependency information.
/// </summary>
public class DependencyResolvedTask
{
    /// <summary>
    /// The task ID.
    /// </summary>
    public required string TaskId { get; set; }

    /// <summary>
    /// The task title.
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// The task status.
    /// </summary>
    public required string Status { get; set; }

    /// <summary>
    /// The task priority.
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Execution order (0 = dependency, higher = later in chain).
    /// </summary>
    public int ExecutionOrder { get; set; }

    /// <summary>
    /// Task IDs that depend on this task.
    /// </summary>
    public List<string> DependentTasks { get; set; } = new();
}

/// <summary>
/// Result of dependency validation.
/// </summary>
public class DependencyValidationResult
{
    /// <summary>
    /// Whether validation succeeded.
    /// </summary>
    public bool IsValid { get; set; } = true;

    /// <summary>
    /// Error message if validation failed (null if valid).
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Type of validation error if any.
    /// </summary>
    public ValidationErrorType ErrorType { get; set; }

    /// <summary>
    /// Task IDs involved in the error (e.g., circular dependency chain).
    /// </summary>
    public List<string> InvolvedTaskIds { get; set; } = new();
}

/// <summary>
/// Enumeration of dependency validation error types.
/// </summary>
public enum ValidationErrorType
{
    /// <summary>
    /// No error.
    /// </summary>
    None = 0,

    /// <summary>
    /// Task not found in database.
    /// </summary>
    TaskNotFound = 1,

    /// <summary>
    /// Referenced dependency task does not exist.
    /// </summary>
    MissingDependency = 2,

    /// <summary>
    /// Circular dependency detected.
    /// </summary>
    CircularDependency = 3,

    /// <summary>
    /// Dependency is in failed or blocked status.
    /// </summary>
    DependencyFailed = 4,
}
