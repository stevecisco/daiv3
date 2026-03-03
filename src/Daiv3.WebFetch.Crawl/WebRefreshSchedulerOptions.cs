namespace Daiv3.WebFetch.Crawl;

/// <summary>
/// Configuration options for the web refresh scheduler.
/// Implements WFC-REQ-008: The system SHALL support scheduled refetch intervals.
/// </summary>
public class WebRefreshSchedulerOptions
{
    /// <summary>
    /// Whether the refresh scheduler is enabled.
    /// Default: true
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Minimum allowed interval between refetch attempts, in seconds.
    /// Must be at least 60 seconds (1 minute) to avoid overwhelming servers.
    /// Default: 3600 (1 hour)
    /// </summary>
    public uint MinIntervalSeconds { get; set; } = 3600;

    /// <summary>
    /// Default interval for new refetch schedules, in seconds.
    /// Used when scheduling a refetch without specifying an interval.
    /// Default: 86400 (24 hours / 1 day)
    /// </summary>
    public uint DefaultIntervalSeconds { get; set; } = 86400;

    /// <summary>
    /// Maximum number of concurrent refetch jobs.
    /// Prevents excessive resource consumption from too many simultaneous refetches.
    /// Default: 10
    /// </summary>
    public uint MaxConcurrentRefetches { get; set; } = 10;

    /// <summary>
    /// Maximum total number of URLs that can be scheduled for refetch.
    /// Prevents unbounded growth of scheduled refetch jobs.
    /// Default: 1000
    /// </summary>
    public uint MaxScheduledRefetches { get; set; } = 1000;

    /// <summary>
    /// Timeout for individual refetch operations, in milliseconds.
    /// Refetches that take longer than this will be cancelled.
    /// Default: 60000 (60 seconds)
    /// </summary>
    public uint RefetchTimeoutMs { get; set; } = 60000;

    /// <summary>
    /// Whether to automatically re-index content in the knowledge system when refetch detects a change.
    /// Requires WFC-REQ-006 (ingestion pipeline) integration.
    /// Default: true
    /// </summary>
    public bool AutoReindexOnChange { get; set; } = true;

    /// <summary>
    /// Maximum number of consecutive refetch failures before marking a schedule as having an error.
    /// Used to prevent repeated failures from being silently ignored.
    /// Default: 5
    /// </summary>
    public uint MaxConsecutiveFailures { get; set; } = 5;

    /// <summary>
    /// Whether to persist refetch schedules to the database.
    /// If true, schedules are recreated on startup.
    /// If false, schedules are lost on shutdown.
    /// Default: true
    /// </summary>
    public bool PersistSchedulesToDatabase { get; set; } = true;

    /// <summary>
    /// Validates the configuration options.
    /// </summary>
    /// <returns>An error message if invalid; null if valid.</returns>
    public string? Validate()
    {
        if (MinIntervalSeconds < 60)
            return "MinIntervalSeconds must be at least 60 seconds.";

        if (DefaultIntervalSeconds < MinIntervalSeconds)
            return "DefaultIntervalSeconds must be >= MinIntervalSeconds.";

        if (MaxConcurrentRefetches < 1)
            return "MaxConcurrentRefetches must be at least 1.";

        if (MaxScheduledRefetches < 1)
            return "MaxScheduledRefetches must be at least 1.";

        if (RefetchTimeoutMs < 1000)
            return "RefetchTimeoutMs must be at least 1000ms.";

        if (MaxConsecutiveFailures < 1)
            return "MaxConsecutiveFailures must be at least 1.";

        return null;
    }
}
