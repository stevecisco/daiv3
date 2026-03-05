using Daiv3.Persistence;
using Daiv3.Persistence.Repositories;
using Daiv3.Knowledge.DocProc;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using System.Text.Json;

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
    private readonly IFileSystemWatcher? _fileSystemWatcher;
    private readonly ILogger<IndexingStatusService> _logger;

    public IndexingStatusService(
        IDatabaseContext databaseContext,
        DocumentRepository documentRepository,
        ILogger<IndexingStatusService> logger,
        IKnowledgeFileOrchestrationService? orchestrationService = null,
        IFileSystemWatcher? fileSystemWatcher = null)
    {
        _databaseContext = databaseContext ?? throw new ArgumentNullException(nameof(databaseContext));
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _orchestrationService = orchestrationService;
        _fileSystemWatcher = fileSystemWatcher;
    }

    /// <inheritdoc/>
    public async Task<IndexingStatistics> GetIndexingStatisticsAsync(CancellationToken ct = default)
    {
        _logger.LogDebug("Getting indexing statistics");

        var allFiles = await GetAllFilesAsync(ct).ConfigureAwait(false);
        var orchestrationStats = _orchestrationService?.GetStatistics() ?? new KnowledgeFileOrchestrationStatistics();

        const string sql = @"
            SELECT 
                COALESCE(SUM(LENGTH(t.embedding_blob) + LENGTH(c.embedding_blob)), 0) as total_storage,
                MAX(d.created_at) as last_scan_time
            FROM documents d
            LEFT JOIN topic_index t ON d.doc_id = t.doc_id
            LEFT JOIN chunk_index c ON d.doc_id = c.doc_id";

        using var connection = await _databaseContext.GetConnectionAsync(ct).ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = sql;

        using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var totalStorage = 0L;
        long? lastScanTimeFromDb = null;

        if (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            totalStorage = reader.IsDBNull(0) ? 0 : reader.GetInt64(0);
            lastScanTimeFromDb = reader.IsDBNull(1) ? (long?)null : reader.GetInt64(1);
        }

        var lastScanTime = orchestrationStats.LastScanCompletedAt ?? lastScanTimeFromDb;

        var stats = new IndexingStatistics
        {
            TotalIndexed = allFiles.Count(f => f.Status == FileIndexingStatus.Indexed),
            TotalNotIndexed = allFiles.Count(f => f.Status == FileIndexingStatus.NotIndexed),
            TotalDiscovered = allFiles.Count,
            TotalErrors = allFiles.Count(f => f.Status == FileIndexingStatus.Error),
            TotalInProgress = allFiles.Count(f => f.Status == FileIndexingStatus.InProgress),
            TotalWarnings = allFiles.Count(f => f.Status == FileIndexingStatus.Warning),
            TotalEmbeddingStorageBytes = totalStorage,
            LastScanTime = lastScanTime,
            LastScanDurationMs = orchestrationStats.LastScanDurationMs,
            NextScheduledScanTime = null,
            IsWatcherActive = _orchestrationService?.IsRunning ?? _fileSystemWatcher?.IsRunning ?? false,
            OrchestrationStats = orchestrationStats
        };

        _logger.LogDebug(
            "Retrieved indexing statistics: {Discovered} discovered, {Indexed} indexed, {Errors} errors, {InProgress} in progress",
            stats.TotalDiscovered,
            stats.TotalIndexed,
            stats.TotalErrors,
            stats.TotalInProgress);

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
        var fileByPath = new Dictionary<string, FileIndexInfo>(StringComparer.OrdinalIgnoreCase);

        var orchestrationStats = _orchestrationService?.GetStatistics() ?? new KnowledgeFileOrchestrationStatistics();
        var activePathSet = new HashSet<string>(orchestrationStats.ActiveFilePaths, StringComparer.OrdinalIgnoreCase);
        var recentErrorByPath = orchestrationStats.RecentFileErrors;

        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var fileInfo = MapFileIndexInfo(reader, activePathSet, recentErrorByPath);
            files.Add(fileInfo);
            fileByPath[fileInfo.FilePath] = fileInfo;
        }

        // Include files discovered in watched paths even if they have not been indexed yet.
        foreach (var discoveredFile in GetDiscoveredFilePaths(ct))
        {
            if (fileByPath.ContainsKey(discoveredFile))
            {
                continue;
            }

            var fileStatus = activePathSet.Contains(discoveredFile)
                ? FileIndexingStatus.InProgress
                : FileIndexingStatus.NotIndexed;

            var fileName = Path.GetFileName(discoveredFile);
            var directoryPath = Path.GetDirectoryName(discoveredFile) ?? string.Empty;
            var extension = Path.GetExtension(discoveredFile);
            var format = string.IsNullOrWhiteSpace(extension)
                ? "unknown"
                : extension.TrimStart('.').ToLowerInvariant();

            long? sizeBytes = null;
            long? lastModifiedUnix = null;

            try
            {
                var fileInfo = new FileInfo(discoveredFile);
                if (fileInfo.Exists)
                {
                    sizeBytes = fileInfo.Length;
                    lastModifiedUnix = new DateTimeOffset(fileInfo.LastWriteTimeUtc).ToUnixTimeSeconds();
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to inspect discovered file: {FilePath}", discoveredFile);
            }

            var discovered = new FileIndexInfo
            {
                DocId = null,
                FilePath = discoveredFile,
                FileName = fileName,
                DirectoryPath = directoryPath,
                Status = fileStatus,
                IndexedAt = null,
                LastModified = lastModifiedUnix,
                SizeBytes = sizeBytes,
                Format = format,
                ErrorMessage = recentErrorByPath.TryGetValue(discoveredFile, out var err) ? err : null,
                ChunkCount = null,
                EmbeddingDimension = null,
                TopicSummary = null,
                IsSensitive = false,
                IsShareable = true,
                MachineLocation = null,
                HasTier1Embedding = false,
                HasTier2Embedding = false
            };

            files.Add(discovered);
            fileByPath[discoveredFile] = discovered;
        }

        files = files
            .OrderByDescending(f => f.IndexedAt ?? f.LastModified ?? 0)
            .ThenBy(f => f.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _logger.LogDebug("Retrieved {Count} files", files.Count);
        return files;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<FileIndexInfo>> GetFilesByStatusAsync(
        FileIndexingStatus status,
        CancellationToken ct = default)
    {
        _logger.LogDebug("Getting files with status: {Status}", status);

        var files = (await GetAllFilesAsync(ct).ConfigureAwait(false))
            .Where(file => file.Status == status)
            .ToList();

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

        var files = (await GetAllFilesAsync(ct).ConfigureAwait(false))
            .Where(file => file.Format != null && file.Format.Equals(format, StringComparison.OrdinalIgnoreCase))
            .ToList();

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

        var files = (await GetAllFilesAsync(ct).ConfigureAwait(false))
            .Where(file => file.FilePath.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            .ToList();

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

        var file = (await GetAllFilesAsync(ct).ConfigureAwait(false))
            .FirstOrDefault(candidate => string.Equals(candidate.FilePath, filePath, StringComparison.OrdinalIgnoreCase));

        if (file != null)
        {
            return file;
        }

        _logger.LogDebug("File not found: {FilePath}", filePath);
        return null;
    }

    private static FileIndexInfo MapFileIndexInfo(
        SqliteDataReader reader,
        IReadOnlySet<string> activePathSet,
        IReadOnlyDictionary<string, string> recentErrorByPath)
    {
        var docId = reader.IsDBNull(0) ? null : reader.GetString(0);
        var sourcePath = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
        var status = reader.IsDBNull(2) ? "pending" : reader.GetString(2);
        var createdAtRaw = reader.IsDBNull(3) ? (long?)null : reader.GetInt64(3);
        var lastModifiedRaw = reader.IsDBNull(4) ? (long?)null : reader.GetInt64(4);
        var sizeBytes = reader.IsDBNull(5) ? (long?)null : reader.GetInt64(5);
        var format = reader.IsDBNull(6) ? "unknown" : reader.GetString(6);
        var metadataJson = reader.IsDBNull(7) ? null : reader.GetString(7);
        var summaryText = reader.IsDBNull(8) ? null : reader.GetString(8);
        var embeddingDimensions = reader.IsDBNull(9) ? (int?)null : reader.GetInt32(9);
        var chunkCount = reader.IsDBNull(10) ? (int?)null : reader.GetInt32(10);

        var metadata = ParseMetadata(metadataJson);
        var warningCount = GetInt(metadata, "warningCount", "warningsCount");
        var metadataErrorMessage = GetString(metadata, "errorMessage", "error", "lastError");
        var isSensitive = GetBool(metadata, "isSensitive", "sensitive", "locked") ?? false;
        var isShareable = GetBool(metadata, "isShareable", "shareable") ?? !isSensitive;
        var machineLocation = GetString(metadata, "machineLocation", "sourceMachine", "node", "machine");

        var statusEnum = MapDatabaseStatusToEnum(status);
        if (activePathSet.Contains(sourcePath))
        {
            statusEnum = FileIndexingStatus.InProgress;
        }
        else if (statusEnum != FileIndexingStatus.Error && warningCount > 0)
        {
            statusEnum = FileIndexingStatus.Warning;
        }

        string? errorMessage = metadataErrorMessage;
        if (statusEnum == FileIndexingStatus.Error && string.IsNullOrWhiteSpace(errorMessage))
        {
            errorMessage = "Indexing failed";
        }

        if (recentErrorByPath.TryGetValue(sourcePath, out var recentError))
        {
            errorMessage = recentError;
            if (statusEnum != FileIndexingStatus.InProgress)
            {
                statusEnum = FileIndexingStatus.Error;
            }
        }

        // Truncate summary to 100 characters
        var topicSummary = summaryText != null && summaryText.Length > 100
            ? summaryText.Substring(0, 100) + "..."
            : summaryText;

        var lastModified = NormalizeTimestamp(lastModifiedRaw);
        var indexedAt = NormalizeTimestamp(createdAtRaw);

        var fileName = Path.GetFileName(sourcePath);
        var directoryPath = Path.GetDirectoryName(sourcePath) ?? string.Empty;

        return new FileIndexInfo
        {
            DocId = docId,
            FilePath = sourcePath,
            FileName = fileName,
            DirectoryPath = directoryPath,
            Status = statusEnum,
            IndexedAt = indexedAt,
            LastModified = lastModified,
            SizeBytes = sizeBytes,
            Format = format,
            ErrorMessage = errorMessage,
            ChunkCount = chunkCount,
            EmbeddingDimension = embeddingDimensions,
            TopicSummary = topicSummary,
            IsSensitive = isSensitive,
            IsShareable = isShareable,
            MachineLocation = machineLocation,
            HasTier1Embedding = embeddingDimensions.HasValue,
            HasTier2Embedding = chunkCount.GetValueOrDefault() > 0
        };
    }

    private static long? NormalizeTimestamp(long? rawValue)
    {
        if (!rawValue.HasValue || rawValue.Value <= 0)
        {
            return null;
        }

        // File.GetLastWriteTimeUtc().ToFileTimeUtc() generates large values.
        // Convert those into Unix seconds for consistent UI rendering.
        if (rawValue.Value > 10_000_000_000)
        {
            try
            {
                return new DateTimeOffset(DateTime.FromFileTimeUtc(rawValue.Value)).ToUnixTimeSeconds();
            }
            catch
            {
                return null;
            }
        }

        return rawValue.Value;
    }

    private static Dictionary<string, JsonElement> ParseMetadata(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(metadataJson);
            if (parsed == null)
            {
                return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            }

            var result = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, value) in parsed)
            {
                result[key] = value;
            }

            return result;
        }
        catch
        {
            return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string? GetString(IReadOnlyDictionary<string, JsonElement> metadata, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!metadata.TryGetValue(key, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                var parsed = value.GetString();
                if (!string.IsNullOrWhiteSpace(parsed))
                {
                    return parsed;
                }
            }
        }

        return null;
    }

    private static int GetInt(IReadOnlyDictionary<string, JsonElement> metadata, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!metadata.TryGetValue(key, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var intValue))
            {
                return intValue;
            }

            if (value.ValueKind == JsonValueKind.Array)
            {
                return value.GetArrayLength();
            }
        }

        return 0;
    }

    private static bool? GetBool(IReadOnlyDictionary<string, JsonElement> metadata, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!metadata.TryGetValue(key, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (value.ValueKind == JsonValueKind.False)
            {
                return false;
            }
        }

        return null;
    }

    private IEnumerable<string> GetDiscoveredFilePaths(CancellationToken ct)
    {
        if (_fileSystemWatcher == null)
        {
            yield break;
        }

        var watchedPaths = _fileSystemWatcher.GetWatchedPaths();
        foreach (var watchedPath in watchedPaths)
        {
            if (ct.IsCancellationRequested)
            {
                yield break;
            }

            if (!Directory.Exists(watchedPath))
            {
                continue;
            }

            IEnumerable<string> filePaths;
            try
            {
                filePaths = Directory.EnumerateFiles(watchedPath, "*", SearchOption.AllDirectories);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Unable to enumerate watched path {WatchPath}", watchedPath);
                continue;
            }

            foreach (var filePath in filePaths)
            {
                if (ct.IsCancellationRequested)
                {
                    yield break;
                }

                yield return filePath;
            }
        }
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

}
