using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Daiv3.Orchestration;
using Daiv3.Orchestration.Interfaces;
using Daiv3.Orchestration.Models;
using Daiv3.Persistence;
using Daiv3.Persistence.Entities;
using Daiv3.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Daiv3.Orchestration.IntegrationTests;

/// <summary>
/// Integration tests for knowledge promotion summarization (KBP-REQ-004).
/// Tests end-to-end workflow of promotion and summary generation.
/// </summary>
public class KnowledgePromotionSummarizationIntegrationTests : IAsyncLifetime
{
    private DatabaseContext? _dbContext;
    private  IKnowledgeSummaryService? _summaryService;
    private LearningStorageService? _learningService;
    private string? _dbPath;

    public async Task InitializeAsync()
    {
        // Create test database
        _dbPath = Path.Combine(Path.GetTempPath(), $"daiv3-knowledge-summary-test-{Guid.NewGuid():N}.db");
        _dbContext = new DatabaseContext(
            Moq.Mock.Of<Microsoft.Extensions.Logging.ILogger<DatabaseContext>>(),
            Microsoft.Extensions.Options.Options.Create(new PersistenceOptions { DatabasePath = _dbPath }));
        await _dbContext.InitializeAsync();

        // Create repositories
        var learningRepository = new LearningRepository(
            _dbContext,
            Moq.Mock.Of<Microsoft.Extensions.Logging.ILogger<LearningRepository>>());

        var promotionRepository = new PromotionRepository(
            _dbContext,
            Moq.Mock.Of<Microsoft.Extensions.Logging.ILogger<PromotionRepository>>());

        // Create mock embedding generator
        var mockEmbeddingGenerator = new Moq.Mock<Daiv3.Knowledge.Embedding.IEmbeddingGenerator>();
        mockEmbeddingGenerator
            .Setup(x => x.GenerateEmbeddingAsync(Moq.It.IsAny<string>(), Moq.It.IsAny<CancellationToken>()))
            .Returns((string text, CancellationToken _) =>
            {
                // Generate simple deterministic embedding
                var embedding = new float[384];
                for (int i = 0; i < Math.Min(text.Length, 384); i++)
                {
                    embedding[i] = (float)text[i] / 1000f;
                }
                return Task.FromResult(embedding);
            });

        // Create services
        _learningService = new LearningStorageService(
            Moq.Mock.Of<Microsoft.Extensions.Logging.ILogger<LearningStorageService>>(),
            learningRepository,
            promotionRepository,
            mockEmbeddingGenerator.Object);

        _summaryService = new KnowledgeSummaryService(
            Moq.Mock.Of<Microsoft.Extensions.Logging.ILogger<KnowledgeSummaryService>>());
    }

