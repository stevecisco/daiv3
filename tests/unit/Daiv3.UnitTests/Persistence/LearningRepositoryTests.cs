using Daiv3.Persistence;
using Daiv3.Persistence.Entities;
using Daiv3.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Daiv3.UnitTests.Persistence;

/// <summary>
/// Unit tests for LearningRepository.
/// Tests CRUD operations, filtering, and semantic search queries.
/// These tests use an in-memory SQLite database for isolation.
/// </summary>
public class LearningRepositoryTests : IAsyncLifetime
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IDatabaseContext _databaseContext;
    private readonly LearningRepository _repository;
    private readonly ILogger<LearningRepositoryTests> _logger;
    private readonly string _testDbPath;

    public LearningRepositoryTests()
    {
        // Use a temporary database file for each test
        _testDbPath = Path.Combine(Path.GetTempPath(), $"learning-test-{Guid.NewGuid()}.db");
        
        // Set up test SQLite database
        var services = new ServiceCollection();
        services.AddLogging();
        
        // Configure persistence with test database path
        services.Configure<PersistenceOptions>(options =>
        {
            options.DatabasePath = _testDbPath;
        });
        
        services.AddPersistence();

        _serviceProvider = services.BuildServiceProvider();
        _databaseContext = _serviceProvider.GetRequiredService<IDatabaseContext>();
        _repository = new LearningRepository(
            _databaseContext,
            _serviceProvider.GetRequiredService<ILogger<LearningRepository>>()
        );
        _logger = _serviceProvider.GetRequiredService<ILogger<LearningRepositoryTests>>();
    }

    public async Task InitializeAsync()
    {
        // Initialize database and run migrations
        await _databaseContext.InitializeAsync();
        _logger.LogInformation("Test database initialized");
    }

    public async Task DisposeAsync()
    {
        await _databaseContext.DisposeAsync();
        
        // Clean up test database file
        try
        {
            if (File.Exists(_testDbPath))
            {
                File.Delete(_testDbPath);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    private Learning CreateTestLearning(
        string title = "Test Learning",
        string description = "Test description",
        string scope = "Global",
        double confidence = 0.85,
        string status = "Active")
    {
        return new Learning
        {
            LearningId = Guid.NewGuid().ToString(),
            Title = title,
            Description = description,
            TriggerType = "UserFeedback",
            Scope = scope,
            SourceAgent = "test-agent",
            SourceTaskId = Guid.NewGuid().ToString(),
            Tags = "test,learning",
            Confidence = confidence,
            Status = status,
            TimesApplied = 0,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            CreatedBy = "test-user"
        };
    }

    [Fact]
    public async Task AddAsync_ValidLearning_ReturnsLearningId()
    {
        // Arrange
        var learning = CreateTestLearning();

        // Act
        var learningId = await _repository.AddAsync(learning);

        // Assert
        Assert.NotNull(learningId);
        Assert.Equal(learning.LearningId, learningId);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingLearning_ReturnsSameLearning()
    {
        // Arrange
        var learning = CreateTestLearning();
        await _repository.AddAsync(learning);

        // Act
        var retrieved = await _repository.GetByIdAsync(learning.LearningId);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(learning.LearningId, retrieved.LearningId);
        Assert.Equal(learning.Title, retrieved.Title);
        Assert.Equal(learning.Description, retrieved.Description);
        Assert.Equal(learning.Scope, retrieved.Scope);
        Assert.Equal(learning.Confidence, retrieved.Confidence);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentLearning_ReturnsNull()
    {
        // Act
        var retrieved = await _repository.GetByIdAsync(Guid.NewGuid().ToString());

        // Assert
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task GetAllAsync_MultipleLearnings_ReturnsAll()
    {
        // Arrange
        var learning1 = CreateTestLearning("Learning 1");
        var learning2 = CreateTestLearning("Learning 2");
        var learning3 = CreateTestLearning("Learning 3");

        await _repository.AddAsync(learning1);
        await Task.Delay(10);
        await _repository.AddAsync(learning2);
        await Task.Delay(10);
        await _repository.AddAsync(learning3);

        // Act
        var all = await _repository.GetAllAsync();

        // Assert
        Assert.NotEmpty(all);
        Assert.Equal(3, all.Count);
        // Verify all three are present (order may vary)
        var titles = all.Select(l => l.Title).ToList();
        Assert.Contains("Learning 1", titles);
        Assert.Contains("Learning 2", titles);
        Assert.Contains("Learning 3", titles);
    }

    [Fact]
    public async Task UpdateAsync_ValidUpdate_ModifiesLearning()
    {
        // Arrange
        var learning = CreateTestLearning();
        await _repository.AddAsync(learning);

        learning.Title = "Updated Title";
        learning.Description = "Updated Description";
        learning.Confidence = 0.95;
        learning.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Act
        await _repository.UpdateAsync(learning);

        // Verify
        var updated = await _repository.GetByIdAsync(learning.LearningId);
        Assert.NotNull(updated);
        Assert.Equal("Updated Title", updated.Title);
        Assert.Equal("Updated Description", updated.Description);
        Assert.Equal(0.95, updated.Confidence);
    }

    [Fact]
    public async Task DeleteAsync_ExistingLearning_ArchivesLearning()
    {
        // Arrange
        var learning = CreateTestLearning();
        await _repository.AddAsync(learning);

        // Act
        await _repository.DeleteAsync(learning.LearningId);

        // Verify - should be archived, not deleted
        var retrieved = await _repository.GetByIdAsync(learning.LearningId);
        Assert.NotNull(retrieved);
        Assert.Equal("Archived", retrieved.Status);
    }

    [Fact]
    public async Task GetActiveAsync_OnlyReturnsActiveLearnings()
    {
        // Arrange
        var active1 = CreateTestLearning("Active 1", status: "Active");
        var active2 = CreateTestLearning("Active 2", status: "Active");
        var suppressed = CreateTestLearning("Suppressed 1", status: "Suppressed");

        await _repository.AddAsync(active1);
        await _repository.AddAsync(active2);
        await _repository.AddAsync(suppressed);

        // Act
        var active = await _repository.GetActiveAsync();

        // Assert
        Assert.Equal(2, active.Count);
        Assert.All(active, l => Assert.Equal("Active", l.Status));
    }

    [Fact]
    public async Task GetByStatusAsync_FiltersByStatus()
    {
        // Arrange
        var learning1 = CreateTestLearning("Learning 1");
        var learning2 = CreateTestLearning("Learning 2");

        await _repository.AddAsync(learning1);
        await _repository.AddAsync(learning2);

        // Archive one
        await _repository.DeleteAsync(learning1.LearningId);

        // Act
        var archived = await _repository.GetByStatusAsync("Archived");
        var active = await _repository.GetByStatusAsync("Active");

        // Assert
        Assert.Single(archived);
        Assert.Single(active);
        Assert.Equal(learning1.LearningId, archived[0].LearningId);
        Assert.Equal(learning2.LearningId, active[0].LearningId);
    }

    [Fact]
    public async Task GetByScopeAsync_OnlyReturnsLearningsWithMatchingScope()
    {
        // Arrange
        var globalLearning = CreateTestLearning("Global", scope: "Global");
        var agentLearning = CreateTestLearning("Agent", scope: "Agent");
        var projectLearning = CreateTestLearning("Project", scope: "Project");

        await _repository.AddAsync(globalLearning);
        await _repository.AddAsync(agentLearning);
        await _repository.AddAsync(projectLearning);

        // Act
        var agentScoped = await _repository.GetByScopeAsync("Agent");

        // Assert
        Assert.Single(agentScoped);
        Assert.Equal(agentLearning.LearningId, agentScoped[0].LearningId);
    }

    [Fact]
    public async Task GetBySourceAgentAsync_FiltersBySourceAgent()
    {
        // Arrange
        var learning1 = CreateTestLearning("Learning 1");
        learning1.SourceAgent = "agent-1";
        
        var learning2 = CreateTestLearning("Learning 2");
        learning2.SourceAgent = "agent-2";

        await _repository.AddAsync(learning1);
        await _repository.AddAsync(learning2);

        // Act
        var agent1Learnings = await _repository.GetBySourceAgentAsync("agent-1");
        var agent2Learnings = await _repository.GetBySourceAgentAsync("agent-2");

        // Assert
        Assert.Single(agent1Learnings);
        Assert.Single(agent2Learnings);
        Assert.Equal(learning1.LearningId, agent1Learnings[0].LearningId);
        Assert.Equal(learning2.LearningId, agent2Learnings[0].LearningId);
    }

    [Fact]
    public async Task GetBySourceTaskAsync_TrackingProvenance()
    {
        // Arrange
        var taskId = Guid.NewGuid().ToString();
        var learning1 = CreateTestLearning("Learning 1");
        learning1.SourceTaskId = taskId;
        
        var learning2 = CreateTestLearning("Learning 2");
        learning2.SourceTaskId = Guid.NewGuid().ToString();

        await _repository.AddAsync(learning1);
        await _repository.AddAsync(learning2);

        // Act
        var taskLearnings = await _repository.GetBySourceTaskAsync(taskId);

        // Assert
        Assert.Single(taskLearnings);
        Assert.Equal(learning1.LearningId, taskLearnings[0].LearningId);
    }

    [Fact]
    public async Task IncrementTimesAppliedAsync_IncrementsCounter()
    {
        // Arrange
        var learning = CreateTestLearning();
        await _repository.AddAsync(learning);

        var initialValue = learning.TimesApplied;

        // Act
        await _repository.IncrementTimesAppliedAsync(learning.LearningId);
        await _repository.IncrementTimesAppliedAsync(learning.LearningId);

        // Verify
        var updated = await _repository.GetByIdAsync(learning.LearningId);
        Assert.NotNull(updated);
        Assert.Equal(initialValue + 2, updated.TimesApplied);
    }

    [Fact]
    public async Task GetWithEmbeddingsAsync_OnlyReturnsLearningsWithEmbeddings()
    {
        // Arrange
        var withEmbedding = CreateTestLearning("With Embedding");
        withEmbedding.EmbeddingBlob = new byte[] { 1, 2, 3, 4 };
        withEmbedding.EmbeddingDimensions = 384;
        
        var withoutEmbedding = CreateTestLearning("Without Embedding");
        withoutEmbedding.EmbeddingBlob = null;

        await _repository.AddAsync(withEmbedding);
        await _repository.AddAsync(withoutEmbedding);

        // Act
        var withEmbeddings = await _repository.GetWithEmbeddingsAsync();

        // Assert
        Assert.Single(withEmbeddings);
        Assert.Equal(withEmbedding.LearningId, withEmbeddings[0].LearningId);
    }

    [Fact]
    public async Task AddAsync_NullTitle_ThrowsArgumentException()
    {
        // Arrange
        var learning = CreateTestLearning();
        learning.Title = null!;

        // Act & Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _repository.AddAsync(learning));
    }

    [Fact]
    public async Task AddAsync_NullDescription_ThrowsArgumentException()
    {
        // Arrange
        var learning = CreateTestLearning();
        learning.Description = null!;

        // Act & Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _repository.AddAsync(learning));
    }
}

