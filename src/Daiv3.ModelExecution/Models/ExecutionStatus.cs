namespace Daiv3.ModelExecution.Models;

/// <summary>
/// Execution status.
/// </summary>
public enum ExecutionStatus
{
    Queued,
    Processing,
    Completed,
    Failed,
    Cancelled
}
