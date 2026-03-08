using Daiv3.Core.Authorization;
using Daiv3.Core.Enums;
using Daiv3.Orchestration.Services;
using Daiv3.Persistence;
using Daiv3.Persistence.Entities;
using Daiv3.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

#pragma warning disable IDISP006 // Test classes use xUnit lifecycle, no explicit IDisposable required.

namespace Daiv3.Persistence.IntegrationTests;

[Collection("Database")]
public class ExecutableSkillApprovalWorkflowIntegrationTests : IAsyncLifetime
{
    private readonly string _testDbPath;
    private readonly ILoggerFactory _loggerFactory;
    private DatabaseContext? _databaseContext;
    private string _testFilePath = string.Empty;

    public ExecutableSkillApprovalWorkflowIntegrationTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"daiv3_skill_approval_test_{Guid.NewGuid():N}.db");
        _loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Warning));
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_databaseContext != null)
        {
            await _databaseContext.DisposeAsync();
            _databaseContext = null;
        }

        SqliteConnection.ClearAllPools();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        await Task.Delay(100);

        if (File.Exists(_testDbPath))
        {
            var attempts = 10;
            while (attempts > 0)
            {
                try
                {
                    File.Delete(_testDbPath);
                    break;
                }
                catch (IOException) when (attempts > 1)
                {
                    attempts--;
                    await Task.Delay(100);
                }
            }
        }

        if (File.Exists(_testFilePath))
        {
            try { File.Delete(_testFilePath); } catch { /* Best effort */ }
        }

        _loggerFactory.Dispose();
    }

    [Fact]
    public async Task EndToEnd_CreateRequestApproveExecute_WorkflowComplete()
    {
        // Arrange
        var context = await CreateInitializedContextAsync();
        var repository = new ExecutableSkillRepository(context, _loggerFactory.CreateLogger<ExecutableSkillRepository>());
        var skillAuditRepository = new SkillAuditLogRepository(context, _loggerFactory.CreateLogger<SkillAuditLogRepository>());
        var skillAuditService = new SkillAuditService(skillAuditRepository, _loggerFactory.CreateLogger<SkillAuditService>());
        var hashService = new SkillHashService(_loggerFactory.CreateLogger<SkillHashService>());
        var approvalService = new ExecutableSkillApprovalService(repository, hashService, skillAuditService, _loggerFactory.CreateLogger<ExecutableSkillApprovalService>());

        approvalService.GrantRole("admin-user", SystemRoles.SkillAdministrator);

        // Create skill file
        _testFilePath = Path.Combine(Path.GetTempPath(), $"TestSkill_{Guid.NewGuid():N}.cs");
        await File.WriteAllTextAsync(_testFilePath, "Console.WriteLine(\"Hello\");");

        var hash = await hashService.ComputeHashAsync(_testFilePath);
        var skill = CreateSkill("TestApprovalSkill", _testFilePath, hash, ApprovalStatus.PendingApproval);

        // Act & Assert: Create skill
        await repository.AddAsync(skill);
        var retrieved = await repository.GetByIdAsync(skill.SkillId);
        Assert.NotNull(retrieved);
        Assert.Equal(ApprovalStatus.PendingApproval.ToString(), retrieved!.ApprovalStatus);

        // Act & Assert: Admin approves skill
        var approved = await approvalService.ApproveSkillAsync(skill.SkillId, "admin-user");
        Assert.Equal(ApprovalStatus.Approved.ToString(), approved.ApprovalStatus);
        Assert.Equal("admin-user", approved.ApprovedBy);
        Assert.NotNull(approved.ApprovedAt);

        // Act & Assert: Validate status persisted
        var afterApproval = await repository.GetByIdAsync(skill.SkillId);
        Assert.Equal(ApprovalStatus.Approved.ToString(), afterApproval!.ApprovalStatus);
    }

    [Fact]
    public async Task EndToEnd_ApproveModifyDetectStale_WorkflowComplete()
    {
        // Arrange
        var context = await CreateInitializedContextAsync();
        var repository = new ExecutableSkillRepository(context, _loggerFactory.CreateLogger<ExecutableSkillRepository>());
        var skillAuditRepository = new SkillAuditLogRepository(context, _loggerFactory.CreateLogger<SkillAuditLogRepository>());
        var skillAuditService = new SkillAuditService(skillAuditRepository, _loggerFactory.CreateLogger<SkillAuditService>());
        var hashService = new SkillHashService(_loggerFactory.CreateLogger<SkillHashService>());
        var approvalService = new ExecutableSkillApprovalService(repository, hashService, skillAuditService, _loggerFactory.CreateLogger<ExecutableSkillApprovalService>());

        approvalService.GrantRole("admin-user", SystemRoles.SkillAdministrator);

        // Create and approve skill
        _testFilePath = Path.Combine(Path.GetTempPath(), $"StaleTestSkill_{Guid.NewGuid():N}.cs");
        await File.WriteAllTextAsync(_testFilePath, "Console.WriteLine(\"v1\");");

        var hash = await hashService.ComputeHashAsync(_testFilePath);
        var skill = CreateSkill("StaleTestSkill", _testFilePath, hash, ApprovalStatus.PendingApproval);
        await repository.AddAsync(skill);

        var approved = await approvalService.ApproveSkillAsync(skill.SkillId, "admin-user");
        Assert.Equal(ApprovalStatus.Approved.ToString(), approved.ApprovalStatus);

        // Act: Modify file
        await File.WriteAllTextAsync(_testFilePath, "Console.WriteLine(\"v2\");");

        // Act: Validate and detect stale
        var isValid = await approvalService.ValidateAndUpdateStaleStatusAsync(skill.SkillId);

        // Assert
        Assert.False(isValid);
        var afterStale = await repository.GetByIdAsync(skill.SkillId);
        Assert.Equal(ApprovalStatus.Stale.ToString(), afterStale!.ApprovalStatus);
    }

    [Fact]
    public async Task NonAdminCannotApprove_ThrowsUnauthorized()
    {
        // Arrange
        var context = await CreateInitializedContextAsync();
        var repository = new ExecutableSkillRepository(context, _loggerFactory.CreateLogger<ExecutableSkillRepository>());
        var skillAuditRepository = new SkillAuditLogRepository(context, _loggerFactory.CreateLogger<SkillAuditLogRepository>());
        var skillAuditService = new SkillAuditService(skillAuditRepository, _loggerFactory.CreateLogger<SkillAuditService>());
        var hashService = new SkillHashService(_loggerFactory.CreateLogger<SkillHashService>());
        var approvalService = new ExecutableSkillApprovalService(repository, hashService, skillAuditService, _loggerFactory.CreateLogger<ExecutableSkillApprovalService>());

        _testFilePath = Path.Combine(Path.GetTempPath(), $"UnauthorizedSkill_{Guid.NewGuid():N}.cs");
        await File.WriteAllTextAsync(_testFilePath, "return 42;");

        var hash = await hashService.ComputeHashAsync(_testFilePath);
        var skill = CreateSkill("UnauthorizedSkill", _testFilePath, hash, ApprovalStatus.PendingApproval);
        await repository.AddAsync(skill);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            approvalService.ApproveSkillAsync(skill.SkillId, "regular-user"));

        var unchanged = await repository.GetByIdAsync(skill.SkillId);
        Assert.Equal(ApprovalStatus.PendingApproval.ToString(), unchanged!.ApprovalStatus);
    }

    [Fact]
    public async Task RevokeApprovedSkill_TransitionsToRevoked()
    {
        // Arrange
        var context = await CreateInitializedContextAsync();
        var repository = new ExecutableSkillRepository(context, _loggerFactory.CreateLogger<ExecutableSkillRepository>());
        var skillAuditRepository = new SkillAuditLogRepository(context, _loggerFactory.CreateLogger<SkillAuditLogRepository>());
        var skillAuditService = new SkillAuditService(skillAuditRepository, _loggerFactory.CreateLogger<SkillAuditService>());
        var hashService = new SkillHashService(_loggerFactory.CreateLogger<SkillHashService>());
        var approvalService = new ExecutableSkillApprovalService(repository, hashService, skillAuditService, _loggerFactory.CreateLogger<ExecutableSkillApprovalService>());

        approvalService.GrantRole("admin-user", SystemRoles.SkillAdministrator);

        _testFilePath = Path.Combine(Path.GetTempPath(), $"RevokeSkill_{Guid.NewGuid():N}.cs");
        await File.WriteAllTextAsync(_testFilePath, "return true;");

        var hash = await hashService.ComputeHashAsync(_testFilePath);
        var skill = CreateSkill("RevokeSkill", _testFilePath, hash, ApprovalStatus.PendingApproval);
        await repository.AddAsync(skill);

        await approvalService.ApproveSkillAsync(skill.SkillId, "admin-user");

        // Act
        var revoked = await approvalService.RevokeApprovalAsync(skill.SkillId, "admin-user");

        // Assert
        Assert.Equal(ApprovalStatus.Revoked.ToString(), revoked.ApprovalStatus);
        var afterRevoke = await repository.GetByIdAsync(skill.SkillId);
        Assert.Equal(ApprovalStatus.Revoked.ToString(), afterRevoke!.ApprovalStatus);
    }

    [Fact]
    public async Task QueryByApprovalStatus_FiltersCorrectly()
    {
        // Arrange
        var context = await CreateInitializedContextAsync();
        var repository = new ExecutableSkillRepository(context, _loggerFactory.CreateLogger<ExecutableSkillRepository>());
        var skillAuditRepository = new SkillAuditLogRepository(context, _loggerFactory.CreateLogger<SkillAuditLogRepository>());
        var skillAuditService = new SkillAuditService(skillAuditRepository, _loggerFactory.CreateLogger<SkillAuditService>());
        var hashService = new SkillHashService(_loggerFactory.CreateLogger<SkillHashService>());
        var approvalService = new ExecutableSkillApprovalService(repository, hashService, skillAuditService, _loggerFactory.CreateLogger<ExecutableSkillApprovalService>());

        approvalService.GrantRole("admin-user", SystemRoles.SkillAdministrator);

        // Create multiple skills with different statuses
        _testFilePath = Path.Combine(Path.GetTempPath(), $"QuerySkill_{Guid.NewGuid():N}.cs");
        await File.WriteAllTextAsync(_testFilePath, "return 1;");
        var hash = await hashService.ComputeHashAsync(_testFilePath);

        var pendingSkill = CreateSkill("PendingSkill", _testFilePath, hash, ApprovalStatus.PendingApproval);
        var approvedSkill = CreateSkill("ApprovedSkill", _testFilePath, hash, ApprovalStatus.PendingApproval);

        await repository.AddAsync(pendingSkill);
        await repository.AddAsync(approvedSkill);

        await approvalService.ApproveSkillAsync(approvedSkill.SkillId, "admin-user");

        // Act
        var pendingSkills = await repository.GetByApprovalStatusAsync(ApprovalStatus.PendingApproval.ToString());
        var approvedSkills = await repository.GetByApprovalStatusAsync(ApprovalStatus.Approved.ToString());

        // Assert
        Assert.Single(pendingSkills);
        Assert.Equal("PendingSkill", pendingSkills[0].Name);

        Assert.Single(approvedSkills);
        Assert.Equal("ApprovedSkill", approvedSkills[0].Name);
    }

    private async Task<DatabaseContext> CreateInitializedContextAsync()
    {
        if (_databaseContext != null)
        {
            await _databaseContext.DisposeAsync();
            _databaseContext = null;
        }

        var options = Options.Create(new PersistenceOptions
        {
            DatabasePath = _testDbPath,
            EnableWAL = true,
            BusyTimeout = 5000
        });

        var logger = _loggerFactory.CreateLogger<DatabaseContext>();
        _databaseContext = new DatabaseContext(logger, options);
        await _databaseContext.InitializeAsync();

        return _databaseContext;
    }

    private static ExecutableSkill CreateSkill(string name, string filePath, string hash, ApprovalStatus status)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        return new ExecutableSkill
        {
            SkillId = Guid.NewGuid().ToString(),
            Name = name,
            FilePath = filePath,
            FileHash = hash,
            MetadataPath = $"{filePath}.md",
            ApprovalStatus = status.ToString(),
            CreatedBy = "test-user",
            CreatedAt = now,
            LastModifiedAt = now
        };
    }
}
