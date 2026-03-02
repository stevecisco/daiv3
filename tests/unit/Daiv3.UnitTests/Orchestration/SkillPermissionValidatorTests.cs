using Daiv3.Orchestration;
using Daiv3.Orchestration.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Daiv3.UnitTests.Orchestration;

/// <summary>
/// Unit tests for SkillPermissionValidator.
/// Tests permission enforcement, wildcard matching, and sandbox configuration.
/// </summary>
public class SkillPermissionValidatorTests
{
    private readonly Mock<ILogger<SkillPermissionValidator>> _loggerMock;
    private readonly SkillSandboxConfiguration _sandboxConfig;
    private readonly SkillPermissionValidator _validator;

    public SkillPermissionValidatorTests()
    {
        _loggerMock = new Mock<ILogger<SkillPermissionValidator>>();
        _sandboxConfig = new SkillSandboxConfiguration();
        _validator = new SkillPermissionValidator(
            _loggerMock.Object,
            Options.Create(_sandboxConfig));
    }

    #region Basic Permission Tests

    [Fact]
    public void ValidatePermissions_SkillWithNoPermissions_AllowsIfUntrustedAllowed()
    {
        // Arrange
        _sandboxConfig.AllowUntrustedSkills = true;
        var skill = new TestSkill("NoPermsSkill", new List<string>());

        // Act
        var result = _validator.ValidatePermissions(skill, "NoPermsSkill");

        // Assert
        Assert.True(result.IsAllowed);
        Assert.Empty(result.DeniedPermissions);
    }

