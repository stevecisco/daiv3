using Daiv3.Knowledge.Embedding;
using Daiv3.Orchestration;
using Daiv3.Orchestration.Models;
using Daiv3.Persistence;
using Daiv3.Persistence.Entities;
using Daiv3.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Daiv3.IntegrationTests.Persistence;

/// <summary>
/// Integration tests for manual learning creation (LM-REQ-009).
/// Tests end-to-end scenarios: creating learnings via ExplicitTriggerContext.
/// </summary>
[Collection("Database")]
public class ManualLearningCreationTests : IAsyncLifetime
{
    private readonly string _testDbPath;
    private readonly ILoggerFactory _loggerFactory;
    private DatabaseContext? _databaseContext;

    public ManualLearningCreationTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"daiv3_manual_learning_test_{Guid.NewGuid():N}.db");
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

    private async Task<LearningService> CreateLearningServiceAsync()
    {
        _databaseContext = new DatabaseContext(
            _loggerFactory.CreateLogger<DatabaseContext>(),
            Options.Create(new PersistenceOptions { DatabasePath = _testDbPath }));
        await _databaseContext.InitializeAsync();

        var repository = new LearningRepository(
            _databaseContext,
            _loggerFactory.CreateLogger<LearningRepository>());

        var mockEmbeddingGenerator = new Mock<IEmbeddingGenerator>();
        mockEmbeddingGenerator
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 0.1f, 0.2f, 0.3f });

        return new LearningService(
            _loggerFactory.CreateLogger<LearningService>(),
            repository,
            mockEmbeddingGenerator.Object);
    }

    [Fact]
    public async Task ManualCreation_WithAllFields_SavesSuccessfully()
    {
        var learningService = await CreateLearningServiceAsync();
        
        var context = new ExplicitTriggerContext
        {
            Title = "Manual Testing Pattern",
            Description = "Always use arrange-act-assert pattern in tests",
            Scope = "Project",
            Confidence = 0.95,
            Tags = "testing,quality",
            SourceAgent = "test-skill",
            SourceTaskId = "task-123",
            CreatedBy = "user",
            AgentReasoning = "User captured a quality pattern"
        };

        var learning = await learningService.CreateExplicitLearningAsync(context);

        Assert.NotNull(learning);
        Assert.False(string.IsNullOrEmpty(learning.LearningId));
        Assert.Equal("Manual Testing Pattern", learning.Title);
        Assert.Equal("Always use arrange-act-assert pattern in tests", learning.Description);
        Assert.Equal("Explicit", learning.TriggerType);
        Assert.Equal("Project", learning.Scope);
        Assert.Equal(0.95, learning.Confidence);
        Assert.Equal("testing,quality", learning.Tags);
        Assert.Equal("test-skill", learning.SourceAgent);
        Assert.Equal("task-123", learning.SourceTaskId);
        Assert.Equal("user", learning.CreatedBy);
        Assert.Equal("Active", learning.Status);
    }

    [Fact]
    public async Task ManualCreation_WithMinimalFields_UsesDefaults()
    {
        var learningService = await CreateLearningServiceAsync();

        var context = new ExplicitTriggerContext
        {
            Title = "Minimal Learning",
            Description = "Created with only required fields",
            CreatedBy = "user"
        };

        var learning = await learningService.CreateExplicitLearningAsync(context);

        Assert.NotNull(learning);
        Assert.Equal("Minimal Learning", learning.Title);
        Assert.Equal("Created with only required fields", learning.Description);
        Assert.Equal("Explicit", learning.TriggerType);
        Assert.Equal("Global", learning.Scope); // Default
        Assert.Equal(0.75, learning.Confidence); // Explicit default
        Assert.Equal("Active", learning.Status);
        Assert.Equal("user", learning.CreatedBy);
    }

    [Fact]
    public async Task ManualCreation_GeneratesEmbedding()
    {
        var learningService = await CreateLearningServiceAsync();

        var context = new ExplicitTriggerContext
        {
            Title = "Learning with Embedding",
            Description = "This should have an embedding for semantic search",
            CreatedBy = "user"
        };

        var learning = await learningService.CreateExplicitLearningAsync(context);

        Assert.NotNull(learning.EmbeddingBlob);
        Assert.True(learning.EmbeddingBlob.Length > 0);
        Assert.Equal(3, learning.EmbeddingDimensions);
    }

    [Fact]
    public async Task ManualCreation_WithDifferentScopes_AllSupported()
    {
        var learningService = await CreateLearningServiceAsync();
        var scopes = new[] { "Global", "Agent", "Skill", "Project", "Domain" };

        foreach (var scope in scopes)
        {
            var context = new ExplicitTriggerContext
            {
                Title = $"Learning - {scope}",
                Description = "Testing scope support",
                Scope = scope,
                CreatedBy = "user"
            };

            var learning = await learningService.CreateExplicitLearningAsync(context);
            Assert.Equal(scope, learning.Scope);
        }
    }

    [Fact]
    public async Task ManualCreation_MarkedAsExplicitTrigger()
    {
        var learningService = await CreateLearningServiceAsync();

        var context = new ExplicitTriggerContext
        {
            Title = "User Created",
            Description = "Should be marked explicit",
            CreatedBy = "user"
        };

        var learning = await learningService.CreateExplicitLearningAsync(context);

        Assert.Equal("Explicit", learning.TriggerType);
        Assert.Equal("user", learning.CreatedBy);
    }

    [Fact]
    public async Task ManualCreation_HighConfidence_Active()
    {
        var learningService = await CreateLearningServiceAsync();

        var context = new ExplicitTriggerContext
        {
            Title = "High Confidence Learning",
            Description = "Should be auto-injected",
            Confidence = 0.95,
            CreatedBy = "user"
        };

        var learning = await learningService.CreateExplicitLearningAsync(context);

        Assert.Equal(0.95, learning.Confidence);
        Assert.Equal("Active", learning.Status);
    }

    [Fact]
    public async Task ManualCreation_LowConfidence_StillActive()
    {
        var learningService = await CreateLearningServiceAsync();

        var context = new ExplicitTriggerContext
        {
            Title = "Low Confidence Learning",
            Description = "Should be injected as suggestion",
            Confidence = 0.3,
            CreatedBy = "user"
        };

        var learning = await learningService.CreateExplicitLearningAsync(context);

        Assert.Equal(0.3, learning.Confidence);
        Assert.Equal("Active", learning.Status);
    }

    [Fact]
    public async Task ManualCreation_MultipleSequential_AllPersisted()
    {
        var learningService = await CreateLearningServiceAsync();
        var learningIds = new List<string>();

        for (int i = 1; i <= 5; i++)
        {
            var context = new ExplicitTriggerContext
            {
                Title = $"Learning #{i}",
                Description = $"Learning {i} created by user",
                Confidence = 0.5 + (i * 0.1),
                CreatedBy = "user"
            };

            var learning = await learningService.CreateExplicitLearningAsync(context);
            learningIds.Add(learning.LearningId);
        }

        Assert.Equal(5, learningIds.Count);
        Assert.True(learningIds.All(id => !string.IsNullOrEmpty(id)));
        Assert.Equal(5, learningIds.Distinct().Count()); // All unique
    }
}
