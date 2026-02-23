using Daiv3.Persistence.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Daiv3.Persistence.Repositories;

/// <summary>
/// Repository for managing Document entities.
/// </summary>
public class DocumentRepository : RepositoryBase<Document>
{
    public DocumentRepository(IDatabaseContext databaseContext, ILogger<DocumentRepository> logger)
        : base(databaseContext, logger)
    {
    }

    public override async Task<Document?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT doc_id, source_path, file_hash, format, size_bytes, last_modified, status, created_at, metadata_json
            FROM documents
            WHERE doc_id = $id";

        var results = await ExecuteReaderAsync(sql, MapDocument, parameters =>
        {
            parameters.Add(new SqliteParameter("$id", id));
        }, ct).ConfigureAwait(false);

        return results.FirstOrDefault();
    }

    public override async Task<IReadOnlyList<Document>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT doc_id, source_path, file_hash, format, size_bytes, last_modified, status, created_at, metadata_json
            FROM documents
            ORDER BY created_at DESC";

        return await ExecuteReaderAsync(sql, MapDocument, null, ct).ConfigureAwait(false);
    }

    public override async Task<string> AddAsync(Document entity, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        const string sql = @"
            INSERT INTO documents (doc_id, source_path, file_hash, format, size_bytes, last_modified, status, created_at, metadata_json)
            VALUES ($doc_id, $source_path, $file_hash, $format, $size_bytes, $last_modified, $status, $created_at, $metadata_json)";

        await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$doc_id", entity.DocId));
            parameters.Add(new SqliteParameter("$source_path", entity.SourcePath));
            parameters.Add(new SqliteParameter("$file_hash", entity.FileHash));
            parameters.Add(new SqliteParameter("$format", entity.Format));
            parameters.Add(new SqliteParameter("$size_bytes", entity.SizeBytes));
            parameters.Add(new SqliteParameter("$last_modified", entity.LastModified));
            parameters.Add(new SqliteParameter("$status", entity.Status));
            parameters.Add(new SqliteParameter("$created_at", entity.CreatedAt));
            parameters.Add(new SqliteParameter("$metadata_json", (object?)entity.MetadataJson ?? DBNull.Value));
        }, ct).ConfigureAwait(false);

        Logger.LogInformation("Added document {DocId} from {SourcePath}", entity.DocId, entity.SourcePath);
        return entity.DocId;
    }

    public override async Task UpdateAsync(Document entity, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        const string sql = @"
            UPDATE documents
            SET source_path = $source_path,
                file_hash = $file_hash,
                format = $format,
                size_bytes = $size_bytes,
                last_modified = $last_modified,
                status = $status,
                metadata_json = $metadata_json
            WHERE doc_id = $doc_id";

        var rowsAffected = await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$doc_id", entity.DocId));
            parameters.Add(new SqliteParameter("$source_path", entity.SourcePath));
            parameters.Add(new SqliteParameter("$file_hash", entity.FileHash));
            parameters.Add(new SqliteParameter("$format", entity.Format));
            parameters.Add(new SqliteParameter("$size_bytes", entity.SizeBytes));
            parameters.Add(new SqliteParameter("$last_modified", entity.LastModified));
            parameters.Add(new SqliteParameter("$status", entity.Status));
            parameters.Add(new SqliteParameter("$metadata_json", (object?)entity.MetadataJson ?? DBNull.Value));
        }, ct).ConfigureAwait(false);

        if (rowsAffected == 0)
        {
            Logger.LogWarning("Document {DocId} not found for update", entity.DocId);
        }
        else
        {
            Logger.LogInformation("Updated document {DocId}", entity.DocId);
        }
    }

    public override async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM documents WHERE doc_id = $id";

        var rowsAffected = await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$id", id));
        }, ct).ConfigureAwait(false);

        if (rowsAffected == 0)
        {
            Logger.LogWarning("Document {DocId} not found for deletion", id);
        }
        else
        {
            Logger.LogInformation("Deleted document {DocId}", id);
        }
    }

    /// <summary>
    /// Gets documents by status.
    /// </summary>
    public async Task<IReadOnlyList<Document>> GetByStatusAsync(string status, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT doc_id, source_path, file_hash, format, size_bytes, last_modified, status, created_at, metadata_json
            FROM documents
            WHERE status = $status
            ORDER BY created_at DESC";

        return await ExecuteReaderAsync(sql, MapDocument, parameters =>
        {
            parameters.Add(new SqliteParameter("$status", status));
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets a document by source path.
    /// </summary>
    public async Task<Document?> GetBySourcePathAsync(string sourcePath, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT doc_id, source_path, file_hash, format, size_bytes, last_modified, status, created_at, metadata_json
            FROM documents
            WHERE source_path = $source_path";

        var results = await ExecuteReaderAsync(sql, MapDocument, parameters =>
        {
            parameters.Add(new SqliteParameter("$source_path", sourcePath));
        }, ct).ConfigureAwait(false);

        return results.FirstOrDefault();
    }

    private static Document MapDocument(SqliteDataReader reader)
    {
        return new Document
        {
            DocId = reader.GetString(0),
            SourcePath = reader.GetString(1),
            FileHash = reader.GetString(2),
            Format = reader.GetString(3),
            SizeBytes = reader.GetInt64(4),
            LastModified = reader.GetInt64(5),
            Status = reader.GetString(6),
            CreatedAt = reader.GetInt64(7),
            MetadataJson = reader.IsDBNull(8) ? null : reader.GetString(8)
        };
    }
}