    [Fact]
    public void ValidatePermissions_SkillWithNoPermissions_BlocksIfUntrustedNotAllowed()
    {
        // Arrange
        _sandboxConfig.AllowUntrustedSkills = false;
        var skill = new TestSkill("NoPermsSkill", new List<string>());

        // Act
        var result = _validator.ValidatePermissions(skill, "NoPermsSkill");

        // Assert
        Assert.False(result.IsAllowed);
        Assert.Contains("untrusted skills", result.DenialReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidatePermissions_EmptyAllowList_AllowsAllPermissions()
    {
        // Arrange - empty allow list means all permissions allowed
        var skill = new TestSkill("FileSkill", new List<string>
        {
            SkillPermissions.FileSystemRead,
            SkillPermissions.FileSystemWrite,
            SkillPermissions.NetworkAccess
        });

        // Act
        var result = _validator.ValidatePermissions(skill, "FileSkill");

        // Assert
        Assert.True(result.IsAllowed);
        Assert.Empty(result.DeniedPermissions);
    }

    [Fact]
    public void ValidatePermissions_DenyListBlocksPermission()
    {
        // Arrange
        _sandboxConfig.GlobalDeniedPermissions.Add(SkillPermissions.FileSystemWrite);
        var skill = new TestSkill("WriteSkill", new List<string>
        {
            SkillPermissions.FileSystemRead,
            SkillPermissions.FileSystemWrite
        });

        // Act
        var result = _validator.ValidatePermissions(skill, "WriteSkill");

        // Assert
        Assert.False(result.IsAllowed);
        Assert.Single(result.DeniedPermissions);
        Assert.Contains(SkillPermissions.FileSystemWrite, result.DeniedPermissions);
    }

    #endregion

    #region Wildcard Permission Tests

    [Fact]
    public void ValidatePermissions_WildcardAllowsAllPermissions()
    {
        // Arrange
        _sandboxConfig.GlobalAllowedPermissions.Add(SkillPermissions.All);
        var skill = new TestSkill("AdminSkill", new List<string>
        {
            SkillPermissions.FileSystemRead,
            SkillPermissions.RegistryWrite,
            SkillPermissions.SystemConfiguration
        });

        // Act
        var result = _validator.ValidatePermissions(skill, "AdminSkill");

        // Assert
        Assert.True(result.IsAllowed);
        Assert.Empty(result.DeniedPermissions);
    }

    [Fact]
    public void ValidatePermissions_HierarchicalWildcard_MatchesSubPermissions()
    {
        // Arrange
        _sandboxConfig.GlobalAllowedPermissions.Add(SkillPermissions.FileSystemAll);
        var skill = new TestSkill("FileSkill", new List<string>
        {
            SkillPermissions.FileSystemRead,
            SkillPermissions.FileSystemWrite
        });

        // Act
        var result = _validator.ValidatePermissions(skill, "FileSkill");

        // Assert
        Assert.True(result.IsAllowed);
        Assert.Empty(result.DeniedPermissions);
    }

    [Fact]
    public void ValidatePermissions_HierarchicalWildcard_BlocksNonMatchingPermissions()
    {
        // Arrange
        _sandboxConfig.GlobalAllowedPermissions.Add(SkillPermissions.FileSystemAll);
        var skill = new TestSkill("MixedSkill", new List<string>
        {
            SkillPermissions.FileSystemRead,
            SkillPermissions.NetworkAccess // This should be blocked
        });

        // Act
        var result = _validator.ValidatePermissions(skill, "MixedSkill");

        // Assert
        Assert.False(result.IsAllowed);
        Assert.Single(result.DeniedPermissions);
        Assert.Contains(SkillPermissions.NetworkAccess, result.DeniedPermissions);
    }

    [Fact]
    public void ValidatePermissions_WildcardDenyBlocksSubPermissions()
    {
        // Arrange
        _sandboxConfig.GlobalDeniedPermissions.Add(SkillPermissions.FileSystemAll);
        var skill = new TestSkill("FileSkill", new List<string>
        {
            SkillPermissions.FileSystemRead,
            SkillPermissions.FileSystemWrite
        });

        // Act
        var result = _validator.ValidatePermissions(skill, "FileSkill");

        // Assert
        Assert.False(result.IsAllowed);
        Assert.Equal(2, result.DeniedPermissions.Count);
        Assert.Contains(SkillPermissions.FileSystemRead, result.DeniedPermissions);
        Assert.Contains(SkillPermissions.FileSystemWrite, result.DeniedPermissions);
    }

    #endregion

    #region Per-Skill Override Tests

    [Fact]
    public void ValidatePermissions_SkillOverride_AllowsSpecificPermissions()
    {
        // Arrange
        _sandboxConfig.GlobalAllowedPermissions.Add(SkillPermissions.FileSystemRead);
        _sandboxConfig.SkillOverrides["SpecialSkill"] = new SkillSandboxOverride
        {
            AllowedPermissions = new List<string>
            {
                SkillPermissions.FileSystemRead,
                SkillPermissions.FileSystemWrite,
                SkillPermissions.DatabaseWrite
            }
        };

        var skill = new TestSkill("SpecialSkill", new List<string>
        {
            SkillPermissions.FileSystemWrite,
            SkillPermissions.DatabaseWrite
        });

        // Act
        var result = _validator.ValidatePermissions(skill, "SpecialSkill");

        // Assert
        Assert.True(result.IsAllowed);
        Assert.Empty(result.DeniedPermissions);
    }

    [Fact]
    public void ValidatePermissions_SkillOverride_DeniedPermissionsAdditive()
    {
        // Arrange
        _sandboxConfig.GlobalDeniedPermissions.Add(SkillPermissions.RegistryWrite);
        _sandboxConfig.SkillOverrides["RestrictedSkill"] = new SkillSandboxOverride
        {
            DeniedPermissions = new List<string> { SkillPermissions.FileSystemWrite }
        };

        var skill = new TestSkill("RestrictedSkill", new List<string>
        {
            SkillPermissions.RegistryWrite,
            SkillPermissions.FileSystemWrite
        });

        // Act
        var result = _validator.ValidatePermissions(skill, "RestrictedSkill");

        // Assert
        Assert.False(result.IsAllowed);
        Assert.Equal(2, result.DeniedPermissions.Count);
        Assert.Contains(SkillPermissions.RegistryWrite, result.DeniedPermissions);
        Assert.Contains(SkillPermissions.FileSystemWrite, result.DeniedPermissions);
    }

    #endregion

    #region Deny Takes Precedence Tests

    [Fact]
    public void ValidatePermissions_DenyTakesPrecedenceOverAllow()
    {
        // Arrange
        _sandboxConfig.GlobalAllowedPermissions.Add(SkillPermissions.FileSystemAll);
        _sandboxConfig.GlobalDeniedPermissions.Add(SkillPermissions.FileSystemWrite);

        var skill = new TestSkill("ConflictSkill", new List<string>
        {
            SkillPermissions.FileSystemWrite
        });

        // Act
        var result = _validator.ValidatePermissions(skill, "ConflictSkill");

        // Assert
        Assert.False(result.IsAllowed);
        Assert.Single(result.DeniedPermissions);
        Assert.Contains(SkillPermissions.FileSystemWrite, result.DeniedPermissions);
    }

    #endregion

    #region Standard Permission Constants Tests

    [Fact]
    public void ValidatePermissions_AllStandardPermissions_ValidatedCorrectly()
    {
        // Arrange
        var skill = new TestSkill("FullPermissionsSkill", new List<string>
        {
            SkillPermissions.FileSystemRead,
            SkillPermissions.FileSystemWrite,
            SkillPermissions.NetworkAccess,
            SkillPermissions.RegistryRead,
            SkillPermissions.RegistryWrite,
            SkillPermissions.ProcessExecute,
            SkillPermissions.DatabaseRead,
            SkillPermissions.DatabaseWrite,
            SkillPermissions.McpInvoke,
            SkillPermissions.UiAutomation,
            SkillPermissions.SystemConfiguration
        });

        // Act - no restrictions, should allow all
        var result = _validator.ValidatePermissions(skill, "FullPermissionsSkill");

        // Assert
        Assert.True(result.IsAllowed);
        Assert.Empty(result.DeniedPermissions);
        Assert.Equal(11, result.RequestedPermissions.Count);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ValidatePermissions_NullSkill_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            _validator.ValidatePermissions(null!, "TestSkill"));
    }

    [Fact]
    public void ValidatePermissions_EmptySkillName_ThrowsArgumentException()
    {
        // Arrange
        var skill = new TestSkill("TestSkill", new List<string>());

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            _validator.ValidatePermissions(skill, ""));
    }

    #endregion

    #region Test Helper Classes

    private class TestSkill : ISkill
    {
        public string Name { get; }
        public string Description => "Test skill for permission validation";
        public SkillCategory Category => SkillCategory.Other;
        public List<ParameterMetadata> Inputs => new();
        public OutputSchema OutputSchema => new() { Type = "object" };
        public List<string> Permissions { get; }

        public TestSkill(string name, List<string> permissions)
        {
            Name = name;
            Permissions = permissions;
        }

        public Task<object> ExecuteAsync(Dictionary<string, object> parameters, CancellationToken ct = default)
        {
            return Task.FromResult<object>("test result");
        }
    }

    #endregion
}
