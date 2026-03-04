using Daiv3.Persistence;
using Daiv3.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

#pragma warning disable IDISP006, IDISP003 // Test classes don't need to implement IDisposable; Test methods create disposable instances without explicit disposal (xUnit cleanup handles it)

namespace Daiv3.UnitTests.Persistence;

/// <summary>
/// Unit tests for promotion reversibility (KBP-NFR-001).
/// Tests the ability to revert promotions and restore learnings to previous scopes.
/// </summary>
[Collection("Database")]
public class PromotionReversibilityTests : IAsyncLifetime
{
    private readonly string _testDbPath;
    private IServiceProvider? _serviceProvider;

    public PromotionReversibilityTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"kbp_nfr_001_{Guid.NewGuid()}.db");
    }

    public async Task InitializeAsync()
    {
        // Dispose previous if exists (for test reruns)
        if (_serviceProvider is IAsyncDisposable oldDisposable)
        {
            await oldDisposable.DisposeAsync();
        }

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
    public async Task RevertPromotion_WithValidPromotion_RestoresLearningToOriginalScope()
    {
        // Arrange
        var learningService = _serviceProvider!.GetRequiredService<LearningStorageService>();
        var promotionRepo = _serviceProvider!.GetRequiredService<PromotionRepository>();
        var revertRepo = _serviceProvider!.GetRequiredService<RevertPromotionRepository>();

        var learningId = await learningService.CreateLearningAsync(
            "Test Learning", "Test Description", "UserFeedback", "Skill", 0.8);

        // Promote Skill → Agent
        var newScope = await learningService.PromoteLearningAsync(learningId, "test-user");
        Assert.Equal("Agent", newScope);

        var promotions = await promotionRepo.GetByLearningIdAsync(learningId);
        Assert.Single(promotions);
        var promotionId = promotions[0].PromotionId;

        // Act: Revert the promotion
        var result = await learningService.RevertPromotionAsync(promotionId, "test-user", "Testing revert");

        // Assert
        Assert.True(result);

        // Verify learning is restored to Skill scope
        var learning = await learningService.GetLearningAsync(learningId);
        Assert.NotNull(learning);
        Assert.Equal("Skill", learning.Scope);

        // Verify revert record exists
        var revertRecord = await revertRepo.GetByPromotionIdAsync(promotionId);
        Assert.NotNull(revertRecord);
        Assert.Equal(learningId, revertRecord.LearningId);
        Assert.Equal("Agent", revertRecord.RevertedFromScope);
        Assert.Equal("Skill", revertRecord.RevertedToScope);
        Assert.Equal("test-user", revertRecord.RevertedBy);
        Assert.Equal("Testing revert", revertRecord.Notes);
    }

    [Fact]
    public async Task RevertPromotion_WithNonexistentPromotion_ReturnsFalse()
    {
        // Arrange
        var learningService = _serviceProvider!.GetRequiredService<LearningStorageService>();

        // Act
        var result = await learningService.RevertPromotionAsync("non-existent-promotion-id");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task RevertPromotion_WhenAlreadyReverted_ReturnsFalse()
    {
        // Arrange
        var learningService = _serviceProvider!.GetRequiredService<LearningStorageService>();
        var promotionRepo = _serviceProvider!.GetRequiredService<PromotionRepository>();

        var learningId = await learningService.CreateLearningAsync(
            "Test Learning", "Test Description", "UserFeedback", "Skill", 0.8);

        var newScope = await learningService.PromoteLearningAsync(learningId);
        var promotions = await promotionRepo.GetByLearningIdAsync(learningId);
        var promotionId = promotions[0].PromotionId;

        // First revert
        var result1 = await learningService.RevertPromotionAsync(promotionId);
        Assert.True(result1);

        // Act: Try to revert again
        var result2 = await learningService.RevertPromotionAsync(promotionId);

        // Assert
        Assert.False(result2);
    }

    [Fact]
    public async Task RevertPromotion_UpdatesMetrics()
    {
        // Arrange
        var learningService = _serviceProvider!.GetRequiredService<LearningStorageService>();
        var promotionRepo = _serviceProvider!.GetRequiredService<PromotionRepository>();
        var metricRepo = _serviceProvider!.GetRequiredService<PromotionMetricRepository>();

        var learningId = await learningService.CreateLearningAsync(
            "Test Learning", "Test Description", "UserFeedback", "Skill", 0.8);

        var newScope = await learningService.PromoteLearningAsync(learningId);
        var promotions = await promotionRepo.GetByLearningIdAsync(learningId);
        var promotionId = promotions[0].PromotionId;

        // Act: Revert promotion
        var result = await learningService.RevertPromotionAsync(promotionId);

        // Assert
        Assert.True(result);

        // Verify metric was recorded
        var metrics = await metricRepo.GetByMetricNameAsync("revert_events");
        Assert.NotEmpty(metrics);
        Assert.Contains(metrics, m => m.Context?.Contains(promotionId) ?? false);
    }

    [Fact]
    public async Task RevertPromotion_PreservesAuditTrail()
    {
        // Arrange
        var learningService = _serviceProvider!.GetRequiredService<LearningStorageService>();
        var promotionRepo = _serviceProvider!.GetRequiredService<PromotionRepository>();
        var revertRepo = _serviceProvider!.GetRequiredService<RevertPromotionRepository>();

        var learningId = await learningService.CreateLearningAsync(
            "Test Learning", "Test Description", "UserFeedback", "Skill", 0.85);

        await learningService.PromoteLearningAsync(learningId, "alice", "task-001");
        var promotions = await promotionRepo.GetByLearningIdAsync(learningId);
        var promotionId = promotions[0].PromotionId;

        // Act: Revert
        await learningService.RevertPromotionAsync(promotionId, "bob", "Incorrect promotion");

        // Assert: Audit trail preserved
        var originalPromotion = await promotionRepo.GetByIdAsync(promotionId);
        Assert.NotNull(originalPromotion);
        Assert.Equal("alice", originalPromotion.PromotedBy);
        Assert.Equal("task-001", originalPromotion.SourceTaskId);

        var revert = await revertRepo.GetByPromotionIdAsync(promotionId);
        Assert.NotNull(revert);
        Assert.Equal("bob", revert.RevertedBy);
        Assert.Equal("Incorrect promotion", revert.Notes);
    }

    [Fact]
    public async Task GetByRevertedByAsync_FiltersCorrectly()
    {
        // Arrange
        var learningService = _serviceProvider!.GetRequiredService<LearningStorageService>();
        var promotionRepo = _serviceProvider!.GetRequiredService<PromotionRepository>();
        var revertRepo = _serviceProvider!.GetRequiredService<RevertPromotionRepository>();

        // Create and revert multiple learnings
        var learning1 = await learningService.CreateLearningAsync(
            "Learning 1", "Desc 1", "UserFeedback", "Skill", 0.8);
        var learning2 = await learningService.CreateLearningAsync(
            "Learning 2", "Desc 2", "UserFeedback", "Agent", 0.85);

        await learningService.PromoteLearningAsync(learning1);
        await learningService.PromoteLearningAsync(learning2);

        var proms1 = await promotionRepo.GetByLearningIdAsync(learning1);
        var proms2 = await promotionRepo.GetByLearningIdAsync(learning2);

        await learningService.RevertPromotionAsync(proms1[0].PromotionId, "alice");
        await learningService.RevertPromotionAsync(proms2[0].PromotionId, "bob");

        // Act
        var aliceReverts = await revertRepo.GetByRevertedByAsync("alice");
        var bobReverts = await revertRepo.GetByRevertedByAsync("bob");

        // Assert
        Assert.Single(aliceReverts);
        Assert.Single(bobReverts);
        Assert.Equal(learning1, aliceReverts[0].LearningId);
        Assert.Equal(learning2, bobReverts[0].LearningId);
    }

    [Fact]
    public async Task GetByTimeRangeAsync_FiltersCorrectly()
    {
        // Arrange
        var learningService = _serviceProvider!.GetRequiredService<LearningStorageService>();
        var promotionRepo = _serviceProvider!.GetRequiredService<PromotionRepository>();
        var revertRepo = _serviceProvider!.GetRequiredService<RevertPromotionRepository>();

        var learningId = await learningService.CreateLearningAsync(
            "Test Learning", "Test Description", "UserFeedback", "Skill", 0.8);

        await learningService.PromoteLearningAsync(learningId);
        var promotions = await promotionRepo.GetByLearningIdAsync(learningId);

        var beforeRevert = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await learningService.RevertPromotionAsync(promotions[0].PromotionId);
        var afterRevert = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 60;

        // Act
        var reverts = await revertRepo.GetByTimeRangeAsync(beforeRevert, afterRevert);

        // Assert
        Assert.NotEmpty(reverts);
        Assert.True(reverts.All(r => r.RevertedAt >= beforeRevert && r.RevertedAt <= afterRevert));
    }
}

