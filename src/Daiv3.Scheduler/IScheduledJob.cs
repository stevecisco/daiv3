namespace Daiv3.Scheduler;

/// <summary>
/// Defines the contract for a background job that can be scheduled and executed by the scheduler.
/// 
/// Implementations of this interface represent units of work that will be executed
/// asynchronously by the scheduler at specified times or intervals.
/// </summary>
public interface IScheduledJob
{
    /// <summary>
    /// Gets the name of the job. Used for logging and identification.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets optional metadata about the job (e.g., description, tags, priority).
    /// </summary>
    /// <remarks>
    /// This can be used to store arbitrary application-specific information
    /// about the job without modifying the interface.
    /// </remarks>
    IDictionary<string, object>? Metadata { get; }

    /// <summary>
    /// Executes the job asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A token that can be used to cancel the job execution.</param>
    /// <returns>A task representing the asynchronous job execution.</returns>
    /// <remarks>
    /// Implementations MUST:
    /// - Handle cancellation requests and throw OperationCanceledException if cancelled
    /// - Not throw unhandled exceptions (catch and log internally, or let errors propagate for retry logic)
    /// - Complete in a reasonable timeframe to avoid blocking scheduling system
    /// 
    /// The scheduler will wrap execution with timeout and error handling.
    /// </remarks>
    Task ExecuteAsync(CancellationToken cancellationToken = default);
}
