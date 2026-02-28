namespace Daiv3.ModelExecution.Models;

/// <summary>
/// Execution status.
/// </summary>
public enum ExecutionStatus
{
    /// <summary>
    /// Request is pending (e.g., waiting for network connectivity).
    /// </summary>
    Pending,

    /// <summary>
    /// Request is queued for execution.
    /// </summary>
    Queued,

    /// <summary>
    /// Request is currently being processed.
    /// </summary>
    Processing,

    /// <summary>
    /// Request completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Request failed with an error.
    /// </summary>
    Failed,

    /// <summary>
    /// Request was cancelled.
    /// </summary>
    Cancelled
}
