namespace Daiv3.WebFetch.Crawl;

/// <summary>
/// Tracks and reports metrics for fetch operation cancellations.
/// Provides observability into cancellation frequency, latency, and reasons.
/// </summary>
public interface ICancellationMetrics
{
    /// <summary>
    /// Records a cancellation request for a fetch operation.
    /// </summary>
    /// <param name="operationType">Type of operation being cancelled (e.g., "Fetch", "Crawl", "Parse").</param>
    /// <param name="url">The URL being fetched when cancelled.</param>
    /// <param name="cancellationReason">Reason for cancellation (e.g., "UserRequested", "Timeout", "ResourceExhausted").</param>
    /// <param name="elapsedMilliseconds">Time elapsed before cancellation.</param>
    void RecordCancellation(string operationType, string url, string cancellationReason, long elapsedMilliseconds);

    /// <summary>
    /// Gets the current cancellation metrics snapshot.
    /// </summary>
    CancellationMetricsSnapshot GetSnapshot();

    /// <summary>
    /// Resets all recorded metrics (used for testing).
    /// </summary>
    void Reset();
}

/// <summary>
/// Snapshot of current cancellation metrics at a point in time.
/// </summary>
public record CancellationMetricsSnapshot(
    /// <summary>Total number of cancellation requests recorded.</summary>
    int TotalCancellations,
    /// <summary>Number of successful cancellations (operation stopped).</summary>
    int SuccessfulCancellations,
    /// <summary>Number of cancellations due to user request.</summary>
    int UserRequestedCancellations,
    /// <summary>Number of cancellations due to timeout.</summary>
    int TimeoutCancellations,
    /// <summary>Number of cancellations due to resource exhaustion.</summary>
    int ResourceExhaustedCancellations,
    /// <summary>Average time (milliseconds) from start to cancellation.</summary>
    double AverageCancellationLatencyMs,
    /// <summary>Fastest cancellation (milliseconds).</summary>
    long? FastestCancellationMs,
    /// <summary>Slowest cancellation (milliseconds).</summary>
    long? SlowestCancellationMs,
    /// <summary>Breakdown of cancellations by operation type.</summary>
    IReadOnlyDictionary<string, int> CancellationsByOperationType
);
