using Microsoft.Extensions.Logging;
using Daiv3.Scheduler;

namespace Daiv3.WebFetch.Crawl;

/// <summary>
/// A scheduled job that refetches a URL and updates content if it has changed.
/// Implements the core refetch logic for WFC-REQ-008.
/// </summary>
internal class RefreshScheduledJob : IScheduledJob
{
    private readonly string _sourceUrl;
    private readonly IWebFetcher _webFetcher;
    private readonly IMarkdownContentStore _contentStore;
    private readonly ILogger _logger;
    private readonly WebRefreshSchedulerOptions _options;

    /// <summary>
    /// The name of the job, used for logging and identification.
    /// </summary>
    public string Name => $"RefreshScheduledJob-{_sourceUrl}";

    /// <summary>
    /// Optional metadata about this job.
    /// Stores tracking information used by the refresh scheduler.
    /// </summary>
    public IDictionary<string, object>? Metadata { get; }

    /// <summary>
    /// Initializes a new RefreshScheduledJob.
    /// </summary>
    public RefreshScheduledJob(
        string sourceUrl,
        IWebFetcher webFetcher,
        IMarkdownContentStore contentStore,
        ILogger logger,
        WebRefreshSchedulerOptions options)
    {
        _sourceUrl = sourceUrl ?? throw new ArgumentNullException(nameof(sourceUrl));
        _webFetcher = webFetcher ?? throw new ArgumentNullException(nameof(webFetcher));
        _contentStore = contentStore ?? throw new ArgumentNullException(nameof(contentStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        Metadata = new Dictionary<string, object>
        {
            { "SourceUrl", sourceUrl },
            { "JobType", "RefreshScheduledJob" },
            { "CreatedAt", DateTime.UtcNow }
        };
    }

    /// <summary>
    /// Executes the refetch operation asynchronously.
    /// </summary>
    /// <remarks>
    /// The refetch logic:
    /// 1. Fetch the URL again using IWebFetcher
    /// 2. Compare the new content hash with the stored hash
    /// 3. If changed, update the markdown store and metadata
    /// 4. If configured, trigger re-indexing for knowledge ingestion
    /// 5. Log success or failure
    /// 
    /// Failures are logged but don't throw unhandled exceptions,
    /// allowing the scheduler to continue with other jobs.
    /// </remarks>
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Starting scheduled refetch for URL: {SourceUrl}",
                _sourceUrl);

            // Step 1: Fetch the URL
            var fetchResult = await _webFetcher.FetchAndExtractAsync(
                _sourceUrl,
                cancellationToken);

            // For success, we expect a non-null result. If FetchAndExtractAsync throws an exception,
            // it will be caught below.
            if (fetchResult == null)
            {
                _logger.LogWarning(
                    "Unexpected null result from refetch of URL {SourceUrl}",
                    _sourceUrl);
                return;
            }

            // Check HTTP status code for errors
            if (fetchResult.StatusCode >= 400)
            {
                _logger.LogWarning(
                    "Failed to refetch URL {SourceUrl}: HTTP {StatusCode}",
                    _sourceUrl,
                    fetchResult.StatusCode);
                return;
            }

            // Step 2: Store the markdown content
            // The content store will handle deduplication and change detection
            var markdownContent = fetchResult.HtmlContent;
            if (!string.IsNullOrEmpty(markdownContent))
            {
                var result = await _contentStore.StoreAsync(
                    _sourceUrl,
                    markdownContent,
                    title: null,
                    description: null,
                    tags: null,
                    cancellationToken: cancellationToken);

                _logger.LogInformation(
                    "Successfully refetched and stored content for URL: {SourceUrl} -> {ContentId}",
                    _sourceUrl,
                    result.Metadata.ContentId);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation(
                "Scheduled refetch for URL {SourceUrl} was cancelled",
                _sourceUrl);
            throw; // Re-throw cancellation exceptions
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error during scheduled refetch of URL {SourceUrl}",
                _sourceUrl);
            // Don't rethrow - allow scheduler to continue with other jobs
        }
    }
}
