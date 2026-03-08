using Daiv3.Core.Enums;
using Daiv3.Persistence.Entities;

namespace Daiv3.Orchestration.Services;

/// <summary>
/// Service for managing executable skill approval workflow with administrative gates.
/// Implements ES-ACC-002 Phase 2: Approval Workflow + Authorization.
/// </summary>
public interface IExecutableSkillApprovalService
{
    /// <summary>
    /// Requests approval for an executable skill.
    /// Sets status to PendingApproval if not already in that state.
    /// </summary>
    /// <param name="skillId">The skill ID to request approval for.</param>
    /// <param name="requestorId">The user or agent requesting approval.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated skill entity.</returns>
    /// <exception cref="InvalidOperationException">Thrown if skill not found.</exception>
    Task<ExecutableSkill> RequestApprovalAsync(string skillId, string requestorId, CancellationToken ct = default);

    /// <summary>
    /// Approves an executable skill. Only administrators with SkillAdministrator role can approve.
    /// Transitions skill from PendingApproval to Approved.
    /// </summary>
    /// <param name="skillId">The skill ID to approve.</param>
    /// <param name="approverAdminId">The administrator approving the skill (must have SkillAdministrator role).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The approved skill entity.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown if approver does not have SkillAdministrator role.</exception>
    /// <exception cref="InvalidOperationException">Thrown if skill not found or not in PendingApproval status.</exception>
    Task<ExecutableSkill> ApproveSkillAsync(string skillId, string approverAdminId, CancellationToken ct = default);

    /// <summary>
    /// Revokes approval for an executable skill. Only administrators can revoke.
    /// Transitions skill from Approved to Revoked.
    /// </summary>
    /// <param name="skillId">The skill ID to revoke.</param>
    /// <param name="adminId">The administrator revoking the skill.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The revoked skill entity.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown if revoker does not have SkillAdministrator role.</exception>
    /// <exception cref="InvalidOperationException">Thrown if skill not found or not in Approved status.</exception>
    Task<ExecutableSkill> RevokeApprovalAsync(string skillId, string adminId, CancellationToken ct = default);

    /// <summary>
    /// Gets the current approval status of a skill.
    /// </summary>
    /// <param name="skillId">The skill ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The approval status enum value.</returns>
    /// <exception cref="InvalidOperationException">Thrown if skill not found.</exception>
    Task<ApprovalStatus> GetApprovalStatusAsync(string skillId, CancellationToken ct = default);

    /// <summary>
    /// Validates that the skill file hash matches the stored hash.
    /// If hash mismatch detected and skill is Approved, automatically transitions to Stale.
    /// </summary>
    /// <param name="skillId">The skill ID to validate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if hash valid, false if mismatch (status auto-updated to Stale if was Approved).</returns>
    /// <exception cref="InvalidOperationException">Thrown if skill not found.</exception>
    Task<bool> ValidateAndUpdateStaleStatusAsync(string skillId, CancellationToken ct = default);

    /// <summary>
    /// Checks if a principal has the SkillAdministrator role.
    /// </summary>
    /// <param name="principalId">The user or principal ID to check.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if principal has admin role, false otherwise.</returns>
    Task<bool> IsAdministratorAsync(string principalId, CancellationToken ct = default);
}
