namespace Daiv3.Core.Enums;

/// <summary>
/// Approval status for executable skills requiring administrative gate before execution.
/// Implements ES-ACC-002 Phase 1: Administrative approval workflow.
/// </summary>
public enum ApprovalStatus
{
    /// <summary>
    /// Skill has been created but not yet submitted for approval.
    /// </summary>
    PendingApproval = 0,

    /// <summary>
    /// Skill has been approved by an administrator and can be executed.
    /// </summary>
    Approved = 1,

    /// <summary>
    /// Previously approved skill has been revoked (manually by admin or due to policy violation).
    /// Cannot execute until re-approved.
    /// </summary>
    Revoked = 2,

    /// <summary>
    /// Skill file or metadata has been modified since last approval.
    /// Hash mismatch detected - requires re-approval before execution.
    /// </summary>
    Stale = 3
}
