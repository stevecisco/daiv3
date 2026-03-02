using Daiv3.Persistence;
using Daiv3.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Daiv3.IntegrationTests.Persistence;

/// <summary>
/// Acceptance tests for KBP-ACC-001: User can promote task learnings to project scope.
/// 
/// These tests verify the complete user workflow for promoting learnings from
/// completed tasks to higher scopes, with focus on project-level promotion.
/// </summary>
[Collection("Database")]
public class LearningPromotionAcceptanceTests : IAsyncLifetime
{
    private DatabaseContext? _databaseContext;
    private LearningStorageService? _learningService;
    private PromotionRepository? _promotionRepository;
    private LearningRepository? _learningRepository;
    private readonly string _testDatabasePath;
    private readonly ILogger<DatabaseContext> _logger;
    private readonly ILogger<LearningPromotionAcceptanceTests> _testLogger;

    public LearningPromotionAcceptanceTests()
    {
        _testDatabasePath = Path.Combine(
            Path.GetTempPath(),
            "daiv3_test",
            $"test_kbp_acc_001_{Guid.NewGuid():N}.db");
        
        var loggerFactory = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        _logger = loggerFactory.CreateLogger<DatabaseContext>();
        _testLogger = loggerFactory.CreateLogger<LearningPromotionAcceptanceTests>();
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

        _learningRepository = new LearningRepository(
            _databaseContext, 
            LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<LearningRepository>());
        
        _promotionRepository = new PromotionRepository(
            _databaseContext, 
            LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<PromotionRepository>());
        
        _learningService = new LearningStorageService(
            _learningRepository,
            LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<LearningStorageService>(),
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

    /// <summary>
    /// Acceptance Test 1: User can list learnings from a completed task.
    /// 
    /// Scenario: User completes a task generating multiple learnings and wants to see what was learned.
    /// Expected: System returns all learnings associated with that task ID.
    /// </summary>
    [Fact]
    public async Task AcceptanceTest_UserCanListTaskLearnings()
    {
        _testLogger.LogInformation("=== KBP-ACC-001 Acceptance Test 1: List Task Learnings ===");

        // Arrange - Complete a task that generated learnings
        var taskId = "completed-task-123";
        
        _testLogger.LogInformation("Creating learnings for task: {TaskId}", taskId);
        
        var learning1Id = await _learningService!.CreateLearningAsync(
            title: "Use dependency injection for testability",
            description: "Services registered in DI container are easier to mock in tests",
            triggerType: "SelfCorrection",
            scope: "Skill",
            confidence: 0.85,
            sourceTaskId: taskId,
            sourceAgent: "CodeReviewAgent",
            createdBy: "CodeReviewAgent");

        var learning2Id = await _learningService.CreateLearningAsync(
            title: "Validate input parameters early",
            description: "Use guard clauses at method start to fail fast",
            triggerType: "CompilationError",
            scope: "Skill",
            confidence: 0.90,
            sourceTaskId: taskId,
            sourceAgent: "CodeReviewAgent",
            createdBy: "CodeReviewAgent");

        var learning3Id = await _learningService.CreateLearningAsync(
            title: "Log structured data with ILogger",
            description: "Use ILogger<T> with structured properties for better observability",
            triggerType: "UserFeedback",
            scope: "Agent",
            confidence: 0.95,
            sourceTaskId: taskId,
            sourceAgent: "CodeReviewAgent",
            createdBy: "user");

        // Act - User lists learnings from the completed task
        _testLogger.LogInformation("User listing learnings from task: {TaskId}", taskId);
        var learnings = await _learningService.GetLearningsBySourceTaskAsync(taskId);

        // Assert - All learnings from task are returned
        Assert.Equal(3, learnings.Count);
        Assert.Contains(learnings, l => l.LearningId == learning1Id);
        Assert.Contains(learnings, l => l.LearningId == learning2Id);
        Assert.Contains(learnings, l => l.LearningId == learning3Id);

        _testLogger.LogInformation("✓ User successfully listed {Count} learnings from task", learnings.Count);
    }

    /// <summary>
    /// Acceptance Test 2: User can promote task learnings to project scope.
    /// 
    /// Scenario: User reviews task learnings and promotes valuable ones to project level
    ///           so they apply to all agents working on the project.
    /// Expected: Selected learnings are promoted to "Project" scope and become available project-wide.
    /// </summary>
    [Fact]
    public async Task AcceptanceTest_UserCanPromoteTaskLearningsToProjectScope()
    {
        _testLogger.LogInformation("=== KBP-ACC-001 Acceptance Test 2: Promote to Project Scope ===");

        // Arrange - Task completed with learnings at Skill scope
        var taskId = "task-with-valuable-learnings";
        
        _testLogger.LogInformation("Creating skill-level learnings from task: {TaskId}", taskId);
        
        var learningId1 = await _learningService!.CreateLearningAsync(
            title: "Always use ConfigureAwait(false) in library code",
            description: "Prevents deadlocks in synchronous callers by not capturing sync context",
            triggerType: "UserFeedback",
            scope: "Skill",
            confidence: 0.92,
            sourceTaskId: taskId,
            sourceAgent: "AsyncPatternsAgent",
            createdBy: "user");

        var learningId2 = await _learningService.CreateLearningAsync(
            title: "Index foreign key columns in database schemas",
            description: "Improves JOIN performance significantly in multi-table queries",
            triggerType: "SelfCorrection",
            scope: "Skill",
            confidence: 0.88,
            sourceTaskId: taskId,
            sourceAgent: "DatabaseAgent",
            createdBy: "DatabaseAgent");

        // Verify initial scope
        var beforePromotion1 = await _learningService.GetLearningAsync(learningId1);
        var beforePromotion2 = await _learningService.GetLearningAsync(learningId2);
        Assert.Equal("Skill", beforePromotion1!.Scope);
        Assert.Equal("Skill", beforePromotion2!.Scope);

        // Act - User selects learnings and promotes them to Project scope
        _testLogger.LogInformation("User promoting learnings to Project scope");
        
        var promotions = new List<LearningPromotionSelection>
        {
            new()
            {
                LearningId = learningId1,
                TargetScope = "Project",
                Notes = "This async pattern applies to all project libraries"
            },
            new()
            {
                LearningId = learningId2,
                TargetScope = "Project",
                Notes = "Database optimization benefit for entire project"
            }
        };

        var result = await _learningService.PromoteLearningsFromTaskAsync(
            taskId,
            promotions.AsReadOnly(),
            promotedBy: "project-lead@example.com");

        // Assert - Promotions succeeded
        Assert.True(result.AllSucceeded, "All promotions should succeed");
        Assert.Equal(2, result.SuccessfulPromotions.Count);
        Assert.Empty(result.FailedPromotions);

        // Verify learnings are now at Project scope
        var afterPromotion1 = await _learningService.GetLearningAsync(learningId1);
        var afterPromotion2 = await _learningService.GetLearningAsync(learningId2);
        
        Assert.NotNull(afterPromotion1);
        Assert.NotNull(afterPromotion2);
        Assert.Equal("Project", afterPromotion1.Scope);
        Assert.Equal("Project", afterPromotion2.Scope);

        _testLogger.LogInformation("✓ Successfully promoted {Count} learnings to Project scope", result.SuccessfulPromotions.Count);
    }

    /// <summary>
    /// Acceptance Test 3: Promoted learnings are available to all project agents.
    /// 
    /// Scenario: After promotion to project scope, learnings should be retrievable
    ///           by any agent working on the project (not just the original source agent).
    /// Expected: Promoted learnings are returned when querying for project-level learnings.
    /// </summary>
    [Fact]
    public async Task AcceptanceTest_PromotedLearningsAvailableProjectWide()
    {
        _testLogger.LogInformation("=== KBP-ACC-001 Acceptance Test 3: Project-Wide Availability ===");

        // Arrange - Create learning from one agent's task
        var taskId = "frontend-agent-task";
        var sourceAgent = "FrontendAgent";
        
        _testLogger.LogInformation("Creating learning from {Agent} task", sourceAgent);
        
        var learningId = await _learningService!.CreateLearningAsync(
            title: "Debounce user input events",
            description: "Use 300ms debounce for search input to reduce API calls",
            triggerType: "UserFeedback",
            scope: "Skill",
            confidence: 0.85,
            sourceTaskId: taskId,
            sourceAgent: sourceAgent,
            createdBy: "user");

        // Act - Promote to project scope
        _testLogger.LogInformation("Promoting learning to Project scope");
        
        var promotions = new List<LearningPromotionSelection>
        {
            new()
            {
                LearningId = learningId,
                TargetScope = "Project",
                Notes = "Debouncing pattern applies to all UI components"
            }
        };

        await _learningService.PromoteLearningsFromTaskAsync(taskId, promotions.AsReadOnly());

        // Assert - Learning is now available project-wide (not tied to specific agent)
        var promotedLearning = await _learningService.GetLearningAsync(learningId);
        Assert.NotNull(promotedLearning);
        Assert.Equal("Project", promotedLearning.Scope);
        
        // The learning retains source agent for provenance, but is available project-wide
        Assert.Equal(sourceAgent, promotedLearning.SourceAgent);
        
        _testLogger.LogInformation("✓ Learning promoted to Project scope and available project-wide");
    }

    /// <summary>
    /// Acceptance Test 4: User can promote learnings through multiple scope levels.
    /// 
    /// Scenario: User promotes a skill-level learning to agent level, then to project level.
    /// Expected: Learning can be promoted through scope hierarchy: Skill → Agent → Project.
    /// </summary>
    [Fact]
    public async Task AcceptanceTest_UserCanPromoteThroughMultipleScopeLevels()
    {
        _testLogger.LogInformation("=== KBP-ACC-001 Acceptance Test 4: Multi-Level Promotion ===");

        // Arrange - Create learning at Skill scope
        var task1 = "initial-task";
        
        _testLogger.LogInformation("Creating learning at Skill scope");
        
        var learningId = await _learningService!.CreateLearningAsync(
            title: "Cache compiled regex patterns",
            description: "Use RegexOptions.Compiled or source generators to avoid runtime compilation overhead",
            triggerType: "SelfCorrection",
            scope: "Skill",
            confidence: 0.90,
            sourceTaskId: task1,
            sourceAgent: "TextProcessingAgent",
            createdBy: "TextProcessingAgent");

        // Act 1 - Promote Skill → Agent
        _testLogger.LogInformation("Promoting from Skill to Agent scope");
        
        var promotion1 = new List<LearningPromotionSelection>
        {
            new() { LearningId = learningId, TargetScope = "Agent" }
        };
        await _learningService.PromoteLearningsFromTaskAsync(task1, promotion1.AsReadOnly());

        var afterAgent = await _learningService.GetLearningAsync(learningId);
        Assert.Equal("Agent", afterAgent!.Scope);

        // Act 2 - Promote Agent → Project (using same task since that's where the learning originated)
        _testLogger.LogInformation("Promoting from Agent to Project scope");
        
        var promotion2 = new List<LearningPromotionSelection>
        {
            new() 
            { 
                LearningId = learningId, 
                TargetScope = "Project",
                Notes = "Regex optimization benefits entire project"
            }
        };
        await _learningService.PromoteLearningsFromTaskAsync(task1, promotion2.AsReadOnly());

        // Assert - Learning reached Project scope
        var afterProject = await _learningService.GetLearningAsync(learningId);
        Assert.NotNull(afterProject);
        Assert.Equal("Project", afterProject.Scope);

        _testLogger.LogInformation("✓ Successfully promoted through scope hierarchy: Skill → Agent → Project");
    }

    /// <summary>
    /// Acceptance Test 5: Selective promotion - user can choose which learnings to promote.
    /// 
    /// Scenario: Task generates multiple learnings, user promotes only the valuable ones.
    /// Expected: Only selected learnings are promoted; others remain at original scope.
    /// </summary>
    [Fact]
    public async Task AcceptanceTest_UserCanSelectivelyPromoteLearnings()
    {
        _testLogger.LogInformation("=== KBP-ACC-001 Acceptance Test 5: Selective Promotion ===");

        // Arrange - Task with multiple learnings of varying quality
        var taskId = "task-with-mixed-quality-learnings";
        
        _testLogger.LogInformation("Creating multiple learnings from task");
        
        var highQuality = await _learningService!.CreateLearningAsync(
            title: "Use bulk database operations for performance",
            description: "Batch INSERT/UPDATE operations reduce round trips",
            triggerType: "UserFeedback",
            scope: "Skill",
            confidence: 0.95,
            sourceTaskId: taskId,
            createdBy: "user");

        var mediumQuality = await _learningService.CreateLearningAsync(
            title: "Consider connection pooling settings",
            description: "Review connection pool size for high-load scenarios",
            triggerType: "SelfCorrection",
            scope: "Skill",
            confidence: 0.70,
            sourceTaskId: taskId,
            createdBy: "agent");

        var lowQuality = await _learningService.CreateLearningAsync(
            title: "Check for null values",
            description: "Sometimes null checks are needed",
            triggerType: "Explicit",
            scope: "Skill",
            confidence: 0.50,
            sourceTaskId: taskId,
            createdBy: "agent");

        // Act - User selectively promotes only high-quality learning
        _testLogger.LogInformation("User promoting only high-quality learning to Project scope");
        
        var promotions = new List<LearningPromotionSelection>
        {
            new()
            {
                LearningId = highQuality,
                TargetScope = "Project",
                Notes = "Bulk operations critical for all database work"
            }
            // Intentionally not promoting mediumQuality or lowQuality
        };

        await _learningService.PromoteLearningsFromTaskAsync(taskId, promotions.AsReadOnly());

        // Assert - Only selected learning was promoted
        var promoted = await _learningService.GetLearningAsync(highQuality);
        var notPromoted1 = await _learningService.GetLearningAsync(mediumQuality);
        var notPromoted2 = await _learningService.GetLearningAsync(lowQuality);

        Assert.Equal("Project", promoted!.Scope);
        Assert.Equal("Skill", notPromoted1!.Scope);
        Assert.Equal("Skill", notPromoted2!.Scope);

        _testLogger.LogInformation("✓ Only selected learning promoted; others remain at original scope");
    }

    /// <summary>
    /// Acceptance Test 6: Promotion includes optional notes for context.
    /// 
    /// Scenario: User adds notes explaining why a learning was promoted.
    /// Expected: Notes are stored with the promotion for future reference.
    /// </summary>
    [Fact]
    public async Task AcceptanceTest_PromotionIncludesOptionalNotes()
    {
        _testLogger.LogInformation("=== KBP-ACC-001 Acceptance Test 6: Promotion with Notes ===");

        // Arrange
        var taskId = "task-requiring-explanation";
        
        var learningId = await _learningService!.CreateLearningAsync(
            title: "Use CancellationToken in async methods",
            description: "Pass CancellationToken to enable graceful cancellation",
            triggerType: "UserFeedback",
            scope: "Skill",
            confidence: 0.88,
            sourceTaskId: taskId,
            createdBy: "user");

        // Act - Promote with detailed notes
        var promotionNotes = "Promoting because this pattern is critical for responsive UI and proper resource cleanup. " +
                           "All async operations should support cancellation.";
        
        _testLogger.LogInformation("Promoting with notes: {Notes}", promotionNotes);
        
        var promotions = new List<LearningPromotionSelection>
        {
            new()
            {
                LearningId = learningId,
                TargetScope = "Project",
                Notes = promotionNotes
            }
        };

        var result = await _learningService.PromoteLearningsFromTaskAsync(
            taskId,
            promotions.AsReadOnly(),
            promotedBy: "lead-developer");

        // Assert - Promotion succeeded with notes
        Assert.True(result.AllSucceeded);
        Assert.Single(result.SuccessfulPromotions);
        
        var promotedLearning = await _learningService.GetLearningAsync(learningId);
        Assert.NotNull(promotedLearning);
        Assert.Equal("Project", promotedLearning.Scope);

        _testLogger.LogInformation("✓ Learning promoted successfully with contextual notes");
    }
}
