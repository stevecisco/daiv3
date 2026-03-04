namespace Daiv3.Orchestration.Interfaces;

/// <summary>
/// Options for web content ingestion configuration.
/// </summary>
public class WebContentIngestionOptions
{
    /// <summary>
    /// Enable automatic ingestion of fetched web content into the knowledge pipeline.
    /// Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Monitor the content store directory for new files and ingest them automatically.
    /// If false, ingestion can only be triggered manually via IngestAsync().
    /// Default: true.
    /// </summary>
    public bool EnableAutoMonitoring { get; set; } = true;

    /// <summary>
    /// Delay in milliseconds before processing a newly detected file.
    /// Used to avoid processing incomplete files during writes.
    /// Default: 1000 (1 second).
    /// </summary>
    public int FileDetectionDelayMs { get; set; } = 1000;

    /// <summary>
    /// Include source URL, fetch date, and content hash as document metadata.
    /// Default: true.
    /// </summary>
    public bool IncludeSourceMetadata { get; set; } = true;

    /// <summary>
    /// Maximum number of files to process concurrently.
    /// Default: 3.
    /// </summary>
    public int MaxConcurrentIngestions { get; set; } = 3;

    /// <summary>
    /// Skip files that have already been ingested (based on file path and hash).
    /// Default: true.
    /// </summary>
    public bool SkipAlreadyIngestedFiles { get; set; } = true;
}

/// <summary>
/// Result of ingesting a single web-fetched content item into the knowledge pipeline.
/// </summary>
public class WebContentIngestionResult
{
    /// <summary>
    /// The source URL of the content.
    /// </summary>
    public string SourceUrl { get; set; } = string.Empty;

    /// <summary>
    /// The file path where the markdown content was stored.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Whether the ingestion was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if ingestion failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Number of chunks created from this content.
    /// </summary>
    public int ChunkCount { get; set; }

    /// <summary>
    /// Total tokens processed (excluding chunks).
    /// </summary>
    public int TotalTokens { get; set; }

    /// <summary>
    /// Time taken to ingest in milliseconds.
    /// </summary>
    public long IngestionTimeMs { get; set; }

    /// <summary>
    /// ISO 8601 timestamp of the fetch.
    /// </summary>
    public string? FetchedAt { get; set; }
}

/// <summary>
/// Integrates web-fetched markdown content into the knowledge ingestion pipeline.
/// Monitors the markdown content store directory and automatically processes new files
/// through the knowledge document processor.
/// </summary>
/// <remarks>
/// Implements <see cref="System.IDisposable"/> to manage file system watcher and internal resources.
/// Callers must dispose instances to properly clean up the directory monitoring infrastructure.
/// </remarks>
public interface IWebContentIngestionService : IDisposable
{
    /// <summary>
    /// Ingest a single markdown file into the knowledge pipeline.
    /// The file should be in the content store directory.
    /// </summary>
    /// <param name="filePath">Full path to the markdown file.</param>
    /// <param name="sourceUrl">Optional source URL (if not available in file metadata).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Ingestion result with status and metrics.</returns>
    Task<WebContentIngestionResult> IngestContentAsync(
        string filePath,
        string? sourceUrl = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Scan the content store directory and ingest all new/unprocessed markdown files.
    /// </summary>
    /// <param name="progressCallback">Optional callback for progress reporting (processed, total, currentFile).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of ingestion results for each file.</returns>
    Task<IReadOnlyList<WebContentIngestionResult>> IngestPendingContentAsync(
        IProgress<(int Processed, int Total, string CurrentFile)>? progressCallback = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Start monitoring the content store directory for new files.
    /// New files will be automatically ingested based on configuration.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when monitoring is stopped.</returns>
    Task StartMonitoringAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop monitoring the content store directory.
    /// </summary>
    Task StopMonitoringAsync();

    /// <summary>
    /// Get statistics about ingestion progress.
    /// </summary>
    /// <returns>Statistics object with ingestion metrics.</returns>
    WebContentIngestionStatistics GetStatistics();
}

/// <summary>
/// Statistics about web content ingestion progress.
/// </summary>
public class WebContentIngestionStatistics
{
    /// <summary>
    /// Total number of content files detected.
    /// </summary>
    public int TotalFilesDetected { get; set; }

    /// <summary>
    /// Number of files successfully ingested.
    /// </summary>
    public int FilesIngested { get; set; }

    /// <summary>
    /// Number of files skipped (already ingested).
    /// </summary>
    public int FilesSkipped { get; set; }

    /// <summary>
    /// Number of files with ingestion errors.
    /// </summary>
    public int FilesWithErrors { get; set; }

    /// <summary>
    /// Total chunks created across all ingested content.
    /// </summary>
    public int TotalChunksCreated { get; set; }

    /// <summary>
    /// Total tokens processed.
    /// </summary>
    public int TotalTokensProcessed { get; set; }

    /// <summary>
    /// Total time spent ingesting (milliseconds).
    /// </summary>
    public long TotalIngestionTimeMs { get; set; }

    /// <summary>
    /// Whether monitoring is currently active.
    /// </summary>
    public bool IsMonitoring { get; set; }
}
