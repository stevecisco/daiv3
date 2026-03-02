using Daiv3.Persistence;
using Daiv3.Persistence.Entities;
using Daiv3.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Daiv3.UnitTests.Persistence;

/// <summary>
/// Unit tests for learning lifecycle operations: suppress, promote, supersede.
/// Tests LM-REQ-008: Users SHALL suppress, promote, or supersede learnings.
/// </summary>
public class LearningLifecycleTests : IAsyncLifetime
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IDatabaseContext _databaseContext;
    private readonly LearningStorageService _learningService;
    private readonly ILogger<LearningLifecycleTests> _logger;
    private readonly string _testDbPath;

    public LearningLifecycleTests()
    {
        // Use a temporary database file for each test
        _testDbPath = Path.Combine(Path.GetTempPath(), $"learning-lifecycle-test-{Guid.NewGuid()}.db");
        
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
        _learningService = _serviceProvider.GetRequiredService<LearningStorageService>();
        _logger = _serviceProvider.GetRequiredService<ILogger<LearningLifecycleTests>>();
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

    [Fact]
    public async Task SuppressLearningAsync_ActiveLearning_SetsStatusToSuppressed()
    {
        // Arrange
        var learningId = await _learningService.CreateLearningAsync(
            "Test Learning",
            "Test description",
            "UserFeedback",
            "Global",
            0.85);

        // Act
        await _learningService.SuppressLearningAsync(learningId);

        // Assert
        var learning = await _learningService.GetLearningAsync(learningId);
        Assert.NotNull(learning);
        Assert.Equal("Suppressed", learning.Status);
    }

    [Fact]
    public async Task SuppressLearningAsync_NonExistentLearning_DoesNotThrow()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid().ToString();

        // Act & Assert - should not throw
        await _learningService.SuppressLearningAsync(nonExistentId);
    }

    [Fact]
    public async Task SupersedeLearningAsync_ActiveLearning_SetsStatusToSuperseded()
    {
        // Arrange
        var learningId = await _learningService.CreateLearningAsync(
            "Test Learning",
            "Test description",
            "UserFeedback",
            "Global",
            0.85);

        // Act
        await _learningService.SupersedeLearningAsync(learningId);

        // Assert
        var learning = await _learningService.GetLearningAsync(learningId);
        Assert.NotNull(learning);
        Assert.Equal("Superseded", learning.Status);
    }

    [Fact]
    public async Task SupersedeLearningAsync_NonExistentLearning_DoesNotThrow()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid().ToString();

        // Act & Assert - should not throw
        await _learningService.SupersedeLearningAsync(nonExistentId);
    }

    [Fact]
    public async Task PromoteLearningAsync_SkillScope_PromotesToAgent()
    {
        // Arrange
        var learningId = await _learningService.CreateLearningAsync(
            "Test Learning",
            "Test description",
            "UserFeedback",
            "Skill",
            0.85);

        // Act
        var newScope = await _learningService.PromoteLearningAsync(learningId);

        // Assert
        Assert.Equal("Agent", newScope);
        var learning = await _learningService.GetLearningAsync(learningId);
        Assert.NotNull(learning);
        Assert.Equal("Agent", learning.Scope);
    }

    [Fact]
    public async Task PromoteLearningAsync_AgentScope_PromotesToProject()
    {
        // Arrange
        var learningId = await _learningService.CreateLearningAsync(
            "Test Learning",
            "Test description",
            "UserFeedback",
            "Agent",
            0.85);

        // Act
        var newScope = await _learningService.PromoteLearningAsync(learningId);

        // Assert
        Assert.Equal("Project", newScope);
        var learning = await _learningService.GetLearningAsync(learningId);
        Assert.NotNull(learning);
        Assert.Equal("Project", learning.Scope);
    }

    [Fact]
    public async Task PromoteLearningAsync_ProjectScope_PromotesToDomain()
    {
        // Arrange
        var learningId = await _learningService.CreateLearningAsync(
            "Test Learning",
            "Test description",
            "UserFeedback",
            "Project",
            0.85);

        // Act
        var newScope = await _learningService.PromoteLearningAsync(learningId);

        // Assert
        Assert.Equal("Domain", newScope);
        var learning = await _learningService.GetLearningAsync(learningId);
        Assert.NotNull(learning);
        Assert.Equal("Domain", learning.Scope);
    }

    [Fact]
    public async Task PromoteLearningAsync_DomainScope_PromotesToGlobal()
    {
        // Arrange
        var learningId = await _learningService.CreateLearningAsync(
            "Test Learning",
            "Test description",
            "UserFeedback",
            "Domain",
            0.85);

        // Act
        var newScope = await _learningService.PromoteLearningAsync(learningId);

        // Assert
        Assert.Equal("Global", newScope);
        var learning = await _learningService.GetLearningAsync(learningId);
        Assert.NotNull(learning);
        Assert.Equal("Global", learning.Scope);
    }

    [Fact]
    public async Task PromoteLearningAsync_GlobalScope_ReturnsNull()
    {
        // Arrange
        var learningId = await _learningService.CreateLearningAsync(
            "Test Learning",
            "Test description",
            "UserFeedback",
            "Global",
            0.85);

        // Act
        var newScope = await _learningService.PromoteLearningAsync(learningId);

        // Assert
        Assert.Null(newScope);
        var learning = await _learningService.GetLearningAsync(learningId);
        Assert.NotNull(learning);
        Assert.Equal("Global", learning.Scope); // Unchanged
    }

    [Fact]
    public async Task PromoteLearningAsync_NonExistentLearning_ReturnsNull()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid().ToString();

        // Act
        var newScope = await _learningService.PromoteLearningAsync(nonExistentId);

        // Assert
        Assert.Null(newScope);
    }

    [Fact]
    public async Task PromoteLearningAsync_MultipleLevels_PromotesSequentially()
    {
        // Arrange
        var learningId = await _learningService.CreateLearningAsync(
            "Test Learning",
            "Test description",
            "UserFeedback",
            "Skill",
            0.85);

        // Act - Promote multiple times
        var scope1 = await _learningService.PromoteLearningAsync(learningId); // Skill → Agent
        var scope2 = await _learningService.PromoteLearningAsync(learningId); // Agent → Project
        var scope3 = await _learningService.PromoteLearningAsync(learningId); // Project → Domain
        var scope4 = await _learningService.PromoteLearningAsync(learningId); // Domain → Global
        var scope5 = await _learningService.PromoteLearningAsync(learningId); // Global → (no change)

        // Assert
        Assert.Equal("Agent", scope1);
        Assert.Equal("Project", scope2);
        Assert.Equal("Domain", scope3);
        Assert.Equal("Global", scope4);
        Assert.Null(scope5); // Already at Global

        var learning = await _learningService.GetLearningAsync(learningId);
        Assert.NotNull(learning);
        Assert.Equal("Global", learning.Scope);
    }

    [Fact]
    public async Task PromoteLearningAsync_UpdatesTimestamp()
    {
        // Arrange
        var learningId = await _learningService.CreateLearningAsync(
            "Test Learning",
            "Test description",
            "UserFeedback",
            "Skill",
            0.85);

        var learningBefore = await _learningService.GetLearningAsync(learningId);
        var updatedAtBefore = learningBefore!.UpdatedAt;

        // Wait to ensure timestamp changes (Unix timestamps are second-precision)
        await Task.Delay(1100);

        // Act
        await _learningService.PromoteLearningAsync(learningId);

        // Assert
        var learningAfter = await _learningService.GetLearningAsync(learningId);
        Assert.NotNull(learningAfter);
        Assert.True(learningAfter.UpdatedAt > updatedAtBefore);
    }

    [Fact]
    public async Task SuppressLearningAsync_UpdatesTimestamp()
    {
        // Arrange
        var learningId = await _learningService.CreateLearningAsync(
            "Test Learning",
            "Test description",
            "UserFeedback",
            "Global",
            0.85);

        var learningBefore = await _learningService.GetLearningAsync(learningId);
        var updatedAtBefore = learningBefore!.UpdatedAt;

        // Wait to ensure timestamp changes (Unix timestamps are second-precision)
        await Task.Delay(1100);

        // Act
        await _learningService.SuppressLearningAsync(learningId);

        // Assert
        var learningAfter = await _learningService.GetLearningAsync(learningId);
        Assert.NotNull(learningAfter);
        Assert.True(learningAfter.UpdatedAt > updatedAtBefore);
    }

    [Fact]
    public async Task SupersedeLearningAsync_UpdatesTimestamp()
    {
        // Arrange
        var learningId = await _learningService.CreateLearningAsync(
            "Test Learning",
            "Test description",
            "UserFeedback",
            "Global",
            0.85);

        var learningBefore = await _learningService.GetLearningAsync(learningId);
        var updatedAtBefore = learningBefore!.UpdatedAt;

        // Wait to ensure timestamp changes (Unix timestamps are second-precision)
        await Task.Delay(1100);

        // Act
        await _learningService.SupersedeLearningAsync(learningId);

        // Assert
        var learningAfter = await _learningService.GetLearningAsync(learningId);
        Assert.NotNull(learningAfter);
        Assert.True(learningAfter.UpdatedAt > updatedAtBefore);
    }
}
