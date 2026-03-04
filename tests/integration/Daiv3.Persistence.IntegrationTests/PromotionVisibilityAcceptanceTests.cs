using Daiv3.Persistence;
using Daiv3.Persistence.Entities;
using Daiv3.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Daiv3.Persistence.IntegrationTests;

/// <summary>
/// Acceptance tests for KBP-ACC-002: Promotion actions are recorded and visible in dashboard.
/// Tests verify that promotions are persisted and can be queried for visibility.
/// </summary>
[Collection("Database")]
public class PromotionVisibilityAcceptanceTests : IAsyncLifetime
{
    private DatabaseContext? _context;
    private LearningStorageService? _learningService;
    private PromotionRepository? _promotionRepository;
    private LearningRepository? _learningRepository;
    private readonly string _testDatabasePath;
    private readonly ILogger<DatabaseContext> _logger;

    public PromotionVisibilityAcceptanceTests()
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

        _context = new DatabaseContext(_logger, persistenceOptions);
        await _context.InitializeAsync();

        _learningRepository = new LearningRepository(_context, NullLogger<LearningRepository>.Instance);
        _promotionRepository = new PromotionRepository(_context, NullLogger<PromotionRepository>.Instance);

        _learningService = new LearningStorageService(
            _learningRepository,
            NullLogger<LearningStorageService>.Instance,
            null,
            _promotionRepository);
    }

    public async Task DisposeAsync()
    {
        if (_context != null)
        {
            await _context.DisposeAsync();
        }

        // Try to delete database files with retries to handle WAL/lock file release delays
        if (File.Exists(_testDatabasePath))
        {
            int maxRetries = 3;
            IOException? lastException = null;

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    File.Delete(_testDatabasePath);

                    // Clean up associated WAL and SHM files
                    var walPath = _testDatabasePath + "-wal";
                    var shmPath = _testDatabasePath + "-shm";
                    if (File.Exists(walPath))
                        File.Delete(walPath);
                    if (File.Exists(shmPath))
                        File.Delete(shmPath);

                    lastException = null; // Success - clear any previous exception
                    break;
                }
                catch (IOException ex)
                {
                    lastException = ex;
                    if (i < maxRetries - 1)
                    {
                        // Wait before retrying in case file handles are being released
                        await Task.Delay(100);
                    }
                }
            }

            // Log warning if we couldn't delete but don't fail the test
            if (lastException != null && File.Exists(_testDatabasePath))
            {
                // Silently fail - file lock will be released when process exits
            }
        }
    }

    [Fact]
    public async Task AcceptanceTest_PromotionActions_AreRecordedInDatabase()
    {
        // Arrange: Create a learning and promote it
        var learning = new Learning
        {
            LearningId = Guid.NewGuid().ToString(),
            Title = "Test Learning",
            Description = "A test learning for promotion recording",
            TriggerType = "UserFeedback",
            Scope = "Skill",
            Status = "Active",
            Confidence = 0.8,
            CreatedBy = "test-user",
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            SourceTaskId = "task-123",
            TimesApplied = 0
        };


        await _learningRepository!.AddAsync(learning);

        // Act: Promote the learning
        var promotions = new List<LearningPromotionSelection>
        {
            new()
            {
                LearningId = learning.LearningId,
                TargetScope = "Project"
            }
        };

        var result = await _learningService!.PromoteLearningsFromTaskAsync(
            "task-123",
            promotions,
            "acceptance-test-user");

        // Assert: Verify promotion was recorded
        Assert.NotNull(result);
        Assert.Single(result.SuccessfulPromotions);
        Assert.Empty(result.FailedPromotions);

        // Verify promotion is in database
        var recordedPromotions = await _promotionRepository!.GetByLearningIdAsync(learning.LearningId);
        Assert.Single(recordedPromotions);

        var promotion = recordedPromotions[0];
        Assert.Equal(learning.LearningId, promotion.LearningId);
        Assert.Equal("Skill", promotion.FromScope);
        Assert.Equal("Project", promotion.ToScope);
        Assert.Equal("acceptance-test-user", promotion.PromotedBy);
        Assert.Equal("task-123", promotion.SourceTaskId);
        Assert.True(promotion.PromotedAt > 0);
    }

    [Fact]
    public async Task AcceptanceTest_PromotionHistory_IsQueryableByLearningId()
    {
        // Arrange: Create a learning and promote it multiple times
        var learning = new Learning
        {
            LearningId = Guid.NewGuid().ToString(),
            Title = "Multi-Promotion Learning",
            Description = "A learning promoted multiple times",
            TriggerType = "SelfCorrection",
            Scope = "Skill",
            Status = "Active",
            Confidence = 0.9,
            CreatedBy = "test-user",
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            SourceTaskId = "task-456",
            TimesApplied = 5
        };


        await _learningRepository!.AddAsync(learning);

        // Promote: Skill → Agent → Project
        await _learningService!.PromoteLearningsFromTaskAsync(
            "task-456",
            [new() { LearningId = learning.LearningId, TargetScope = "Agent" }],
            "user1");

        await Task.Delay(100); // Ensure timestamp difference

        await _learningService!.PromoteLearningsFromTaskAsync(
            "task-456",
            [new() { LearningId = learning.LearningId, TargetScope = "Project" }],
            "user2");

        // Act: Query promotion history
        var promotionHistory = await _promotionRepository!.GetByLearningIdAsync(learning.LearningId);

        // Assert: Verify all promotions are retrievable
        Assert.Equal(2, promotionHistory.Count);

        // Most recent first (Project promotion)
        var recent = promotionHistory[0];
        Assert.Equal("Agent", recent.FromScope);
        Assert.Equal("Project", recent.ToScope);
        Assert.Equal("user2", recent.PromotedBy);

        // Older promotion (Agent promotion)
        var older = promotionHistory[1];
        Assert.Equal("Skill", older.FromScope);
        Assert.Equal("Agent", older.ToScope);
        Assert.Equal("user1", older.PromotedBy);

        // Timestamps should be in descending order
        Assert.True(recent.PromotedAt >= older.PromotedAt);
    }

    [Fact]
    public async Task AcceptanceTest_PromotionHistory_IsQueryableByTask()
    {
        // Arrange: Create multiple learnings from the same task and promote them
        var taskId = "task-789";
        var learningIds = new List<string>();

        for (int i = 0; i < 3; i++)
        {
            var learning = new Learning
            {
                LearningId = Guid.NewGuid().ToString(),
                Title = $"Task Learning {i + 1}",
                Description = $"Learning {i + 1} from task {taskId}",
                TriggerType = "CompilationError",
                Scope = "Skill",
                Status = "Active",
                Confidence = 0.7 + (i * 0.1),
                CreatedBy = "test-user",
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                SourceTaskId = taskId,
                TimesApplied = i
            };


            await _learningRepository!.AddAsync(learning);
            learningIds.Add(learning.LearningId);
        }

        // Promote all learnings from the task
        var promotionSelections = learningIds.Select(id => new LearningPromotionSelection
        {
            LearningId = id,
            TargetScope = "Agent"
        }).ToList();

        await _learningService!.PromoteLearningsFromTaskAsync(
            taskId,
            promotionSelections,
            "batch-promoter");

        // Act: Query promotions by task
        var taskPromotions = await _promotionRepository!.GetBySourceTaskIdAsync(taskId);

        // Assert: Verify all promotions from the task are retrievable
        Assert.Equal(3, taskPromotions.Count);
        Assert.All(taskPromotions, p =>
        {
            Assert.Equal(taskId, p.SourceTaskId);
            Assert.Equal("Skill", p.FromScope);
            Assert.Equal("Agent", p.ToScope);
            Assert.Equal("batch-promoter", p.PromotedBy);
        });
    }

    [Fact]
    public async Task AcceptanceTest_PromotionHistory_IsQueryableByScope()
    {
        // Arrange: Create learnings and promote them to different scopes
        var learnings = new List<Learning>();

        for (int i = 0; i < 4; i++)
        {
            var learning = new Learning
            {
                LearningId = Guid.NewGuid().ToString(),
                Title = $"Scope Test Learning {i + 1}",
                Description = $"Learning for scope filtering test {i + 1}",
                TriggerType = "KnowledgeConflict",
                Scope = "Skill",
                Status = "Active",
                Confidence = 0.8,
                CreatedBy = "test-user",
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                SourceTaskId = $"task-scope-{i}",
                TimesApplied = 0
            };


            await _learningRepository!.AddAsync(learning);
            learnings.Add(learning);
        }

        // Promote 2 to Project, 2 to Global
        await _learningService!.PromoteLearningsFromTaskAsync(
            "task-scope-0",
            [new() { LearningId = learnings[0].LearningId, TargetScope = "Project" }],
            "user-a");

        await _learningService!.PromoteLearningsFromTaskAsync(
            "task-scope-1",
            [new() { LearningId = learnings[1].LearningId, TargetScope = "Project" }],
            "user-b");

        await _learningService!.PromoteLearningsFromTaskAsync(
            "task-scope-2",
            [new() { LearningId = learnings[2].LearningId, TargetScope = "Global" }],
            "user-c");

        await _learningService!.PromoteLearningsFromTaskAsync(
            "task-scope-3",
            [new() { LearningId = learnings[3].LearningId, TargetScope = "Global" }],
            "user-d");

        // Act: Query promotions by scope
        var projectPromotions = await _promotionRepository!.GetByToScopeAsync("Project");
        var globalPromotions = await _promotionRepository!.GetByToScopeAsync("Global");

        // Assert: Verify correct filtering by scope
        Assert.Equal(2, projectPromotions.Count);
        Assert.All(projectPromotions, p => Assert.Equal("Project", p.ToScope));

        Assert.Equal(2, globalPromotions.Count);
        Assert.All(globalPromotions, p => Assert.Equal("Global", p.ToScope));
    }

    [Fact]
    public async Task AcceptanceTest_PromotionHistory_IncludesProvenanceMetadata()
    {
        // Arrange: Create a learning with full provenance tracking
        var learning = new Learning
        {
            LearningId = Guid.NewGuid().ToString(),
            Title = "Provenance Test Learning",
            Description = "Learning for testing full provenance metadata",
            TriggerType = "Explicit",
            Scope = "Skill",
            Status = "Active",
            Confidence = 0.95,
            CreatedBy = "test-user",
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            SourceTaskId = "task-provenance-123",
            SourceAgent = "TestAgent",
            TimesApplied = 0
        };


        await _learningRepository!.AddAsync(learning);

        // Act: Promote with notes
        var promotionSelections = new List<LearningPromotionSelection>
        {
            new()
            {
                LearningId = learning.LearningId,
                TargetScope = "Domain",
                Notes = "Promoted due to high confidence and broad applicability"
            }
        };

        await _learningService!.PromoteLearningsFromTaskAsync(
            "task-provenance-123",
            promotionSelections,
            "provenance-tester");

        // Assert: Verify all provenance metadata is recorded
        var promotions = await _promotionRepository!.GetByLearningIdAsync(learning.LearningId);
        Assert.Single(promotions);

        var promotion = promotions[0];
        Assert.Equal(learning.LearningId, promotion.LearningId);
        Assert.Equal("Skill", promotion.FromScope);
        Assert.Equal("Domain", promotion.ToScope);
        Assert.Equal("provenance-tester", promotion.PromotedBy);
        Assert.Equal("task-provenance-123", promotion.SourceTaskId);
        Assert.Equal(learning.SourceAgent, promotion.SourceAgent);
        Assert.Equal("Promoted due to high confidence and broad applicability", promotion.Notes);
        Assert.True(promotion.PromotedAt > 0);
    }

    [Fact]
    public async Task AcceptanceTest_PromotionStatistics_AreComputable()
    {
        // Arrange: Create multiple promotions for statistics aggregation
        var taskId = "task-stats";
        var learningIds = new List<string>();

        for (int i = 0; i < 10; i++)
        {
            var learning = new Learning
            {
                LearningId = Guid.NewGuid().ToString(),
                Title = $"Stats Learning {i + 1}",
                Description = $"Learning for statistics test {i + 1}",
                TriggerType = "UserFeedback",
                Scope = "Skill",
                Status = "Active",
                Confidence = 0.7,
                CreatedBy = "test-user",
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                SourceTaskId = taskId,
                TimesApplied = 0
            };


            await _learningRepository!.AddAsync(learning);
            learningIds.Add(learning.LearningId);
        }

        // Promote 5 to Agent, 3 to Project, 2 to Global
        var targetScopes = new[] { "Agent", "Agent", "Agent", "Agent", "Agent", "Project", "Project", "Project", "Global", "Global" };

        for (int i = 0; i < learningIds.Count; i++)
        {
            await _learningService!.PromoteLearningsFromTaskAsync(
                taskId,
                [new() { LearningId = learningIds[i], TargetScope = targetScopes[i] }],
                $"user-{i % 3}"); // 3 different users

            await Task.Delay(10); // Ensure unique timestamps
        }

        // Act: Retrieve all promotions for statistics
        var allPromotions = await _promotionRepository!.GetAllAsync();

        // Assert: Verify statistics can be computed
        Assert.Equal(10, allPromotions.Count);

        var byScopeGroups = allPromotions.GroupBy(p => p.ToScope).ToList();
        Assert.Equal(3, byScopeGroups.Count); // Agent, Project, Global

        var agentCount = allPromotions.Count(p => p.ToScope == "Agent");
        var projectCount = allPromotions.Count(p => p.ToScope == "Project");
        var globalCount = allPromotions.Count(p => p.ToScope == "Global");

        Assert.Equal(5, agentCount);
        Assert.Equal(3, projectCount);
        Assert.Equal(2, globalCount);

        // Verify promoter distribution
        var byPromoterGroups = allPromotions.GroupBy(p => p.PromotedBy).ToList();
        Assert.Equal(3, byPromoterGroups.Count); // user-0, user-1, user-2

        // Verify all promotions are from the same task
        Assert.All(allPromotions, p => Assert.Equal(taskId, p.SourceTaskId));

        // Verify chronological ordering (most recent first)
        var promotedAtTimes = allPromotions.Select(p => p.PromotedAt).ToList();
        var sortedTimes = promotedAtTimes.OrderByDescending(t => t).ToList();
        Assert.Equal(sortedTimes, promotedAtTimes);
    }
}
