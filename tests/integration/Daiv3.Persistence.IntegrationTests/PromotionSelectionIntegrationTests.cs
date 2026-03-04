using Daiv3.Persistence;
using Daiv3.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Daiv3.IntegrationTests.Persistence;

/// <summary>
/// Integration tests for learning promotion selection workflow (KBP-REQ-002).
/// Tests the full end-to-end workflow of selecting and promoting learnings from a task.
/// </summary>
[Collection("Database")]
public class PromotionSelectionIntegrationTests : IAsyncLifetime
{
    private DatabaseContext? _databaseContext;
    private LearningStorageService? _learningService;
    private PromotionRepository? _promotionRepository;
    private LearningRepository? _learningRepository;
    private readonly string _testDatabasePath;
    private readonly ILogger<DatabaseContext> _logger;

    public PromotionSelectionIntegrationTests()
    {
        _testDatabasePath = Path.Combine(
            Path.GetTempPath(),
            "daiv3_test",
            $"test_{Guid.NewGuid()}.db");

        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        _logger = loggerFactory.CreateLogger<DatabaseContext>();
    }

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_testDatabasePath)!);

        var persistenceOptions = Options.Create(new PersistenceOptions
        {
            DatabasePath = _testDatabasePath
        });

        _databaseContext = new DatabaseContext(
            _logger,
            persistenceOptions);

        await _databaseContext.InitializeAsync();

        _learningRepository = new LearningRepository(_databaseContext, NullLogger<LearningRepository>.Instance);
        _promotionRepository = new PromotionRepository(_databaseContext, NullLogger<PromotionRepository>.Instance);

        _learningService = new LearningStorageService(
            _learningRepository,
            NullLogger<LearningStorageService>.Instance,
            null,
            _promotionRepository);
    }

    public async Task DisposeAsync()
    {
        if (_databaseContext != null)
        {
            await _databaseContext.DisposeAsync();
            _databaseContext = null;
        }

        // Clean up test database
        SqliteConnection.ClearAllPools();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        await Task.Delay(200);

        if (File.Exists(_testDatabasePath))
        {
            try
            {
                File.Delete(_testDatabasePath);
            }
            catch
            {
                // File may be locked, ignore
            }
        }
    }

    [Fact]
    public async Task PromoteLearningsFromTask_WithStoredLearnings_SuccessfullyPromotes()
    {
        // Arrange
        var taskId = "task-integration-001";

        // Create test learnings from the task
        var learningId1 = await _learningService!.CreateLearningAsync(
            title: "Database Query Optimization",
            description: "Using indexes improves query performance significantly",
            triggerType: "UserFeedback",
            scope: "Skill",
            confidence: 0.95,
            sourceTaskId: taskId,
            sourceAgent: "DatabaseAgent",
            createdBy: "user");

        var learningId2 = await _learningService.CreateLearningAsync(
            title: "Async/Await Patterns",
            description: "Always use ConfigureAwait(false) in libraries",
            triggerType: "SelfCorrection",
            scope: "Agent",
            confidence: 0.85,
            sourceTaskId: taskId,
            sourceAgent: "DatabaseAgent",
            createdBy: "user");

        // Verify learnings were created
        var learningsFromTask = await _learningService.GetLearningsBySourceTaskAsync(taskId);
        Assert.Equal(2, learningsFromTask.Count);

        // Act - Create promotion selections
        var promotions = new List<LearningPromotionSelection>
        {
            new()
            {
                LearningId = learningId1,
                TargetScope = "Agent",
                Notes = "Promoting skill to agent level for team-wide application"
            },
            new()
            {
                LearningId = learningId2,
                TargetScope = "Project",
                Notes = "Promoting agent pattern to project level"
            }
        };

        // Execute the batch promotion
        var result = await _learningService.PromoteLearningsFromTaskAsync(
            taskId,
            promotions.AsReadOnly(),
            "test-user");

        // Assert
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.SuccessfulPromotions.Count);
        Assert.Empty(result.FailedPromotions);
        Assert.True(result.AllSucceeded);

        // Verify promotions were recorded
        var updatedLearning1 = await _learningService.GetLearningAsync(learningId1);
        Assert.NotNull(updatedLearning1);
        Assert.Equal("Agent", updatedLearning1.Scope);

        var updatedLearning2 = await _learningService.GetLearningAsync(learningId2);
        Assert.NotNull(updatedLearning2);
        Assert.Equal("Project", updatedLearning2.Scope);
    }

    [Fact]
    public async Task PromoteLearningsFromTask_WithMultipleTasks_OnlyAffectsSelectedTask()
    {
        // Arrange
        var task1 = "task-001";
        var task2 = "task-002";

        // Create learnings from task 1
        var task1Learning1 = await _learningService!.CreateLearningAsync(
            title: "Task 1 Learning 1",
            description: "Test",
            triggerType: "Explicit",
            scope: "Skill",
            confidence: 0.8,
            sourceTaskId: task1,
            createdBy: "user");

        // Create learnings from task 2
        var task2Learning1 = await _learningService.CreateLearningAsync(
            title: "Task 2 Learning 1",
            description: "Test",
            triggerType: "Explicit",
            scope: "Skill",
            confidence: 0.8,
            sourceTaskId: task2,
            createdBy: "user");

        // Act - Promote only task 1 learnings
        var promotions = new List<LearningPromotionSelection>
        {
            new() { LearningId = task1Learning1, TargetScope = "Agent" }
        };

        await _learningService.PromoteLearningsFromTaskAsync(task1, promotions.AsReadOnly());

        // Assert - Task 1 learning should be promoted
        var promoted = await _learningService.GetLearningAsync(task1Learning1);
        Assert.Equal("Agent", promoted!.Scope);

        // Task 2 learning should remain unchanged
        var unchanged = await _learningService.GetLearningAsync(task2Learning1);
        Assert.Equal("Skill", unchanged!.Scope);
    }

    [Fact]
    public async Task PromoteLearningsFromTaskAsync_TrackingPromotionHistory()
    {
        // Arrange
        var taskId = "task-history-001";

        var learningId = await _learningService!.CreateLearningAsync(
            title: "Test Learning",
            description: "For promotion history tracking",
            triggerType: "UserFeedback",
            scope: "Skill",
            confidence: 0.9,
            sourceTaskId: taskId,
            createdBy: "user");

        var promotions = new List<LearningPromotionSelection>
        {
            new() { LearningId = learningId, TargetScope = "Agent" }
        };

        // Act
        var result = await _learningService.PromoteLearningsFromTaskAsync(
            taskId,
            promotions.AsReadOnly(),
            "test-user");

        // Assert - Promotion was successful
        Assert.Single(result.SuccessfulPromotions);
        Assert.True(result.AllSucceeded);

        // Verify the learning scope was updated
        var updatedLearning = await _learningService.GetLearningAsync(learningId);
        Assert.NotNull(updatedLearning);
        Assert.Equal("Agent", updatedLearning.Scope);
        Assert.True(updatedLearning.UpdatedAt > 0);
    }

    [Fact]
    public async Task ListTaskLearningsAsync_RetrievesAllLearningsFromTask()
    {
        // Arrange
        var taskId = "task-list-001";

        // Create multiple learnings from the same task
        for (int i = 0; i < 5; i++)
        {
            await _learningService!.CreateLearningAsync(
                title: $"Learning {i + 1}",
                description: $"Test learning {i + 1}",
                triggerType: "Explicit",
                scope: "Skill",
                confidence: 0.5 + (i * 0.1),
                sourceTaskId: taskId,
                createdBy: "user");
        }

        // Act
        var learnings = await _learningService!.GetLearningsBySourceTaskAsync(taskId);

        // Assert
        Assert.Equal(5, learnings.Count);

        // Verify they're sorted by creation time (descending)
        for (int i = 0; i < learnings.Count - 1; i++)
        {
            Assert.True(learnings[i].CreatedAt >= learnings[i + 1].CreatedAt);
        }
    }

    [Fact]
    public async Task PartialPromotionWithErrors_RecordsFailuresCorrectly()
    {
        // Arrange
        var taskId = "task-partial-001";

        // Create one valid learning
        var validLearningId = await _learningService!.CreateLearningAsync(
            title: "Valid Learning",
            description: "Test",
            triggerType: "Explicit",
            scope: "Skill",
            confidence: 0.8,
            sourceTaskId: taskId,
            createdBy: "user");

        // Create promotion selections with both valid and invalid IDs
        var promotions = new List<LearningPromotionSelection>
        {
            new() { LearningId = validLearningId, TargetScope = "Agent" },
            new() { LearningId = "non-existent-id", TargetScope = "Agent" },
            new() { LearningId = validLearningId, TargetScope = "InvalidScope" }
        };

        // Act
        var result = await _learningService.PromoteLearningsFromTaskAsync(taskId, promotions.AsReadOnly());

        // Assert
        Assert.Equal(3, result.TotalCount);
        Assert.Single(result.SuccessfulPromotions);
        Assert.Equal(2, result.FailedPromotions.Count);
        Assert.False(result.AllSucceeded);

        // Check error codes
        var errors = result.FailedPromotions.Values.ToList();
        Assert.Contains(errors, e => e.ErrorCode == "LearningNotFound");
        Assert.Contains(errors, e => e.ErrorCode == "InvalidTargetScope");
    }
}
