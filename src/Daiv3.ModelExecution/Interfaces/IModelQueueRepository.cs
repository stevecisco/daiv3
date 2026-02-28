using Daiv3.ModelExecution.Models;

namespace Daiv3.ModelExecution.Interfaces;

/// <summary>
/// Repository for persisting model queue state.
/// </summary>
/// <remarks>
/// Implements MQ-REQ-013: Persists online tasks when offline for later retry.
/// </remarks>
public interface IModelQueueRepository
{
    /// <summary>
    /// Saves a pending execution request to persistent storage.
    /// </summary>
    /// <param name="request">The execution request</param>
    /// <param name="priority">Request priority</param>
    /// <param name="modelId">Target model identifier</param>
    /// <param name="ct">Cancellation token</param>
    Task SavePendingRequestAsync(
        ExecutionRequest request,
        ExecutionPriority priority,
        string modelId,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves all pending requests that are waiting for retry.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of pending requests with their priorities and model IDs</returns>
    Task<List<(ExecutionRequest Request, ExecutionPriority Priority, string ModelId)>> GetPendingRequestsAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Updates the status of a queued request.
    /// </summary>
    /// <param name="requestId">Request ID</param>
    /// <param name="status">New status</param>
    /// <param name="errorMessage">Error message if failed</param>
    /// <param name="ct">Cancellation token</param>
    Task UpdateRequestStatusAsync(
        Guid requestId,
        ExecutionStatus status,
        string? errorMessage = null,
        CancellationToken ct = default);

    /// <summary>
    /// Removes a request from the persistent queue.
    /// </summary>
    /// <param name="requestId">Request ID</param>
    /// <param name="ct">Cancellation token</param>
    Task DeleteRequestAsync(Guid requestId, CancellationToken ct = default);

    /// <summary>
    /// Gets the count of pending requests.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Number of pending requests</returns>
    Task<int> GetPendingCountAsync(CancellationToken ct = default);
}
