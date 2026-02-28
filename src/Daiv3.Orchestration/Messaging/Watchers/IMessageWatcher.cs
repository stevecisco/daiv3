namespace Daiv3.Orchestration.Messaging.Watchers;

/// <summary>
/// Interface for message watchers that detect and deliver new messages to subscribers.
/// Different implementations support different storage backends (FileSystem, Azure Blob, etc.).
/// </summary>
public interface IMessageWatcher : IAsyncDisposable
{
    /// <summary>
    /// Starts watching for new messages on the specified topic.
    /// Calls the handler function when new messages are detected.
    /// </summary>
    /// <param name="topic">The topic pattern to watch (supports wildcards like "task-*").</param>
    /// <param name="handler">Async handler to invoke when messages are detected.</param>
    /// <param name="ct">Cancellation token for stopping the watch.</param>
    /// <returns>A task that completes when the watch is started (runs in background).</returns>
    Task StartWatchingAsync(string topic, Func<IAgentMessage, Task> handler, CancellationToken ct);

    /// <summary>
    /// Stops watching for messages on a specific topic.
    /// </summary>
    /// <param name="topic">The topic to stop watching.</param>
    /// <returns>A task representing the stop operation.</returns>
    Task StopWatchingAsync(string topic);

    /// <summary>
    /// Gets the health status of the watcher.
    /// </summary>
    /// <returns>A tuple indicating health (isHealthy, diagnosticMessage).</returns>
    Task<(bool IsHealthy, string DiagnosticMessage)> GetHealthAsync();

    /// <summary>
    /// Gets the number of active watches.
    /// </summary>
    int ActiveWatchCount { get; }
}
