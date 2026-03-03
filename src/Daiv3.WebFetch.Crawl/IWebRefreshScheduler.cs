namespace Daiv3.WebFetch.Crawl;

/// <summary>
/// Defines the contract for scheduling and managing periodic refetches of previously fetched URLs.
/// Implements WFC-REQ-008: The system SHALL support scheduled refetch intervals.
/// </summary>
public interface IWebRefreshScheduler
{
    /// <summary>
    /// Schedules a URL to be refetched at a specified interval.
    /// </summary>
    /// <param name="sourceUrl">The URL to schedule for refetch.</param>
    /// <param name="intervalSeconds">The interval in seconds between refetch attempts (minimum 60 seconds).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A RefreshScheduleResult indicating success or the reason for failure.
    /// On success, includes the scheduler job ID for the refetch task.
    /// </returns>
    /// <remarks>
    /// - If the URL is already scheduled, updates its interval.
    /// - Minimum interval is 60 seconds (enforced).
    /// - Maximum concurrent refetch jobs is controlled by configuration.
    /// - Refetch jobs use the scheduler (PTS-REQ-007) to execute periodically.
    /// </remarks>
    Task<RefreshScheduleResult> ScheduleRefetchAsync(
        string sourceUrl,
        uint intervalSeconds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a scheduled refetch for a specific URL.
    /// </summary>
    /// <param name="sourceUrl">The URL whose refetch should be cancelled.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the refetch was scheduled and cancelled; false if no refetch was scheduled for this URL.</returns>
    Task<bool> CancelRefetchAsync(
        string sourceUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all URLs that are currently scheduled for refetch.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of refresh schedule metadata for all scheduled URLs.</returns>
    Task<IReadOnlyList<RefreshScheduleMetadata>> GetScheduledRefetchesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets refresh schedule metadata for a specific URL.
    /// </summary>
    /// <param name="sourceUrl">The URL to query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The refresh schedule metadata if scheduled; null if not scheduled.</returns>
    Task<RefreshScheduleMetadata?> GetRefetchMetadataAsync(
        string sourceUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the refetch interval for a URL that is already scheduled.
    /// </summary>
    /// <param name="sourceUrl">The URL whose refetch interval should be updated.</param>
    /// <param name="newIntervalSeconds">The new interval in seconds (minimum 60).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// True if the interval was successfully updated.
    /// False if the URL is not currently scheduled for refetch.
    /// </returns>
    Task<bool> UpdateRefetchIntervalAsync(
        string sourceUrl,
        uint newIntervalSeconds,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the result of attempting to schedule a refetch.
/// </summary>
public record RefreshScheduleResult
{
    /// <summary>
    /// Indicates whether the scheduling operation was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The ID of the scheduler job created for this refetch (if successful).
    /// Used to track and manage the refetch task in the scheduler.
    /// </summary>
    public string? SchedulerJobId { get; init; }

    /// <summary>
    /// An error message if the operation failed; otherwise null.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// The reason the operation failed (if applicable).
    /// Examples: "InvalidInterval", "MaxJobsExceeded", "UrlNotFound", "DuplicateSchedule"
    /// </summary>
    public string? FailureReason { get; init; }

    /// <summary>
    /// Creates a successful RefreshScheduleResult.
    /// </summary>
    public static RefreshScheduleResult CreateSuccess(string schedulerJobId) =>
        new()
        {
            Success = true,
            SchedulerJobId = schedulerJobId,
            ErrorMessage = null,
            FailureReason = null
        };

    /// <summary>
    /// Creates a failed RefreshScheduleResult.
    /// </summary>
    public static RefreshScheduleResult CreateFailure(string reason, string message) =>
        new()
        {
            Success = false,
            SchedulerJobId = null,
            ErrorMessage = message,
            FailureReason = reason
        };
}

/// <summary>
/// Represents metadata about a scheduled refetch for a specific URL.
/// </summary>
public record RefreshScheduleMetadata
{
    /// <summary>
    /// The URL that is scheduled for refetch.
    /// </summary>
    public required string SourceUrl { get; init; }

    /// <summary>
    /// The interval in seconds between refetch attempts.
    /// </summary>
    public required uint IntervalSeconds { get; init; }

    /// <summary>
    /// The ID of the scheduler job managing this refetch.
    /// </summary>
    public required string SchedulerJobId { get; init; }

    /// <summary>
    /// When the refetch schedule was created (Unix timestamp, seconds).
    /// </summary>
    public required long CreatedAtUnixSeconds { get; init; }

    /// <summary>
    /// When the refetch schedule was last updated (Unix timestamp, seconds).
    /// </summary>
    public required long UpdatedAtUnixSeconds { get; init; }

    /// <summary>
    /// When the refetch is next scheduled to run (Unix timestamp, seconds).
    /// </summary>
    public required long NextRefetchUnixSeconds { get; init; }

    /// <summary>
    /// The status of this refetch schedule (Active, Paused, Error).
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Number of times this URL has been successfully refetched.
    /// </summary>
    public required uint SuccessfulRefetchCount { get; init; }

    /// <summary>
    /// Number of times a refetch attempt failed for this URL.
    /// </summary>
    public required uint FailureCount { get; init; }

    /// <summary>
    /// The last error message if a recent refetch failed; otherwise null.
    /// </summary>
    public string? LastErrorMessage { get; init; }
}
