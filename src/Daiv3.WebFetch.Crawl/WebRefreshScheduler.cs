using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Daiv3.Scheduler;

namespace Daiv3.WebFetch.Crawl;

/// <summary>
/// Implementation of IWebRefreshScheduler that manages scheduled refetches of previously fetched URLs.
/// Implements WFC-REQ-008: The system SHALL support scheduled refetch intervals.
/// </summary>
public class WebRefreshScheduler : IWebRefreshScheduler
{
    private readonly IScheduler _scheduler;
    private readonly IWebFetcher _webFetcher;
    private readonly IMarkdownContentStore _contentStore;
    private readonly ILogger<WebRefreshScheduler> _logger;
    private readonly WebRefreshSchedulerOptions _options;

    // In-memory tracking of scheduled refetches
    // Key: Source URL (normalized)
    // Value: Refresh schedule metadata
    private readonly ConcurrentDictionary<string, RefreshScheduleMetadata> _scheduledRefetches =
        new(StringComparer.OrdinalIgnoreCase);

    // Maps source URL to scheduler job ID for easy lookup
    private readonly ConcurrentDictionary<string, string> _urlToJobIdMap =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new WebRefreshScheduler.
    /// </summary>
    public WebRefreshScheduler(
        IScheduler scheduler,
        IWebFetcher webFetcher,
        IMarkdownContentStore contentStore,
        ILogger<WebRefreshScheduler> logger,
        WebRefreshSchedulerOptions options)
    {
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _webFetcher = webFetcher ?? throw new ArgumentNullException(nameof(webFetcher));
        _contentStore = contentStore ?? throw new ArgumentNullException(nameof(contentStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        var validationError = _options.Validate();
        if (!string.IsNullOrEmpty(validationError))
            throw new ArgumentException($"Invalid WebRefreshSchedulerOptions: {validationError}");

        if (!_options.Enabled)
            _logger.LogInformation("WebRefreshScheduler is disabled via configuration");
    }

    /// <summary>
    /// Schedules a URL to be refetched at the specified interval.
    /// </summary>
    public async Task<RefreshScheduleResult> ScheduleRefetchAsync(
        string sourceUrl,
        uint intervalSeconds,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return RefreshScheduleResult.CreateFailure(
                "SchedulerDisabled",
                "The web refresh scheduler is disabled.");
        }

        if (string.IsNullOrWhiteSpace(sourceUrl))
        {
            return RefreshScheduleResult.CreateFailure(
                "InvalidUrl",
                "Source URL cannot be null or empty.");
        }

        if (intervalSeconds < _options.MinIntervalSeconds)
        {
            return RefreshScheduleResult.CreateFailure(
                "InvalidInterval",
                $"Interval must be at least {_options.MinIntervalSeconds} seconds (minimum is {_options.MinIntervalSeconds}s).");
        }

        // Normalize the URL
        var normalizedUrl = sourceUrl.Trim();

        // Check if already scheduled - if so, update the interval
        if (_scheduledRefetches.TryGetValue(normalizedUrl, out var existingMetadata))
        {
            _logger.LogInformation(
                "URL {SourceUrl} is already scheduled; updating interval from {OldInterval}s to {NewInterval}s",
                normalizedUrl,
                existingMetadata.IntervalSeconds,
                intervalSeconds);

            var updated = await UpdateRefetchIntervalAsync(normalizedUrl, intervalSeconds, cancellationToken);
            if (updated)
            {
                return RefreshScheduleResult.CreateSuccess(_urlToJobIdMap[normalizedUrl]);
            }

            return RefreshScheduleResult.CreateFailure(
                "UpdateFailed",
                "Failed to update existing refetch schedule.");
        }

        // Check max concurrent refetches
        if (_scheduledRefetches.Count >= _options.MaxScheduledRefetches)
        {
            return RefreshScheduleResult.CreateFailure(
                "MaxJobsExceeded",
                $"Maximum scheduled refetches ({_options.MaxScheduledRefetches}) has been reached.");
        }

        try
        {
            // Create the scheduled job
            var job = new RefreshScheduledJob(
                normalizedUrl,
                _webFetcher,
                _contentStore,
                _logger,
                _options);

            // Schedule the job with the scheduler (PTS-REQ-007)
            var schedulerJobId = await _scheduler.ScheduleRecurringAsync(
                job,
                intervalSeconds,
                delaySeconds: 0, // Start immediately
                cancellationToken: cancellationToken);

            // Track the scheduled refetch
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var metadata = new RefreshScheduleMetadata
            {
                SourceUrl = normalizedUrl,
                IntervalSeconds = intervalSeconds,
                SchedulerJobId = schedulerJobId,
                CreatedAtUnixSeconds = now,
                UpdatedAtUnixSeconds = now,
                NextRefetchUnixSeconds = now + (long)intervalSeconds,
                Status = "Active",
                SuccessfulRefetchCount = 0,
                FailureCount = 0,
                LastErrorMessage = null
            };

            _scheduledRefetches[normalizedUrl] = metadata;
            _urlToJobIdMap[normalizedUrl] = schedulerJobId;

            _logger.LogInformation(
                "Scheduled refetch for URL {SourceUrl} with interval {IntervalSeconds}s (Job ID: {JobId})",
                normalizedUrl,
                intervalSeconds,
                schedulerJobId);

            return RefreshScheduleResult.CreateSuccess(schedulerJobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to schedule refetch for URL {SourceUrl}",
                normalizedUrl);

            return RefreshScheduleResult.CreateFailure(
                "SchedulingError",
                $"Failed to schedule refetch: {ex.Message}");
        }
    }

    /// <summary>
    /// Cancels a scheduled refetch for a specific URL.
    /// </summary>
    public async Task<bool> CancelRefetchAsync(
        string sourceUrl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceUrl))
            return false;

