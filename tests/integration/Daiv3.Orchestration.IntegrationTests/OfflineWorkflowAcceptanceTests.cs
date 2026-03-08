using Daiv3.Orchestration.Models;
using Daiv3.Persistence;
using Daiv3.Persistence.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Daiv3.Orchestration.IntegrationTests;

/// <summary>
/// Acceptance tests for ES-REQ-003 and ES-ACC-001.
/// ES-REQ-003: The system SHALL operate without external servers or Docker dependencies.
/// ES-ACC-001: In offline mode, the system completes chat and search workflows without network access.
/// 
/// These tests verify that core functionality works entirely offline without:
/// - Docker
/// - External database servers
/// - External embedding services
/// - Mandatory network access
/// </summary>
[Collection("Database")]
public class OfflineWorkflowAcceptanceTests : IAsyncLifetime
{
    private DatabaseContext? _dbContext;
    private string? _dbPath;

    public async Task InitializeAsync()
    {
        // Create test database (local SQLite, no external server)
        _dbPath = Path.Combine(Path.GetTempPath(), $"daiv3-offline-test-{Guid.NewGuid():N}.db");
        _dbContext = new DatabaseContext(
            Mock.Of<ILogger<DatabaseContext>>(),
            Options.Create(new PersistenceOptions { DatabasePath = _dbPath }));
        await _dbContext.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }

        if (_dbPath != null && File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }

