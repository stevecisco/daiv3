using Daiv3.Persistence.Entities;

namespace Daiv3.Orchestration.Services;

/// <summary>
/// Service for writing and querying skill lifecycle audit events.
/// Implements ES-ACC-002 Phase 4: Skill creator integration + audit trail.
/// </summary>
public interface ISkillAuditService
{
    /// <summary>
    /// Logs a skill lifecycle event.
    /// </summary>
    Task LogEventAsync(
        string skillId,
        string eventType,
        string actorId,
        IDictionary<string, string>? metadata = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets full audit trail for a skill ordered by newest first.
    /// </summary>
    Task<IReadOnlyList<SkillAuditLog>> GetSkillAuditTrailAsync(string skillId, CancellationToken ct = default);

    /// <summary>
    /// Queries audit events by optional filters.
    /// </summary>
    Task<IReadOnlyList<SkillAuditLog>> QueryAuditEventsAsync(
        string? skillId = null,
        string? eventType = null,
        long? fromUnixSeconds = null,
        long? toUnixSeconds = null,
        CancellationToken ct = default);
}
