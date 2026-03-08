using Daiv3.Persistence.Entities;

namespace Daiv3.Persistence.Repositories;

/// <summary>
/// Repository for managing ExecutableSkill entities with approval status tracking.
/// Implements ES-ACC-002 Phase 1: Foundation - Data Model + Hash Service.
/// </summary>
public interface IExecutableSkillRepository : IRepository<ExecutableSkill>
{
    /// <summary>
    /// Gets all skills with a specific approval status.
    /// </summary>
    /// <param name="approvalStatus">Approval status filter (e.g., "Approved", "PendingApproval", "Stale").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of skills matching the status.</returns>
    Task<IReadOnlyList<ExecutableSkill>> GetByApprovalStatusAsync(string approvalStatus, CancellationToken ct = default);

    /// <summary>
    /// Gets a skill by name (unique constraint on name).
    /// </summary>
    /// <param name="name">Skill name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The skill if found, null otherwise.</returns>
    Task<ExecutableSkill?> GetByNameAsync(string name, CancellationToken ct = default);

    /// <summary>
    /// Checks if a skill with the given name already exists.
    /// </summary>
    /// <param name="name">Skill name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if exists, false otherwise.</returns>
    Task<bool> ExistsByNameAsync(string name, CancellationToken ct = default);
}
