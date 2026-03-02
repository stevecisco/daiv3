using Daiv3.Persistence;
using Daiv3.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Daiv3.Persistence.IntegrationTests;

/// <summary>
/// Integration tests for promotion transparency and metrics (KBP-NFR-001).
/// Tests instrumentation, metrics collection, and operational observability.
/// </summary>
[Collection("Database")]
public class PromotionTransparencyIntegrationTests : IAsyncLifetime
{
    private readonly string _testDbPath;
    private IServiceProvider? _serviceProvider;

    public PromotionTransparencyIntegrationTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"promotion_transparency_{Guid.NewGuid()}.db");
    }

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.Configure<PersistenceOptions>(options =>
        {
            options.DatabasePath = _testDbPath;
        });
        services.AddPersistence();

        _serviceProvider = services.BuildServiceProvider();
        await _serviceProvider.InitializeDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        if (_serviceProvider is IAsyncDisposable disposable)
        {
            await disposable.DisposeAsync();
        }

        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); }
            catch { /* Ignore cleanup errors */ }
        }
    }

    [Fact]
    public async Task PromotionHistoryTracking_CreatesAuditTrail()
    {
        // Arrange
        var learningService = _serviceProvider!.GetRequiredService<ILearningStorageService>();
        var promotionRepo = _serviceProvider!.GetRequiredService<PromotionRepository>();

        var learning1 = await learningService.CreateLearningAsync(
            "Learning 1", "Description", "UserFeedback", "Skill", 0.8);
        var learning2 = await learningService.CreateLearningAsync(
            "Learning 2", "Description", "UserFeedback", "Agent", 0.85);

        // Act: Promote both learnings
        await learningService.PromoteLearningAsync(learning1, "alice", "task-001");
        await learningService.PromoteLearningAsync(learning2, "bob", "task-002");

        // Additional promotion of learning1
        var promotion = await promotionRepo.GetByLearningIdAsync(learning1);
        await learningService.PromoteLearningAsync(learning1, "alice", "task-003");

        // Assert: Full audit trail
        var learning1History = await promotionRepo.GetByLearningIdAsync(learning1);
        var learning2History = await promotionRepo.GetByLearningIdAsync(learning2);

        Assert.Equal(2, learning1History.Count);
        Assert.Single(learning2History);

        // Source task tracking
        var taskPromotions = await promotionRepo.GetBySourceTaskIdAsync("task-001");
        Assert.Single(taskPromotions);
        Assert.Equal(learning1, taskPromotions[0].LearningId);

        // Promoter tracking
        var alicePromotions = await promotionRepo.GetByPromotedByAsync("alice");
        var bobPromotions = await promotionRepo.GetByPromotedByAsync("bob");

        Assert.Equal(2, alicePromotions.Count);
        Assert.Single(bobPromotions);
    }

    [Fact]
    public async Task PromotionMetrricsCollection_RecordsRevertEvents()
    {
        // Arrange
        var learningService = _serviceProvider!.GetRequiredService<ILearningStorageService>();
        var promotionRepo = _serviceProvider!.GetRequiredService<PromotionRepository>();
        var metricRepo = _serviceProvider!.GetRequiredService<PromotionMetricRepository>();

        var learningId = await learningService.CreateLearningAsync(
            "Test Learning", "Description", "UserFeedback", "Skill", 0.8);

        // Act: Create and revert promotions
        await learningService.PromoteLearningAsync(learningId, "user1");
        var promotions = await promotionRepo.GetByLearningIdAsync(learningId);
        var promotionId = promotions[0].PromotionId;

        await learningService.RevertPromotionAsync(promotionId, "user2");

        // Assert: Metrics recorded
        var revertMetrics = await metricRepo.GetByMetricNameAsync("revert_events");
        Assert.NotEmpty(revertMetrics);

        // Verify metric context
        var revertMetric = revertMetrics.FirstOrDefault();
        Assert.NotNull(revertMetric);
        Assert.Equal("revert_events", revertMetric.MetricName);
        Assert.Equal(1.0, revertMetric.MetricValue);
        Assert.NotNull(revertMetric.Context);
        Assert.Contains(promotionId, revertMetric.Context);
    }

    [Fact]
    public async Task PromotionQueryFilters_ProvideCompleteVisibility()
    {
        // Arrange
        var learningService = _serviceProvider!.GetRequiredService<ILearningStorageService>();
        var promotionRepo = _serviceProvider!.GetRequiredService<PromotionRepository>();

        // Create multiple learnings with different promotions
        var l1 = await learningService.CreateLearningAsync(
            "L1", "D", "UserFeedback", "Skill", 0.8);
        var l2 = await learningService.CreateLearningAsync(
            "L2", "D", "SelfCorrection", "Agent", 0.85);
        var l3 = await learningService.CreateLearningAsync(
            "L3", "D", "ToolFailure", "Project", 0.9);

        // Promote all
        await learningService.PromoteLearningAsync(l1, "alice", "task-a");
        await learningService.PromoteLearningAsync(l2, "bob", "task-b");
        await learningService.PromoteLearningAsync(l3, "alice", "task-a");

        // Act + Assert: Query by various filters
        var allPromotions = await promotionRepo.GetAllAsync();
        Assert.Equal(3, allPromotions.Count);

        var alicePromotions = await promotionRepo.GetByPromotedByAsync("alice");
        Assert.Equal(2, alicePromotions.Count);

        var taskAPromotions = await promotionRepo.GetBySourceTaskIdAsync("task-a");
        Assert.Equal(2, taskAPromotions.Count);

        var toAgentPromotions = await promotionRepo.GetByToScopeAsync("Agent");
        Assert.Single(toAgentPromotions);

        var toProjectPromotions = await promotionRepo.GetByToScopeAsync("Project");
        Assert.Single(toProjectPromotions);

        // Time-based query
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var recentPromotions = await promotionRepo.GetByTimeRangeAsync(now - 3600, now + 3600);
        Assert.Equal(3, recentPromotions.Count);
    }

    [Fact]
    public async Task RevertPromotionWithMetrics_CompletesSuccessfully()
    {
        // Arrange
        var learningService = _serviceProvider!.GetRequiredService<ILearningStorageService>();
        var promotionRepo = _serviceProvider!.GetRequiredService<PromotionRepository>();
        var revertRepo = _serviceProvider!.GetRequiredService<RevertPromotionRepository>();
        var metricRepo = _serviceProvider!.GetRequiredService<PromotionMetricRepository>();

        var learningId = await learningService.CreateLearningAsync(
            "Test", "Description", "UserFeedback", "Skill", 0.8);

        // Act: Full lifecycle
        var promotedScope = await learningService.PromoteLearningAsync(
            learningId,
            "alice",
            "task-001",
            null,
            "Promoting because of high test performance");

        var promotions = await promotionRepo.GetByLearningIdAsync(learningId);
        var promotionId = promotions[0].PromotionId;

        var reverted = await learningService.RevertPromotionAsync(
            promotionId,
            "alice",
            "Reverting: not applicable after discussion");

        // Assert: Complete transparency and reversibility
        Assert.Equal("Agent", promotedScope);
        Assert.True(reverted);

        // Verify learning state
        var learning = await learningService.GetLearningAsync(learningId);
        Assert.Equal("Skill", learning!.Scope);

        // Verify audit trail
        var originalPromotion = await promotionRepo.GetByIdAsync(promotionId);
        Assert.NotNull(originalPromotion);
        Assert.Equal("Skill", originalPromotion.FromScope);
        Assert.Equal("Agent", originalPromotion.ToScope);
        Assert.Equal("Promoting because of high test performance", originalPromotion.Notes);

        var revertRecord = await revertRepo.GetByPromotionIdAsync(promotionId);
        Assert.NotNull(revertRecord);
        Assert.Equal("Reverting: not applicable after discussion", revertRecord.Notes);

        // Verify metrics collected
        var metrics = await metricRepo.GetLatestByMetricNameAsync("revert_events");
        Assert.NotNull(metrics);
    }

    [Fact]
    public async Task PromotionTransparency_WithBatchPromotions()
    {
        // Arrange
        var learningService = _serviceProvider!.GetRequiredService<ILearningStorageService>();
        var promotionRepo = _serviceProvider!.GetRequiredService<PromotionRepository>();

        // Create learnings from a simulated task
        var l1 = await learningService.CreateLearningAsync("L1", "D", "UserFeedback", "Skill", 0.8);
        var l2 = await learningService.CreateLearningAsync("L2", "D", "UserFeedback", "Skill", 0.85);
        var l3 = await learningService.CreateLearningAsync("L3", "D", "UserFeedback", "Agent", 0.9);

        // Act: Batch promotion
        var selections = new[]
        {
            new LearningPromotionSelection { LearningId = l1, TargetScope = "Project", Notes = "High confidence" },
            new LearningPromotionSelection { LearningId = l2, TargetScope = "Project", Notes = "Frequently reused" },
            new LearningPromotionSelection { LearningId = l3, TargetScope = "Project", Notes = "Cross-task pattern" }
        };

        var result = await learningService.PromoteLearningsFromTaskAsync("task-batch-001", selections, "batch-processor");

        // Assert: Transparency
        Assert.Equal(3, result.SuccessfulPromotions.Count);
        Assert.Empty(result.FailedPromotions);

        var taskPromotions = await promotionRepo.GetBySourceTaskIdAsync("task-batch-001");
        Assert.Equal(3, taskPromotions.Count);

        // Verify all have correct source task
        Assert.All(taskPromotions, p => Assert.Equal("task-batch-001", p.SourceTaskId));

        // Verify notes preserved
        var l1Promotions = await promotionRepo.GetByLearningIdAsync(l1);
        Assert.Equal("High confidence", l1Promotions[0].Notes);
    }
}
