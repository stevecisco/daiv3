using Daiv3.Core.Authorization;
using Daiv3.Core.Enums;
using Daiv3.Orchestration.Services;
using Daiv3.Persistence.Entities;
using Daiv3.Persistence.Repositories;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Daiv3.Orchestration.Tests;

public class ExecutableSkillRunnerTests
{
    private readonly Mock<IExecutableSkillRepository> _mockRepository;
    private readonly Mock<IExecutableSkillApprovalService> _mockApprovalService;
    private readonly Mock<ISkillHashService> _mockHashService;
    private readonly Mock<ILogger<ExecutableSkillRunner>> _mockLogger;
    private readonly ExecutableSkillRunner _runner;

    public ExecutableSkillRunnerTests()
    {
        _mockRepository = new Mock<IExecutableSkillRepository>();
        _mockApprovalService = new Mock<IExecutableSkillApprovalService>();
        _mockHashService = new Mock<ISkillHashService>();
        _mockLogger = new Mock<ILogger<ExecutableSkillRunner>>();

        _runner = new ExecutableSkillRunner(
            _mockRepository.Object,
            _mockApprovalService.Object,
            _mockHashService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task ValidateBeforeExecutionAsync_SkillNotFound_ReturnsFailure()
    {
        // Arrange
        var skillId = Guid.NewGuid().ToString();
        _mockRepository
            .Setup(r => r.GetByIdAsync(skillId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExecutableSkill?)null);

        // Act
        var result = await _runner.ValidateBeforeExecutionAsync(skillId);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("NotFound", result.ErrorCode);
        Assert.Contains(skillId.ToString(), result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateBeforeExecutionAsync_SkillNotApproved_ReturnsFailure()
    {
        // Arrange
        var skillId = Guid.NewGuid().ToString();
        var skill = new ExecutableSkill
        {
            SkillId = skillId,
            Name = "TestSkill",
            FilePath = "/path/to/skill.cs",
            FileHash = "hash123",
            ApprovalStatus = ApprovalStatus.PendingApproval.ToString(),
            CreatedBy = "user1",
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(skillId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(skill);

        // Act
        var result = await _runner.ValidateBeforeExecutionAsync(skillId);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("ApprovalRequired", result.ErrorCode);
        Assert.Contains("PendingApproval", result.ErrorMessage);
        Assert.Contains("administrator", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateBeforeExecutionAsync_RevokedSkill_ReturnsFailure()
    {
        // Arrange
        var skillId = Guid.NewGuid().ToString();
        var skill = new ExecutableSkill
        {
            SkillId = skillId,
            Name = "RevokedSkill",
            FilePath = "/path/to/skill.cs",
            FileHash = "hash123",
            ApprovalStatus = ApprovalStatus.Revoked.ToString(),
            CreatedBy = "user1",
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(skillId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(skill);

        // Act
        var result = await _runner.ValidateBeforeExecutionAsync(skillId);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("ApprovalRequired", result.ErrorCode);
        Assert.Contains("Revoked", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateBeforeExecutionAsync_StaleSkill_ReturnsFailure()
    {
        // Arrange
        var skillId = Guid.NewGuid().ToString();
        var skill = new ExecutableSkill
        {
            SkillId = skillId,
            Name = "StaleSkill",
            FilePath = "/path/to/skill.cs",
            FileHash = "hash123",
            ApprovalStatus = ApprovalStatus.Stale.ToString(),
            CreatedBy = "user1",
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(skillId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(skill);

        // Act
        var result = await _runner.ValidateBeforeExecutionAsync(skillId);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("ApprovalRequired", result.ErrorCode);
        Assert.Contains("Stale", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateBeforeExecutionAsync_FileNotFound_ReturnsFailure()
    {
        // Arrange
        var skillId = Guid.NewGuid().ToString();
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent-{Guid.NewGuid()}.cs");
        var skill = new ExecutableSkill
        {
            SkillId = skillId,
            Name = "TestSkill",
            FilePath = nonExistentPath,
            FileHash = "hash123",
            ApprovalStatus = ApprovalStatus.Approved.ToString(),
            CreatedBy = "user1",
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(skillId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(skill);

        // Act
        var result = await _runner.ValidateBeforeExecutionAsync(skillId);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("FileNotFound", result.ErrorCode);
        Assert.Contains(nonExistentPath, result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateBeforeExecutionAsync_HashValidationFails_ReturnsFailure()
    {
        // Arrange
        var skillId = Guid.NewGuid().ToString();
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, "// Test skill");

        try
        {
            var skill = new ExecutableSkill
            {
                SkillId = skillId,
                Name = "TestSkill",
                FilePath = tempFile,
                FileHash = "hash123",
                ApprovalStatus = ApprovalStatus.Approved.ToString(),
                CreatedBy = "user1",
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            _mockRepository
                .Setup(r => r.GetByIdAsync(skillId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(skill);

            _mockHashService
                .Setup(h => h.ValidateHashAsync(skill, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Act
            var result = await _runner.ValidateBeforeExecutionAsync(skillId);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal("IntegrityFailure", result.ErrorCode);
            Assert.Contains("integrity check", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("re-approval", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ValidateBeforeExecutionAsync_AllChecksPass_ReturnsSuccess()
    {
        // Arrange
        var skillId = Guid.NewGuid().ToString();
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, "// Test skill");

        try
        {
            var skill = new ExecutableSkill
            {
                SkillId = skillId,
                Name = "TestSkill",
                FilePath = tempFile,
                FileHash = "hash123",
                ApprovalStatus = ApprovalStatus.Approved.ToString(),
                CreatedBy = "user1",
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            _mockRepository
                .Setup(r => r.GetByIdAsync(skillId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(skill);

            _mockHashService
                .Setup(h => h.ValidateHashAsync(skill, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _runner.ValidateBeforeExecutionAsync(skillId);

            // Assert
            Assert.True(result.IsValid);
            Assert.Null(result.ErrorCode);
            Assert.Null(result.ErrorMessage);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ValidationFails_ReturnsError()
    {
        // Arrange
        var skillId = Guid.NewGuid().ToString();
        var principal = SystemPrincipal.CreateUser("user1");
        var parameters = new Dictionary<string, string>();

        _mockRepository
            .Setup(r => r.GetByIdAsync(skillId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExecutableSkill?)null);

        // Act
        var result = await _runner.ExecuteAsync(skillId, parameters, principal);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("not found", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_NotApproved_ReturnsErrorWithRemediationGuidance()
    {
        // Arrange
        var skillId = Guid.NewGuid().ToString();
        var principal = SystemPrincipal.CreateUser("user1");
        var parameters = new Dictionary<string, string>();

        var skill = new ExecutableSkill
        {
            SkillId = skillId,
            Name = "UnapprovedSkill",
            FilePath = "/path/to/skill.cs",
            FileHash = "hash123",
            ApprovalStatus = ApprovalStatus.PendingApproval.ToString(),
            CreatedBy = "user1",
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(skillId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(skill);

        // Act
        var result = await _runner.ExecuteAsync(skillId, parameters, principal);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("PendingApproval", result.ErrorMessage);
        Assert.Contains("administrator", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_IntegrityFailure_ReturnsErrorWithRemediationGuidance()
    {
        // Arrange
        var skillId = Guid.NewGuid().ToString();
        var principal = SystemPrincipal.CreateUser("user1");
        var parameters = new Dictionary<string, string>();
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, "// Test skill");

        try
        {
            var skill = new ExecutableSkill
            {
                SkillId = skillId,
                Name = "TamperedSkill",
                FilePath = tempFile,
                FileHash = "hash123",
                ApprovalStatus = ApprovalStatus.Approved.ToString(),
                CreatedBy = "user1",
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            _mockRepository
                .Setup(r => r.GetByIdAsync(skillId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(skill);

            _mockHashService
                .Setup(h => h.ValidateHashAsync(skill, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Act
            var result = await _runner.ExecuteAsync(skillId, parameters, principal);

            // Assert
            Assert.False(result.Success);
            Assert.NotNull(result.ErrorMessage);
            Assert.Contains("integrity", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("re-approval", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void SkillValidationResult_Success_CreatesValidResult()
    {
        // Act
        var result = SkillValidationResult.Success();

        // Assert
        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public void SkillValidationResult_Failure_CreatesInvalidResult()
    {
        // Act
        var result = SkillValidationResult.Failure("TestError", "Test error message");

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("TestError", result.ErrorCode);
        Assert.Equal("Test error message", result.ErrorMessage);
    }

    [Fact]
    public void SkillExecutionResult_SuccessResult_CreatesValidResult()
    {
        // Act
        var result = SkillExecutionResult.SuccessResult(
            output: "{\"result\":\"test\"}",
            standardOutput: "Output text",
            exitCode: 0,
            executionTimeMs: 150);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("{\"result\":\"test\"}", result.Output);
        Assert.Equal("Output text", result.StandardOutput);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(150, result.ExecutionTimeMs);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void SkillExecutionResult_ErrorResult_CreatesFailureResult()
    {
        // Act
        var result = SkillExecutionResult.ErrorResult(
            errorMessage: "Execution failed",
            standardError: "Error details",
            exitCode: 1);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Execution failed", result.ErrorMessage);
        Assert.Equal("Error details", result.StandardError);
        Assert.Equal(1, result.ExitCode);
        Assert.Null(result.Output);
    }
}
