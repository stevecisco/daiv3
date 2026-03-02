using Daiv3.Persistence;
using Daiv3.Persistence.Entities;
using Daiv3.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Daiv3.UnitTests.Persistence;

/// <summary>
/// Unit tests for PromotionRepository.
/// Tests CRUD operations and promotion history queries for KBP-DATA-001 and KBP-DATA-002.
/// These tests use an in-memory SQLite database for isolation.
/// </summary>
public class PromotionRepositoryTests : IAsyncLifetime
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IDatabaseContext _databaseContext;
    private readonly PromotionRepository _promotionRepository;
    private readonly LearningRepository _learningRepository;
    private readonly ILogger<PromotionRepositoryTests> _logger;
    private readonly string _testDbPath;

    public PromotionRepositoryTests()
    {
        // Use a temporary database file for each test
        _testDbPath = Path.Combine(Path.GetTempPath(), $"promotion-test-{Guid.NewGuid()}.db");
        
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
        _promotionRepository = _serviceProvider.GetRequiredService<PromotionRepository>();
        _learningRepository = _serviceProvider.GetRequiredService<LearningRepository>();
        _logger = _serviceProvider.GetRequiredService<ILogger<PromotionRepositoryTests>>();
    }

    public async Task InitializeAsync()
    {
        // Initialize database and run migrations
        await _databaseContext.InitializeAsync();
        _logger.LogInformation("Test database initialized");
    }

    public async Task DisposeAsync()
    {
        // Dispose service provider which handles all services including database context
        if (_serviceProvider is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
        
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

    private Learning CreateTestLearning(string scope = "Skill")
    {
        return new Learning
        {
            LearningId = Guid.NewGuid().ToString(),
            Title = "Test Learning",
            Description = "Test description",
            TriggerType = "UserFeedback",
            Scope = scope,
            SourceAgent = "test-agent",
            SourceTaskId = Guid.NewGuid().ToString(),
            Confidence = 0.85,
            Status = "Active",
            TimesApplied = 0,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            CreatedBy = "test-user"
        };
    }

    private Promotion CreateTestPromotion(
        string learningId,
        string fromScope = "Skill",
        string toScope = "Agent",
        string? sourceTaskId = null,
        string? sourceAgent = null,
        string? notes = null)
    {
        return new Promotion
        {
            PromotionId = Guid.NewGuid().ToString(),
            LearningId = learningId,
            FromScope = fromScope,
            ToScope = toScope,
            PromotedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            PromotedBy = "test-user",
            SourceTaskId = sourceTaskId,
            SourceAgent = sourceAgent,
            Notes = notes
        };
    }

    [Fact]
    public async Task AddAsync_ValidPromotion_ReturnsPromotionId()
    {
        // Arrange
        var learning = CreateTestLearning();
        await _learningRepository.AddAsync(learning);

        var promotion = CreateTestPromotion(learning.LearningId);

        // Act
        var promotionId = await _promotionRepository.AddAsync(promotion);

        // Assert
        Assert.NotNull(promotionId);
        Assert.Equal(promotion.PromotionId, promotionId);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingPromotion_ReturnsSamePromotion()
    {
        // Arrange
        var learning = CreateTestLearning();
        await _learningRepository.AddAsync(learning);

        var promotion = CreateTestPromotion(learning.LearningId);
        await _promotionRepository.AddAsync(promotion);

        // Act
        var retrieved = await _promotionRepository.GetByIdAsync(promotion.PromotionId);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(promotion.PromotionId, retrieved.PromotionId);
        Assert.Equal(promotion.LearningId, retrieved.LearningId);
        Assert.Equal(promotion.FromScope, retrieved.FromScope);
        Assert.Equal(promotion.ToScope, retrieved.ToScope);
        Assert.Equal(promotion.PromotedBy, retrieved.PromotedBy);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentPromotion_ReturnsNull()
    {
        // Act
        var result = await _promotionRepository.GetByIdAsync("non-existent-id");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByLearningIdAsync_MultiplePromotions_ReturnsAllForLearning()
    {
        // Arrange
        var learning = CreateTestLearning("Skill");
        await _learningRepository.AddAsync(learning);

        var promotion1 = CreateTestPromotion(learning.LearningId, "Skill", "Agent");
        var promotion2 = CreateTestPromotion(learning.LearningId, "Agent", "Project");
        var promotion3 = CreateTestPromotion(learning.LearningId, "Project", "Global");

        await _promotionRepository.AddAsync(promotion1);
        await Task.Delay(10); // Ensure different timestamps
        await _promotionRepository.AddAsync(promotion2);
        await Task.Delay(10);
        await _promotionRepository.AddAsync(promotion3);

        // Act
        var promotions = await _promotionRepository.GetByLearningIdAsync(learning.LearningId);

        // Assert
        Assert.NotEmpty(promotions);
        Assert.Equal(3, promotions.Count);
        Assert.All(promotions, p => Assert.Equal(learning.LearningId, p.LearningId));
        
        // Should be ordered by most recent first
        Assert.True(promotions[0].PromotedAt >= promotions[1].PromotedAt);
        Assert.True(promotions[1].PromotedAt >= promotions[2].PromotedAt);
    }

    [Fact]
    public async Task AddAsync_WithSourceTaskId_StoresProvenance()
    {
        // Arrange - KBP-DATA-001: Source task/session ID tracking
        var learning = CreateTestLearning();
        await _learningRepository.AddAsync(learning);

        var sourceTaskId = Guid.NewGuid().ToString();
        var promotion = CreateTestPromotion(learning.LearningId, sourceTaskId: sourceTaskId);

        // Act
        await _promotionRepository.AddAsync(promotion);
        var retrieved = await _promotionRepository.GetByIdAsync(promotion.PromotionId);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(sourceTaskId, retrieved.SourceTaskId);
    }

    [Fact]
    public async Task AddAsync_WithSourceAgent_StoresProvenance()
    {
        // Arrange
        var learning = CreateTestLearning();
        await _learningRepository.AddAsync(learning);

        var sourceAgent = "automation-agent-001";
        var promotion = CreateTestPromotion(learning.LearningId, sourceAgent: sourceAgent);

        // Act
        await _promotionRepository.AddAsync(promotion);
        var retrieved = await _promotionRepository.GetByIdAsync(promotion.PromotionId);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(sourceAgent, retrieved.SourceAgent);
    }

    [Fact]
    public async Task AddAsync_WithNotes_StoresNotes()
    {
        // Arrange
        var learning = CreateTestLearning();
        await _learningRepository.AddAsync(learning);

        var notes = "Promoted due to high confidence and frequent usage";
        var promotion = CreateTestPromotion(learning.LearningId, notes: notes);

        // Act
        await _promotionRepository.AddAsync(promotion);
        var retrieved = await _promotionRepository.GetByIdAsync(promotion.PromotionId);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(notes, retrieved.Notes);
    }

    [Fact]
    public async Task GetBySourceTaskIdAsync_ReturnsPromotionsFromTask()
    {
        // Arrange - KBP-DATA-001: Query promotions by source task
        var taskId = Guid.NewGuid().ToString();
        
        var learning1 = CreateTestLearning();
        var learning2 = CreateTestLearning();
        await _learningRepository.AddAsync(learning1);
        await _learningRepository.AddAsync(learning2);

        var promotion1 = CreateTestPromotion(learning1.LearningId, sourceTaskId: taskId);
        var promotion2 = CreateTestPromotion(learning2.LearningId, sourceTaskId: taskId);
        var promotion3 = CreateTestPromotion(learning1.LearningId, sourceTaskId: "different-task");

        await _promotionRepository.AddAsync(promotion1);
        await _promotionRepository.AddAsync(promotion2);
        await _promotionRepository.AddAsync(promotion3);

        // Act
        var promotions = await _promotionRepository.GetBySourceTaskIdAsync(taskId);

        // Assert
        Assert.Equal(2, promotions.Count);
        Assert.All(promotions, p => Assert.Equal(taskId, p.SourceTaskId));
    }

    [Fact]
    public async Task GetByToScopeAsync_ReturnsPromotionsToTargetScope()
    {
        // Arrange - KBP-DATA-002: Query by target scope
        var learning1 = CreateTestLearning();
        var learning2 = CreateTestLearning();
        var learning3 = CreateTestLearning();
        await _learningRepository.AddAsync(learning1);
        await _learningRepository.AddAsync(learning2);
        await _learningRepository.AddAsync(learning3);

        var promotionToGlobal1 = CreateTestPromotion(learning1.LearningId, "Domain", "Global");
        var promotionToGlobal2 = CreateTestPromotion(learning2.LearningId, "Project", "Global");
        var promotionToProject = CreateTestPromotion(learning3.LearningId, "Agent", "Project");

        await _promotionRepository.AddAsync(promotionToGlobal1);
        await _promotionRepository.AddAsync(promotionToGlobal2);
        await _promotionRepository.AddAsync(promotionToProject);

        // Act
        var globalPromotions = await _promotionRepository.GetByToScopeAsync("Global");

        // Assert
        Assert.Equal(2, globalPromotions.Count);
        Assert.All(globalPromotions, p => Assert.Equal("Global", p.ToScope));
    }

    [Fact]
    public async Task GetByPromotedByAsync_ReturnsPromotionsByUser()
    {
        // Arrange
        var learning1 = CreateTestLearning();
        var learning2 = CreateTestLearning();
        await _learningRepository.AddAsync(learning1);
        await _learningRepository.AddAsync(learning2);

        var userPromotion1 = CreateTestPromotion(learning1.LearningId);
        userPromotion1.PromotedBy = "alice";
        var userPromotion2 = CreateTestPromotion(learning2.LearningId);
        userPromotion2.PromotedBy = "alice";
        var otherPromotion = CreateTestPromotion(learning1.LearningId);
        otherPromotion.PromotedBy = "bob";

        await _promotionRepository.AddAsync(userPromotion1);
        await _promotionRepository.AddAsync(userPromotion2);
        await _promotionRepository.AddAsync(otherPromotion);

        // Act
        var alicePromotions = await _promotionRepository.GetByPromotedByAsync("alice");

        // Assert
        Assert.Equal(2, alicePromotions.Count);
        Assert.All(alicePromotions, p => Assert.Equal("alice", p.PromotedBy));
    }

    [Fact]
    public async Task GetByTimeRangeAsync_ReturnsPromotionsInRange()
    {
        // Arrange - KBP-DATA-002: Timestamp tracking
        var learning = CreateTestLearning();
        await _learningRepository.AddAsync(learning);

        var baseTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        
        var promotion1 = CreateTestPromotion(learning.LearningId);
        promotion1.PromotedAt = baseTime - 3600; // 1 hour ago
        
        var promotion2 = CreateTestPromotion(learning.LearningId);
        promotion2.PromotedAt = baseTime - 1800; // 30 minutes ago
        
        var promotion3 = CreateTestPromotion(learning.LearningId);
        promotion3.PromotedAt = baseTime + 3600; // 1 hour from now

        await _promotionRepository.AddAsync(promotion1);
        await _promotionRepository.AddAsync(promotion2);
        await _promotionRepository.AddAsync(promotion3);

        // Act - Query last hour
        var promotions = await _promotionRepository.GetByTimeRangeAsync(baseTime - 3601, baseTime);

        // Assert
        Assert.Equal(2, promotions.Count);
        Assert.Contains(promotions, p => p.PromotionId == promotion1.PromotionId);
        Assert.Contains(promotions, p => p.PromotionId == promotion2.PromotionId);
        Assert.DoesNotContain(promotions, p => p.PromotionId == promotion3.PromotionId);
    }

    [Fact]
    public async Task UpdateAsync_ModifiesPromotion()
    {
        // Arrange
        var learning = CreateTestLearning();
        await _learningRepository.AddAsync(learning);

        var promotion = CreateTestPromotion(learning.LearningId);
        await _promotionRepository.AddAsync(promotion);

        // Act - Update notes
        promotion.Notes = "Updated notes about this promotion";
        await _promotionRepository.UpdateAsync(promotion);
        var updated = await _promotionRepository.GetByIdAsync(promotion.PromotionId);

        // Assert
        Assert.NotNull(updated);
        Assert.Equal("Updated notes about this promotion", updated.Notes);
    }

    [Fact]
    public async Task DeleteAsync_RemovesPromotion()
    {
        // Arrange
        var learning = CreateTestLearning();
        await _learningRepository.AddAsync(learning);

        var promotion = CreateTestPromotion(learning.LearningId);
        await _promotionRepository.AddAsync(promotion);

        // Act
        await _promotionRepository.DeleteAsync(promotion.PromotionId);
        var deleted = await _promotionRepository.GetByIdAsync(promotion.PromotionId);

        // Assert
        Assert.Null(deleted);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllPromotions()
    {
        // Arrange
        var learning1 = CreateTestLearning();
        var learning2 = CreateTestLearning();
        await _learningRepository.AddAsync(learning1);
        await _learningRepository.AddAsync(learning2);

        var promotion1 = CreateTestPromotion(learning1.LearningId);
        var promotion2 = CreateTestPromotion(learning2.LearningId);
        var promotion3 = CreateTestPromotion(learning1.LearningId, "Agent", "Project");

        await _promotionRepository.AddAsync(promotion1);
        await _promotionRepository.AddAsync(promotion2);
        await _promotionRepository.AddAsync(promotion3);

        // Act
        var all = await _promotionRepository.GetAllAsync();

        // Assert
        Assert.True(all.Count >= 3); // May have more from other tests
        Assert.Contains(all, p => p.PromotionId == promotion1.PromotionId);
        Assert.Contains(all, p => p.PromotionId == promotion2.PromotionId);
        Assert.Contains(all, p => p.PromotionId == promotion3.PromotionId);
    }

    [Fact]
    public async Task AddAsync_NullableFIeldsAreOptional()
    {
        // Arrange - Test that optional fields can be null
        var learning = CreateTestLearning();
        await _learningRepository.AddAsync(learning);

        var promotion = new Promotion
        {
            PromotionId = Guid.NewGuid().ToString(),
            LearningId = learning.LearningId,
            FromScope = "Skill",
            ToScope = "Agent",
            PromotedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            PromotedBy = "user",
            SourceTaskId = null,
            SourceAgent = null,
            Notes = null
        };

        // Act
        await _promotionRepository.AddAsync(promotion);
        var retrieved = await _promotionRepository.GetByIdAsync(promotion.PromotionId);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Null(retrieved.SourceTaskId);
        Assert.Null(retrieved.SourceAgent);
        Assert.Null(retrieved.Notes);
    }
}
