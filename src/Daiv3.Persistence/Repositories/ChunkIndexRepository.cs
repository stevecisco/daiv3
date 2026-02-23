using Daiv3.Persistence.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Daiv3.Persistence.Repositories;

/// <summary>
/// Repository for managing ChunkIndex entities.
/// Tier 2: Multiple embeddings per document for fine-grained search.
/// </summary>
public class ChunkIndexRepository : RepositoryBase<ChunkIndex>
{
    public ChunkIndexRepository(IDatabaseContext databaseContext, ILogger<ChunkIndexRepository> logger)
        : base(databaseContext, logger)
    {
    }

    public override async Task<ChunkIndex?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT chunk_id, doc_id, chunk_text, embedding_blob, embedding_dimensions, chunk_order, topic_tags, created_at
            FROM chunk_index
            WHERE chunk_id = $id";

        var results = await ExecuteReaderAsync(sql, MapChunkIndex, parameters =>
        {
            parameters.Add(new SqliteParameter("$id", id));
        }, ct).ConfigureAwait(false);

        return results.FirstOrDefault();
    }

    public override async Task<IReadOnlyList<ChunkIndex>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT chunk_id, doc_id, chunk_text, embedding_blob, embedding_dimensions, chunk_order, topic_tags, created_at
            FROM chunk_index
            ORDER BY created_at DESC";

        return await ExecuteReaderAsync(sql, MapChunkIndex, null, ct).ConfigureAwait(false);
    }

    public override async Task<string> AddAsync(ChunkIndex entity, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        const string sql = @"
            INSERT INTO chunk_index (chunk_id, doc_id, chunk_text, embedding_blob, embedding_dimensions, chunk_order, topic_tags, created_at)
            VALUES ($chunk_id, $doc_id, $chunk_text, $embedding_blob, $embedding_dimensions, $chunk_order, $topic_tags, $created_at)";

        await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$chunk_id", entity.ChunkId));
            parameters.Add(new SqliteParameter("$doc_id", entity.DocId));
            parameters.Add(new SqliteParameter("$chunk_text", entity.ChunkText));
            parameters.Add(new SqliteParameter("$embedding_blob", entity.EmbeddingBlob));
            parameters.Add(new SqliteParameter("$embedding_dimensions", entity.EmbeddingDimensions));
            parameters.Add(new SqliteParameter("$chunk_order", entity.ChunkOrder));
            parameters.Add(new SqliteParameter("$topic_tags", (object?)entity.TopicTags ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$created_at", entity.CreatedAt));
        }, ct).ConfigureAwait(false);

        Logger.LogInformation("Added chunk index entry {ChunkId} for document {DocId}", entity.ChunkId, entity.DocId);
        return entity.ChunkId;
    }

    public override async Task UpdateAsync(ChunkIndex entity, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        const string sql = @"
            UPDATE chunk_index
            SET doc_id = $doc_id,
                chunk_text = $chunk_text,
                embedding_blob = $embedding_blob,
                embedding_dimensions = $embedding_dimensions,
                chunk_order = $chunk_order,
                topic_tags = $topic_tags
            WHERE chunk_id = $chunk_id";

        var rowsAffected = await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$chunk_id", entity.ChunkId));
            parameters.Add(new SqliteParameter("$doc_id", entity.DocId));
            parameters.Add(new SqliteParameter("$chunk_text", entity.ChunkText));
            parameters.Add(new SqliteParameter("$embedding_blob", entity.EmbeddingBlob));
            parameters.Add(new SqliteParameter("$embedding_dimensions", entity.EmbeddingDimensions));
            parameters.Add(new SqliteParameter("$chunk_order", entity.ChunkOrder));
            parameters.Add(new SqliteParameter("$topic_tags", (object?)entity.TopicTags ?? DBNull.Value));
        }, ct).ConfigureAwait(false);

        if (rowsAffected == 0)
        {
            Logger.LogWarning("Chunk index entry {ChunkId} not found for update", entity.ChunkId);
        }
        else
        {
            Logger.LogInformation("Updated chunk index entry {ChunkId}", entity.ChunkId);
        }
    }

    public override async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM chunk_index WHERE chunk_id = $id";

        var rowsAffected = await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$id", id));
        }, ct).ConfigureAwait(false);

        if (rowsAffected > 0)
        {
            Logger.LogInformation("Deleted chunk index entry {ChunkId}", id);
        }
    }

    /// <summary>
    /// Gets all chunk entries for a specific document.
    /// </summary>
    public async Task<IReadOnlyList<ChunkIndex>> GetByDocumentIdAsync(string docId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT chunk_id, doc_id, chunk_text, embedding_blob, embedding_dimensions, chunk_order, topic_tags, created_at
            FROM chunk_index
            WHERE doc_id = $doc_id
            ORDER BY chunk_order ASC";

        return await ExecuteReaderAsync(sql, MapChunkIndex, parameters =>
        {
            parameters.Add(new SqliteParameter("$doc_id", docId));
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes all chunk entries for a specific document.
    /// </summary>
    public async Task<int> DeleteByDocumentIdAsync(string docId, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM chunk_index WHERE doc_id = $doc_id";

        var rowsAffected = await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$doc_id", docId));
        }, ct).ConfigureAwait(false);

        if (rowsAffected > 0)
        {
            Logger.LogInformation("Deleted {RowCount} chunk index entries for document {DocId}", rowsAffected, docId);
        }

        return rowsAffected;
    }

    /// <summary>
    /// Gets the count of chunk index entries.
    /// </summary>
    public async Task<int> GetCountAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT COUNT(*) FROM chunk_index";

        using var connection = await DatabaseContext.GetConnectionAsync(ct).ConfigureAwait(false);
        using var command = new SqliteCommand(sql, connection);
        var result = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);

        return result is int count ? count : 0;
    }

    /// <summary>
    /// Gets the count of chunk entries for a specific document.
    /// </summary>
    public async Task<int> GetCountByDocumentIdAsync(string docId, CancellationToken ct = default)
    {
        const string sql = "SELECT COUNT(*) FROM chunk_index WHERE doc_id = $doc_id";

        using var connection = await DatabaseContext.GetConnectionAsync(ct).ConfigureAwait(false);
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.Add(new SqliteParameter("$doc_id", docId));
        var result = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);

        return result is int count ? count : 0;
    }

    private static ChunkIndex MapChunkIndex(SqliteDataReader reader)
    {
        return new ChunkIndex
        {
            ChunkId = reader.GetString(0),
            DocId = reader.GetString(1),
            ChunkText = reader.GetString(2),
            EmbeddingBlob = (byte[])reader.GetValue(3),
            EmbeddingDimensions = reader.GetInt32(4),
            ChunkOrder = reader.GetInt32(5),
            TopicTags = reader.IsDBNull(6) ? null : reader.GetString(6),
            CreatedAt = reader.GetInt64(7)
        };
    }
}
