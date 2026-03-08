namespace Daiv3.Core.Enums;

/// <summary>
/// Supported lifecycle events for executable skills.
/// Implements ES-ACC-002 Phase 4: Skill audit trail.
/// </summary>
public enum SkillAuditEventType
{
    Created = 0,
    ApprovalRequested = 1,
    Approved = 2,
    Revoked = 3,
    Executed = 4,
    ExecutionDenied = 5,
    HashMismatch = 6,
    FileModified = 7
}
