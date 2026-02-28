using Daiv3.Persistence;
using Daiv3.Persistence.Entities;
using Daiv3.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Daiv3.IntegrationTests.Persistence;

[Collection("Database")]
public class ProjectRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly string _testDbPath;
    private readonly ILoggerFactory _loggerFactory;
    private DatabaseContext? _databaseContext;

    public ProjectRepositoryIntegrationTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"daiv3_project_repo_test_{Guid.NewGuid():N}.db");
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
            var remainingAttempts = 10;
            while (remainingAttempts > 0)
            {
                try
                {
                    File.Delete(_testDbPath);
                    break;
                }
                catch (IOException) when (remainingAttempts > 1)
                {
                    remainingAttempts--;
                    await Task.Delay(100);
                }
            }
        }

        _loggerFactory.Dispose();
    }

    [Fact]
    public async Task AddAndGetById_PersistsProjectCoreFields()
    {
        var databaseContext = await CreateInitializedContextAsync();
        var repository = new ProjectRepository(databaseContext, _loggerFactory.CreateLogger<ProjectRepository>());

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var projectId = Guid.NewGuid().ToString();

        await repository.AddAsync(new Project
        {
            ProjectId = projectId,
            Name = "PTS Core Project",
            Description = "Project persistence coverage",
            RootPaths = "C:/workspace/project",
            CreatedAt = now,
            UpdatedAt = now,
            Status = "active",
            ConfigJson = "{\"localOnly\":true}"
        });

        var project = await repository.GetByIdAsync(projectId);

        Assert.NotNull(project);
        Assert.Equal(projectId, project!.ProjectId);
        Assert.Equal("PTS Core Project", project.Name);
        Assert.Equal("Project persistence coverage", project.Description);
        Assert.Equal("active", project.Status);
        Assert.Equal(now, project.CreatedAt);
        Assert.Equal(now, project.UpdatedAt);
    }

    [Fact]
    public async Task Update_ChangesDescriptionStatusAndUpdatedTimestamp()
    {
        var databaseContext = await CreateInitializedContextAsync();
        var repository = new ProjectRepository(databaseContext, _loggerFactory.CreateLogger<ProjectRepository>());

        var createdAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var updatedAt = createdAt + 120;
        var projectId = Guid.NewGuid().ToString();

        await repository.AddAsync(new Project
        {
            ProjectId = projectId,
            Name = "Updatable Project",
            Description = "Original",
            RootPaths = "C:/workspace/update",
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
            Status = "active"
        });

        var existing = await repository.GetByIdAsync(projectId);
        Assert.NotNull(existing);

        existing!.Description = "Updated description";
        existing.Status = "archived";
        existing.UpdatedAt = updatedAt;
        await repository.UpdateAsync(existing);

        var updated = await repository.GetByIdAsync(projectId);
        Assert.NotNull(updated);
        Assert.Equal("Updated description", updated!.Description);
        Assert.Equal("archived", updated.Status);
        Assert.Equal(createdAt, updated.CreatedAt);
        Assert.Equal(updatedAt, updated.UpdatedAt);
    }

    [Fact]
    public async Task GetByStatusAndGetByName_ReturnExpectedProject()
    {
        var databaseContext = await CreateInitializedContextAsync();
        var repository = new ProjectRepository(databaseContext, _loggerFactory.CreateLogger<ProjectRepository>());
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await repository.AddAsync(new Project
        {
            ProjectId = Guid.NewGuid().ToString(),
            Name = "Alpha",
            Description = "Active project",
            RootPaths = "C:/workspace/alpha",
            CreatedAt = now,
            UpdatedAt = now,
            Status = "active"
        });

        await repository.AddAsync(new Project
        {
            ProjectId = Guid.NewGuid().ToString(),
            Name = "Beta",
            Description = "Archived project",
            RootPaths = "C:/workspace/beta",
            CreatedAt = now,
            UpdatedAt = now + 30,
            Status = "archived"
        });

        var archived = await repository.GetByStatusAsync("archived");
        var alpha = await repository.GetByNameAsync("Alpha");

        Assert.Single(archived);
        Assert.Equal("Beta", archived[0].Name);
        Assert.NotNull(alpha);
        Assert.Equal("active", alpha!.Status);
    }

    private async Task<DatabaseContext> CreateInitializedContextAsync()
    {
        var options = Options.Create(new PersistenceOptions
        {
            DatabasePath = _testDbPath,
            EnableWAL = true,
            BusyTimeout = 5000,
            MaxPoolSize = 10
        });

        _databaseContext = new DatabaseContext(_loggerFactory.CreateLogger<DatabaseContext>(), options);
        await _databaseContext.InitializeAsync();
        await _databaseContext.MigrateToLatestAsync();
        return _databaseContext;
    }
}