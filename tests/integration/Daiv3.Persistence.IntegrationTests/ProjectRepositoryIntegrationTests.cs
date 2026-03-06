using Daiv3.Persistence;
using Daiv3.Persistence.Entities;
using Daiv3.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

#pragma warning disable IDISP006 // Test classes don't need to implement IDisposable

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
        var rootPathA = Path.Combine(Path.GetTempPath(), "pts-req-002", projectId, "src");
        var rootPathB = Path.Combine(Path.GetTempPath(), "pts-req-002", projectId, "docs");
        var serializedRootPaths = ProjectRootPaths.Serialize([rootPathA, rootPathB]);

        await repository.AddAsync(new Project
        {
            ProjectId = projectId,
            Name = "PTS Core Project",
            Description = "Project persistence coverage",
            RootPaths = serializedRootPaths,
            CreatedAt = now,
            UpdatedAt = now,
            Status = "active",
            ConfigJson = new ProjectConfiguration
            {
                Instructions = "Focus on reliable delivery and concise outputs.",
                ModelPreferences = new ProjectModelPreferences
                {
                    PreferredModelId = "phi-4-mini",
                    FallbackModelId = "gpt-4o-mini"
                }
            }.ToJsonOrNull()
        });

        var project = await repository.GetByIdAsync(projectId);

        Assert.NotNull(project);
        Assert.Equal(projectId, project!.ProjectId);
        Assert.Equal("PTS Core Project", project.Name);
        Assert.Equal("Project persistence coverage", project.Description);
        Assert.Equal("active", project.Status);
        Assert.Equal(now, project.CreatedAt);
        Assert.Equal(now, project.UpdatedAt);
        var config = ProjectConfiguration.Parse(project.ConfigJson);
        Assert.Equal("Focus on reliable delivery and concise outputs.", config.Instructions);
        Assert.Equal("phi-4-mini", config.ModelPreferences.PreferredModelId);
        Assert.Equal("gpt-4o-mini", config.ModelPreferences.FallbackModelId);

        var parsedRootPaths = ProjectRootPaths.Parse(project.RootPaths);
        Assert.Equal(2, parsedRootPaths.Count);
        Assert.Contains(Path.GetFullPath(rootPathA), parsedRootPaths);
        Assert.Contains(Path.GetFullPath(rootPathB), parsedRootPaths);
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

    [Fact]
    public async Task AddAsync_WithEmptyRootPaths_ThrowsArgumentException()
    {
        var databaseContext = await CreateInitializedContextAsync();
        var repository = new ProjectRepository(databaseContext, _loggerFactory.CreateLogger<ProjectRepository>());
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => repository.AddAsync(new Project
        {
            ProjectId = Guid.NewGuid().ToString(),
            Name = "Invalid Root Path Project",
            Description = "Should fail",
            RootPaths = "   ",
            CreatedAt = now,
            UpdatedAt = now,
            Status = "active"
        }));

        Assert.Equal("entity", exception.ParamName);
    }

    // CT-REQ-011: Dashboard query method tests
    [Fact]
    public async Task GetByAssignedAgentAsync_ReturnsProjectsForAgent()
    {
        var databaseContext = await CreateInitializedContextAsync();
        var repository = new ProjectRepository(databaseContext, _loggerFactory.CreateLogger<ProjectRepository>());
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Create projects with different agent assignments
        await repository.AddAsync(CreateTestProject("proj-1", "Project 1", "agent-alpha", priority: 1, now: now));
        await repository.AddAsync(CreateTestProject("proj-2", "Project 2", "agent-alpha", priority: 2, now: now));
        await repository.AddAsync(CreateTestProject("proj-3", "Project 3", "agent-beta", priority: 1, now: now));
        await repository.AddAsync(CreateTestProject("proj-4", "Project 4", null, priority: 1, now: now));

        var alphaProjects = await repository.GetByAssignedAgentAsync("agent-alpha");
        var betaProjects = await repository.GetByAssignedAgentAsync("agent-beta");
        var unassignedProjects = await repository.GetByAssignedAgentAsync(null);

        Assert.Equal(2, alphaProjects.Count);
        Assert.All(alphaProjects, p => Assert.Equal("agent-alpha", p.AssignedAgent));
        Assert.Single(betaProjects);
        Assert.Equal("agent-beta", betaProjects[0].AssignedAgent);
        Assert.Single(unassignedProjects);
        Assert.Null(unassignedProjects[0].AssignedAgent);
    }

    [Fact]
    public async Task GetByPriorityAsync_ReturnsSortedByPriority()
    {
        var databaseContext = await CreateInitializedContextAsync();
        var repository = new ProjectRepository(databaseContext, _loggerFactory.CreateLogger<ProjectRepository>());
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await repository.AddAsync(CreateTestProject("proj-1", "P2 Project", null, priority: 2, now: now));
        await repository.AddAsync(CreateTestProject("proj-2", "P0 Project", null, priority: 0, now: now));
        await repository.AddAsync(CreateTestProject("proj-3", "P1 Project", null, priority: 1, now: now));

        var projects = await repository.GetByPriorityAsync();

        Assert.Equal(3, projects.Count);
        Assert.Equal(0, projects[0].Priority);
        Assert.Equal(1, projects[1].Priority);
        Assert.Equal(2, projects[2].Priority);
    }

    [Fact]
    public async Task GetSubProjectsAsync_ReturnsChildProjects()
    {
        var databaseContext = await CreateInitializedContextAsync();
        var repository = new ProjectRepository(databaseContext, _loggerFactory.CreateLogger<ProjectRepository>());
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var parentId = "parent-1";
        await repository.AddAsync(CreateTestProject(parentId, "Parent Project", null, priority: 1, now: now));
        await repository.AddAsync(CreateTestProject("child-1", "Child 1", null, priority: 1, now: now, parentId: parentId));
        await repository.AddAsync(CreateTestProject("child-2", "Child 2", null, priority: 2, now: now, parentId: parentId));
        await repository.AddAsync(CreateTestProject("other-1", "Other Project", null, priority: 1, now: now));

        var subProjects = await repository.GetSubProjectsAsync(parentId);

        Assert.Equal(2, subProjects.Count);
        Assert.All(subProjects, p => Assert.Equal(parentId, p.ParentProjectId));
    }

    [Fact]
    public async Task GetRootProjectsAsync_ReturnsOnlyRootProjects()
    {
        var databaseContext = await CreateInitializedContextAsync();
        var repository = new ProjectRepository(databaseContext, _loggerFactory.CreateLogger<ProjectRepository>());
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var parentId = "parent-1";
        await repository.AddAsync(CreateTestProject(parentId, "Root Project 1", null, priority: 1, now: now));
        await repository.AddAsync(CreateTestProject("root-2", "Root Project 2", null, priority: 2, now: now));
        await repository.AddAsync(CreateTestProject("child-1", "Child Project", null, priority: 1, now: now, parentId: parentId));

        var rootProjects = await repository.GetRootProjectsAsync();

        Assert.Equal(2, rootProjects.Count);
        Assert.All(rootProjects, p => Assert.Null(p.ParentProjectId));
    }

    [Fact]
    public async Task GetProjectsApproachingDeadlineAsync_ReturnsUpcomingDeadlines()
    {
        var databaseContext = await CreateInitializedContextAsync();
        var repository = new ProjectRepository(databaseContext, _loggerFactory.CreateLogger<ProjectRepository>());
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var tomorrow = DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds();
        var nextWeek = DateTimeOffset.UtcNow.AddDays(7).ToUnixTimeSeconds();
        var nextMonth = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds();

        await repository.AddAsync(CreateTestProject("proj-1", "Due Tomorrow", null, priority: 1, now: now, deadline: tomorrow, status: "active"));
        await repository.AddAsync(CreateTestProject("proj-2", "Due Next Week", null, priority: 1, now: now, deadline: nextWeek, status: "active"));
        await repository.AddAsync(CreateTestProject("proj-3", "Due Next Month", null, priority: 1, now: now, deadline: nextMonth, status: "active"));
        await repository.AddAsync(CreateTestProject("proj-4", "No Deadline", null, priority: 1, now: now, status: "active"));

        var upcomingProjects = await repository.GetProjectsApproachingDeadlineAsync(7);

        Assert.Equal(2, upcomingProjects.Count);
        Assert.All(upcomingProjects, p => Assert.True(p.Deadline.HasValue));
        Assert.Contains(upcomingProjects, p => p.ProjectId == "proj-1");
        Assert.Contains(upcomingProjects, p => p.ProjectId == "proj-2");
    }

    [Fact]
    public async Task DashboardFields_PersistAndRetrieveCorrectly()
    {
        var databaseContext = await CreateInitializedContextAsync();
        var repository = new ProjectRepository(databaseContext, _loggerFactory.CreateLogger<ProjectRepository>());
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var deadline = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds();

        var projectId = Guid.NewGuid().ToString();
        var project = CreateTestProject(projectId, "Dashboard Test", "agent-1", priority: 1, now: now, 
            deadline: deadline, progress: 45.5, estimatedCost: 150.00, actualCost: 120.50);

        await repository.AddAsync(project);
        var retrieved = await repository.GetByIdAsync(projectId);

        Assert.NotNull(retrieved);
        Assert.Equal(1, retrieved!.Priority);
        Assert.Equal(45.5, retrieved.ProgressPercent);
        Assert.Equal(deadline, retrieved.Deadline);
        Assert.Equal("agent-1", retrieved.AssignedAgent);
        Assert.Equal(150.00, retrieved.EstimatedCost!.Value, 2);
        Assert.Equal(120.50, retrieved.ActualCost!.Value, 2);
    }

    private static Project CreateTestProject(
        string projectId, 
        string name, 
        string? assignedAgent, 
        int priority, 
        long now,
        string? parentId = null,
        long? deadline = null,
        double progress = 0.0,
        double? estimatedCost = null,
        double? actualCost = null,
        string status = "active")
    {
        return new Project
        {
            ProjectId = projectId,
            Name = name,
            Description = $"Test project {name}",
            RootPaths = ProjectRootPaths.Serialize([Path.GetTempPath()]),
            CreatedAt = now,
            UpdatedAt = now,
            Status = status,
            Priority = priority,
            ProgressPercent = progress,
            Deadline = deadline,
            AssignedAgent = assignedAgent,
            EstimatedCost = estimatedCost,
            ActualCost = actualCost,
            ParentProjectId = parentId
        };
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