using System.Collections.Concurrent;

namespace Daiv3.WebFetch.Crawl;

/// <summary>
/// Default implementation of cancellation metrics tracking.
/// Thread-safe recording and reporting of fetch operation cancellations.
/// </summary>
public class CancellationMetrics : ICancellationMetrics
{
    private int _totalCancellations;
    private int _successfulCancellations;
    private int _userRequestedCancellations;
    private int _timeoutCancellations;
    private int _resourceExhaustedCancellations;
    private readonly List<long> _cancellationLatencies = new();
    private readonly ConcurrentDictionary<string, int> _cancellationsByOperationType = new();
    private readonly object _lock = new();

    /// <summary>
    /// Records a cancellation request for a fetch operation.
    /// </summary>
    public void RecordCancellation(string operationType, string url, string cancellationReason, long elapsedMilliseconds)
    {
        if (string.IsNullOrWhiteSpace(operationType))
            throw new ArgumentException("Operation type cannot be null or empty.", nameof(operationType));

        if (elapsedMilliseconds < 0)
            throw new ArgumentException("Elapsed milliseconds cannot be negative.", nameof(elapsedMilliseconds));

        lock (_lock)
        {
            _totalCancellations++;
            _successfulCancellations++; // Assume successful if recorded
            _cancellationLatencies.Add(elapsedMilliseconds);

            // Track by reason
            switch (cancellationReason)
            {
                case "UserRequested":
                    _userRequestedCancellations++;
                    break;
                case "Timeout":
                    _timeoutCancellations++;
                    break;
                case "ResourceExhausted":
                    _resourceExhaustedCancellations++;
                    break;
            }

            // Track by operation type
            _cancellationsByOperationType.AddOrUpdate(operationType, 1, (_, count) => count + 1);
        }
    }

    /// <summary>
    /// Gets the current cancellation metrics snapshot.
    /// </summary>
    public CancellationMetricsSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            var byOperation = _cancellationsByOperationType.ToDictionary(x => x.Key, x => x.Value);

            double avgLatency = _cancellationLatencies.Count > 0 
                ? _cancellationLatencies.Average() 
                : 0;

            return new CancellationMetricsSnapshot(
                TotalCancellations: _totalCancellations,
                SuccessfulCancellations: _successfulCancellations,
                UserRequestedCancellations: _userRequestedCancellations,
                TimeoutCancellations: _timeoutCancellations,
                ResourceExhaustedCancellations: _resourceExhaustedCancellations,
                AverageCancellationLatencyMs: avgLatency,
                FastestCancellationMs: _cancellationLatencies.Count > 0 ? _cancellationLatencies.Min() : null,
                SlowestCancellationMs: _cancellationLatencies.Count > 0 ? _cancellationLatencies.Max() : null,
                CancellationsByOperationType: byOperation
            );
        }
    }

    /// <summary>
    /// Resets all recorded metrics (used for testing).
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _totalCancellations = 0;
            _successfulCancellations = 0;
            _userRequestedCancellations = 0;
            _timeoutCancellations = 0;
            _resourceExhaustedCancellations = 0;
            _cancellationLatencies.Clear();
            _cancellationsByOperationType.Clear();
        }
    }
}
