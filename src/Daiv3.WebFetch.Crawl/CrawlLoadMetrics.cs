using System.Collections.Concurrent;

namespace Daiv3.WebFetch.Crawl;

/// <summary>
/// Default thread-safe implementation for crawl load metrics.
/// </summary>
public class CrawlLoadMetrics : ICrawlLoadMetrics
{
    private readonly object _lock = new();
    private int _totalRequests;
    private int _rateLimitedRequests;
    private long _totalAppliedDelayMs;
    private int _robotsBlockedUrls;
    private int _hostRequestCapSkips;
    private int _requestsPerMinuteThresholdBreaches;

    private readonly ConcurrentDictionary<string, int> _requestsByHost = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, int> _robotsBlockedByHost = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, int> _hostRequestCapSkipsByHost = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, int> _requestsPerMinuteThresholdBreachesByHost = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public void RecordRequest(string hostKey, int appliedDelayMs)
    {
        if (string.IsNullOrWhiteSpace(hostKey))
            throw new ArgumentException("Host key cannot be null or empty.", nameof(hostKey));

        if (appliedDelayMs < 0)
            throw new ArgumentException("Applied delay cannot be negative.", nameof(appliedDelayMs));

        lock (_lock)
        {
            _totalRequests++;
            _totalAppliedDelayMs += appliedDelayMs;

            if (appliedDelayMs > 0)
            {
                _rateLimitedRequests++;
            }

            _requestsByHost.AddOrUpdate(hostKey, 1, (_, count) => count + 1);
        }
    }

    /// <inheritdoc />
    public void RecordRobotsBlocked(string hostKey)
    {
        if (string.IsNullOrWhiteSpace(hostKey))
            throw new ArgumentException("Host key cannot be null or empty.", nameof(hostKey));

        lock (_lock)
        {
            _robotsBlockedUrls++;
            _robotsBlockedByHost.AddOrUpdate(hostKey, 1, (_, count) => count + 1);
        }
    }

    /// <inheritdoc />
    public void RecordHostRequestCapSkip(string hostKey)
    {
        if (string.IsNullOrWhiteSpace(hostKey))
            throw new ArgumentException("Host key cannot be null or empty.", nameof(hostKey));

        lock (_lock)
        {
            _hostRequestCapSkips++;
            _hostRequestCapSkipsByHost.AddOrUpdate(hostKey, 1, (_, count) => count + 1);
        }
    }

    /// <inheritdoc />
    public void RecordRequestsPerMinuteThresholdBreach(string hostKey)
    {
        if (string.IsNullOrWhiteSpace(hostKey))
            throw new ArgumentException("Host key cannot be null or empty.", nameof(hostKey));

        lock (_lock)
        {
            _requestsPerMinuteThresholdBreaches++;
            _requestsPerMinuteThresholdBreachesByHost.AddOrUpdate(hostKey, 1, (_, count) => count + 1);
        }
    }

    /// <inheritdoc />
    public CrawlLoadMetricsSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            var requestsByHost = _requestsByHost.ToDictionary(x => x.Key, x => x.Value);
            var robotsBlockedByHost = _robotsBlockedByHost.ToDictionary(x => x.Key, x => x.Value);
            var hostRequestCapSkipsByHost = _hostRequestCapSkipsByHost.ToDictionary(x => x.Key, x => x.Value);
            var thresholdBreachesByHost = _requestsPerMinuteThresholdBreachesByHost.ToDictionary(x => x.Key, x => x.Value);

            var averageAppliedDelayMs = _totalRequests > 0
                ? (double)_totalAppliedDelayMs / _totalRequests
                : 0;

            return new CrawlLoadMetricsSnapshot(
                TotalRequests: _totalRequests,
                RateLimitedRequests: _rateLimitedRequests,
                TotalAppliedDelayMs: _totalAppliedDelayMs,
                AverageAppliedDelayMs: averageAppliedDelayMs,
                RobotsBlockedUrls: _robotsBlockedUrls,
                HostRequestCapSkips: _hostRequestCapSkips,
                RequestsPerMinuteThresholdBreaches: _requestsPerMinuteThresholdBreaches,
                RequestsByHost: requestsByHost,
                RobotsBlockedByHost: robotsBlockedByHost,
                HostRequestCapSkipsByHost: hostRequestCapSkipsByHost,
                RequestsPerMinuteThresholdBreachesByHost: thresholdBreachesByHost);
        }
    }

    /// <inheritdoc />
    public void Reset()
    {
        lock (_lock)
        {
            _totalRequests = 0;
            _rateLimitedRequests = 0;
            _totalAppliedDelayMs = 0;
            _robotsBlockedUrls = 0;
            _hostRequestCapSkips = 0;
            _requestsPerMinuteThresholdBreaches = 0;
            _requestsByHost.Clear();
            _robotsBlockedByHost.Clear();
            _hostRequestCapSkipsByHost.Clear();
            _requestsPerMinuteThresholdBreachesByHost.Clear();
        }
    }
}
