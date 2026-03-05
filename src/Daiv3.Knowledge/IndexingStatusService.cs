using Daiv3.Persistence;
using Daiv3.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Daiv3.Knowledge;

/// <summary>
/// Service for querying indexing status and file information.
/// Implements CT-REQ-005: Dashboard SHALL display indexing progress, file browser, per-file status.
/// </summary>
public sealed class IndexingStatusService : IIndexingStatusService
{
    private readonly IDatabaseContext _databaseContext;
    private readonly DocumentRepository _documentRepository;
    private readonly IKnowledgeFileOrchestrationService? _orchestrationService;
    private readonly ILogger<IndexingStatusService> _logger;

    public IndexingStatusService(
        IDatabaseContext databaseContext,
        DocumentRepository documentRepository,
        ILogger<IndexingStatusService> logger,
        IKnowledgeFileOrchestrationService? orchestrationService = null)
    {
        _databaseContext = databaseContext ?? throw new ArgumentNullException(nameof(databaseContext));
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _orchestrationService = orchestrationService;
    }

    /// <inheritdoc/>
    public async Task<IndexingStatistics> GetIndexingStatisticsAsync(CancellationToken ct = default)
    {
        _logger.LogDebug("Getting indexing statistics");

        const string sql = @"
            SELECT 
                COUNT(DISTINCT d.doc_id) as total_indexed,
                SUM(CASE WHEN d.status = 'error' THEN 1 ELSE 0 END) as total_errors,
                SUM(CASE WHEN d.status = 'pending' THEN 1 ELSE 0 END) as total_pending,
                MAX(d.created_at) as last_scan_time,
                COALESCE(SUM(LENGTH(t.embedding_blob) + LENGTH(c.embedding_blob)), 0) as total_storage
            FROM documents d
            LEFT JOIN topic_index t ON d.doc_id = t.doc_id
            LEFT JOIN chunk_index c ON d.doc_id = c.doc_id";

        using var connection = await _databaseContext.GetConnectionAsync(ct).ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = sql;

        using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            _logger.LogWarning("No statistics data returned from database");
            return new IndexingStatistics
            {
                TotalIndexed = 0,
                TotalNotIndexed = 0,
                TotalErrors = 0,
                TotalInProgress = 0,
                TotalWarnings = 0,
                TotalEmbeddingStorageBytes = 0,
                LastScanTime = null,
                IsWatcherActive = _orchestrationService?.IsRunning ?? false,
                OrchestrationStats = _orchestrationService?.GetStatistics() 
                    ?? new KnowledgeFileOrchestrationStatistics()
            };
        }

        var totalIndexed = reader.GetInt32(0);
        var totalErrors = reader.GetInt32(1);
        var totalPending = reader.GetInt32(2);
        var lastScanTime = reader.IsDBNull(3) ? (long?)null : reader.GetInt64(3);
        var totalStorage = reader.GetInt64(4);

        var stats = new IndexingStatistics
        {
            TotalIndexed = totalIndexed,
            TotalNotIndexed = totalPending,
            TotalErrors = totalErrors,
            TotalInProgress = 0, // Would need separate tracking for in-progress files
            TotalWarnings = 0, // Could parse from metadata_json if we store warnings
            TotalEmbeddingStorageBytes = totalStorage,
            LastScanTime = lastScanTime,
            IsWatcherActive = _orchestrationService?.IsRunning ?? false,
            OrchestrationStats = _orchestrationService?.GetStatistics() 
                ?? new KnowledgeFileOrchestrationStatistics()
        };

        _logger.LogDebug(
            "Retrieved statistics: {Indexed} indexed, {Errors} errors, {Pending} pending",
            totalIndexed, totalErrors, totalPending);

