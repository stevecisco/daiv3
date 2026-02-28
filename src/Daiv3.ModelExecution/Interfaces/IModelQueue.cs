using Daiv3.ModelExecution.Models;

namespace Daiv3.ModelExecution.Interfaces;

/// <summary>
/// Manages model execution requests with priority-based scheduling.
/// </summary>
/// <remarks>
/// - P0 (Immediate): Preempts current execution, switches model if needed
/// - P1 (Normal): Batches with current model before switching
/// - P2 (Background): Drains same-model queue before model switch
/// - Only one Foundry Local model loaded at a time
/// </remarks>
public interface IModelQueue
{
    /// <summary>
    /// Enqueues an execution request.
    /// </summary>
    /// <remarks>
    /// - Returns immediately with request ID
    /// - Request is executed asynchronously in background
    /// - Use GetStatusAsync to poll results or ProcessAsync to wait
    /// </remarks>
    /// <param name="request">Execution request</param>
    /// <param name="priority">Execution priority (default: Normal)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Request ID for tracking</returns>
    Task<Guid> EnqueueAsync(
        ExecutionRequest request,
        ExecutionPriority priority = ExecutionPriority.Normal,
        CancellationToken ct = default);

    /// <summary>
    /// Processes a request and waits for completion.
    /// </summary>
    /// <remarks>
    /// - Blocks until request completes or timeout
    /// - Used when immediate result needed
    /// - Check IsCompleted before accessing Result
    /// </remarks>
    /// <param name="requestId">Request ID from EnqueueAsync</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Execution result</returns>
    /// <exception cref="InvalidOperationException">If request not found</exception>
    /// <exception cref="OperationCanceledException">If cancelled</exception>
    Task<ExecutionResult> ProcessAsync(Guid requestId, CancellationToken ct = default);

    /// <summary>
    /// Gets status of an enqueued request.
    /// </summary>
    /// <param name="requestId">Request ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Current status snapshot</returns>
    Task<ExecutionRequestStatus> GetStatusAsync(Guid requestId, CancellationToken ct = default);

    /// <summary>
    /// Gets count of pending requests by priority.
    /// </summary>
    /// <returns>Status summary</returns>
    Task<QueueStatus> GetQueueStatusAsync();

    /// <summary>
    /// Gets observable queue metrics and runtime counters.
    /// </summary>
    /// <returns>Queue metrics snapshot</returns>
    Task<QueueMetrics> GetMetricsAsync();
}
