using Daiv3.Persistence.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Daiv3.Persistence.Repositories;

/// <summary>
/// Repository for managing GlossaryEntry entities.
/// Implements GLO-DATA-001: Glossary entries SHALL include term, definition, and related terms.
/// Provides CRUD operations and specialized queries for glossary management and search.
/// </summary>
public class GlossaryRepository : RepositoryBase<GlossaryEntry>, IGlossaryRepository
{
    public GlossaryRepository(IDatabaseContext databaseContext, ILogger<GlossaryRepository> logger)
        : base(databaseContext, logger)
    {
    }

    public override async Task<GlossaryEntry?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT glossary_id, term, definition, related_terms_json, category, created_at, updated_at, created_by, updated_by, notes
            FROM glossary
            WHERE glossary_id = $id";

        var results = await ExecuteReaderAsync(sql, MapGlossaryEntry, parameters =>
        {
            parameters.Add(new SqliteParameter("$id", id));
        }, ct).ConfigureAwait(false);

        return results.FirstOrDefault();
    }

    public override async Task<IReadOnlyList<GlossaryEntry>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT glossary_id, term, definition, related_terms_json, category, created_at, updated_at, created_by, updated_by, notes
            FROM glossary
            ORDER BY term ASC";

        return await ExecuteReaderAsync(sql, MapGlossaryEntry, null, ct).ConfigureAwait(false);
    }

    public override async Task<string> AddAsync(GlossaryEntry entity, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        const string sql = @"
            INSERT INTO glossary (glossary_id, term, definition, related_terms_json, category, created_at, updated_at, created_by, updated_by, notes)
            VALUES ($glossary_id, $term, $definition, $related_terms_json, $category, $created_at, $updated_at, $created_by, $updated_by, $notes)";

        await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$glossary_id", entity.GlossaryId));
            parameters.Add(new SqliteParameter("$term", entity.Term));
            parameters.Add(new SqliteParameter("$definition", entity.Definition));
            parameters.Add(new SqliteParameter("$related_terms_json", (object?)entity.RelatedTermsJson ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$category", (object?)entity.Category ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$created_at", entity.CreatedAt));
            parameters.Add(new SqliteParameter("$updated_at", entity.UpdatedAt));
            parameters.Add(new SqliteParameter("$created_by", (object?)entity.CreatedBy ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$updated_by", (object?)entity.UpdatedBy ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$notes", (object?)entity.Notes ?? DBNull.Value));
        }, ct).ConfigureAwait(false);

        Logger.LogInformation("Added glossary entry {GlossaryId} for term '{Term}'", entity.GlossaryId, entity.Term);
        return entity.GlossaryId;
    }

    public override async Task UpdateAsync(GlossaryEntry entity, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        const string sql = @"
            UPDATE glossary
            SET term = $term,
                definition = $definition,
                related_terms_json = $related_terms_json,
                category = $category,
                updated_at = $updated_at,
                updated_by = $updated_by,
                notes = $notes
            WHERE glossary_id = $glossary_id";

        var rowsAffected = await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$glossary_id", entity.GlossaryId));
            parameters.Add(new SqliteParameter("$term", entity.Term));
            parameters.Add(new SqliteParameter("$definition", entity.Definition));
            parameters.Add(new SqliteParameter("$related_terms_json", (object?)entity.RelatedTermsJson ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$category", (object?)entity.Category ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$updated_at", entity.UpdatedAt));
            parameters.Add(new SqliteParameter("$updated_by", (object?)entity.UpdatedBy ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$notes", (object?)entity.Notes ?? DBNull.Value));
        }, ct).ConfigureAwait(false);

        if (rowsAffected == 0)
        {
            Logger.LogWarning("Glossary entry {GlossaryId} not found for update", entity.GlossaryId);
        }
        else
        {
            Logger.LogInformation("Updated glossary entry {GlossaryId} for term '{Term}'", entity.GlossaryId, entity.Term);
        }
    }

    public override async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM glossary WHERE glossary_id = $id";

        var rowsAffected = await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$id", id));
        }, ct).ConfigureAwait(false);

        if (rowsAffected == 0)
        {
            Logger.LogWarning("Glossary entry {GlossaryId} not found for deletion", id);
        }
        else
        {
            Logger.LogInformation("Deleted glossary entry {GlossaryId}", id);
        }
    }

    /// <summary>
    /// Gets a glossary entry by term (case-insensitive).
    /// </summary>
    public async Task<GlossaryEntry?> GetByTermAsync(string term, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT glossary_id, term, definition, related_terms_json, category, created_at, updated_at, created_by, updated_by, notes
            FROM glossary
            WHERE LOWER(term) = LOWER($term)
            LIMIT 1";

        var results = await ExecuteReaderAsync(sql, MapGlossaryEntry, parameters =>
        {
            parameters.Add(new SqliteParameter("$term", term));
        }, ct).ConfigureAwait(false);

        return results.FirstOrDefault();
    }

    /// <summary>
    /// Gets all glossary entries by category.
    /// </summary>
    public async Task<IReadOnlyList<GlossaryEntry>> GetByCategoryAsync(string category, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT glossary_id, term, definition, related_terms_json, category, created_at, updated_at, created_by, updated_by, notes
            FROM glossary
            WHERE category = $category
            ORDER BY term ASC";

        return await ExecuteReaderAsync(sql, MapGlossaryEntry, parameters =>
        {
            parameters.Add(new SqliteParameter("$category", category));
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets all unique categories in the glossary.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetAllCategoriesAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT DISTINCT category
            FROM glossary
            WHERE category IS NOT NULL
            ORDER BY category ASC";

        using var connection = await DatabaseContext.GetConnectionAsync(ct).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        var categories = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            categories.Add(reader.GetString(0));
        }

        return categories;
    }

    /// <summary>
    /// Searches glossary entries by term prefix (case-insensitive).
    /// </summary>
    public async Task<IReadOnlyList<GlossaryEntry>> SearchByTermPrefixAsync(string termPrefix, int maxResults = 10, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT glossary_id, term, definition, related_terms_json, category, created_at, updated_at, created_by, updated_by, notes
            FROM glossary
            WHERE LOWER(term) LIKE LOWER($prefix)
            ORDER BY term ASC
            LIMIT $limit";

        return await ExecuteReaderAsync(sql, MapGlossaryEntry, parameters =>
        {
            parameters.Add(new SqliteParameter("$prefix", $"{termPrefix}%"));
            parameters.Add(new SqliteParameter("$limit", maxResults));
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Searches glossary entries by definition content (case-insensitive partial match).
    /// </summary>
    public async Task<IReadOnlyList<GlossaryEntry>> SearchDefinitionAsync(string searchText, int maxResults = 20, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT glossary_id, term, definition, related_terms_json, category, created_at, updated_at, created_by, updated_by, notes
            FROM glossary
            WHERE LOWER(definition) LIKE LOWER($search)
            ORDER BY term ASC
            LIMIT $limit";

        return await ExecuteReaderAsync(sql, MapGlossaryEntry, parameters =>
        {
            parameters.Add(new SqliteParameter("$search", $"%{searchText}%"));
            parameters.Add(new SqliteParameter("$limit", maxResults));
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the total number of glossary entries.
    /// </summary>
    public async Task<int> GetCountAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT COUNT(*) FROM glossary";

        using var connection = await DatabaseContext.GetConnectionAsync(ct).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        var result = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return result is long longValue ? (int)longValue : 0;
    }

    /// <summary>
    /// Gets the total number of glossary entries in a specific category.
    /// </summary>
    public async Task<int> GetCountByCategoryAsync(string category, CancellationToken ct = default)
    {
        const string sql = "SELECT COUNT(*) FROM glossary WHERE category = $category";

        using var connection = await DatabaseContext.GetConnectionAsync(ct).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add(new SqliteParameter("$category", category));

        var result = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return result is long longValue ? (int)longValue : 0;
    }

    /// <summary>
    /// Gets glossary entries modified within a date range.
    /// </summary>
    public async Task<IReadOnlyList<GlossaryEntry>> GetModifiedDateRangeAsync(long afterDate, long? beforeDate = null, CancellationToken ct = default)
    {
        string sql;
        if (beforeDate.HasValue)
        {
            sql = @"
                SELECT glossary_id, term, definition, related_terms_json, category, created_at, updated_at, created_by, updated_by, notes
                FROM glossary
                WHERE updated_at >= $after_date AND updated_at <= $before_date
                ORDER BY updated_at DESC";
        }
        else
        {
            sql = @"
                SELECT glossary_id, term, definition, related_terms_json, category, created_at, updated_at, created_by, updated_by, notes
                FROM glossary
                WHERE updated_at >= $after_date
                ORDER BY updated_at DESC";
        }

        return await ExecuteReaderAsync(sql, MapGlossaryEntry, parameters =>
        {
            parameters.Add(new SqliteParameter("$after_date", afterDate));
            if (beforeDate.HasValue)
            {
                parameters.Add(new SqliteParameter("$before_date", beforeDate.Value));
            }
        }, ct).ConfigureAwait(false);
    }

    private static GlossaryEntry MapGlossaryEntry(SqliteDataReader reader)
    {
        return new GlossaryEntry
        {
            GlossaryId = reader.GetString(0),
            Term = reader.GetString(1),
            Definition = reader.GetString(2),
            RelatedTermsJson = reader.IsDBNull(3) ? null : reader.GetString(3),
            Category = reader.IsDBNull(4) ? null : reader.GetString(4),
            CreatedAt = reader.GetInt64(5),
            UpdatedAt = reader.GetInt64(6),
            CreatedBy = reader.IsDBNull(7) ? null : reader.GetString(7),
            UpdatedBy = reader.IsDBNull(8) ? null : reader.GetString(8),
            Notes = reader.IsDBNull(9) ? null : reader.GetString(9)
        };
    }
}
