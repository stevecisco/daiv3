using Daiv3.Core.Authorization;
using Daiv3.Core.Enums;
using Daiv3.Orchestration.Services;
using Daiv3.Persistence.Entities;
using Daiv3.Persistence.Repositories;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

#pragma warning disable IDISP025 // Test class dispose pattern is sufficient for unit-test fixture cleanup.

namespace Daiv3.Orchestration.Tests;

/// <summary>
/// Unit tests for ExecutableSkillApprovalService.
/// Validates approval workflow, admin authorization, and state transitions.
/// </summary>
public class ExecutableSkillApprovalServiceTests
{
    private readonly Mock<IExecutableSkillRepository> _repositoryMock;
    private readonly Mock<ISkillHashService> _hashServiceMock;
    private readonly Mock<ILogger<ExecutableSkillApprovalService>> _loggerMock;
    private readonly ExecutableSkillApprovalService _service;

    public ExecutableSkillApprovalServiceTests()
    {
        _repositoryMock = new Mock<IExecutableSkillRepository>();
        _hashServiceMock = new Mock<ISkillHashService>();
        _loggerMock = new Mock<ILogger<ExecutableSkillApprovalService>>();
        _service = new ExecutableSkillApprovalService(_repositoryMock.Object, _hashServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task RequestApprovalAsync_SetsPendingApprovalStatus()
    {
        var skill = CreateSkill("TestSkill", ApprovalStatus.PendingApproval);
        skill.ApprovalStatus = "Stale"; // Start from Stale

        _repositoryMock.Setup(r => r.GetByIdAsync(skill.SkillId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(skill);

        var result = await _service.RequestApprovalAsync(skill.SkillId, "user123");

        Assert.Equal(ApprovalStatus.PendingApproval.ToString(), result.ApprovalStatus);
        _repositoryMock.Verify(r => r.UpdateAsync(It.IsAny<ExecutableSkill>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RequestApprovalAsync_AlreadyPending_NoUpdate()
    {
        var skill = CreateSkill("TestSkill", ApprovalStatus.PendingApproval);

        _repositoryMock.Setup(r => r.GetByIdAsync(skill.SkillId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(skill);

        var result = await _service.RequestApprovalAsync(skill.SkillId, "user123");

        Assert.Equal(ApprovalStatus.PendingApproval.ToString(), result.ApprovalStatus);
        _repositoryMock.Verify(r => r.UpdateAsync(It.IsAny<ExecutableSkill>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApproveSkillAsync_AdminCanApprove()
    {
        var skill = CreateSkill("TestSkill", ApprovalStatus.PendingApproval);
        _service.GrantRole("admin123", SystemRoles.SkillAdministrator);

        _repositoryMock.Setup(r => r.GetByIdAsync(skill.SkillId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(skill);
        _hashServiceMock.Setup(h => h.ValidateHashAsync(It.IsAny<ExecutableSkill>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.ApproveSkillAsync(skill.SkillId, "admin123");

        Assert.Equal(ApprovalStatus.Approved.ToString(), result.ApprovalStatus);
        Assert.Equal("admin123", result.ApprovedBy);
        Assert.NotNull(result.ApprovedAt);
        _repositoryMock.Verify(r => r.UpdateAsync(It.IsAny<ExecutableSkill>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApproveSkillAsync_NonAdminDenied()
    {
        var skill = CreateSkill("TestSkill", ApprovalStatus.PendingApproval);

        _repositoryMock.Setup(r => r.GetByIdAsync(skill.SkillId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(skill);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.ApproveSkillAsync(skill.SkillId, "regular-user"));

        _repositoryMock.Verify(r => r.UpdateAsync(It.IsAny<ExecutableSkill>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApproveSkillAsync_NotPendingApproval_ThrowsInvalidOperation()
    {
        var skill = CreateSkill("TestSkill", ApprovalStatus.Approved);
        _service.GrantRole("admin123", SystemRoles.SkillAdministrator);

        _repositoryMock.Setup(r => r.GetByIdAsync(skill.SkillId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(skill);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ApproveSkillAsync(skill.SkillId, "admin123"));
    }

    [Fact]
    public async Task ApproveSkillAsync_HashValidationFails_ThrowsInvalidOperation()
    {
        var skill = CreateSkill("TestSkill", ApprovalStatus.PendingApproval);
        _service.GrantRole("admin123", SystemRoles.SkillAdministrator);

        _repositoryMock.Setup(r => r.GetByIdAsync(skill.SkillId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(skill);
        _hashServiceMock.Setup(h => h.ValidateHashAsync(It.IsAny<ExecutableSkill>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ApproveSkillAsync(skill.SkillId, "admin123"));
    }

    [Fact]
    public async Task RevokeApprovalAsync_AdminCanRevoke()
    {
        var skill = CreateSkill("TestSkill", ApprovalStatus.Approved);
        _service.GrantRole("admin123", SystemRoles.SkillAdministrator);

        _repositoryMock.Setup(r => r.GetByIdAsync(skill.SkillId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(skill);

        var result = await _service.RevokeApprovalAsync(skill.SkillId, "admin123");

        Assert.Equal(ApprovalStatus.Revoked.ToString(), result.ApprovalStatus);
        _repositoryMock.Verify(r => r.UpdateAsync(It.IsAny<ExecutableSkill>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RevokeApprovalAsync_NonAdminDenied()
    {
        var skill = CreateSkill("TestSkill", ApprovalStatus.Approved);

        _repositoryMock.Setup(r => r.GetByIdAsync(skill.SkillId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(skill);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.RevokeApprovalAsync(skill.SkillId, "regular-user"));
    }

    [Fact]
    public async Task RevokeApprovalAsync_NotApproved_ThrowsInvalidOperation()
    {
        var skill = CreateSkill("TestSkill", ApprovalStatus.PendingApproval);
        _service.GrantRole("admin123", SystemRoles.SkillAdministrator);

        _repositoryMock.Setup(r => r.GetByIdAsync(skill.SkillId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(skill);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.RevokeApprovalAsync(skill.SkillId, "admin123"));
    }

    [Fact]
    public async Task ValidateAndUpdateStaleStatusAsync_HashMismatch_MarksSkillStale()
    {
        var skill = CreateSkill("TestSkill", ApprovalStatus.Approved);

        _repositoryMock.Setup(r => r.GetByIdAsync(skill.SkillId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(skill);
        _hashServiceMock.Setup(h => h.ValidateHashAsync(It.IsAny<ExecutableSkill>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.ValidateAndUpdateStaleStatusAsync(skill.SkillId);

        Assert.False(result);
        _repositoryMock.Verify(r => r.UpdateAsync(
            It.Is<ExecutableSkill>(s => s.ApprovalStatus == ApprovalStatus.Stale.ToString()),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ValidateAndUpdateStaleStatusAsync_HashValid_NoUpdate()
    {
        var skill = CreateSkill("TestSkill", ApprovalStatus.Approved);

        _repositoryMock.Setup(r => r.GetByIdAsync(skill.SkillId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(skill);
        _hashServiceMock.Setup(h => h.ValidateHashAsync(It.IsAny<ExecutableSkill>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.ValidateAndUpdateStaleStatusAsync(skill.SkillId);

        Assert.True(result);
        _repositoryMock.Verify(r => r.UpdateAsync(It.IsAny<ExecutableSkill>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetApprovalStatusAsync_ReturnsCurrentStatus()
    {
        var skill = CreateSkill("TestSkill", ApprovalStatus.Approved);

        _repositoryMock.Setup(r => r.GetByIdAsync(skill.SkillId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(skill);

        var result = await _service.GetApprovalStatusAsync(skill.SkillId);

        Assert.Equal(ApprovalStatus.Approved, result);
    }

    [Fact]
    public async Task IsAdministratorAsync_SystemPrincipal_ReturnsTrue()
    {
        var result = await _service.IsAdministratorAsync("system");

        Assert.True(result);
    }

    [Fact]
    public async Task IsAdministratorAsync_RegularUser_ReturnsFalse()
    {
        var result = await _service.IsAdministratorAsync("user123");

        Assert.False(result);
    }

    [Fact]
    public void GrantRole_AddsRoleToPrincipal()
    {
        _service.GrantRole("user123", SystemRoles.SkillAdministrator);

        var result = _service.IsAdministratorAsync("user123").Result;

        Assert.True(result);
    }

    [Fact]
    public void RevokeRole_RemovesRoleFromPrincipal()
    {
        _service.GrantRole("user123", SystemRoles.SkillAdministrator);
        _service.RevokeRole("user123", SystemRoles.SkillAdministrator);

        var result = _service.IsAdministratorAsync("user123").Result;

        Assert.False(result);
    }

    private static ExecutableSkill CreateSkill(string name, ApprovalStatus status)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        return new ExecutableSkill
        {
            SkillId = Guid.NewGuid().ToString(),
            Name = name,
            FilePath = $"C:/skills/{name}.cs",
            FileHash = $"HASH_{Guid.NewGuid():N}",
            MetadataPath = $"C:/skills/{name}.md",
            ApprovalStatus = status.ToString(),
            CreatedBy = "test-user",
            CreatedAt = now,
            LastModifiedAt = now
        };
    }
}
