using Daiv3.Persistence.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Daiv3.Persistence.Repositories;

/// <summary>
/// Repository for managing TopicIndex entities.
/// Tier 1: One embedding per document for fast coarse search.
/// </summary>
public class TopicIndexRepository : RepositoryBase<TopicIndex>
{
    public TopicIndexRepository(IDatabaseContext databaseContext, ILogger<TopicIndexRepository> logger)
        : base(databaseContext, logger)
    {
    }

    public override async Task<TopicIndex?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT doc_id, summary_text, embedding_blob, embedding_dimensions, source_path, file_hash, ingested_at, metadata_json
            FROM topic_index
            WHERE doc_id = $id";

        var results = await ExecuteReaderAsync(sql, MapTopicIndex, parameters =>
        {
            parameters.Add(new SqliteParameter("$id", id));
        }, ct).ConfigureAwait(false);

        return results.FirstOrDefault();
    }

    public override async Task<IReadOnlyList<TopicIndex>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT doc_id, summary_text, embedding_blob, embedding_dimensions, source_path, file_hash, ingested_at, metadata_json
            FROM topic_index
            ORDER BY ingested_at DESC";

        return await ExecuteReaderAsync(sql, MapTopicIndex, null, ct).ConfigureAwait(false);
    }

    public override async Task<string> AddAsync(TopicIndex entity, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        const string sql = @"
            INSERT INTO topic_index (doc_id, summary_text, embedding_blob, embedding_dimensions, source_path, file_hash, ingested_at, metadata_json)
            VALUES ($doc_id, $summary_text, $embedding_blob, $embedding_dimensions, $source_path, $file_hash, $ingested_at, $metadata_json)";

        await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$doc_id", entity.DocId));
            parameters.Add(new SqliteParameter("$summary_text", entity.SummaryText));
            parameters.Add(new SqliteParameter("$embedding_blob", entity.EmbeddingBlob));
            parameters.Add(new SqliteParameter("$embedding_dimensions", entity.EmbeddingDimensions));
            parameters.Add(new SqliteParameter("$source_path", entity.SourcePath));
            parameters.Add(new SqliteParameter("$file_hash", entity.FileHash));
            parameters.Add(new SqliteParameter("$ingested_at", entity.IngestedAt));
            parameters.Add(new SqliteParameter("$metadata_json", (object?)entity.MetadataJson ?? DBNull.Value));
        }, ct).ConfigureAwait(false);

        Logger.LogInformation("Added topic index entry for {DocId}", entity.DocId);
        return entity.DocId;
    }

    public override async Task UpdateAsync(TopicIndex entity, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        const string sql = @"
            UPDATE topic_index
            SET summary_text = $summary_text,
                embedding_blob = $embedding_blob,
                embedding_dimensions = $embedding_dimensions,
                source_path = $source_path,
                file_hash = $file_hash,
                ingested_at = $ingested_at,
                metadata_json = $metadata_json
            WHERE doc_id = $doc_id";

        var rowsAffected = await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$doc_id", entity.DocId));
            parameters.Add(new SqliteParameter("$summary_text", entity.SummaryText));
            parameters.Add(new SqliteParameter("$embedding_blob", entity.EmbeddingBlob));
            parameters.Add(new SqliteParameter("$embedding_dimensions", entity.EmbeddingDimensions));
            parameters.Add(new SqliteParameter("$source_path", entity.SourcePath));
            parameters.Add(new SqliteParameter("$file_hash", entity.FileHash));
            parameters.Add(new SqliteParameter("$ingested_at", entity.IngestedAt));
            parameters.Add(new SqliteParameter("$metadata_json", (object?)entity.MetadataJson ?? DBNull.Value));
        }, ct).ConfigureAwait(false);

        if (rowsAffected == 0)
        {
            Logger.LogWarning("Topic index entry {DocId} not found for update", entity.DocId);
        }
        else
        {
            Logger.LogInformation("Updated topic index entry for {DocId}", entity.DocId);
        }
    }

    public override async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM topic_index WHERE doc_id = $id";

        var rowsAffected = await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$id", id));
        }, ct).ConfigureAwait(false);

        if (rowsAffected > 0)
        {
            Logger.LogInformation("Deleted topic index entry for {DocId}", id);
        }
    }

    /// <summary>
    /// Gets all topic index entries (loads all embeddings into memory for batch processing).
    /// </summary>
    public async Task<IReadOnlyList<TopicIndex>> GetAllWithEmbeddingsAsync(CancellationToken ct = default)
    {
        return await GetAllAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets a topic index entry by source path.
    /// </summary>
    public async Task<TopicIndex?> GetBySourcePathAsync(string sourcePath, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT doc_id, summary_text, embedding_blob, embedding_dimensions, source_path, file_hash, ingested_at, metadata_json
            FROM topic_index
            WHERE source_path = $source_path";

        var results = await ExecuteReaderAsync(sql, MapTopicIndex, parameters =>
        {
            parameters.Add(new SqliteParameter("$source_path", sourcePath));
        }, ct).ConfigureAwait(false);

        return results.FirstOrDefault();
    }

    /// <summary>
    /// Gets the count of topic index entries.
    /// </summary>
    public async Task<int> GetCountAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT COUNT(*) FROM topic_index";

        using var connection = await DatabaseContext.GetConnectionAsync(ct).ConfigureAwait(false);
        using var command = new SqliteCommand(sql, connection);
        var result = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);

        return result switch
        {
            long count => checked((int)count),
            int count => count,
            _ => 0
        };
    }

    private static TopicIndex MapTopicIndex(SqliteDataReader reader)
    {
        return new TopicIndex
        {
            DocId = reader.GetString(0),
            SummaryText = reader.GetString(1),
            EmbeddingBlob = (byte[])reader.GetValue(2),
            EmbeddingDimensions = reader.GetInt32(3),
            SourcePath = reader.GetString(4),
            FileHash = reader.GetString(5),
            IngestedAt = reader.GetInt64(6),
            MetadataJson = reader.IsDBNull(7) ? null : reader.GetString(7)
        };
    }
}
