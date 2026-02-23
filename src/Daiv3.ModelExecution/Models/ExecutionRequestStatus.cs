namespace Daiv3.ModelExecution.Models;

/// <summary>
/// Request status snapshot.
/// </summary>
public class ExecutionRequestStatus
{
    public Guid RequestId { get; set; }
    public ExecutionStatus Status { get; set; }
    public int QueuePosition { get; set; }
    public ExecutionResult? Result { get; set; }
}
