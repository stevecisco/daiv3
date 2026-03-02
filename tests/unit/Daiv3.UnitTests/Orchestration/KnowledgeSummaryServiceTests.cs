using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Daiv3.Orchestration;
using Daiv3.Orchestration.Models;
using Daiv3.Persistence.Entities;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Daiv3.UnitTests.Orchestration;

/// <summary>
/// Unit tests for KnowledgeSummaryService per KBP-REQ-004.
/// </summary>
public class KnowledgeSummaryServiceTests
{
    private readonly Mock<ILogger<KnowledgeSummaryService>> _mockLogger;
    private readonly KnowledgeSummaryService _service;

    public KnowledgeSummaryServiceTests()
    {
        _mockLogger = new Mock<ILogger<KnowledgeSummaryService>>();
        _service = new KnowledgeSummaryService(_mockLogger.Object);
    }

    [Fact]
    public async Task GenerateSummaryAsync_WithSinglePromotion_GeneratesValidSummary()
    {
        // Arrange
        var learnings = new List<Learning>
        {
            new Learning
            {
                LearningId = "test-learning-1",
                Title = "Test Learning",
                Description = "A test learning description",
                TriggerType = "UserFeedback",
                Scope = "Agent",
                Confidence = 0.9,
                Status = "Active",
                SourceAgent = "TestAgent",
                SourceTaskId = "task-123",
                CreatedBy = "user",
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            }
        };

        var targetScopes = new Dictionary<string, string>
        {
            ["test-learning-1"] = "Project"
        };

        // Act
        var summary = await _service.GenerateSummaryAsync(
            learnings,
            targetScopes,
            "task-123",
            "user",
            CancellationToken.None);

        // Assert
        Assert.NotNull(summary);
        Assert.Equal(1, summary.PromotedCount);
        Assert.Single(summary.LearningIds);
        Assert.Contains("test-learning-1", summary.LearningIds);
        Assert.Contains("Agent", summary.SourceScopes);
        Assert.Contains("Project", summary.TargetScopes);
        Assert.Equal("task-123", summary.SourceTaskId);
        Assert.Equal("user", summary.PromotedBy);
        Assert.NotEmpty(summary.SummaryText);
        Assert.Contains("Test Learning", summary.SummaryText);
        Assert.NotNull(summary.Details);
        Assert.Single(summary.Details);
    }

    [Fact]
    public async Task GenerateSummaryAsync_WithMultiplePromotions_GroupsByTargetScope()
    {
        // Arrange
        var learnings = new List<Learning>
        {
            new Learning
            {
                LearningId = "learning-1",
                Title = "Learning 1",
                Description = "Description 1",
                TriggerType = "SelfCorrection",
                Scope = "Agent",
                Confidence = 0.85,
                Status = "Active",
                SourceAgent = "Agent1",
                CreatedBy = "user",
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            },
            new Learning
            {
                LearningId = "learning-2",
                Title = "Learning 2",
                Description = "Description 2",
                TriggerType = "CompilationError",
                Scope = "Skill",
                Confidence = 0.75,
                Status = "Active",
                SourceAgent = "Agent2",
                CreatedBy = "user",
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            },
            new Learning
            {
                LearningId = "learning-3",
                Title = "Learning 3",
                Description = "Description 3",
                TriggerType = "ToolFailure",
                Scope = "Agent",
                Confidence = 0.92,
                Status = "Active",
                SourceAgent = "Agent1",
                CreatedBy = "user",
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            }
        };

        var targetScopes = new Dictionary<string, string>
        {
            ["learning-1"] = "Project",
            ["learning-2"] = "Project",
            ["learning-3"] = "Domain"
        };

        // Act
        var summary = await _service.GenerateSummaryAsync(
            learnings,
            targetScopes,
            "task-456",
            "user",
            CancellationToken.None);

        // Assert
        Assert.NotNull(summary);
        Assert.Equal(3, summary.PromotedCount);
        Assert.Equal(3, summary.LearningIds.Count);
        Assert.Contains("Project", summary.TargetScopes);
        Assert.Contains("Domain", summary.TargetScopes);
        Assert.Contains("**To Project scope", summary.SummaryText);
        Assert.Contains("**To Domain scope", summary.SummaryText);
        Assert.NotNull(summary.Details);
        Assert.Equal(3, summary.Details.Count);
    }

    [Fact]
    public async Task GenerateSummaryAsync_WithEmptyList_ReturnsEmptySummary()
    {
        // Arrange
        var learnings = new List<Learning>();
        var targetScopes = new Dictionary<string, string>();

        // Act
        var summary = await _service.GenerateSummaryAsync(
            learnings,
            targetScopes,
            null,
            "user",
            CancellationToken.None);

        // Assert
        Assert.NotNull(summary);
        Assert.Equal(0, summary.PromotedCount);
        Assert.Empty(summary.LearningIds);
        Assert.Empty(summary.SourceScopes);
        Assert.Empty(summary.TargetScopes);
        Assert.Contains("No learnings were promoted", summary.SummaryText);
    }

    [Fact]
    public async Task GenerateSummaryAsync_IncludesStatistics()
    {
        // Arrange
        var learnings = new List<Learning>
        {
            new Learning
            {
                LearningId = "learning-1",
                Title = "Learning 1",
                Description = "Description 1",
                TriggerType = "UserFeedback",
                Scope = "Agent",
                Confidence = 0.9,
                Status = "Active",
                SourceAgent = "Agent1",
                CreatedBy = "user",
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            },
            new Learning
            {
                LearningId = "learning-2",
                Title = "Learning 2",
                Description = "Description 2",
                TriggerType = "UserFeedback",
                Scope = "Agent",
                Confidence = 0.8,
                Status = "Active",
                SourceAgent = "Agent1",
                CreatedBy = "user",
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            }
        };

        var targetScopes = new Dictionary<string, string>
        {
            ["learning-1"] = "Project",
            ["learning-2"] = "Project"
        };

        // Act
        var summary = await _service.GenerateSummaryAsync(
            learnings,
            targetScopes,
            "task-789",
            "user",
            CancellationToken.None);

        // Assert
        Assert.NotNull(summary);
        Assert.Contains("**Statistics:**", summary.SummaryText);
        Assert.Contains("Average confidence: 0.85", summary.SummaryText); // (0.9 + 0.8) / 2
        Assert.Contains("UserFeedback (2)", summary.SummaryText);
    }

