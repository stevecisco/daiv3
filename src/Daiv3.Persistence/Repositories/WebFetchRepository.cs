using Daiv3.Persistence.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Daiv3.Persistence.Repositories;

/// <summary>
/// Repository for managing WebFetch entities.
/// Implements WFC-DATA-001: Metadata SHALL include source URL, fetch date, and content hash.
/// </summary>
public class WebFetchRepository : RepositoryBase<WebFetch>, IWebFetchRepository
{
    public WebFetchRepository(IDatabaseContext databaseContext, ILogger<WebFetchRepository> logger)
        : base(databaseContext, logger)
    {
    }

    public override async Task<WebFetch?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT web_fetch_id, doc_id, source_url, content_hash, fetch_date, title, description, status, error_message, created_at, updated_at
            FROM web_fetches
            WHERE web_fetch_id = $id";

        var results = await ExecuteReaderAsync(sql, MapWebFetch, parameters =>
        {
            parameters.Add(new SqliteParameter("$id", id));
        }, ct).ConfigureAwait(false);

        return results.FirstOrDefault();
    }

    public override async Task<IReadOnlyList<WebFetch>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT web_fetch_id, doc_id, source_url, content_hash, fetch_date, title, description, status, error_message, created_at, updated_at
            FROM web_fetches
            ORDER BY fetch_date DESC";

        return await ExecuteReaderAsync(sql, MapWebFetch, null, ct).ConfigureAwait(false);
    }

    public override async Task<string> AddAsync(WebFetch entity, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        const string sql = @"
            INSERT INTO web_fetches (web_fetch_id, doc_id, source_url, content_hash, fetch_date, title, description, status, error_message, created_at, updated_at)
            VALUES ($web_fetch_id, $doc_id, $source_url, $content_hash, $fetch_date, $title, $description, $status, $error_message, $created_at, $updated_at)";

        await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$web_fetch_id", entity.WebFetchId));
            parameters.Add(new SqliteParameter("$doc_id", entity.DocId));
            parameters.Add(new SqliteParameter("$source_url", entity.SourceUrl));
            parameters.Add(new SqliteParameter("$content_hash", entity.ContentHash));
            parameters.Add(new SqliteParameter("$fetch_date", entity.FetchDate));
            parameters.Add(new SqliteParameter("$title", (object?)entity.Title ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$description", (object?)entity.Description ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$status", entity.Status));
            parameters.Add(new SqliteParameter("$error_message", (object?)entity.ErrorMessage ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$created_at", entity.CreatedAt));
            parameters.Add(new SqliteParameter("$updated_at", entity.UpdatedAt));
        }, ct).ConfigureAwait(false);

        Logger.LogInformation("Added web fetch {WebFetchId} from URL {SourceUrl}", entity.WebFetchId, entity.SourceUrl);
        return entity.WebFetchId;
    }

    public override async Task UpdateAsync(WebFetch entity, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        const string sql = @"
            UPDATE web_fetches
            SET doc_id = $doc_id,
                source_url = $source_url,
                content_hash = $content_hash,
                fetch_date = $fetch_date,
                title = $title,
                description = $description,
                status = $status,
                error_message = $error_message,
                updated_at = $updated_at
            WHERE web_fetch_id = $web_fetch_id";

        var rowsAffected = await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$web_fetch_id", entity.WebFetchId));
            parameters.Add(new SqliteParameter("$doc_id", entity.DocId));
            parameters.Add(new SqliteParameter("$source_url", entity.SourceUrl));
            parameters.Add(new SqliteParameter("$content_hash", entity.ContentHash));
            parameters.Add(new SqliteParameter("$fetch_date", entity.FetchDate));
            parameters.Add(new SqliteParameter("$title", (object?)entity.Title ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$description", (object?)entity.Description ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$status", entity.Status));
            parameters.Add(new SqliteParameter("$error_message", (object?)entity.ErrorMessage ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$updated_at", entity.UpdatedAt));
        }, ct).ConfigureAwait(false);

        if (rowsAffected == 0)
        {
            Logger.LogWarning("Web fetch {WebFetchId} not found for update", entity.WebFetchId);
        }
        else
        {
            Logger.LogInformation("Updated web fetch {WebFetchId}", entity.WebFetchId);
        }
    }

    public override async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM web_fetches WHERE web_fetch_id = $id";

        var rowsAffected = await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$id", id));
        }, ct).ConfigureAwait(false);

        if (rowsAffected == 0)
        {
            Logger.LogWarning("Web fetch {WebFetchId} not found for deletion", id);
        }
        else
        {
            Logger.LogInformation("Deleted web fetch {WebFetchId}", id);
        }
    }

    /// <summary>
    /// Gets a web fetch by source URL.
    /// </summary>
    public async Task<WebFetch?> GetBySourceUrlAsync(string sourceUrl, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT web_fetch_id, doc_id, source_url, content_hash, fetch_date, title, description, status, error_message, created_at, updated_at
            FROM web_fetches
            WHERE source_url = $source_url
            LIMIT 1";

        var results = await ExecuteReaderAsync(sql, MapWebFetch, parameters =>
        {
            parameters.Add(new SqliteParameter("$source_url", sourceUrl));
        }, ct).ConfigureAwait(false);

        return results.FirstOrDefault();
    }

    /// <summary>
    /// Gets all web fetch records for a specific document.
    /// </summary>
    public async Task<IReadOnlyList<WebFetch>> GetByDocIdAsync(string docId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT web_fetch_id, doc_id, source_url, content_hash, fetch_date, title, description, status, error_message, created_at, updated_at
            FROM web_fetches
            WHERE doc_id = $doc_id
            ORDER BY fetch_date DESC";

        return await ExecuteReaderAsync(sql, MapWebFetch, parameters =>
        {
            parameters.Add(new SqliteParameter("$doc_id", docId));
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets web fetch records by status.
    /// </summary>
    public async Task<IReadOnlyList<WebFetch>> GetByStatusAsync(string status, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT web_fetch_id, doc_id, source_url, content_hash, fetch_date, title, description, status, error_message, created_at, updated_at
            FROM web_fetches
            WHERE status = $status
            ORDER BY fetch_date DESC";

        return await ExecuteReaderAsync(sql, MapWebFetch, parameters =>
        {
            parameters.Add(new SqliteParameter("$status", status));
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets web fetch records that were fetched before a certain date.
    /// </summary>
    public async Task<IReadOnlyList<WebFetch>> GetFetchedBeforeDateAsync(long beforeDate, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT web_fetch_id, doc_id, source_url, content_hash, fetch_date, title, description, status, error_message, created_at, updated_at
            FROM web_fetches
            WHERE fetch_date < $before_date
            ORDER BY fetch_date DESC";

        return await ExecuteReaderAsync(sql, MapWebFetch, parameters =>
        {
            parameters.Add(new SqliteParameter("$before_date", beforeDate));
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets web fetch records fetched after a certain date.
    /// </summary>
    public async Task<IReadOnlyList<WebFetch>> GetFetchedAfterDateAsync(long afterDate, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT web_fetch_id, doc_id, source_url, content_hash, fetch_date, title, description, status, error_message, created_at, updated_at
            FROM web_fetches
            WHERE fetch_date >= $after_date
            ORDER BY fetch_date DESC";

        return await ExecuteReaderAsync(sql, MapWebFetch, parameters =>
        {
            parameters.Add(new SqliteParameter("$after_date", afterDate));
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets web fetch records by content hash.
    /// </summary>
    public async Task<IReadOnlyList<WebFetch>> GetByContentHashAsync(string contentHash, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT web_fetch_id, doc_id, source_url, content_hash, fetch_date, title, description, status, error_message, created_at, updated_at
            FROM web_fetches
            WHERE content_hash = $content_hash
            ORDER BY fetch_date DESC";

        return await ExecuteReaderAsync(sql, MapWebFetch, parameters =>
        {
            parameters.Add(new SqliteParameter("$content_hash", contentHash));
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the most recent web fetch for a given source URL.
    /// </summary>
    public async Task<WebFetch?> GetMostRecentBySourceUrlAsync(string sourceUrl, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT web_fetch_id, doc_id, source_url, content_hash, fetch_date, title, description, status, error_message, created_at, updated_at
            FROM web_fetches
            WHERE source_url = $source_url
            ORDER BY fetch_date DESC
            LIMIT 1";

        var results = await ExecuteReaderAsync(sql, MapWebFetch, parameters =>
        {
            parameters.Add(new SqliteParameter("$source_url", sourceUrl));
        }, ct).ConfigureAwait(false);

        return results.FirstOrDefault();
    }

    /// <summary>
    /// Updates the status and error message for a web fetch.
    /// </summary>
    public async Task UpdateStatusAsync(string webFetchId, string status, string? errorMessage = null, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE web_fetches
            SET status = $status,
                error_message = $error_message,
                updated_at = $updated_at
            WHERE web_fetch_id = $web_fetch_id";

        var rowsAffected = await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$web_fetch_id", webFetchId));
            parameters.Add(new SqliteParameter("$status", status));
            parameters.Add(new SqliteParameter("$error_message", (object?)errorMessage ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$updated_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        }, ct).ConfigureAwait(false);

        if (rowsAffected == 0)
        {
            Logger.LogWarning("Web fetch {WebFetchId} not found for status update", webFetchId);
        }
        else
        {
            Logger.LogInformation("Updated web fetch {WebFetchId} status to {Status}", webFetchId, status);
        }
    }

    /// <summary>
    /// Updates content hash and fetch date for a web fetch (refetch operation).
    /// </summary>
    public async Task UpdateContentAsync(string webFetchId, string newContentHash, long newFetchDate, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE web_fetches
            SET content_hash = $content_hash,
                fetch_date = $fetch_date,
                status = 'active',
                error_message = NULL,
                updated_at = $updated_at
            WHERE web_fetch_id = $web_fetch_id";

        var rowsAffected = await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$web_fetch_id", webFetchId));
            parameters.Add(new SqliteParameter("$content_hash", newContentHash));
            parameters.Add(new SqliteParameter("$fetch_date", newFetchDate));
            parameters.Add(new SqliteParameter("$updated_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        }, ct).ConfigureAwait(false);

        if (rowsAffected == 0)
        {
            Logger.LogWarning("Web fetch {WebFetchId} not found for content update", webFetchId);
        }
        else
        {
            Logger.LogInformation("Updated web fetch {WebFetchId} content hash and fetch date", webFetchId);
        }
    }

    private static WebFetch MapWebFetch(SqliteDataReader reader)
    {
        return new WebFetch
        {
            WebFetchId = reader.GetString(0),
            DocId = reader.GetString(1),
            SourceUrl = reader.GetString(2),
            ContentHash = reader.GetString(3),
            FetchDate = reader.GetInt64(4),
            Title = reader.IsDBNull(5) ? null : reader.GetString(5),
            Description = reader.IsDBNull(6) ? null : reader.GetString(6),
            Status = reader.GetString(7),
            ErrorMessage = reader.IsDBNull(8) ? null : reader.GetString(8),
            CreatedAt = reader.GetInt64(9),
            UpdatedAt = reader.GetInt64(10)
        };
    }
}
