using Daiv3.Persistence.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Daiv3.Persistence.Repositories;

/// <summary>
/// Repository for executable skill lifecycle audit events.
/// Implements ES-ACC-002 Phase 4: Skill audit trail.
/// </summary>
public class SkillAuditLogRepository : RepositoryBase<SkillAuditLog>, ISkillAuditRepository
{
    public SkillAuditLogRepository(IDatabaseContext databaseContext, ILogger<SkillAuditLogRepository> logger)
        : base(databaseContext, logger)
    {
    }

    public override async Task<SkillAuditLog?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT audit_id, skill_id, event_type, actor_id, metadata_json, event_at
            FROM skill_audit_log
            WHERE audit_id = $id";

        var results = await ExecuteReaderAsync(sql, MapSkillAuditLog, parameters =>
        {
            parameters.Add(new SqliteParameter("$id", id));
        }, ct).ConfigureAwait(false);

        return results.FirstOrDefault();
    }

    public override async Task<IReadOnlyList<SkillAuditLog>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT audit_id, skill_id, event_type, actor_id, metadata_json, event_at
            FROM skill_audit_log
            ORDER BY event_at DESC";

        return await ExecuteReaderAsync(sql, MapSkillAuditLog, null, ct).ConfigureAwait(false);
    }

    public override async Task<string> AddAsync(SkillAuditLog entity, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.AuditId);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.SkillId);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.EventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.ActorId);

        const string sql = @"
            INSERT INTO skill_audit_log (audit_id, skill_id, event_type, actor_id, metadata_json, event_at)
            VALUES ($audit_id, $skill_id, $event_type, $actor_id, $metadata_json, $event_at)";

        await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$audit_id", entity.AuditId));
            parameters.Add(new SqliteParameter("$skill_id", entity.SkillId));
            parameters.Add(new SqliteParameter("$event_type", entity.EventType));
            parameters.Add(new SqliteParameter("$actor_id", entity.ActorId));
            parameters.Add(new SqliteParameter("$metadata_json", (object?)entity.MetadataJson ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$event_at", entity.EventAt));
        }, ct).ConfigureAwait(false);

        return entity.AuditId;
    }

    public override Task UpdateAsync(SkillAuditLog entity, CancellationToken ct = default)
    {
        throw new NotSupportedException("SkillAuditLog is immutable and cannot be updated.");
    }

    public override async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        const string sql = "DELETE FROM skill_audit_log WHERE audit_id = $id";

        await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$id", id));
        }, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SkillAuditLog>> GetBySkillIdAsync(string skillId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillId);

        const string sql = @"
            SELECT audit_id, skill_id, event_type, actor_id, metadata_json, event_at
            FROM skill_audit_log
            WHERE skill_id = $skill_id
            ORDER BY event_at DESC";

        return await ExecuteReaderAsync(sql, MapSkillAuditLog, parameters =>
        {
            parameters.Add(new SqliteParameter("$skill_id", skillId));
        }, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SkillAuditLog>> GetByEventTypeAsync(string eventType, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);

        const string sql = @"
            SELECT audit_id, skill_id, event_type, actor_id, metadata_json, event_at
            FROM skill_audit_log
            WHERE event_type = $event_type
            ORDER BY event_at DESC";

        return await ExecuteReaderAsync(sql, MapSkillAuditLog, parameters =>
        {
            parameters.Add(new SqliteParameter("$event_type", eventType));
        }, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SkillAuditLog>> QueryAsync(
        string? skillId = null,
        string? eventType = null,
        long? fromUnixSeconds = null,
        long? toUnixSeconds = null,
        CancellationToken ct = default)
    {
        var sql = new StringBuilder(@"
            SELECT audit_id, skill_id, event_type, actor_id, metadata_json, event_at
            FROM skill_audit_log
            WHERE 1=1");

        var queryValues = new Dictionary<string, object?>();

        if (!string.IsNullOrWhiteSpace(skillId))
        {
            sql.Append(" AND skill_id = $skill_id");
            queryValues["$skill_id"] = skillId;
        }

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            sql.Append(" AND event_type = $event_type");
            queryValues["$event_type"] = eventType;
        }

        if (fromUnixSeconds.HasValue)
        {
            sql.Append(" AND event_at >= $from_event_at");
            queryValues["$from_event_at"] = fromUnixSeconds.Value;
        }

        if (toUnixSeconds.HasValue)
        {
            sql.Append(" AND event_at <= $to_event_at");
            queryValues["$to_event_at"] = toUnixSeconds.Value;
        }

        sql.Append(" ORDER BY event_at DESC");

        return await ExecuteReaderAsync(sql.ToString(), MapSkillAuditLog, parameters =>
        {
            foreach (var pair in queryValues)
            {
                parameters.Add(new SqliteParameter(pair.Key, pair.Value ?? DBNull.Value));
            }
        }, ct).ConfigureAwait(false);
    }

    private static SkillAuditLog MapSkillAuditLog(SqliteDataReader reader)
    {
        return new SkillAuditLog
        {
            AuditId = reader.GetString(reader.GetOrdinal("audit_id")),
            SkillId = reader.GetString(reader.GetOrdinal("skill_id")),
            EventType = reader.GetString(reader.GetOrdinal("event_type")),
            ActorId = reader.GetString(reader.GetOrdinal("actor_id")),
            MetadataJson = reader.IsDBNull(reader.GetOrdinal("metadata_json"))
                ? null
                : reader.GetString(reader.GetOrdinal("metadata_json")),
            EventAt = reader.GetInt64(reader.GetOrdinal("event_at"))
        };
    }
}
