using Daiv3.Orchestration;
using Daiv3.Orchestration.Models;
using Daiv3.Persistence.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Daiv3.UnitTests.Orchestration;

public sealed class KnowledgeInternetDraftServiceTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly KnowledgeInternetDraftService _service;

    public KnowledgeInternetDraftServiceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"daiv3-internet-draft-tests-{Guid.NewGuid():N}");

        var options = Options.Create(new InternetKnowledgeDraftOptions
        {
            OutputDirectory = _tempDirectory,
            MaxDescriptionLength = 80
        });

        _service = new KnowledgeInternetDraftService(
            Mock.Of<ILogger<KnowledgeInternetDraftService>>(),
            options);
    }

    [Fact]
    public async Task CreateDraftArtifactAsync_WithInternetPromotion_WritesMarkdownFile()
    {
        var learning = CreateLearning("learning-1", "Internet Learning", "Detailed description for internet promotion.", 0.92);

        var targetScopes = new Dictionary<string, string>
        {
            [learning.LearningId] = "Internet"
        };

        var summary = CreateSummary(learning.LearningId);

        var artifact = await _service.CreateDraftArtifactAsync(
            new[] { learning },
            targetScopes,
            summary,
            "task-123",
            "user",
            CancellationToken.None);

        Assert.NotNull(artifact);
        Assert.True(File.Exists(artifact.ArtifactPath));
        Assert.Contains("Internet Learning", artifact.Content);
        Assert.Contains("Review Required", artifact.Content);
        Assert.Contains("task-123", artifact.Content);
    }

    [Fact]
    public async Task CreateDraftArtifactAsync_IgnoresNonInternetTargets()
    {
        var internetLearning = CreateLearning("learning-internet", "Internet Learning", "Should be included.", 0.90);
        var projectLearning = CreateLearning("learning-project", "Project Learning", "Should be excluded.", 0.80);

        var targetScopes = new Dictionary<string, string>
        {
            [internetLearning.LearningId] = "Internet",
            [projectLearning.LearningId] = "Project"
        };

        var summary = CreateSummary(internetLearning.LearningId, projectLearning.LearningId);

        var artifact = await _service.CreateDraftArtifactAsync(
            new[] { internetLearning, projectLearning },
            targetScopes,
            summary,
            "task-456",
            "user",
            CancellationToken.None);

        Assert.Single(artifact.LearningIds);
        Assert.Contains(internetLearning.LearningId, artifact.LearningIds);
        Assert.DoesNotContain(projectLearning.LearningId, artifact.LearningIds);
        Assert.Contains("Internet Learning", artifact.Content);
        Assert.DoesNotContain("Project Learning", artifact.Content);
    }

    [Fact]
    public async Task CreateDraftArtifactAsync_WithoutInternetTarget_ThrowsArgumentException()
    {
        var learning = CreateLearning("learning-1", "Project Learning", "Description", 0.76);

        var targetScopes = new Dictionary<string, string>
        {
            [learning.LearningId] = "Project"
        };

        var summary = CreateSummary(learning.LearningId);

        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateDraftArtifactAsync(
            new[] { learning },
            targetScopes,
            summary,
            "task-789",
            "user",
            CancellationToken.None));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    private static Learning CreateLearning(string id, string title, string description, double confidence)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        return new Learning
        {
            LearningId = id,
            Title = title,
            Description = description,
            TriggerType = "UserFeedback",
            Scope = "Global",
            Confidence = confidence,
            Status = "Active",
            SourceAgent = "agent-test",
            SourceTaskId = "task-test",
            CreatedBy = "user",
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static KnowledgeSummary CreateSummary(params string[] learningIds)
    {
        return new KnowledgeSummary
        {
            SummaryText = "Promotion summary",
            LearningIds = learningIds,
            SourceScopes = new[] { "Global" },
            TargetScopes = new[] { "Internet" },
            PromotedCount = learningIds.Length,
            SourceTaskId = "task-summary",
            PromotedBy = "user",
            GeneratedAt = DateTimeOffset.UtcNow,
            Details = null
        };
    }
}
