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
        
        var repository = new Daiv3.Persistence.Repositories.LearningRepository(
            context,
            _loggerFactory.CreateLogger<Daiv3.Persistence.Repositories.LearningRepository>());

        return new LearningStorageService(
            repository,
            _loggerFactory.CreateLogger<LearningStorageService>());
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
}