        var normalizedUrl = sourceUrl.Trim();

        if (!_scheduledRefetches.TryRemove(normalizedUrl, out var metadata))
            return false;

        // Also remove from the mapping
        _urlToJobIdMap.TryRemove(normalizedUrl, out _);

        try
        {
            // Cancel the scheduler job
            await _scheduler.CancelJobAsync(metadata.SchedulerJobId, cancellationToken);

            _logger.LogInformation(
                "Cancelled scheduled refetch for URL {SourceUrl} (Job ID: {JobId})",
                normalizedUrl,
                metadata.SchedulerJobId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error cancelling scheduler job {JobId} for URL {SourceUrl}",
                metadata.SchedulerJobId,
                normalizedUrl);

            // Even if cancelling the scheduler job failed, we've already removed it from tracking
            return true;
        }
    }

    /// <summary>
    /// Gets all URLs that are currently scheduled for refetch.
    /// </summary>
    public Task<IReadOnlyList<RefreshScheduleMetadata>> GetScheduledRefetchesAsync(
        CancellationToken cancellationToken = default)
    {
        var result = _scheduledRefetches.Values.ToList().AsReadOnly();
        return Task.FromResult((IReadOnlyList<RefreshScheduleMetadata>)result);
    }

    /// <summary>
    /// Gets metadata for a specific scheduled refetch.
    /// </summary>
    public Task<RefreshScheduleMetadata?> GetRefetchMetadataAsync(
        string sourceUrl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceUrl))
            return Task.FromResult((RefreshScheduleMetadata?)null);

        var normalizedUrl = sourceUrl.Trim();
        var found = _scheduledRefetches.TryGetValue(normalizedUrl, out var metadata);

        return Task.FromResult(found ? metadata : null);
    }

    /// <summary>
    /// Updates the refetch interval for a URL that is already scheduled.
    /// </summary>
    public async Task<bool> UpdateRefetchIntervalAsync(
        string sourceUrl,
        uint newIntervalSeconds,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceUrl))
            return false;

        if (newIntervalSeconds < _options.MinIntervalSeconds)
        {
            _logger.LogWarning(
                "Invalid interval {IntervalSeconds}s for URL {SourceUrl}; minimum is {MinInterval}s",
                newIntervalSeconds,
                sourceUrl,
                _options.MinIntervalSeconds);
            return false;
        }

        var normalizedUrl = sourceUrl.Trim();

        if (!_scheduledRefetches.TryGetValue(normalizedUrl, out var existingMetadata))
            return false;

        try
        {
            // Update the scheduler job schedule
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var modificationRequest = new ScheduleModificationRequest
            {
                IntervalSeconds = newIntervalSeconds
            };

            var updated = await _scheduler.ModifyJobScheduleAsync(
                existingMetadata.SchedulerJobId,
                modificationRequest,
                cancellationToken);

            if (!updated)
            {
                _logger.LogWarning(
                    "Failed to modify scheduler job {JobId} for URL {SourceUrl}",
                    existingMetadata.SchedulerJobId,
                    normalizedUrl);
                return false;
            }

            // Update metadata
            var updatedMetadata = existingMetadata with
            {
                IntervalSeconds = newIntervalSeconds,
                UpdatedAtUnixSeconds = now,
                NextRefetchUnixSeconds = now + (long)newIntervalSeconds
            };

            _scheduledRefetches[normalizedUrl] = updatedMetadata;

            _logger.LogInformation(
                "Updated refetch interval for URL {SourceUrl} to {IntervalSeconds}s",
                normalizedUrl,
                newIntervalSeconds);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error updating refetch interval for URL {SourceUrl}",
                normalizedUrl);
            return false;
        }
    }

    /// <summary>
    /// Null logger implementation for cases where logger factory is not available.
    /// </summary>
    internal class NullLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}