    [Fact]
    public async Task GenerateSummaryAsync_HandlesLongDescriptions()
    {
        // Arrange
        var longDescription = new string('x', 200);
        var learnings = new List<Learning>
        {
            new Learning
            {
                LearningId = "learning-1",
                Title = "Learning with Long Description",
                Description = longDescription,
                TriggerType = "Explicit",
                Scope = "Skill",
                Confidence = 0.75,
                Status = "Active",
                SourceAgent = "Agent1",
                CreatedBy = "user",
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            }
        };

        var targetScopes = new Dictionary<string, string>
        {
            ["learning-1"] = "Agent"
        };

        // Act
        var summary = await _service.GenerateSummaryAsync(
            learnings,
            targetScopes,
            null,
            "user",
            CancellationToken.None);

        // Assert
        Assert.NotNull(summary);
        Assert.Contains("...", summary.SummaryText); // Description should be truncated
        Assert.DoesNotContain(longDescription, summary.SummaryText); // Full description should not appear
    }

    [Fact]
    public async Task GenerateSummaryAsync_IncludesDetailedLearningInfo()
    {
        // Arrange
        var learnings = new List<Learning>
        {
            new Learning
            {
                LearningId = "learning-1",
                Title = "Test Learning",
                Description = "Test description",
                TriggerType = "KnowledgeConflict",
                Scope = "Agent",
                Confidence = 0.88,
                Status = "Active",
                SourceAgent = "Agent1",
                CreatedBy = "user",
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            }
        };

        var targetScopes = new Dictionary<string, string>
        {
            ["learning-1"] = "Project"
        };

        // Act
        var summary = await _service.GenerateSummaryAsync(
            learnings,
            targetScopes,
            "task-999",
            "user",
            CancellationToken.None);

        // Assert
        Assert.NotNull(summary.Details);
        Assert.Single(summary.Details);
        
        var detail = summary.Details[0];
        Assert.Equal("learning-1", detail.LearningId);
        Assert.Equal("Test Learning", detail.Title);
        Assert.Equal("Test description", detail.Description);
        Assert.Equal(0.88, detail.Confidence);
        Assert.Equal("Agent", detail.SourceScope);
        Assert.Equal("Project", detail.TargetScope);
        Assert.Equal("KnowledgeConflict", detail.TriggerType);
    }

    [Fact]
    public async Task GenerateSummaryAsync_ThrowsOnNullLearnings()
    {
        // Arrange
        var targetScopes = new Dictionary<string, string>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _service.GenerateSummaryAsync(null!, targetScopes, null, "user"));
    }

    [Fact]
    public async Task GenerateSummaryAsync_ThrowsOnNullTargetScopes()
    {
        // Arrange
        var learnings = new List<Learning>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _service.GenerateSummaryAsync(learnings, null!, null, "user"));
    }

    [Fact]
    public async Task GenerateSummaryAsync_ThrowsOnEmptyPromotedBy()
    {
        // Arrange
        var learnings = new List<Learning>();
        var targetScopes = new Dictionary<string, string>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.GenerateSummaryAsync(learnings, targetScopes, null, ""));
    }

    [Fact]
    public async Task GenerateSummaryAsync_SortsScopesByHierarchy()
    {
        // Arrange - Create learnings with varied target scopes
        var learnings = new List<Learning>
        {
            CreateTestLearning("l1", "Skill"),
            CreateTestLearning("l2", "Agent"),
            CreateTestLearning("l3", "Global"),
            CreateTestLearning("l4", "Project"),
            CreateTestLearning("l5", "Domain")
        };

        var targetScopes = new Dictionary<string, string>
        {
            ["l1"] = "Global",
            ["l2"] = "Domain",
            ["l3"] = "Skill",
            ["l4"] = "Agent",
            ["l5"] = "Project"
        };

        // Act
        var summary = await _service.GenerateSummaryAsync(
            learnings,
            targetScopes,
            null,
            "user",
            CancellationToken.None);

        // Assert - Check that scopes appear in hierarchical order in summary text
        var skillIndex = summary.SummaryText.IndexOf("**To Skill scope");
        var agentIndex = summary.SummaryText.IndexOf("**To Agent scope");
        var projectIndex = summary.SummaryText.IndexOf("**To Project scope");
        var domainIndex = summary.SummaryText.IndexOf("**To Domain scope");
        var globalIndex = summary.SummaryText.IndexOf("**To Global scope");

        Assert.True(skillIndex < agentIndex, "Skill should appear before Agent");
        Assert.True(agentIndex < projectIndex, "Agent should appear before Project");
        Assert.True(projectIndex < domainIndex, "Project should appear before Domain");
        Assert.True(domainIndex < globalIndex, "Domain should appear before Global");
    }

    private static Learning CreateTestLearning(string id, string scope)
    {
        return new Learning
        {
            LearningId = id,
            Title = $"Learning {id}",
            Description = $"Description for {id}",
            TriggerType = "Explicit",
            Scope = scope,
            Confidence = 0.8,
            Status = "Active",
            SourceAgent = "TestAgent",
            CreatedBy = "user",
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
    }
}