        return stats;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<FileIndexInfo>> GetAllFilesAsync(CancellationToken ct = default)
    {
        _logger.LogDebug("Getting all files with indexing status");

        const string sql = @"
            SELECT 
                d.doc_id,
                d.source_path,
                d.status,
                d.created_at,
                d.last_modified,
                d.size_bytes,
                d.format,
                d.metadata_json,
                t.summary_text,
                t.embedding_dimensions,
                (SELECT COUNT(*) FROM chunk_index c WHERE c.doc_id = d.doc_id) as chunk_count
            FROM documents d
            LEFT JOIN topic_index t ON d.doc_id = t.doc_id
            ORDER BY d.created_at DESC";

        using var connection = await _databaseContext.GetConnectionAsync(ct).ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = sql;

        using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var files = new List<FileIndexInfo>();

        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            files.Add(MapFileIndexInfo(reader));
        }

        _logger.LogDebug("Retrieved {Count} files", files.Count);
        return files;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<FileIndexInfo>> GetFilesByStatusAsync(
        FileIndexingStatus status,
        CancellationToken ct = default)
    {
        _logger.LogDebug("Getting files with status: {Status}", status);

        var dbStatus = MapStatusToDatabase(status);
        
        const string sql = @"
            SELECT 
                d.doc_id,
                d.source_path,
                d.status,
                d.created_at,
                d.last_modified,
                d.size_bytes,
                d.format,
                d.metadata_json,
                t.summary_text,
                t.embedding_dimensions,
                (SELECT COUNT(*) FROM chunk_index c WHERE c.doc_id = d.doc_id) as chunk_count
            FROM documents d
            LEFT JOIN topic_index t ON d.doc_id = t.doc_id
            WHERE d.status = $status
            ORDER BY d.created_at DESC";

        using var connection = await _databaseContext.GetConnectionAsync(ct).ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add(new SqliteParameter("$status", dbStatus));

        using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var files = new List<FileIndexInfo>();

        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            files.Add(MapFileIndexInfo(reader));
        }

        _logger.LogDebug("Retrieved {Count} files with status {Status}", files.Count, status);
        return files;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<FileIndexInfo>> GetFilesByFormatAsync(
        string format,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(format, nameof(format));
        _logger.LogDebug("Getting files with format: {Format}", format);

        const string sql = @"
            SELECT 
                d.doc_id,
                d.source_path,
                d.status,
                d.created_at,
                d.last_modified,
                d.size_bytes,
                d.format,
                d.metadata_json,
                t.summary_text,
                t.embedding_dimensions,
                (SELECT COUNT(*) FROM chunk_index c WHERE c.doc_id = d.doc_id) as chunk_count
            FROM documents d
            LEFT JOIN topic_index t ON d.doc_id = t.doc_id
            WHERE d.format = $format
            ORDER BY d.created_at DESC";

        using var connection = await _databaseContext.GetConnectionAsync(ct).ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add(new SqliteParameter("$format", format.ToLowerInvariant()));

        using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var files = new List<FileIndexInfo>();

        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            files.Add(MapFileIndexInfo(reader));
        }

        _logger.LogDebug("Retrieved {Count} files with format {Format}", files.Count, format);
        return files;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<FileIndexInfo>> SearchFilesAsync(
        string searchTerm,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(searchTerm, nameof(searchTerm));
        _logger.LogDebug("Searching files with term: {SearchTerm}", searchTerm);

        const string sql = @"
            SELECT 
                d.doc_id,
                d.source_path,
                d.status,
                d.created_at,
                d.last_modified,
                d.size_bytes,
                d.format,
                d.metadata_json,
                t.summary_text,
                t.embedding_dimensions,
                (SELECT COUNT(*) FROM chunk_index c WHERE c.doc_id = d.doc_id) as chunk_count
            FROM documents d
            LEFT JOIN topic_index t ON d.doc_id = t.doc_id
            WHERE d.source_path LIKE $searchTerm
            ORDER BY d.created_at DESC";

        using var connection = await _databaseContext.GetConnectionAsync(ct).ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add(new SqliteParameter("$searchTerm", $"%{searchTerm}%"));

        using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var files = new List<FileIndexInfo>();

        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            files.Add(MapFileIndexInfo(reader));
        }

