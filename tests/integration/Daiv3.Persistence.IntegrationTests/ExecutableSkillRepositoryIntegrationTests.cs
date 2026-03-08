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
public class ExecutableSkillRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly string _testDbPath;
    private readonly ILoggerFactory _loggerFactory;
    private DatabaseContext? _databaseContext;

    public ExecutableSkillRepositoryIntegrationTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"daiv3_executable_skill_repo_test_{Guid.NewGuid():N}.db");
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

        _loggerFactory.Dispose();
    }

    [Fact]
    public async Task AddAndGetById_PersistsExecutableSkill()
    {
        var databaseContext = await CreateInitializedContextAsync();
        var repository = new ExecutableSkillRepository(databaseContext, _loggerFactory.CreateLogger<ExecutableSkillRepository>());

        var skill = CreateSkill(name: "DataFetcher", approvalStatus: "PendingApproval");

        await repository.AddAsync(skill);
        var retrieved = await repository.GetByIdAsync(skill.SkillId);

        Assert.NotNull(retrieved);
        Assert.Equal(skill.SkillId, retrieved!.SkillId);
        Assert.Equal("DataFetcher", retrieved.Name);
        Assert.Equal(skill.FilePath, retrieved.FilePath);
        Assert.Equal(skill.FileHash, retrieved.FileHash);
        Assert.Equal("PendingApproval", retrieved.ApprovalStatus);
    }

    [Fact]
    public async Task GetByApprovalStatusAsync_FiltersByStatus()
    {
        var databaseContext = await CreateInitializedContextAsync();
        var repository = new ExecutableSkillRepository(databaseContext, _loggerFactory.CreateLogger<ExecutableSkillRepository>());

        var approvedSkill = CreateSkill(name: "ApprovedSkill", approvalStatus: "Approved");
        var pendingSkill = CreateSkill(name: "PendingSkill", approvalStatus: "PendingApproval");

        await repository.AddAsync(approvedSkill);
        await repository.AddAsync(pendingSkill);

        var approved = await repository.GetByApprovalStatusAsync("Approved");

        Assert.Single(approved);
        Assert.Equal("ApprovedSkill", approved[0].Name);
    }

    [Fact]
    public async Task GetByNameAsync_ReturnsMatchingSkill()
    {
        var databaseContext = await CreateInitializedContextAsync();
        var repository = new ExecutableSkillRepository(databaseContext, _loggerFactory.CreateLogger<ExecutableSkillRepository>());

        var skill = CreateSkill(name: "HasherSkill", approvalStatus: "PendingApproval");
        await repository.AddAsync(skill);

        var retrieved = await repository.GetByNameAsync("HasherSkill");

        Assert.NotNull(retrieved);
        Assert.Equal(skill.SkillId, retrieved!.SkillId);
    }

    [Fact]
    public async Task UpdateAsync_PersistsChangedFields()
    {
        var databaseContext = await CreateInitializedContextAsync();
        var repository = new ExecutableSkillRepository(databaseContext, _loggerFactory.CreateLogger<ExecutableSkillRepository>());

        var skill = CreateSkill(name: "TransformSkill", approvalStatus: "PendingApproval");
        await repository.AddAsync(skill);

        skill.ApprovalStatus = "Approved";
        skill.ApprovedBy = "admin-user";
        skill.ApprovedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        skill.FileHash = "NEW_HASH_123";

        await repository.UpdateAsync(skill);

        var updated = await repository.GetByIdAsync(skill.SkillId);

        Assert.NotNull(updated);
        Assert.Equal("Approved", updated!.ApprovalStatus);
        Assert.Equal("admin-user", updated.ApprovedBy);
        Assert.Equal("NEW_HASH_123", updated.FileHash);
    }

    [Fact]
    public async Task DeleteAsync_RemovesSkill()
    {
        var databaseContext = await CreateInitializedContextAsync();
        var repository = new ExecutableSkillRepository(databaseContext, _loggerFactory.CreateLogger<ExecutableSkillRepository>());

        var skill = CreateSkill(name: "DeleteSkill", approvalStatus: "PendingApproval");
        await repository.AddAsync(skill);

        await repository.DeleteAsync(skill.SkillId);
        var deleted = await repository.GetByIdAsync(skill.SkillId);

        Assert.Null(deleted);
    }

    [Fact]
    public async Task ExistsByNameAsync_ReturnsExpectedResult()
    {
        var databaseContext = await CreateInitializedContextAsync();
        var repository = new ExecutableSkillRepository(databaseContext, _loggerFactory.CreateLogger<ExecutableSkillRepository>());

        var skill = CreateSkill(name: "ExistsSkill", approvalStatus: "PendingApproval");
        await repository.AddAsync(skill);

        var exists = await repository.ExistsByNameAsync("ExistsSkill");
        var notExists = await repository.ExistsByNameAsync("MissingSkill");

        Assert.True(exists);
        Assert.False(notExists);
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

    private static ExecutableSkill CreateSkill(string name, string approvalStatus)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        return new ExecutableSkill
        {
            SkillId = Guid.NewGuid().ToString(),
            Name = name,
            FilePath = $"C:/skills/{name}.cs",
            FileHash = $"HASH_{Guid.NewGuid():N}",
            MetadataPath = $"C:/skills/{name}.md",
            ApprovalStatus = approvalStatus,
            CreatedBy = "test-user",
            CreatedAt = now,
            ApprovedBy = approvalStatus == "Approved" ? "admin" : null,
            ApprovedAt = approvalStatus == "Approved" ? now : null,
            LastModifiedAt = now
        };
    }
}
