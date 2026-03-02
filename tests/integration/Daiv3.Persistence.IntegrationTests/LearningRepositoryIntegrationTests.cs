using Daiv3.Persistence;
using Daiv3.Persistence.Entities;
using Daiv3.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Daiv3.IntegrationTests.Persistence;

[Collection("Database")]
public class LearningRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly string _testDbPath;
    private readonly ILoggerFactory _loggerFactory;
    private DatabaseContext? _databaseContext;

    public LearningRepositoryIntegrationTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"daiv3_learning_repo_test_{Guid.NewGuid():N}.db");
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
    public async Task AddAndGetById_PersistsLearningWithProvenanceAndTimestamps()
    {
        var databaseContext = await CreateInitializedContextAsync();
        var repository = new LearningRepository(databaseContext, _loggerFactory.CreateLogger<LearningRepository>());

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var learningId = Guid.NewGuid().ToString();

        var learning = new Learning
        {
            LearningId = learningId,
            Title = "Always use 'using' blocks for file streams",
            Description = "When working with FileStream, always wrap in a using block to ensure proper disposal. Leaving streams open caused file lock errors in task ABC-123.",
            TriggerType = "CompilationError",
            Scope = "Global",
            SourceAgent = "agent-001",
            SourceTaskId = "task-abc-123",
            Tags = "csharp,file-io,streams",
            Confidence = 0.95,
            Status = "Active",
            TimesApplied = 0,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = "agent-001"
        };

        await repository.AddAsync(learning);

        var retrieved = await repository.GetByIdAsync(learningId);

        Assert.NotNull(retrieved);
        Assert.Equal(learningId, retrieved!.LearningId);
        Assert.Equal("Always use 'using' blocks for file streams", retrieved.Title);
        Assert.Equal("When working with FileStream, always wrap in a using block to ensure proper disposal. Leaving streams open caused file lock errors in task ABC-123.", retrieved.Description);
        Assert.Equal("CompilationError", retrieved.TriggerType);
        Assert.Equal("Global", retrieved.Scope);
        Assert.Equal("agent-001", retrieved.SourceAgent);
        Assert.Equal("task-abc-123", retrieved.SourceTaskId);
        Assert.Equal("csharp,file-io,streams", retrieved.Tags);
        Assert.Equal(0.95, retrieved.Confidence);
        Assert.Equal("Active", retrieved.Status);
        Assert.Equal(0, retrieved.TimesApplied);
        Assert.Equal(now, retrieved.CreatedAt);
        Assert.Equal(now, retrieved.UpdatedAt);
        Assert.Equal("agent-001", retrieved.CreatedBy);
    }

    [Fact]
    public async Task AddAndGetById_PersistsLearningWithEmbedding()
    {
        var databaseContext = await CreateInitializedContextAsync();
        var repository = new LearningRepository(databaseContext, _loggerFactory.CreateLogger<LearningRepository>());

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var learningId = Guid.NewGuid().ToString();
        var embedding = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }; // Mock embedding

        var learning = new Learning
        {
            LearningId = learningId,
            Title = "Test learning with embedding",
            Description = "This learning has an embedding for semantic retrieval.",
            TriggerType = "Explicit",
            Scope = "Agent",
            SourceAgent = "agent-002",
            SourceTaskId = null,
            EmbeddingBlob = embedding,
            EmbeddingDimensions = 384,
            Tags = "test",
            Confidence = 0.8,
            Status = "Active",
            TimesApplied = 5,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = "user"
        };

        await repository.AddAsync(learning);

        var retrieved = await repository.GetByIdAsync(learningId);

        Assert.NotNull(retrieved);
        Assert.NotNull(retrieved!.EmbeddingBlob);
        Assert.Equal(384, retrieved.EmbeddingDimensions);
        Assert.Equal(embedding, retrieved.EmbeddingBlob);
        Assert.Equal(5, retrieved.TimesApplied);
    }

    [Fact]
    public async Task Update_ChangesLearningFieldsAndUpdateTimestamp()
    {
        var databaseContext = await CreateInitializedContextAsync();
        var repository = new LearningRepository(databaseContext, _loggerFactory.CreateLogger<LearningRepository>());

        var createdAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var learningId = Guid.NewGuid().ToString();

        await repository.AddAsync(new Learning
        {
            LearningId = learningId,
            Title = "Original title",
            Description = "Original description",
            TriggerType = "UserFeedback",
            Scope = "Agent",
            SourceAgent = "agent-003",
            SourceTaskId = "task-001",
            Confidence = 0.7,
            Status = "Active",
            TimesApplied = 2,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
            CreatedBy = "agent-003"
        });

        var existing = await repository.GetByIdAsync(learningId);
        Assert.NotNull(existing);

        var updatedAt = createdAt + 300;
        existing!.Title = "Updated title";
        existing.Description = "Updated description with more details.";
        existing.Confidence = 0.9;
        existing.Status = "Superseded";
        existing.TimesApplied = 10;
        existing.UpdatedAt = updatedAt;

        await repository.UpdateAsync(existing);

        var updated = await repository.GetByIdAsync(learningId);
        Assert.NotNull(updated);
        Assert.Equal("Updated title", updated!.Title);
        Assert.Equal("Updated description with more details.", updated.Description);
        Assert.Equal(0.9, updated.Confidence);
        Assert.Equal("Superseded", updated.Status);
        Assert.Equal(10, updated.TimesApplied);
        Assert.Equal(createdAt, updated.CreatedAt); // CreatedAt unchanged
        Assert.Equal(updatedAt, updated.UpdatedAt);
    }

    [Fact]
    public async Task Delete_SoftDeletesByArchivingLearning()
    {
        var databaseContext = await CreateInitializedContextAsync();
        var repository = new LearningRepository(databaseContext, _loggerFactory.CreateLogger<LearningRepository>());

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var learningId = Guid.NewGuid().ToString();

        await repository.AddAsync(new Learning
        {
            LearningId = learningId,
            Title = "Learning to be deleted",
            Description = "This learning will be soft-deleted.",
            TriggerType = "SelfCorrection",
            Scope = "Project",
            SourceAgent = "agent-004",
            SourceTaskId = "task-002",
            Confidence = 0.6,
            Status = "Active",
            TimesApplied = 1,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = "agent-004"
        });

        await repository.DeleteAsync(learningId);

        var archived = await repository.GetByIdAsync(learningId);
        Assert.NotNull(archived);
        Assert.Equal("Archived", archived!.Status);
        Assert.True(archived.UpdatedAt >= now); // UpdatedAt changed or stayed same
    }

    [Fact]
    public async Task GetActiveAsync_ReturnsOnlyActiveLearnings()
    {
        var databaseContext = await CreateInitializedContextAsync();
        var repository = new LearningRepository(databaseContext, _loggerFactory.CreateLogger<LearningRepository>());

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await repository.AddAsync(CreateTestLearning("learning-active-1", "Active", now));
        await repository.AddAsync(CreateTestLearning("learning-active-2", "Active", now));
        await repository.AddAsync(CreateTestLearning("learning-suppressed", "Suppressed", now));
        await repository.AddAsync(CreateTestLearning("learning-archived", "Archived", now));

        var activeLearnings = await repository.GetActiveAsync();

        Assert.Equal(2, activeLearnings.Count);
        Assert.All(activeLearnings, learning => Assert.Equal("Active", learning.Status));
    }

    [Fact]
    public async Task GetByStatusAsync_FiltersLearningsByStatus()
    {
        var databaseContext = await CreateInitializedContextAsync();
        var repository = new LearningRepository(databaseContext, _loggerFactory.CreateLogger<LearningRepository>());

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await repository.AddAsync(CreateTestLearning("learning-suppressed-1", "Suppressed", now));
        await repository.AddAsync(CreateTestLearning("learning-suppressed-2", "Suppressed", now));
        await repository.AddAsync(CreateTestLearning("learning-active", "Active", now));

        var suppressedLearnings = await repository.GetByStatusAsync("Suppressed");

        Assert.Equal(2, suppressedLearnings.Count);
        Assert.All(suppressedLearnings, learning => Assert.Equal("Suppressed", learning.Status));
    }

    [Fact]
    public async Task GetByScopeAsync_FiltersLearningsByScope()
    {
        var databaseContext = await CreateInitializedContextAsync();
        var repository = new LearningRepository(databaseContext, _loggerFactory.CreateLogger<LearningRepository>());

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await repository.AddAsync(CreateTestLearningWithScope("learning-global", "Global", now));
        await repository.AddAsync(CreateTestLearningWithScope("learning-agent", "Agent", now));
        await repository.AddAsync(CreateTestLearningWithScope("learning-project", "Project", now));

        var globalLearnings = await repository.GetByScopeAsync("Global");

        Assert.Single(globalLearnings);
        Assert.Equal("Global", globalLearnings[0].Scope);

        var agentLearnings = await repository.GetByScopeAsync("Agent");
        Assert.Single(agentLearnings);
        Assert.Equal("Agent", agentLearnings[0].Scope);
    }

    [Fact]
    public async Task GetBySourceAgentAsync_ReturnsLearningsFromSpecificAgent()
    {
        var databaseContext = await CreateInitializedContextAsync();
        var repository = new LearningRepository(databaseContext, _loggerFactory.CreateLogger<LearningRepository>());

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await repository.AddAsync(CreateTestLearningWithAgent("learning-1", "agent-alpha", now));
        await repository.AddAsync(CreateTestLearningWithAgent("learning-2", "agent-alpha", now));
        await repository.AddAsync(CreateTestLearningWithAgent("learning-3", "agent-beta", now));

        var alphaLearnings = await repository.GetBySourceAgentAsync("agent-alpha");

        Assert.Equal(2, alphaLearnings.Count);
        Assert.All(alphaLearnings, learning => Assert.Equal("agent-alpha", learning.SourceAgent));
    }

    [Fact]
    public async Task GetBySourceTaskAsync_ReturnsLearningsFromSpecificTask()
    {
        var databaseContext = await CreateInitializedContextAsync();
        var repository = new LearningRepository(databaseContext, _loggerFactory.CreateLogger<LearningRepository>());

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await repository.AddAsync(CreateTestLearningWithTask("learning-1", "task-xyz-001", now));
        await repository.AddAsync(CreateTestLearningWithTask("learning-2", "task-xyz-001", now));
        await repository.AddAsync(CreateTestLearningWithTask("learning-3", "task-abc-002", now));

        var taskLearnings = await repository.GetBySourceTaskAsync("task-xyz-001");

        Assert.Equal(2, taskLearnings.Count);
        Assert.All(taskLearnings, learning => Assert.Equal("task-xyz-001", learning.SourceTaskId));
    }

    [Fact]
    public async Task IncrementTimesAppliedAsync_IncrementsCounterAndUpdatesTimestamp()
    {
        var databaseContext = await CreateInitializedContextAsync();
        var repository = new LearningRepository(databaseContext, _loggerFactory.CreateLogger<LearningRepository>());

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var learningId = Guid.NewGuid().ToString();

        await repository.AddAsync(new Learning
        {
            LearningId = learningId,
            Title = "Test increment",
            Description = "Test times applied increment",
            TriggerType = "Explicit",
            Scope = "Global",
            SourceAgent = "agent-005",
            SourceTaskId = null,
            Confidence = 0.8,
            Status = "Active",
            TimesApplied = 5,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = "user"
        });

        await repository.IncrementTimesAppliedAsync(learningId);

        var updated = await repository.GetByIdAsync(learningId);
        Assert.NotNull(updated);
        Assert.Equal(6, updated!.TimesApplied);
        Assert.True(updated.UpdatedAt >= now); // UpdatedAt changed or stayed same

        // Increment again
        await repository.IncrementTimesAppliedAsync(learningId);
        var updated2 = await repository.GetByIdAsync(learningId);
        Assert.Equal(7, updated2!.TimesApplied);
    }

    [Fact]
    public async Task GetWithEmbeddingsAsync_ReturnsOnlyActiveLearningsWithEmbeddings()
    {
        var databaseContext = await CreateInitializedContextAsync();
        var repository = new LearningRepository(databaseContext, _loggerFactory.CreateLogger<LearningRepository>());

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var embedding = new byte[] { 1, 2, 3, 4 };

        await repository.AddAsync(CreateTestLearningWithEmbedding("learning-1", embedding, now));
        await repository.AddAsync(CreateTestLearningWithEmbedding("learning-2", embedding, now));
        await repository.AddAsync(CreateTestLearning("learning-no-embed", "Active", now)); // No embedding

        var withEmbeddings = await repository.GetWithEmbeddingsAsync();

        Assert.Equal(2, withEmbeddings.Count);
        Assert.All(withEmbeddings, learning =>
        {
            Assert.NotNull(learning.EmbeddingBlob);
            Assert.Equal("Active", learning.Status);
        });
    }

    [Fact]
    public async Task Schema_EnforcesCheckConstraintsOnTriggerType()
    {
        var databaseContext = await CreateInitializedContextAsync();
        var repository = new LearningRepository(databaseContext, _loggerFactory.CreateLogger<LearningRepository>());

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var learning = CreateTestLearning("invalid-trigger", "Active", now);
        learning.TriggerType = "InvalidTriggerType"; // Not in CHECK constraint

        await Assert.ThrowsAsync<SqliteException>(async () => await repository.AddAsync(learning));
    }

    [Fact]
    public async Task Schema_EnforcesCheckConstraintsOnScope()
    {
        var databaseContext = await CreateInitializedContextAsync();
        var repository = new LearningRepository(databaseContext, _loggerFactory.CreateLogger<LearningRepository>());

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var learning = CreateTestLearning("invalid-scope", "Active", now);
        learning.Scope = "InvalidScope"; // Not in CHECK constraint

        await Assert.ThrowsAsync<SqliteException>(async () => await repository.AddAsync(learning));
    }

    [Fact]
    public async Task Schema_EnforcesCheckConstraintsOnConfidence()
    {
        var databaseContext = await CreateInitializedContextAsync();
        var repository = new LearningRepository(databaseContext, _loggerFactory.CreateLogger<LearningRepository>());

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var learning = CreateTestLearning("invalid-confidence", "Active", now);
        learning.Confidence = 1.5; // Outside 0.0-1.0 range

        await Assert.ThrowsAsync<SqliteException>(async () => await repository.AddAsync(learning));
    }

    [Fact]
    public async Task ProvenanceFields_AllowNullValuesForManualLearnings()
    {
        var databaseContext = await CreateInitializedContextAsync();
        var repository = new LearningRepository(databaseContext, _loggerFactory.CreateLogger<LearningRepository>());

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var learningId = Guid.NewGuid().ToString();

        var learning = new Learning
        {
            LearningId = learningId,
            Title = "Manual learning",
            Description = "Created manually by user without source agent or task",
            TriggerType = "Explicit",
            Scope = "Global",
            SourceAgent = null, // Provenance field can be null
            SourceTaskId = null, // Provenance field can be null
            Confidence = 0.9,
            Status = "Active",
            TimesApplied = 0,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = "user"
        };

        await repository.AddAsync(learning);

        var retrieved = await repository.GetByIdAsync(learningId);
        Assert.NotNull(retrieved);
        Assert.Null(retrieved!.SourceAgent);
        Assert.Null(retrieved.SourceTaskId);
        Assert.Equal("user", retrieved.CreatedBy);
    }

    // Helper methods
    private async Task<DatabaseContext> CreateInitializedContextAsync()
    {
        var options = Options.Create(new PersistenceOptions { DatabasePath = _testDbPath });
        var context = new DatabaseContext(_loggerFactory.CreateLogger<DatabaseContext>(), options);
        await context.InitializeAsync();
        _databaseContext = context;
        return context;
    }

    private static Learning CreateTestLearning(string id, string status, long timestamp)
    {
        return new Learning
        {
            LearningId = id,
            Title = $"Test learning {id}",
            Description = $"Description for {id}",
            TriggerType = "Explicit",
            Scope = "Global",
            SourceAgent = "test-agent",
            SourceTaskId = "test-task",
            Confidence = 0.8,
            Status = status,
            TimesApplied = 0,
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
            CreatedBy = "test"
        };
    }

    private static Learning CreateTestLearningWithScope(string id, string scope, long timestamp)
    {
        var learning = CreateTestLearning(id, "Active", timestamp);
        learning.Scope = scope;
        return learning;
    }

    private static Learning CreateTestLearningWithAgent(string id, string sourceAgent, long timestamp)
    {
        var learning = CreateTestLearning(id, "Active", timestamp);
        learning.SourceAgent = sourceAgent;
        return learning;
    }

    private static Learning CreateTestLearningWithTask(string id, string sourceTaskId, long timestamp)
    {
        var learning = CreateTestLearning(id, "Active", timestamp);
        learning.SourceTaskId = sourceTaskId;
        return learning;
    }

    private static Learning CreateTestLearningWithEmbedding(string id, byte[] embedding, long timestamp)
    {
        var learning = CreateTestLearning(id, "Active", timestamp);
        learning.EmbeddingBlob = embedding;
        learning.EmbeddingDimensions = 384;
        return learning;
    }
}