    public async Task DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }

        if (!string.IsNullOrWhiteSpace(_dbPath) && File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }

    [Fact]
    public async Task EndToEnd_PromoteLearningsFromTask_GeneratesSummaryWithCorrectMetadata()
    {
        // Arrange - Create test learnings
        var taskId = "test-task-" + Guid.NewGuid().ToString("N");
        var learning1 = await CreateTestLearning("Learning 1", "Description 1", "UserFeedback", "Agent", 0.9, taskId);
        var learning2 = await CreateTestLearning("Learning 2", "Description 2", "SelfCorrection", "Skill", 0.85, taskId);

        var promotions = new List<LearningPromotionSelection>
        {
            new() { LearningId = learning1.LearningId, TargetScope = "Project", Notes = "High confidence" },
            new() { LearningId = learning2.LearningId, TargetScope = "Project", Notes = "Useful pattern" }
        };

        // Act - Execute promotion
        var promotionResult = await _learningService!.PromoteLearningsFromTaskAsync(
            taskId,
            promotions.AsReadOnly(),
            "test-user",
            CancellationToken.None);

        // Fetch promoted learnings for summary generation
        var promotedLearnings = new List<Learning>();
        foreach (var promo in promotionResult.SuccessfulPromotions)
        {
            var learning = await _learningService.GetLearningAsync(promo.LearningId);
            if (learning != null)
            {
                promotedLearnings.Add(learning);
            }
        }

        // Build target scope map
        var targetScopeMap = promotionResult.SuccessfulPromotions
            .ToDictionary(p => p.LearningId, p => p.TargetScope);

        // Generate summary
        var summary = await _summaryService!.GenerateSummaryAsync(
            promotedLearnings.AsReadOnly(),
            targetScopeMap,
            taskId,
            "test-user",
            CancellationToken.None);

        // Assert
        Assert.NotNull(summary);
        Assert.Equal(2, summary.PromotedCount);
        Assert.Equal(2, summary.LearningIds.Count);
        Assert.Contains(learning1.LearningId, summary.LearningIds);
        Assert.Contains(learning2.LearningId, summary.LearningIds);

        Assert.Contains("Project", summary.TargetScopes);
        Assert.Equal(taskId, summary.SourceTaskId);
        Assert.Equal("test-user", summary.PromotedBy);

        Assert.NotEmpty(summary.SummaryText);
        Assert.Contains("Learning 1", summary.SummaryText);
        Assert.Contains("Learning 2", summary.SummaryText);
        Assert.Contains("**To Project scope", summary.SummaryText);
        Assert.Contains("**Statistics:**", summary.SummaryText);

        Assert.NotNull(summary.Details);
        Assert.Equal(2, summary.Details.Count);
    }

    [Fact]
    public async Task Summary_IncludesPromotionProvenance()
    {
        // Arrange
        var taskId = "task-provenance-" + Guid.NewGuid().ToString("N");
        var learning = await CreateTestLearning("Provenance Test", "Test description", "CompilationError", "Agent", 0.88, taskId);

        var promotions = new List<LearningPromotionSelection>
        {
            new() { LearningId = learning.LearningId, TargetScope = "Domain", Notes = "Important fix" }
        };

        // Act
        var promotionResult = await _learningService!.PromoteLearningsFromTaskAsync(
            taskId,
            promotions.AsReadOnly(),
            "test-user",
            CancellationToken.None);

        var promotedLearnings = new List<Learning>
        {
            await _learningService.GetLearningAsync(learning.LearningId) ?? throw new InvalidOperationException()
        };

        var targetScopeMap = new Dictionary<string, string>
        {
            [learning.LearningId] = "Domain"
        };

        var summary = await _summaryService!.GenerateSummaryAsync(
            promotedLearnings.AsReadOnly(),
            targetScopeMap,
            taskId,
            "test-user",
            CancellationToken.None);

        // Assert - Verify provenance information
        Assert.Contains(taskId, summary.SummaryText);
        Assert.Contains("test-user", summary.SummaryText);
        Assert.NotNull(summary.Details);
        Assert.Single(summary.Details);

        var detail = summary.Details[0];
        Assert.Equal("CompilationError", detail.TriggerType);
        Assert.Equal("Agent", detail.SourceScope);
        Assert.Equal("Domain", detail.TargetScope);
    }

    [Fact]
    public async Task Summary_HandlesMultipleTargetScopes()
    {
        // Arrange
        var taskId = "task-multiscope-" + Guid.NewGuid().ToString("N");
        var learning1 = await CreateTestLearning("Learning A", "Description A", "UserFeedback", "Agent", 0.9, taskId);
        var learning2 = await CreateTestLearning("Learning B", "Description B", "ToolFailure", "Skill", 0.75, taskId);
        var learning3 = await CreateTestLearning("Learning C", "Description C", "KnowledgeConflict", "Agent", 0.82, taskId);

        var promotions = new List<LearningPromotionSelection>
        {
            new() { LearningId = learning1.LearningId, TargetScope = "Project", Notes = null },
            new() { LearningId = learning2.LearningId, TargetScope = "Domain", Notes = null },
            new() { LearningId = learning3.LearningId, TargetScope = "Project", Notes = null }
        };

        // Act
        var promotionResult = await _learningService!.PromoteLearningsFromTaskAsync(
            taskId,
            promotions.AsReadOnly(),
            "test-user",
            CancellationToken.None);

        var promotedLearnings = new List<Learning>();
        foreach (var promo in promotionResult.SuccessfulPromotions)
        {
            var learning = await _learningService.GetLearningAsync(promo.LearningId);
            if (learning != null) promotedLearnings.Add(learning);
        }

        var targetScopeMap = promotionResult.SuccessfulPromotions
            .ToDictionary(p => p.LearningId, p => p.TargetScope);

        var summary = await _summaryService!.GenerateSummaryAsync(
            promotedLearnings.AsReadOnly(),
            targetScopeMap,
            taskId,
            "test-user",
            CancellationToken.None);

        // Assert
        Assert.Equal(3, summary.PromotedCount);
        Assert.Contains("Project", summary.TargetScopes);
        Assert.Contains("Domain", summary.TargetScopes);

        // Verify summary groups by target scope
        Assert.Contains("**To Project scope (2 items)**", summary.SummaryText);
        Assert.Contains("**To Domain scope (1 item)**", summary.SummaryText);
    }

    [Fact]
    public async Task Summary_IncludesAverageConfidenceAndTriggerStatistics()
    {
        // Arrange
        var taskId = "task-stats-" + Guid.NewGuid().ToString("N");
        var learning1 = await CreateTestLearning("L1", "D1", "UserFeedback", "Agent", 0.95, taskId);
        var learning2 = await CreateTestLearning("L2", "D2", "UserFeedback", "Agent", 0.85, taskId);
        var learning3 = await CreateTestLearning("L3", "D3", "SelfCorrection", "Skill", 0.80, taskId);

        var promotions = new List<LearningPromotionSelection>
        {
            new() { LearningId = learning1.LearningId, TargetScope = "Project" },
            new() { LearningId = learning2.LearningId, TargetScope = "Project" },
            new() { LearningId = learning3.LearningId, TargetScope = "Project" }
        };

        // Act
        var promotionResult = await _learningService!.PromoteLearningsFromTaskAsync(
            taskId,
            promotions.AsReadOnly(),
            "test-user",
            CancellationToken.None);

        var promotedLearnings = new List<Learning>();
        foreach (var promo in promotionResult.SuccessfulPromotions)
        {
            var learning = await _learningService.GetLearningAsync(promo.LearningId);
            if (learning != null) promotedLearnings.Add(learning);
        }

        var targetScopeMap = promotionResult.SuccessfulPromotions
            .ToDictionary(p => p.LearningId, p => p.TargetScope);

        var summary = await _summaryService!.GenerateSummaryAsync(
            promotedLearnings.AsReadOnly(),
            targetScopeMap,
            taskId,
            "test-user",
            CancellationToken.None);

        // Assert
        var avgConfidence = (0.95 + 0.85 + 0.80) / 3.0;
        Assert.Contains($"Average confidence: {avgConfidence:F2}", summary.SummaryText);
        Assert.Contains("UserFeedback (2)", summary.SummaryText);
        Assert.Contains("SelfCorrection (1)", summary.SummaryText);
    }

    private async Task<Learning> CreateTestLearning(
        string title,
        string description,
        string triggerType,
        string scope,
        double confidence,
        string sourceTaskId)
    {
        var learning = new Learning
        {
            LearningId = Guid.NewGuid().ToString(),
            Title = title,
            Description = description,
            TriggerType = triggerType,
            Scope = scope,
            Confidence = confidence,
            Status = "Active",
            SourceAgent = "TestAgent",
            SourceTaskId = sourceTaskId,
            CreatedBy = "test-user",
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Tags = null,
            TimesApplied = 0,
            EmbeddingBlob = null,
            EmbeddingDimensions = null
        };

        await _learningService!.CreateLearningAsync(
            learning.LearningId,
            learning.Title,
            learning.Description,
            triggerType,
            confidence,
            scope,
            "TestAgent",
            sourceTaskId,
            "test-user",
            CancellationToken.None);
        
        return learning;
    }
}
