namespace Daiv3.Knowledge;

/// <summary>
/// Enumeration of file indexing statuses.
/// </summary>
public enum FileIndexingStatus
{
    /// <summary>
    /// File is discovered but not yet indexed.
    /// </summary>
    NotIndexed,

    /// <summary>
    /// File is currently being processed.
    /// </summary>
    InProgress,

    /// <summary>
    /// File has been successfully indexed.
    /// </summary>
    Indexed,

    /// <summary>
    /// File indexing encountered a warning (partial processing).
    /// </summary>
    Warning,

    /// <summary>
    /// File indexing failed with an error.
    /// </summary>
    Error
}

/// <summary>
/// Represents the indexing status of a single file.
/// </summary>
public sealed class FileIndexInfo
{
    /// <summary>
    /// Document ID (if indexed).
    /// </summary>
    public string? DocId { get; init; }

    /// <summary>
    /// Full file path.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Current indexing status.
    /// </summary>
    public FileIndexingStatus Status { get; init; }

    /// <summary>
    /// Last indexed timestamp (Unix seconds).
    /// </summary>
    public long? IndexedAt { get; init; }

    /// <summary>
    /// Last file modification timestamp (Unix seconds).
    /// </summary>
    public long? LastModified { get; init; }

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long? SizeBytes { get; init; }

    /// <summary>
    /// File format (pdf, docx, md, txt, etc.).
    /// </summary>
    public string? Format { get; init; }

    /// <summary>
    /// Error message (if status is Error).
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Number of chunks created for this document.
    /// </summary>
    public int? ChunkCount { get; init; }

    /// <summary>
    /// Embedding dimension (384 or 768).
    /// </summary>
    public int? EmbeddingDimension { get; init; }

    /// <summary>
    /// Topic summary preview (first 100 characters).
    /// </summary>
    public string? TopicSummary { get; init; }

    /// <summary>
    /// Whether the file is marked as sensitive/not shareable.
    /// </summary>
    public bool IsSensitive { get; init; }
}

/// <summary>
/// Overall indexing statistics.
/// </summary>
public sealed class IndexingStatistics
{
    /// <summary>
    /// Total number of documents indexed.
    /// </summary>
    public int TotalIndexed { get; init; }

    /// <summary>
    /// Total number of files discovered but not indexed.
    /// </summary>
    public int TotalNotIndexed { get; init; }

    /// <summary>
    /// Total number of files with indexing errors.
    /// </summary>
    public int TotalErrors { get; init; }

    /// <summary>
    /// Total number of files currently being processed.
    /// </summary>
    public int TotalInProgress { get; init; }

    /// <summary>
    /// Total number of files with warnings.
    /// </summary>
    public int TotalWarnings { get; init; }

    /// <summary>
    /// Total storage used by embeddings (bytes).
    /// </summary>
    public long TotalEmbeddingStorageBytes { get; init; }

    /// <summary>
    /// Last scan time (Unix seconds).
    /// </summary>
    public long? LastScanTime { get; init; }

    /// <summary>
    /// Whether file system watcher is currently running.
    /// </summary>
    public bool IsWatcherActive { get; init; }

    /// <summary>
    /// Statistics from the orchestration service.
    /// </summary>
    public required KnowledgeFileOrchestrationStatistics OrchestrationStats { get; init; }
}

/// <summary>
/// Service for querying indexing status and file information.
/// Implements CT-REQ-005: Dashboard SHALL display indexing progress, file browser, per-file status.
/// </summary>
public interface IIndexingStatusService
{
    /// <summary>
    /// Gets overall indexing statistics.
    /// </summary>
    Task<IndexingStatistics> GetIndexingStatisticsAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets all files with their indexing status.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of files with status information.</returns>
    Task<IReadOnlyList<FileIndexInfo>> GetAllFilesAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets files filtered by status.
    /// </summary>
    /// <param name="status">Status to filter by.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of files with the specified status.</returns>
    Task<IReadOnlyList<FileIndexInfo>> GetFilesByStatusAsync(
        FileIndexingStatus status,
        CancellationToken ct = default);

    /// <summary>
    /// Gets files filtered by format.
    /// </summary>
    /// <param name="format">Format to filter by (pdf, docx, md, txt, etc.).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of files with the specified format.</returns>
    Task<IReadOnlyList<FileIndexInfo>> GetFilesByFormatAsync(
        string format,
        CancellationToken ct = default);

    /// <summary>
    /// Searches files by name or path.
    /// </summary>
    /// <param name="searchTerm">Search term to match against file path.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of matching files.</returns>
    Task<IReadOnlyList<FileIndexInfo>> SearchFilesAsync(
        string searchTerm,
        CancellationToken ct = default);

    /// <summary>
    /// Gets detailed information about a specific file.
    /// </summary>
    /// <param name="filePath">File path to retrieve details for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Detailed file information or null if not found.</returns>
    Task<FileIndexInfo?> GetFileDetailsAsync(
        string filePath,
        CancellationToken ct = default);
}
