using Daiv3.Persistence;
using Daiv3.Persistence.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Daiv3.IntegrationTests.Persistence;

/// <summary>
/// Integration tests for learning management workflows (LM-REQ-007).
/// Tests end-to-end scenarios: list, view, edit, statistics.
/// </summary>
[Collection("Database")]
public class LearningManagementWorkflowTests : IAsyncLifetime
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly List<string> _testDbPaths = new();

    public LearningManagementWorkflowTests()
    {
        _loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Warning));
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        await Task.Delay(100);

        // Clean up all test database files
        foreach (var dbPath in _testDbPaths)
        {
            if (File.Exists(dbPath))
            {
                var remainingAttempts = 10;
                while (remainingAttempts > 0)
                {
                    try
                    {
                        File.Delete(dbPath);
                        break;
                    }
                    catch (IOException) when (remainingAttempts > 1)
                    {
                        remainingAttempts--;
                        await Task.Delay(100);
                    }
                }
            }
        }

        _loggerFactory.Dispose();
    }

    private async Task<LearningStorageService> CreateServiceAsync()
    {
        var testDbPath = Path.Combine(Path.GetTempPath(), $"daiv3_learning_test_{Guid.NewGuid():N}.db");
        _testDbPaths.Add(testDbPath);
        
        var options = Options.Create(new PersistenceOptions { DatabasePath = testDbPath });
        var context = new DatabaseContext(_loggerFactory.CreateLogger<DatabaseContext>(), options);
        await context.InitializeAsync();
        
        var learningRepository = new Daiv3.Persistence.Repositories.LearningRepository(
            context,
            _loggerFactory.CreateLogger<Daiv3.Persistence.Repositories.LearningRepository>());

        var promotionRepository = new Daiv3.Persistence.Repositories.PromotionRepository(
            context,
            _loggerFactory.CreateLogger<Daiv3.Persistence.Repositories.PromotionRepository>());

        return new LearningStorageService(
            learningRepository,
            _loggerFactory.CreateLogger<LearningStorageService>(),
            metricsCollector: null,
            promotionRepository: promotionRepository);
    }

    private async Task<Daiv3.Persistence.Repositories.PromotionRepository> CreatePromotionRepositoryAsync(string existingDbPath)
    {
        var options = Options.Create(new PersistenceOptions { DatabasePath = existingDbPath });
        var context = new DatabaseContext(_loggerFactory.CreateLogger<DatabaseContext>(), options);
        await context.InitializeAsync();
        
        return new Daiv3.Persistence.Repositories.PromotionRepository(
            context,
            _loggerFactory.CreateLogger<Daiv3.Persistence.Repositories.PromotionRepository>());
    }

    [Fact]
    public async Task ListAllLearnings_ReturnsCreatedLearnings()
    {
        // Arrange: Create test learnings
        var learningService = await CreateServiceAsync();
        var learning1Id = await learningService.CreateLearningAsync(
            title: "Use dependency injection",
            description: "Always use constructor injection for better testability",
            triggerType: "UserFeedback",
            scope: "Global",
            confidence: 0.9,
            sourceAgent: "agent-001",
            tags: "architecture,di");

        var learning2Id = await learningService.CreateLearningAsync(
            title: "Close database connections",
            description: "Always close database connections to prevent leaks",
            triggerType: "CompilationError",
            scope: "Project",
            confidence: 0.85,
            sourceAgent: "agent-002",
            tags: "database,resources");

        // Act: List all learnings
        var allLearnings = await learningService.GetAllLearningsAsync();

        // Assert
        Assert.Equal(2, allLearnings.Count);
        Assert.Contains(allLearnings, l => l.LearningId == learning1Id);
        Assert.Contains(allLearnings, l => l.LearningId == learning2Id);
    }

    [Fact]
    public async Task FilterByStatus_ReturnsOnlyMatchingLearnings()
    {
        // Arrange: Create learnings with different statuses
        var learningService = await CreateServiceAsync();
        var activeLearningId = await learningService.CreateLearningAsync(
            title: "Active learning",
            description: "This is active",
            triggerType: "Explicit",
            scope: "Global",
            confidence: 0.8);

        var suppressedLearningId = await learningService.CreateLearningAsync(
            title: "Suppressed learning",
            description: "This will be suppressed",
            triggerType: "Explicit",
            scope: "Global",
            confidence: 0.7);

        await learningService.SuppressLearningAsync(suppressedLearningId);

        // Act: Filter by status
        var activeLearnings = await learningService.GetLearningsByStatusAsync("Active");
        var suppressedLearnings = await learningService.GetLearningsByStatusAsync("Suppressed");

        // Assert
        Assert.Contains(activeLearnings, l => l.LearningId == activeLearningId);
        Assert.DoesNotContain(activeLearnings, l => l.LearningId == suppressedLearningId);
        Assert.Contains(suppressedLearnings, l => l.LearningId == suppressedLearningId);
        Assert.DoesNotContain(suppressedLearnings, l => l.LearningId == activeLearningId);
    }

    [Fact]
    public async Task FilterByScope_ReturnsOnlyMatchingLearnings()
    {
        // Arrange: Create learnings with different scopes
        var learningService = await CreateServiceAsync();
        var globalLearningId = await learningService.CreateLearningAsync(
            title: "Global learning",
            description: "Applies globally",
            triggerType: "Explicit",
            scope: "Global",
            confidence: 0.9);

        var agentLearningId = await learningService.CreateLearningAsync(
            title: "Agent-specific learning",
            description: "Applies to specific agent",
            triggerType: "Explicit",
            scope: "Agent",
            confidence: 0.8,
            sourceAgent: "agent-123");

        // Act: Filter by scope
        var globalLearnings = await learningService.GetLearningsByScopeAsync("Global");
        var agentLearnings = await learningService.GetLearningsByScopeAsync("Agent");

        // Assert
        Assert.Contains(globalLearnings, l => l.LearningId == globalLearningId);
        Assert.DoesNotContain(globalLearnings, l => l.LearningId == agentLearningId);
        Assert.Contains(agentLearnings, l => l.LearningId == agentLearningId);
    }

    [Fact]
    public async Task ViewLearning_ReturnsFullDetails()
    {
        // Arrange: Create a learning with full details
        var learningService = await CreateServiceAsync();
        var learningId = await learningService.CreateLearningAsync(
            title: "Test view operation",
            description: "This tests the view functionality with detailed info",
            triggerType: "SelfCorrection",
            scope: "Task",
            confidence: 0.92,
            sourceAgent: "agent-view-test",
            sourceTaskId: "task-123",
            tags: "test,view,detailed",
            createdBy: "test-user");

        // Act: Retrieve the learning
        var learning = await learningService.GetLearningAsync(learningId);

        // Assert: Verify all fields are populated correctly
        Assert.NotNull(learning);
        Assert.Equal(learningId, learning!.LearningId);
        Assert.Equal("Test view operation", learning.Title);
        Assert.Equal("This tests the view functionality with detailed info", learning.Description);
        Assert.Equal("SelfCorrection", learning.TriggerType);
        Assert.Equal("Task", learning.Scope);
        Assert.Equal(0.92, learning.Confidence);
        Assert.Equal("agent-view-test", learning.SourceAgent);
        Assert.Equal("task-123", learning.SourceTaskId);
        Assert.Equal("test,view,detailed", learning.Tags);
        Assert.Equal("test-user", learning.CreatedBy);
        Assert.Equal("Active", learning.Status);
        Assert.Equal(0, learning.TimesApplied);
        Assert.True(learning.CreatedAt > 0);
        Assert.True(learning.UpdatedAt > 0);
    }

    [Fact]
    public async Task EditLearning_UpdatesFields()
    {
        // Arrange: Create a learning
        var learningService = await CreateServiceAsync();
        var learningId = await learningService.CreateLearningAsync(
            title: "Original title",
            description: "Original description",
            triggerType: "Explicit",
            scope: "Global",
            confidence: 0.7,
            tags: "original");

        var learning = await learningService.GetLearningAsync(learningId);
        Assert.NotNull(learning);

        var originalUpdatedAt = learning!.UpdatedAt;

        // Act: Edit the learning
        learning.Title = "Updated title";
        learning.Description = "Updated description";
        learning.Confidence = 0.95;
        learning.Tags = "updated,modified";
        learning.Scope = "Project";

        await Task.Delay(100); // Ensure timestamp will be different
        await learningService.UpdateLearningAsync(learning);

        // Assert: Verify updates persisted
        var updatedLearning = await learningService.GetLearningAsync(learningId);
        Assert.NotNull(updatedLearning);
        Assert.Equal("Updated title", updatedLearning!.Title);
        Assert.Equal("Updated description", updatedLearning.Description);
        Assert.Equal(0.95, updatedLearning.Confidence);
        Assert.Equal("updated,modified", updatedLearning.Tags);
        Assert.Equal("Project", updatedLearning.Scope);
        Assert.True(updatedLearning.UpdatedAt > originalUpdatedAt, "UpdatedAt timestamp should be greater");
    }

    [Fact]
    public async Task EditLearning_StatusChange_UpdatesPersistence()
    {
        // Arrange: Create an active learning
        var learningService = await CreateServiceAsync();
        var learningId = await learningService.CreateLearningAsync(
            title: "Learning to suppress",
            description: "This will be suppressed",
            triggerType: "Explicit",
            scope: "Global",
            confidence: 0.8);

        var learning = await learningService.GetLearningAsync(learningId);
        Assert.NotNull(learning);
        Assert.Equal("Active", learning!.Status);

        // Act: Suppress the learning
        learning.Status = "Suppressed";
        await learningService.UpdateLearningAsync(learning);

        // Assert: Verify status changed
        var updatedLearning = await learningService.GetLearningAsync(learningId);
        Assert.NotNull(updatedLearning);
        Assert.Equal("Suppressed", updatedLearning!.Status);

        // Verify it doesn't appear in active query
        var activeLearnings = await learningService.GetActiveLearningsAsync();
        Assert.DoesNotContain(activeLearnings, l => l.LearningId == learningId);
    }

    [Fact]
    public async Task Statistics_ReflectActualData()
    {
        // Arrange: Create learnings with diverse characteristics
        var learningService = await CreateServiceAsync();
        await learningService.CreateLearningAsync(
            title: "Global learning 1",
            description: "Description",
            triggerType: "UserFeedback",
            scope: "Global",
            confidence: 0.9);

        await learningService.CreateLearningAsync(
            title: "Global learning 2",
            description: "Description",
            triggerType: "CompilationError",
            scope: "Global",
            confidence: 0.85);

        var agentLearningId = await learningService.CreateLearningAsync(
            title: "Agent learning",
            triggerType: "SelfCorrection",
            description: "Description",
            scope: "Agent",
            confidence: 0.8);

        var suppressedLearningId = await learningService.CreateLearningAsync(
            title: "Suppressed learning",
            description: "Description",
            triggerType: "Explicit",
            scope: "Global",
            confidence: 0.7);

        await learningService.SuppressLearningAsync(suppressedLearningId);

        // Act: Query statistics
        var allLearnings = await learningService.GetAllLearningsAsync();
        var byStatus = allLearnings.GroupBy(l => l.Status).ToDictionary(g => g.Key, g => g.Count());
        var byScope = allLearnings.GroupBy(l => l.Scope).ToDictionary(g => g.Key, g => g.Count());
        var byTrigger = allLearnings.GroupBy(l => l.TriggerType).ToDictionary(g => g.Key, g => g.Count());
        var avgConfidence = allLearnings.Average(l => l.Confidence);

        // Assert: Verify statistics
        Assert.Equal(4, allLearnings.Count);
        Assert.Equal(3, byStatus.GetValueOrDefault("Active", 0));
        Assert.Equal(1, byStatus.GetValueOrDefault("Suppressed", 0));
        Assert.Equal(3, byScope.GetValueOrDefault("Global", 0));
        Assert.Equal(1, byScope.GetValueOrDefault("Agent", 0));
        Assert.Equal(1, byTrigger.GetValueOrDefault("UserFeedback", 0));
        Assert.Equal(1, byTrigger.GetValueOrDefault("CompilationError", 0));
        Assert.Equal(1, byTrigger.GetValueOrDefault("SelfCorrection", 0));
        Assert.Equal(1, byTrigger.GetValueOrDefault("Explicit", 0));
        Assert.Equal(0.8125, avgConfidence); // (0.9 + 0.85 + 0.8 + 0.7) / 4
    }

    [Fact]
    public async Task ConfidenceFiltering_WorksCorrectly()
    {
        // Arrange: Create learnings with different confidence levels
        var learningService = await CreateServiceAsync();
        await learningService.CreateLearningAsync(
            title: "High confidence",
            description: "Description",
            triggerType: "Explicit",
            scope: "Global",
            confidence: 0.95);

        await learningService.CreateLearningAsync(
            title: "Medium confidence",
            description: "Description",
            triggerType: "Explicit",
            scope: "Global",
            confidence: 0.75);

        await learningService.CreateLearningAsync(
            title: "Low confidence",
            description: "Description",
            triggerType: "Explicit",
            scope: "Global",
            confidence: 0.55);

        // Act: Filter by minimum confidence
        var allLearnings = await learningService.GetAllLearningsAsync();
        var highConfidence = allLearnings.Where(l => l.Confidence >= 0.8).ToList();
        var mediumConfidence = allLearnings.Where(l => l.Confidence >= 0.6 && l.Confidence < 0.8).ToList();

        // Assert
        Assert.Single(highConfidence);
        Assert.Equal("High confidence", highConfidence[0].Title);
        Assert.Equal(2, mediumConfidence.Count);
    }

    [Fact]
    public async Task SourceAgentFiltering_WorksCorrectly()
    {
        // Arrange: Create learnings from different agents
        var learningService = await CreateServiceAsync();
        await learningService.CreateLearningAsync(
            title: "Agent 1 learning",
            description: "Description",
            triggerType: "Explicit",
            scope: "Global",
            confidence: 0.8,
            sourceAgent: "agent-001");

        await learningService.CreateLearningAsync(
            title: "Agent 2 learning",
            description: "Description",
            triggerType: "Explicit",
            scope: "Global",
            confidence: 0.8,
            sourceAgent: "agent-002");

        await learningService.CreateLearningAsync(
            title: "Agent 1 another learning",
            description: "Description",
            triggerType: "Explicit",
            scope: "Agent",
            confidence: 0.85,
            sourceAgent: "agent-001");

        // Act: Filter by source agent
        var agent1Learnings = await learningService.GetLearningsBySourceAgentAsync("agent-001");
        var agent2Learnings = await learningService.GetLearningsBySourceAgentAsync("agent-002");

        // Assert
        Assert.Equal(2, agent1Learnings.Count);
        Assert.Single(agent2Learnings);
        Assert.All(agent1Learnings, l => Assert.Equal("agent-001", l.SourceAgent));
        Assert.All(agent2Learnings, l => Assert.Equal("agent-002", l.SourceAgent));
    }

    [Fact]
    public async Task SuppressLearning_ChangesStatusAndPreventsInjection()
    {
        // Arrange: Create an active learning
        var learningService = await CreateServiceAsync();
        var learningId = await learningService.CreateLearningAsync(
            title: "To be suppressed",
            description: "This learning will be suppressed",
            triggerType: "UserFeedback",
            scope: "Global",
            confidence: 0.85);

        // Verify initial status
        var learningBefore = await learningService.GetLearningAsync(learningId);
        Assert.NotNull(learningBefore);
        Assert.Equal("Active", learningBefore!.Status);

        // Act: Suppress the learning
        await learningService.SuppressLearningAsync(learningId);

        // Assert: Status changed and not in active list
        var learningAfter = await learningService.GetLearningAsync(learningId);
        Assert.NotNull(learningAfter);
        Assert.Equal("Suppressed", learningAfter!.Status);

        var activeLearnings = await learningService.GetActiveLearningsAsync();
        Assert.DoesNotContain(activeLearnings, l => l.LearningId == learningId);

        var suppressedLearnings = await learningService.GetLearningsByStatusAsync("Suppressed");
        Assert.Contains(suppressedLearnings, l => l.LearningId == learningId);
    }

    [Fact]
    public async Task SupersedeLearning_MarksAsReplaced()
    {
        // Arrange: Create an learning to be superseded
        var learningService = await CreateServiceAsync();
        var oldLearningId = await learningService.CreateLearningAsync(
            title: "Old approach",
            description: "This will be replaced by a better approach",
            triggerType: "UserFeedback",
            scope: "Global",
            confidence: 0.75);

        var newLearningId = await learningService.CreateLearningAsync(
            title: "Better approach",
            description: "This supersedes the old learning",
            triggerType: "UserFeedback",
            scope: "Global",
            confidence: 0.9);

        // Act: Supersede the old learning
        await learningService.SupersedeLearningAsync(oldLearningId);

        // Assert: Old learning marked as superseded
        var oldLearning = await learningService.GetLearningAsync(oldLearningId);
        Assert.NotNull(oldLearning);
        Assert.Equal("Superseded", oldLearning!.Status);

        var newLearning = await learningService.GetLearningAsync(newLearningId);
        Assert.NotNull(newLearning);
        Assert.Equal("Active", newLearning!.Status);

        // Only new learning should be active
        var activeLearnings = await learningService.GetActiveLearningsAsync();
        Assert.Contains(activeLearnings, l => l.LearningId == newLearningId);
        Assert.DoesNotContain(activeLearnings, l => l.LearningId == oldLearningId);
    }

    [Fact]
    public async Task PromoteLearning_ProgressesThroughScopeHierarchy()
    {
        // Arrange: Create a skill-scoped learning
        var learningService = await CreateServiceAsync();
        var learningId = await learningService.CreateLearningAsync(
            title: "Valuable insight",
            description: "This deserves broader scope",
            triggerType: "SelfCorrection",
            scope: "Skill",
            confidence: 0.95);

        // Act & Assert: Promote through hierarchy
        var learning = await learningService.GetLearningAsync(learningId);
        Assert.Equal("Skill", learning!.Scope);

        // Skill → Agent
        var scope1 = await learningService.PromoteLearningAsync(learningId);
        Assert.Equal("Agent", scope1);
        learning = await learningService.GetLearningAsync(learningId);
        Assert.Equal("Agent", learning!.Scope);

        // Agent → Project
        var scope2 = await learningService.PromoteLearningAsync(learningId);
        Assert.Equal("Project", scope2);
        learning = await learningService.GetLearningAsync(learningId);
        Assert.Equal("Project", learning!.Scope);

        // Project → Domain
        var scope3 = await learningService.PromoteLearningAsync(learningId);
        Assert.Equal("Domain", scope3);
        learning = await learningService.GetLearningAsync(learningId);
        Assert.Equal("Domain", learning!.Scope);

        // Domain → Global
        var scope4 = await learningService.PromoteLearningAsync(learningId);
        Assert.Equal("Global", scope4);
        learning = await learningService.GetLearningAsync(learningId);
        Assert.Equal("Global", learning!.Scope);

        // Global → (no change)
        var scope5 = await learningService.PromoteLearningAsync(learningId);
        Assert.Null(scope5);
        learning = await learningService.GetLearningAsync(learningId);
        Assert.Equal("Global", learning!.Scope);
    }

    [Fact]
    public async Task PromoteLearning_FromDifferentStartingScopes()
    {
        // Arrange: Create learnings at different scopes
        var learningService = await CreateServiceAsync();
        
        var agentLearningId = await learningService.CreateLearningAsync(
            "Agent Learning", "desc", "Explicit", "Agent", 0.8);
        
        var projectLearningId = await learningService.CreateLearningAsync(
            "Project Learning", "desc", "Explicit", "Project", 0.85);

        // Act & Assert: Agent → Project
        var newScope1 = await learningService.PromoteLearningAsync(agentLearningId);
        Assert.Equal("Project", newScope1);

        // Act & Assert: Project → Domain
        var newScope2 = await learningService.PromoteLearningAsync(projectLearningId);
        Assert.Equal("Domain", newScope2);
    }

    [Fact]
    public async Task LifecycleOperations_MaintainDataIntegrity()
    {
        // Arrange: Create multiple learnings
        var learningService = await CreateServiceAsync();
        var learning1Id = await learningService.CreateLearningAsync(
            "Learning 1", "Active learning", "Explicit", "Skill", 0.8);
        
        var learning2Id = await learningService.CreateLearningAsync(
            "Learning 2", "To suppress", "Explicit", "Agent", 0.75);
        
        var learning3Id = await learningService.CreateLearningAsync(
            "Learning 3", "To supersede", "Explicit", "Project", 0.7);

        // Act: Perform various lifecycle operations
        await learningService.PromoteLearningAsync(learning1Id); // Skill → Agent
        await learningService.SuppressLearningAsync(learning2Id);
        await learningService.SupersedeLearningAsync(learning3Id);

        // Assert: Verify each learning has correct state
        var learning1 = await learningService.GetLearningAsync(learning1Id);
        Assert.Equal("Agent", learning1!.Scope);
        Assert.Equal("Active", learning1.Status);

        var learning2 = await learningService.GetLearningAsync(learning2Id);
        Assert.Equal("Agent", learning2!.Scope);
        Assert.Equal("Suppressed", learning2.Status);

        var learning3 = await learningService.GetLearningAsync(learning3Id);
        Assert.Equal("Project", learning3!.Scope);
        Assert.Equal("Superseded", learning3.Status);

        // Assert: Statistics are correct
        var stats = await learningService.GetStatisticsAsync();
        Assert.Equal(3, stats.TotalLearnings);
        Assert.Equal(1, stats.ActiveCount);
        Assert.Equal(1, stats.SuppressedCount);
        Assert.Equal(1, stats.SupersededCount);
    }

    // ===== KBP-DATA-001/002: Promotion History Tracking Integration Tests =====

    [Fact]
    public async Task PromoteLearning_RecordsPromotionHistory()
    {
        // Arrange - KBP-DATA-001/002: Create learning and promote it
        var learningService = await CreateServiceAsync();
        var learningId = await learningService.CreateLearningAsync(
            "Important Pattern", "Always use async/await", "UserFeedback", "Skill", 0.9);

        var testDbPath = _testDbPaths.Last();
        var promotionRepo = await CreatePromotionRepositoryAsync(testDbPath);

        // Act: Promote learning (Skill → Agent)
        var newScope = await learningService.PromoteLearningAsync(
            learningId,
            promotedBy: "integration-test-user",
            sourceTaskId: "task-12345",
            sourceAgent: "test-agent",
            notes: "High confidence and frequent usage");

        // Assert: Promotion was recorded
        Assert.Equal("Agent", newScope);
        
        var promotions = await promotionRepo.GetByLearningIdAsync(learningId);
        Assert.Single(promotions);
        
        var promotion = promotions[0];
        Assert.Equal(learningId, promotion.LearningId);
        Assert.Equal("Skill", promotion.FromScope);
        Assert.Equal("Agent", promotion.ToScope);
        Assert.Equal("integration-test-user", promotion.PromotedBy);
        Assert.Equal("task-12345", promotion.SourceTaskId);
        Assert.Equal("test-agent", promotion.SourceAgent);
        Assert.Equal("High confidence and frequent usage", promotion.Notes);
        Assert.True(promotion.PromotedAt > 0);
    }

    [Fact]
    public async Task PromoteLearningMultipleTimes_RecordsFullHistory()
    {
        // Arrange
        var learningService = await CreateServiceAsync();
        var learningId = await learningService.CreateLearningAsync(
            "Evolving Pattern", "Pattern description", "UserFeedback", "Skill", 0.8);

        var testDbPath = _testDbPaths.Last();
        var promotionRepo = await CreatePromotionRepositoryAsync(testDbPath);

        // Act: Promote through hierarchy (Skill → Agent → Project → Domain)
        await learningService.PromoteLearningAsync(learningId, "user", "task-1"); // Skill → Agent
        await Task.Delay(100); // Ensure different timestamps
        await learningService.PromoteLearningAsync(learningId, "user", "task-2"); // Agent → Project
        await Task.Delay(100);
        await learningService.PromoteLearningAsync(learningId, "user", "task-3"); // Project → Domain

        // Assert: All promotions recorded
        var promotions = await promotionRepo.GetByLearningIdAsync(learningId);
        Assert.Equal(3, promotions.Count);
        
        // Verify we have all three scope transitions (order may vary due to timing)
        var scopeTransitions = promotions.Select(p => $"{p.FromScope}→{p.ToScope}").ToHashSet();
        Assert.Contains("Skill→Agent", scopeTransitions);
        Assert.Contains("Agent→Project", scopeTransitions);
        Assert.Contains("Project→Domain", scopeTransitions);
        
        // Verify timestamps are in descending order (most recent first)
        for (int i = 0; i < promotions.Count - 1; i++)
        {
            Assert.True(promotions[i].PromotedAt >= promotions[i + 1].PromotedAt,
                $"Promotions should be ordered by timestamp DESC");
        }
    }

    [Fact]
    public async Task GetBySourceTaskId_ReturnsPromotionsFromTask()
    {
        // Arrange - KBP-DATA-001: Query promotions by source task
        var learningService = await CreateServiceAsync();
        var taskId = Guid.NewGuid().ToString();
        
        var learning1Id = await learningService.CreateLearningAsync(
            "Pattern 1", "Description 1", "UserFeedback", "Skill", 0.9);
        var learning2Id = await learningService.CreateLearningAsync(
            "Pattern 2", "Description 2", "UserFeedback", "Agent", 0.85);

        var testDbPath = _testDbPaths.Last();
        var promotionRepo = await CreatePromotionRepositoryAsync(testDbPath);

        // Act: Promote both learnings with same task ID
        await learningService.PromoteLearningAsync(learning1Id, "user", taskId);
        await learningService.PromoteLearningAsync(learning2Id, "user", taskId);

        // Assert: Can retrieve all promotions from that task
        var taskPromotions = await promotionRepo.GetBySourceTaskIdAsync(taskId);
        Assert.Equal(2, taskPromotions.Count);
        Assert.All(taskPromotions, p => Assert.Equal(taskId, p.SourceTaskId));
    }

    [Fact]
    public async Task GetByToScope_ReturnsPromotionsToTargetScope()
    {
        // Arrange - KBP-DATA-002: Query by target scope
        var learningService = await CreateServiceAsync();
        
        var learning1Id = await learningService.CreateLearningAsync(
            "Global Pattern 1", "Desc 1", "UserFeedback", "Domain", 0.95);
        var learning2Id = await learningService.CreateLearningAsync(
            "Global Pattern 2", "Desc 2", "UserFeedback", "Domain", 0.92);
        var learning3Id = await learningService.CreateLearningAsync(
            "Project Pattern", "Desc 3", "UserFeedback", "Agent", 0.8);

        var testDbPath = _testDbPaths.Last();
        var promotionRepo = await CreatePromotionRepositoryAsync(testDbPath);

        // Act: Promote first two to Global, third to Project
        await learningService.PromoteLearningAsync(learning1Id); // Domain → Global
        await learningService.PromoteLearningAsync(learning2Id); // Domain → Global
        await learningService.PromoteLearningAsync(learning3Id); // Agent → Project

        // Assert: Can query by target scope
        var globalPromotions = await promotionRepo.GetByToScopeAsync("Global");
        Assert.Equal(2, globalPromotions.Count);
        Assert.All(globalPromotions, p => Assert.Equal("Global", p.ToScope));

        var projectPromotions = await promotionRepo.GetByToScopeAsync("Project");
        Assert.Single(projectPromotions);
        Assert.Equal("Project", projectPromotions[0].ToScope);
    }

    [Fact]
    public async Task PromotionHistory_PersistsAcrossSessions()
    {
        // Arrange: Create learning and promote across multiple service instances
        var testDbPath = Path.Combine(Path.GetTempPath(), $"daiv3_promotion_persist_{Guid.NewGuid():N}.db");
        _testDbPaths.Add(testDbPath);

        string learningId;
        
        // Session 1: Create and promote
        {
            var options = Options.Create(new PersistenceOptions { DatabasePath = testDbPath });
            var context = new DatabaseContext(_loggerFactory.CreateLogger<DatabaseContext>(), options);
            await context.InitializeAsync();
            
            var learningRepo = new Daiv3.Persistence.Repositories.LearningRepository(
                context, _loggerFactory.CreateLogger<Daiv3.Persistence.Repositories.LearningRepository>());
            var promotionRepo = new Daiv3.Persistence.Repositories.PromotionRepository(
                context, _loggerFactory.CreateLogger<Daiv3.Persistence.Repositories.PromotionRepository>());
            
            var service = new LearningStorageService(
                learningRepo,
                _loggerFactory.CreateLogger<LearningStorageService>(),
                null,
                promotionRepo);

            learningId = await service.CreateLearningAsync(
                "Persistent Pattern", "Description", "UserFeedback", "Skill", 0.9);
            await service.PromoteLearningAsync(learningId, "user1", "task-1");
            
            await context.DisposeAsync();
        }

        // Session 2: Verify and promote again
        {
            var options = Options.Create(new PersistenceOptions { DatabasePath = testDbPath });
            var context = new DatabaseContext(_loggerFactory.CreateLogger<DatabaseContext>(), options);
            await context.InitializeAsync();
            
            var promotionRepo = new Daiv3.Persistence.Repositories.PromotionRepository(
                context, _loggerFactory.CreateLogger<Daiv3.Persistence.Repositories.PromotionRepository>());

            // Assert: First promotion persisted
            var promotions = await promotionRepo.GetByLearningIdAsync(learningId);
            Assert.Single(promotions);
            Assert.Equal("Skill", promotions[0].FromScope);
            Assert.Equal("Agent", promotions[0].ToScope);
            
            await context.DisposeAsync();
        }
    }

    [Fact]
    public async Task PromotionWithOptionalFields_WorksCorrectly()
    {
        // Arrange - Test that optional fields (source task/agent/notes) work when null
        var learningService = await CreateServiceAsync();
        var learningId = await learningService.CreateLearningAsync(
            "Simple Pattern", "Description", "UserFeedback", "Skill", 0.8);

        var testDbPath = _testDbPaths.Last();
        var promotionRepo = await CreatePromotionRepositoryAsync(testDbPath);

        // Act: Promote without optional fields
        await learningService.PromoteLearningAsync(learningId);

        // Assert: Promotion recorded with null optional fields
        var promotions = await promotionRepo.GetByLearningIdAsync(learningId);
        Assert.Single(promotions);
        
        var promotion = promotions[0];
        Assert.Null(promotion.SourceTaskId);
        Assert.Null(promotion.SourceAgent);
        Assert.Null(promotion.Notes);
        Assert.Equal("user", promotion.PromotedBy); // Default value
    }
}

