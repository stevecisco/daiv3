using Daiv3.Core.Authorization;
using Daiv3.Core.Enums;
using Daiv3.Persistence.Entities;
using Daiv3.Persistence.Repositories;
using Microsoft.Extensions.Logging;

namespace Daiv3.Orchestration.Services;

/// <summary>
/// Default implementation of IExecutableSkillApprovalService.
/// Implements ES-ACC-002 Phase 2: Approval Workflow + Authorization.
/// </summary>
public class ExecutableSkillApprovalService : IExecutableSkillApprovalService
{
    private readonly IExecutableSkillRepository _repository;
    private readonly ISkillHashService _hashService;
    private readonly ILogger<ExecutableSkillApprovalService> _logger;

    // Simple in-memory role store for Phase 2
    // TODO: Replace with proper identity/authorization service in future phase
    private readonly Dictionary<string, List<string>> _principalRoles = new(StringComparer.OrdinalIgnoreCase);

    public ExecutableSkillApprovalService(
        IExecutableSkillRepository repository,
        ISkillHashService hashService,
        ILogger<ExecutableSkillApprovalService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _hashService = hashService ?? throw new ArgumentNullException(nameof(hashService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Initialize with system principal having admin role
        _principalRoles["system"] = new List<string> { SystemRoles.System, SystemRoles.SkillAdministrator };
    }

    public async Task<ExecutableSkill> RequestApprovalAsync(string skillId, string requestorId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillId, nameof(skillId));
        ArgumentException.ThrowIfNullOrWhiteSpace(requestorId, nameof(requestorId));

        var skill = await _repository.GetByIdAsync(skillId, ct).ConfigureAwait(false);
        if (skill == null)
        {
            throw new InvalidOperationException($"Skill not found: {skillId}");
        }

        if (skill.ApprovalStatus == ApprovalStatus.PendingApproval.ToString())
        {
            _logger.LogInformation("Skill {SkillId} '{SkillName}' is already pending approval", skillId, skill.Name);
            return skill;
        }

        skill.ApprovalStatus = ApprovalStatus.PendingApproval.ToString();
        skill.LastModifiedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await _repository.UpdateAsync(skill, ct).ConfigureAwait(false);

        _logger.LogInformation("Approval requested for skill {SkillId} '{SkillName}' by {RequestorId}",
            skillId, skill.Name, requestorId);

        return skill;
    }

    public async Task<ExecutableSkill> ApproveSkillAsync(string skillId, string approverAdminId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillId, nameof(skillId));
        ArgumentException.ThrowIfNullOrWhiteSpace(approverAdminId, nameof(approverAdminId));

        // Validate administrator role
        if (!await IsAdministratorAsync(approverAdminId, ct).ConfigureAwait(false))
        {
            _logger.LogWarning("Approval attempt denied: {ApproverId} does not have SkillAdministrator role", approverAdminId);
            throw new UnauthorizedAccessException($"Principal {approverAdminId} does not have SkillAdministrator role");
        }

        var skill = await _repository.GetByIdAsync(skillId, ct).ConfigureAwait(false);
        if (skill == null)
        {
            throw new InvalidOperationException($"Skill not found: {skillId}");
        }

        if (skill.ApprovalStatus != ApprovalStatus.PendingApproval.ToString())
        {
            throw new InvalidOperationException(
                $"Skill {skillId} cannot be approved: current status is {skill.ApprovalStatus}, expected PendingApproval");
        }

        // Validate hash before approval
        var hashValid = await _hashService.ValidateHashAsync(skill, ct).ConfigureAwait(false);
        if (!hashValid)
        {
            _logger.LogError("Cannot approve skill {SkillId}: file hash validation failed", skillId);
            throw new InvalidOperationException(
                $"Cannot approve skill {skillId}: file hash validation failed. File may have been modified.");
        }

        skill.ApprovalStatus = ApprovalStatus.Approved.ToString();
        skill.ApprovedBy = approverAdminId;
        skill.ApprovedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        skill.LastModifiedAt = skill.ApprovedAt.Value;

        await _repository.UpdateAsync(skill, ct).ConfigureAwait(false);

        _logger.LogInformation("Skill {SkillId} '{SkillName}' approved by {ApproverId}",
            skillId, skill.Name, approverAdminId);

        return skill;
    }

