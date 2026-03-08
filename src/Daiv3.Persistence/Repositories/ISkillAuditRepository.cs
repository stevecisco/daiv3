using Daiv3.Persistence.Entities;

namespace Daiv3.Persistence.Repositories;

/// <summary>
/// Repository for persisting and querying executable skill lifecycle audit events.
/// Implements ES-ACC-002 Phase 4: Skill audit trail.
/// </summary>
public interface ISkillAuditRepository : IRepository<SkillAuditLog>
{
    /// <summary>
    /// Gets audit events for a specific skill ordered by newest first.
    /// </summary>
    Task<IReadOnlyList<SkillAuditLog>> GetBySkillIdAsync(string skillId, CancellationToken ct = default);

    /// <summary>
    /// Gets audit events for a specific event type ordered by newest first.
    /// </summary>
    Task<IReadOnlyList<SkillAuditLog>> GetByEventTypeAsync(string eventType, CancellationToken ct = default);

    /// <summary>
    /// Queries audit events by optional filters.
    /// </summary>
    Task<IReadOnlyList<SkillAuditLog>> QueryAsync(
        string? skillId = null,
        string? eventType = null,
        long? fromUnixSeconds = null,
        long? toUnixSeconds = null,
        CancellationToken ct = default);
}
