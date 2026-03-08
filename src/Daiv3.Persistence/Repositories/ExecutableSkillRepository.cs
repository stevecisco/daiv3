using Daiv3.Persistence.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Daiv3.Persistence.Repositories;

/// <summary>
/// Repository for managing ExecutableSkill entities with approval status tracking.
/// Implements ES-ACC-002 Phase 1: Foundation - Data Model + Hash Service.
/// </summary>
public class ExecutableSkillRepository : RepositoryBase<ExecutableSkill>, IExecutableSkillRepository
{
    public ExecutableSkillRepository(IDatabaseContext databaseContext, ILogger<ExecutableSkillRepository> logger)
        : base(databaseContext, logger)
    {
    }

    public override async Task<ExecutableSkill?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT skill_id, name, file_path, file_hash, metadata_path, approval_status,
                   created_by, created_at, approved_by, approved_at, last_modified_at
            FROM executable_skills
            WHERE skill_id = $id";

        var results = await ExecuteReaderAsync(sql, MapExecutableSkill, parameters =>
        {
            parameters.Add(new SqliteParameter("$id", id));
        }, ct).ConfigureAwait(false);

        return results.FirstOrDefault();
    }

    public override async Task<IReadOnlyList<ExecutableSkill>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT skill_id, name, file_path, file_hash, metadata_path, approval_status,
                   created_by, created_at, approved_by, approved_at, last_modified_at
            FROM executable_skills
            ORDER BY created_at DESC";

        return await ExecuteReaderAsync(sql, MapExecutableSkill, null, ct).ConfigureAwait(false);
    }

    public override async Task<string> AddAsync(ExecutableSkill entity, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.SkillId);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.FilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.FileHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.ApprovalStatus);

        const string sql = @"
            INSERT INTO executable_skills (
                skill_id, name, file_path, file_hash, metadata_path, approval_status,
                created_by, created_at, approved_by, approved_at, last_modified_at
            )
            VALUES (
                $skill_id, $name, $file_path, $file_hash, $metadata_path, $approval_status,
                $created_by, $created_at, $approved_by, $approved_at, $last_modified_at
            )";

        await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$skill_id", entity.SkillId));
            parameters.Add(new SqliteParameter("$name", entity.Name));
            parameters.Add(new SqliteParameter("$file_path", entity.FilePath));
            parameters.Add(new SqliteParameter("$file_hash", entity.FileHash));
            parameters.Add(new SqliteParameter("$metadata_path", entity.MetadataPath));
            parameters.Add(new SqliteParameter("$approval_status", entity.ApprovalStatus));
            parameters.Add(new SqliteParameter("$created_by", (object?)entity.CreatedBy ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$created_at", entity.CreatedAt));
            parameters.Add(new SqliteParameter("$approved_by", (object?)entity.ApprovedBy ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$approved_at", entity.ApprovedAt.HasValue ? entity.ApprovedAt.Value : DBNull.Value));
            parameters.Add(new SqliteParameter("$last_modified_at", entity.LastModifiedAt));
        }, ct).ConfigureAwait(false);

        Logger.LogInformation("Added executable skill {SkillId} '{SkillName}' (status: {ApprovalStatus})",
            entity.SkillId, entity.Name, entity.ApprovalStatus);
        return entity.SkillId;
    }

    public override async Task UpdateAsync(ExecutableSkill entity, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.SkillId);

        const string sql = @"
            UPDATE executable_skills
            SET name = $name,
                file_path = $file_path,
                file_hash = $file_hash,
                metadata_path = $metadata_path,
                approval_status = $approval_status,
                approved_by = $approved_by,
                approved_at = $approved_at,
                last_modified_at = $last_modified_at
            WHERE skill_id = $skill_id";

        var rowsAffected = await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$skill_id", entity.SkillId));
            parameters.Add(new SqliteParameter("$name", entity.Name));
            parameters.Add(new SqliteParameter("$file_path", entity.FilePath));
            parameters.Add(new SqliteParameter("$file_hash", entity.FileHash));
            parameters.Add(new SqliteParameter("$metadata_path", entity.MetadataPath));
            parameters.Add(new SqliteParameter("$approval_status", entity.ApprovalStatus));
            parameters.Add(new SqliteParameter("$approved_by", (object?)entity.ApprovedBy ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$approved_at", entity.ApprovedAt.HasValue ? entity.ApprovedAt.Value : DBNull.Value));
            parameters.Add(new SqliteParameter("$last_modified_at", entity.LastModifiedAt));
        }, ct).ConfigureAwait(false);

        if (rowsAffected > 0)
        {
            Logger.LogInformation("Updated executable skill {SkillId} '{SkillName}' (status: {ApprovalStatus})",
                entity.SkillId, entity.Name, entity.ApprovalStatus);
        }
    }

    public override async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        const string sql = "DELETE FROM executable_skills WHERE skill_id = $id";

        var rowsAffected = await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$id", id));
        }, ct).ConfigureAwait(false);

        if (rowsAffected > 0)
        {
            Logger.LogInformation("Deleted executable skill {SkillId}", id);
        }
    }

    public async Task<IReadOnlyList<ExecutableSkill>> GetByApprovalStatusAsync(string approvalStatus, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(approvalStatus);

        const string sql = @"
            SELECT skill_id, name, file_path, file_hash, metadata_path, approval_status,
                   created_by, created_at, approved_by, approved_at, last_modified_at
            FROM executable_skills
            WHERE approval_status = $approval_status
            ORDER BY created_at DESC";

        return await ExecuteReaderAsync(sql, MapExecutableSkill, parameters =>
        {
            parameters.Add(new SqliteParameter("$approval_status", approvalStatus));
        }, ct).ConfigureAwait(false);
    }

    public async Task<ExecutableSkill?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        const string sql = @"
            SELECT skill_id, name, file_path, file_hash, metadata_path, approval_status,
                   created_by, created_at, approved_by, approved_at, last_modified_at
            FROM executable_skills
            WHERE name = $name";

        var results = await ExecuteReaderAsync(sql, MapExecutableSkill, parameters =>
        {
            parameters.Add(new SqliteParameter("$name", name));
        }, ct).ConfigureAwait(false);

        return results.FirstOrDefault();
    }

    public async Task<bool> ExistsByNameAsync(string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        const string sql = "SELECT COUNT(*) FROM executable_skills WHERE name = $name";

        await using var connection = await DatabaseContext.GetConnectionAsync(ct).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add(new SqliteParameter("$name", name));

        var result = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return Convert.ToInt64(result) > 0;
    }

    private static ExecutableSkill MapExecutableSkill(SqliteDataReader reader)
    {
        return new ExecutableSkill
        {
            SkillId = reader.GetString(reader.GetOrdinal("skill_id")),
            Name = reader.GetString(reader.GetOrdinal("name")),
            FilePath = reader.GetString(reader.GetOrdinal("file_path")),
            FileHash = reader.GetString(reader.GetOrdinal("file_hash")),
            MetadataPath = reader.GetString(reader.GetOrdinal("metadata_path")),
            ApprovalStatus = reader.GetString(reader.GetOrdinal("approval_status")),
            CreatedBy = reader.IsDBNull(reader.GetOrdinal("created_by")) ? null : reader.GetString(reader.GetOrdinal("created_by")),
            CreatedAt = reader.GetInt64(reader.GetOrdinal("created_at")),
            ApprovedBy = reader.IsDBNull(reader.GetOrdinal("approved_by")) ? null : reader.GetString(reader.GetOrdinal("approved_by")),
            ApprovedAt = reader.IsDBNull(reader.GetOrdinal("approved_at")) ? null : reader.GetInt64(reader.GetOrdinal("approved_at")),
            LastModifiedAt = reader.GetInt64(reader.GetOrdinal("last_modified_at"))
        };
    }
}