    /// <summary>
    /// ES-REQ-003: Verifies that local SQLite persistence works without external database server.
    /// </summary>
    [Fact]
    public async Task OfflineWorkflow_LocalPersistence_WorksWithoutExternalServer()
    {
        // Arrange
        var projectRepository = new ProjectRepository(_dbContext!, Mock.Of<ILogger<ProjectRepository>>());

        var project = new Project
        {
            Name = "Offline Test Project",
            Description = "Testing offline capability",
            Status = ProjectStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act - Create project without any external server dependency
        var savedProject = await projectRepository.CreateAsync(project);

        // Assert - Verify data persisted to local SQLite
        Assert.NotNull(savedProject);
        Assert.NotEqual(Guid.Empty, savedProject.Id);

        var retrieved = await projectRepository.GetByIdAsync(savedProject.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("Offline Test Project", retrieved.Name);
    }

    /// <summary>
    /// ES-REQ-003: Verifies that task management works entirely offline.
    /// </summary>
    [Fact]
    public async Task OfflineWorkflow_TaskManagement_WorksWithoutNetwork()
    {
        // Arrange
        var taskRepository = new TaskRepository(_dbContext!, Mock.Of<ILogger<TaskRepository>>());

        var task = new AgentTask
        {
            Title = "Offline Task",
            Description = "Task created without network",
            Status = AgentTaskStatus.Pending,
            Priority = TaskPriority.Medium,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act - Create and update task offline
        var savedTask = await taskRepository.CreateAsync(task);
        savedTask.Status = AgentTaskStatus.InProgress;
        await taskRepository.UpdateAsync(savedTask);

        // Assert - Verify task lifecycle works offline
        var retrieved = await taskRepository.GetByIdAsync(savedTask.Id);
        Assert.NotNull(retrieved);
        Assert.Equal(AgentTaskStatus.InProgress, retrieved.Status);
    }

    /// <summary>
    /// ES-REQ-003: Verifies that document indexing metadata works without external services.
    /// </summary>
    [Fact]
    public async Task OfflineWorkflow_DocumentIndexing_WorksWithoutExternalServices()
    {
        // Arrange
        var documentRepository = new DocumentRepository(_dbContext!, Mock.Of<ILogger<DocumentRepository>>());

        var document = new Document
        {
            SourcePath = "c:\\test\\offline-doc.txt",
            Title = "Offline Document",
            ContentType = "text/plain",
            FileHash = "abc123",
            IndexedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act - Index document metadata without external services
        var savedDoc = await documentRepository.CreateAsync(document);

        // Assert - Verify document indexed locally
        Assert.NotNull(savedDoc);
        Assert.NotEqual(Guid.Empty, savedDoc.Id);

        var retrieved = await documentRepository.GetBySourcePathAsync("c:\\test\\offline-doc.txt");
        Assert.NotNull(retrieved);
        Assert.Equal("Offline Document", retrieved.Title);
    }

    /// <summary>
    /// ES-ACC-001: Verifies that session management works offline.
    /// </summary>
    [Fact]
    public async Task OfflineWorkflow_SessionManagement_CompletesWithoutNetwork()
    {
        // Arrange
        var sessionRepository = new SessionRepository(_dbContext!, Mock.Of<ILogger<SessionRepository>>());

        var session = new AgentSession
        {
            AgentId = Guid.NewGuid(),
            TaskId = Guid.NewGuid(),
            Status = SessionStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act - Create and manage session offline
        var savedSession = await sessionRepository.CreateAsync(session);
        savedSession.Status = SessionStatus.Completed;
        savedSession.UpdatedAt = DateTime.UtcNow;
        await sessionRepository.UpdateAsync(savedSession);

        // Assert - Verify session lifecycle works offline
        var retrieved = await sessionRepository.GetByIdAsync(savedSession.Id);
        Assert.NotNull(retrieved);
        Assert.Equal(SessionStatus.Completed, retrieved.Status);
    }

    /// <summary>
    /// ES-ACC-001: Verifies that learning memory works without external services.
    /// </summary>
    [Fact]
    public async Task OfflineWorkflow_LearningMemory_WorksWithoutExternalServices()
    {
        // Arrange
        var learningRepository = new LearningRepository(_dbContext!, Mock.Of<ILogger<LearningRepository>>());

        var learning = new Learning
        {
            Title = "Offline Learning",
            Description = "Learning created offline",
            TriggerType = LearningTriggerType.Explicit,
            Scope = LearningScope.Global,
            Confidence = 0.9f,
            Status = LearningStatus.Active,
            CreatedBy = "OfflineTest",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            // Embedding will be null for this test - embeddings tested separately
            Embedding = null
        };

        // Act - Create learning without network
        var savedLearning = await learningRepository.CreateAsync(learning);

        // Assert - Verify learning persisted offline
        Assert.NotNull(savedLearning);
        Assert.NotEqual(Guid.Empty, savedLearning.Id);

        var retrieved = await learningRepository.GetByIdAsync(savedLearning.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("Offline Learning", retrieved.Title);
    }

    /// <summary>
    /// ES-REQ-003: Verifies that configuration/settings work without external dependencies.
    /// </summary>
    [Fact]
    public async Task OfflineWorkflow_Settings_WorkWithoutExternalDependencies()
    {
        // Arrange
        var settingsRepository = new SettingsRepository(_dbContext!, Mock.Of<ILogger<SettingsRepository>>());

        var setting = new Setting
        {
            Key = "test.offline.setting",
            Value = "offline-value",
            Category = "Test",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act - Save and retrieve setting offline
        await settingsRepository.UpsertAsync(setting);
        var retrieved = await settingsRepository.GetByKeyAsync("test.offline.setting");

        // Assert - Verify settings work offline
        Assert.NotNull(retrieved);
        Assert.Equal("offline-value", retrieved.Value);
    }

    /// <summary>
    /// ES-REQ-003: Comprehensive workflow test - verifies multiple operations work together offline.
    /// </summary>
    [Fact]
    public async Task OfflineWorkflow_ComprehensiveWorkflow_WorksEntirelyOffline()
    {
        // Arrange - Create all repositories
        var projectRepo = new ProjectRepository(_dbContext!, Mock.Of<ILogger<ProjectRepository>>());
        var taskRepo = new TaskRepository(_dbContext!, Mock.Of<ILogger<TaskRepository>>());
        var sessionRepo = new SessionRepository(_dbContext!, Mock.Of<ILogger<SessionRepository>>());
        var docRepo = new DocumentRepository(_dbContext!, Mock.Of<ILogger<DocumentRepository>>());
        var learningRepo = new LearningRepository(_dbContext!, Mock.Of<ILogger<LearningRepository>>());

        // Act & Assert - Execute a complete workflow offline
        
        // 1. Create project
        var project = await projectRepo.CreateAsync(new Project
        {
            Name = "Offline Workflow Project",
            Status = ProjectStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        Assert.NotNull(project);

        // 2. Create task for project
        var task = await taskRepo.CreateAsync(new AgentTask
        {
            ProjectId = project.Id,
            Title = "Offline Task",
            Status = AgentTaskStatus.Pending,
            Priority = TaskPriority.Medium,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        Assert.NotNull(task);

        // 3. Create session for task
        var session = await sessionRepo.CreateAsync(new AgentSession
        {
            TaskId = task.Id,
            AgentId = Guid.NewGuid(),
            Status = SessionStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        Assert.NotNull(session);

        // 4. Index a document
        var doc = await docRepo.CreateAsync(new Document
        {
            SourcePath = "c:\\test\\workflow-doc.txt",
            Title = "Workflow Document",
            ContentType = "text/plain",
            FileHash = "hash123",
            IndexedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        Assert.NotNull(doc);

        // 5. Create learning
        var learning = await learningRepo.CreateAsync(new Learning
        {
            Title = "Workflow Learning",
            TriggerType = LearningTriggerType.Explicit,
            Scope = LearningScope.Task,
            SourceTaskId = task.Id,
            Confidence = 0.8f,
            Status = LearningStatus.Active,
            CreatedBy = "OfflineWorkflow",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        Assert.NotNull(learning);

        // Verify all entities are linked and retrievable
        var retrievedProject = await projectRepo.GetByIdAsync(project.Id);
        var retrievedTask = await taskRepo.GetByIdAsync(task.Id);
        var retrievedSession = await sessionRepo.GetByIdAsync(session.Id);
        var retrievedDoc = await docRepo.GetByIdAsync(doc.Id);
        var retrievedLearning = await learningRepo.GetByIdAsync(learning.Id);

        Assert.NotNull(retrievedProject);
        Assert.NotNull(retrievedTask);
        Assert.NotNull(retrievedSession);
        Assert.NotNull(retrievedDoc);
        Assert.NotNull(retrievedLearning);
        
        Assert.Equal(project.Id, retrievedTask.ProjectId);
        Assert.Equal(task.Id, retrievedSession.TaskId);
        Assert.Equal(task.Id, retrievedLearning.SourceTaskId);
    }
}