    public async Task<ExecutableSkill> RevokeApprovalAsync(string skillId, string adminId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillId, nameof(skillId));
        ArgumentException.ThrowIfNullOrWhiteSpace(adminId, nameof(adminId));

        // Validate administrator role
        if (!await IsAdministratorAsync(adminId, ct).ConfigureAwait(false))
        {
            _logger.LogWarning("Revoke attempt denied: {AdminId} does not have SkillAdministrator role", adminId);
            throw new UnauthorizedAccessException($"Principal {adminId} does not have SkillAdministrator role");
        }

        var skill = await _repository.GetByIdAsync(skillId, ct).ConfigureAwait(false);
        if (skill == null)
        {
            throw new InvalidOperationException($"Skill not found: {skillId}");
        }

        if (skill.ApprovalStatus != ApprovalStatus.Approved.ToString())
        {
            throw new InvalidOperationException(
                $"Skill {skillId} cannot be revoked: current status is {skill.ApprovalStatus}, expected Approved");
        }

        skill.ApprovalStatus = ApprovalStatus.Revoked.ToString();
        skill.LastModifiedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await _repository.UpdateAsync(skill, ct).ConfigureAwait(false);

        _logger.LogWarning("Skill {SkillId} '{SkillName}' revoked by {AdminId}",
            skillId, skill.Name, adminId);

        return skill;
    }

    public async Task<ApprovalStatus> GetApprovalStatusAsync(string skillId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillId, nameof(skillId));

        var skill = await _repository.GetByIdAsync(skillId, ct).ConfigureAwait(false);
        if (skill == null)
        {
            throw new InvalidOperationException($"Skill not found: {skillId}");
        }

        return Enum.Parse<ApprovalStatus>(skill.ApprovalStatus);
    }

    public async Task<bool> ValidateAndUpdateStaleStatusAsync(string skillId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillId, nameof(skillId));

        var skill = await _repository.GetByIdAsync(skillId, ct).ConfigureAwait(false);
        if (skill == null)
        {
            throw new InvalidOperationException($"Skill not found: {skillId}");
        }

        var hashValid = await _hashService.ValidateHashAsync(skill, ct).ConfigureAwait(false);

        if (!hashValid && skill.ApprovalStatus == ApprovalStatus.Approved.ToString())
        {
            _logger.LogWarning("Hash mismatch detected for approved skill {SkillId}: marking as Stale", skillId);

            skill.ApprovalStatus = ApprovalStatus.Stale.ToString();
            skill.LastModifiedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            await _repository.UpdateAsync(skill, ct).ConfigureAwait(false);

            _logger.LogInformation("Skill {SkillId} '{SkillName}' marked as Stale due to file modification",
                skillId, skill.Name);
        }

        return hashValid;
    }

    public Task<bool> IsAdministratorAsync(string principalId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(principalId, nameof(principalId));

        if (_principalRoles.TryGetValue(principalId, out var roles))
        {
            return Task.FromResult(roles.Contains(SystemRoles.SkillAdministrator, StringComparer.OrdinalIgnoreCase));
        }

        return Task.FromResult(false);
    }

    /// <summary>
    /// Grants a role to a principal. For testing/admin purposes.
    /// TODO: Replace with proper identity management in future phase.
    /// </summary>
    public void GrantRole(string principalId, string role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(principalId, nameof(principalId));
        ArgumentException.ThrowIfNullOrWhiteSpace(role, nameof(role));

        if (!_principalRoles.ContainsKey(principalId))
        {
            _principalRoles[principalId] = new List<string>();
        }

        if (!_principalRoles[principalId].Contains(role, StringComparer.OrdinalIgnoreCase))
        {
            _principalRoles[principalId].Add(role);
            _logger.LogInformation("Granted role {Role} to principal {PrincipalId}", role, principalId);
        }
    }

    /// <summary>
    /// Revokes a role from a principal. For testing/admin purposes.
    /// TODO: Replace with proper identity management in future phase.
    /// </summary>
    public void RevokeRole(string principalId, string role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(principalId, nameof(principalId));
        ArgumentException.ThrowIfNullOrWhiteSpace(role, nameof(role));

        if (_principalRoles.TryGetValue(principalId, out var roles))
        {
            roles.RemoveAll(r => r.Equals(role, StringComparison.OrdinalIgnoreCase));
            _logger.LogInformation("Revoked role {Role} from principal {PrincipalId}", role, principalId);
        }
    }
}
