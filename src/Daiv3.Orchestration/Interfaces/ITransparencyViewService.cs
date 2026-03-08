namespace Daiv3.Orchestration.Interfaces;

using Daiv3.Orchestration.Models;

/// <summary>
/// Service interface for exposing system transparency view data.
/// Implements ES-REQ-004: The system SHALL expose a transparency view that shows model usage,
/// indexing status, queue state, and agent activity.
/// </summary>
public interface ITransparencyViewService
{
    /// <summary>
    /// Gets a comprehensive snapshot of the current transparency view data.
    /// Aggregates model usage, indexing status, queue state, and agent activity.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Comprehensive transparency view data.</returns>
    Task<TransparencyViewData> GetTransparencyViewAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets detailed model usage information including historical statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Model usage statistics.</returns>
    Task<ModelUsageStatus> GetModelUsageAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets real-time indexing progress and status.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Indexing status and progress.</returns>
    Task<IndexingStatusExtended> GetIndexingStatusAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets current queue state and task visibility.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Queue state and pending tasks.</returns>
    Task<QueueStateExtended> GetQueueStateAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets active agent execution statistics and status.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Agent activity information.</returns>
    Task<AgentActivityExtended> GetAgentActivityAsync(
        CancellationToken cancellationToken = default);
}