        _logger.LogDebug("Search returned {Count} files", files.Count);
        return files;
    }

    /// <inheritdoc/>
    public async Task<FileIndexInfo?> GetFileDetailsAsync(
        string filePath,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath, nameof(filePath));
        _logger.LogDebug("Getting file details for: {FilePath}", filePath);

        const string sql = @"
            SELECT 
                d.doc_id,
                d.source_path,
                d.status,
                d.created_at,
                d.last_modified,
                d.size_bytes,
                d.format,
                d.metadata_json,
                t.summary_text,
                t.embedding_dimensions,
                (SELECT COUNT(*) FROM chunk_index c WHERE c.doc_id = d.doc_id) as chunk_count
            FROM documents d
            LEFT JOIN topic_index t ON d.doc_id = t.doc_id
            WHERE d.source_path = $filePath";

        using var connection = await _databaseContext.GetConnectionAsync(ct).ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add(new SqliteParameter("$filePath", filePath));

        using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        
        if (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            return MapFileIndexInfo(reader);
        }

        _logger.LogDebug("File not found: {FilePath}", filePath);
        return null;
    }

    private static FileIndexInfo MapFileIndexInfo(SqliteDataReader reader)
    {
        var docId = reader.IsDBNull(0) ? null : reader.GetString(0);
        var sourcePath = reader.GetString(1);
        var status = reader.GetString(2);
        var createdAt = reader.GetInt64(3);
        var lastModified = reader.GetInt64(4);
        var sizeBytes = reader.GetInt64(5);
        var format = reader.GetString(6);
        var metadataJson = reader.IsDBNull(7) ? null : reader.GetString(7);
        var summaryText = reader.IsDBNull(8) ? null : reader.GetString(8);
        var embeddingDimensions = reader.IsDBNull(9) ? (int?)null : reader.GetInt32(9);
        var chunkCount = reader.IsDBNull(10) ? (int?)null : reader.GetInt32(10);

        // Extract error message from status field if it contains error info
        string? errorMessage = null;
        if (status == "error" && !string.IsNullOrEmpty(metadataJson))
        {
            // Could parse JSON to extract error message if stored there
            errorMessage = "Indexing failed"; // Placeholder
        }

        // Truncate summary to 100 characters
        var topicSummary = summaryText != null && summaryText.Length > 100
            ? summaryText.Substring(0, 100) + "..."
            : summaryText;

        return new FileIndexInfo
        {
            DocId = docId,
            FilePath = sourcePath,
            Status = MapDatabaseStatusToEnum(status),
            IndexedAt = createdAt,
            LastModified = lastModified,
            SizeBytes = sizeBytes,
            Format = format,
            ErrorMessage = errorMessage,
            ChunkCount = chunkCount,
            EmbeddingDimension = embeddingDimensions,
            TopicSummary = topicSummary,
            IsSensitive = false // Could be extracted from metadata_json
        };
    }

    private static FileIndexingStatus MapDatabaseStatusToEnum(string status)
    {
        return status.ToLowerInvariant() switch
        {
            "indexed" => FileIndexingStatus.Indexed,
            "pending" => FileIndexingStatus.NotIndexed,
            "error" => FileIndexingStatus.Error,
            "processing" => FileIndexingStatus.InProgress,
            "warning" => FileIndexingStatus.Warning,
            _ => FileIndexingStatus.NotIndexed
        };
    }

    private static string MapStatusToDatabase(FileIndexingStatus status)
    {
        return status switch
        {
            FileIndexingStatus.Indexed => "indexed",
            FileIndexingStatus.NotIndexed => "pending",
            FileIndexingStatus.Error => "error",
            FileIndexingStatus.InProgress => "processing",
            FileIndexingStatus.Warning => "warning",
            _ => "pending"
        };
    }
}
