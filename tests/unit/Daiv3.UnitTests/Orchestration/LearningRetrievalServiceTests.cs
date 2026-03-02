using Daiv3.Knowledge.Embedding;
using Daiv3.Orchestration;
using Daiv3.Orchestration.Interfaces;
using Daiv3.Persistence;
using Daiv3.Persistence.Entities;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Daiv3.UnitTests.Orchestration;

/// <summary>
/// Unit tests for LearningRetrievalService (LM-REQ-005).
/// Tests semantic retrieval and ranking of relevant learnings for agent execution.
/// </summary>
public class LearningRetrievalServiceTests
{
    private readonly Mock<ILogger<LearningRetrievalService>> _mockLogger;
    private readonly Mock<ILearningStorageService> _mockStorageService;
    private readonly Mock<IEmbeddingGenerator> _mockEmbeddingGenerator;
    private readonly SimpleVectorSimilarityStubForLearnings _vectorSimilarity;
    private readonly LearningRetrievalService _service;

    public LearningRetrievalServiceTests()
    {
        _mockLogger = new Mock<ILogger<LearningRetrievalService>>();
        _mockStorageService = new Mock<ILearningStorageService>(MockBehavior.Strict);
        _mockEmbeddingGenerator = new Mock<IEmbeddingGenerator>();
        _vectorSimilarity = new SimpleVectorSimilarityStubForLearnings();

        _service = new LearningRetrievalService(
            _mockLogger.Object,
            _mockStorageService.Object,
            _mockEmbeddingGenerator.Object,
            _vectorSimilarity);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new LearningRetrievalService(null!, _mockStorageService.Object, _mockEmbeddingGenerator.Object, _vectorSimilarity));
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenStorageServiceIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new LearningRetrievalService(_mockLogger.Object, null!, _mockEmbeddingGenerator.Object, _vectorSimilarity));
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenEmbeddingGeneratorIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new LearningRetrievalService(_mockLogger.Object, _mockStorageService.Object, null!, _vectorSimilarity));
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenVectorSimilarityIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new LearningRetrievalService(_mockLogger.Object, _mockStorageService.Object, _mockEmbeddingGenerator.Object, null!));
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task RetrieveLearningsAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.RetrieveLearningsAsync(null!));
    }

    [Fact]
    public async Task RetrieveLearningsAsync_ThrowsArgumentException_WhenTaskGoalIsEmpty()
    {
        var context = new LearningRetrievalContext { TaskGoal = "" };

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.RetrieveLearningsAsync(context));
    }

    [Fact]
    public async Task RetrieveLearningsAsync_ThrowsArgumentOutOfRangeException_WhenMinConfidenceIsNegative()
    {
        var context = new LearningRetrievalContext
        {
            TaskGoal = "Test task",
            MinConfidence = -0.1
        };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.RetrieveLearningsAsync(context));
    }

    [Fact]
    public async Task RetrieveLearningsAsync_ThrowsArgumentOutOfRangeException_WhenMinConfidenceIsAboveOne()
    {
        var context = new LearningRetrievalContext
        {
            TaskGoal = "Test task",
            MinConfidence = 1.1
        };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.RetrieveLearningsAsync(context));
    }

    [Fact]
    public async Task RetrieveLearningsAsync_ThrowsArgumentOutOfRangeException_WhenMinSimilarityIsNegative()
    {
        var context = new LearningRetrievalContext
        {
            TaskGoal = "Test task",
            MinSimilarity = -0.1
        };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.RetrieveLearningsAsync(context));
    }

    [Fact]
    public async Task RetrieveLearningsAsync_ThrowsArgumentOutOfRangeException_WhenMaxResultsIsZero()
    {
        var context = new LearningRetrievalContext
        {
            TaskGoal = "Test task",
            MaxResults = 0
        };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.RetrieveLearningsAsync(context));
    }

    [Fact]
    public async Task RetrieveLearningsAsync_ThrowsArgumentOutOfRangeException_WhenMaxRetrievalTimeMsIsZero()
    {
        var context = new LearningRetrievalContext
        {
            TaskGoal = "Test task",
            MaxRetrievalTimeMs = 0
        };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.RetrieveLearningsAsync(context));
    }

    [Fact]
    public async Task RetrieveLearningsAsync_ThrowsArgumentOutOfRangeException_WhenSlowRetrievalWarningMsIsZero()
    {
        var context = new LearningRetrievalContext
        {
            TaskGoal = "Test task",
            SlowRetrievalWarningMs = 0
        };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.RetrieveLearningsAsync(context));
    }

    [Fact]
    public async Task RetrieveLearningsAsync_ThrowsArgumentOutOfRangeException_WhenMaxCandidatesToScoreIsZero()
    {
        var context = new LearningRetrievalContext
        {
            TaskGoal = "Test task",
            MaxCandidatesToScore = 0
        };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.RetrieveLearningsAsync(context));
    }

    #endregion

    #region Retrieval Tests

    [Fact]
    public async Task RetrieveLearningsAsync_ReturnsEmpty_WhenNoLearningsWithEmbeddings()
    {
        // Arrange
        var context = new LearningRetrievalContext { TaskGoal = "Test task" };

        _mockStorageService
            .Setup(x => x.GetEmbeddedLearningsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Learning>());

        // Act
        var result = await _service.RetrieveLearningsAsync(context);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task RetrieveLearningsAsync_ReturnsEmpty_WhenNoActiveLearnings()
    {
        // Arrange
        var context = new LearningRetrievalContext { TaskGoal = "Test task" };

        var learnings = new List<Learning>
        {
            CreateLearning("1", "Suppressed learning", "Suppressed", 0.9, 384)
        };

        _mockStorageService
            .Setup(x => x.GetEmbeddedLearningsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(learnings);

        // Act
        var result = await _service.RetrieveLearningsAsync(context);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task RetrieveLearningsAsync_FiltersLearningsByScope()
    {
        // Arrange
        var context = new LearningRetrievalContext
        {
            TaskGoal = "Test task",
            Scope = "Agent",
            MinConfidence = 0.5,
            MinSimilarity = 0.3
        };

        var learnings = new List<Learning>
        {
            CreateLearning("1", "Agent learning", "Active", 0.8, 384, scope: "Agent"),
            CreateLearning("2", "Global learning", "Active", 0.9, 384, scope: "Global")
        };

        _mockStorageService
            .Setup(x => x.GetEmbeddedLearningsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(learnings);

        _mockEmbeddingGenerator
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[384]);

        // Configure stub to return higher score for agent learning
        _vectorSimilarity.BatchScoreGenerator = i => 0.85f;

        // Act
        var result = await _service.RetrieveLearningsAsync(context);

        // Assert
        Assert.Single(result);
        Assert.Equal("1", result[0].Learning.LearningId);
        Assert.Equal("Agent", result[0].Learning.Scope);
    }

    [Fact]
    public async Task RetrieveLearningsAsync_FiltersLearningsByConfidence()
    {
        // Arrange
        var context = new LearningRetrievalContext
        {
            TaskGoal = "Test task",
            MinConfidence = 0.7,
            MinSimilarity = 0.3
        };

        var learnings = new List<Learning>
        {
            CreateLearning("1", "High confidence", "Active", 0.9, 384),
            CreateLearning("2", "Low confidence", "Active", 0.5, 384)
        };

        _mockStorageService
            .Setup(x => x.GetEmbeddedLearningsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(learnings);

        _mockEmbeddingGenerator
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[384]);

        _vectorSimilarity.BatchScoreGenerator = i => 0.8f;

        // Act
        var result = await _service.RetrieveLearningsAsync(context);

        // Assert
        Assert.Single(result);
        Assert.Equal("1", result[0].Learning.LearningId);
        Assert.Equal(0.9, result[0].Learning.Confidence);
    }

    [Fact]
    public async Task RetrieveLearningsAsync_ReturnsTopNBySimilarityScore()
    {
        // Arrange
        var context = new LearningRetrievalContext
        {
            TaskGoal = "Test task coding patterns",
            MinConfidence = 0.5,
            MinSimilarity = 0.3,
            MaxResults = 2
        };

        var learnings = new List<Learning>
        {
            CreateLearning("1", "Highly relevant learning", "Active", 0.8, 384),
            CreateLearning("2", "Somewhat relevant learning", "Active", 0.7, 384),
            CreateLearning("3", "Least relevant learning", "Active", 0.6, 384)
        };

        _mockStorageService
            .Setup(x => x.GetEmbeddedLearningsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(learnings);

        _mockEmbeddingGenerator
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[384]);

        _vectorSimilarity.BatchScoreGenerator = i =>
        {
            return i switch
            {
                0 => 0.9f, // Highly relevant
                1 => 0.6f, // Somewhat relevant
                2 => 0.4f, // Least relevant
                _ => 0.5f
            };
        };

        // Act
        var result = await _service.RetrieveLearningsAsync(context);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("1", result[0].Learning.LearningId);
        Assert.Equal(1, result[0].Rank);
        Assert.True(result[0].SimilarityScore > result[1].SimilarityScore);
        Assert.Equal("2", result[1].Learning.LearningId);
        Assert.Equal(2, result[1].Rank);
    }

    [Fact]
    public async Task RetrieveLearningsAsync_FiltersLearningsBySimilarityThreshold()
    {
        // Arrange
        var context = new LearningRetrievalContext
        {
            TaskGoal = "Test task",
            MinConfidence = 0.5,
            MinSimilarity = 0.7  // High similarity threshold
        };

        var learnings = new List<Learning>
        {
            CreateLearning("1", "Relevant learning", "Active", 0.8, 384),
            CreateLearning("2", "Not relevant learning", "Active", 0.8, 384)
        };

        _mockStorageService
            .Setup(x => x.GetEmbeddedLearningsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(learnings);

        _mockEmbeddingGenerator
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[384]);

        _vectorSimilarity.BatchScoreGenerator = i => i == 0 ? 0.85f : 0.5f;

        // Act
        var result = await _service.RetrieveLearningsAsync(context);

        // Assert
        Assert.Single(result);
        Assert.Equal("1", result[0].Learning.LearningId);
        Assert.True(result[0].SimilarityScore >= context.MinSimilarity);
    }

    [Fact]
    public async Task RetrieveLearningsAsync_ReturnsEmpty_WhenEmbeddingGenerationFails()
    {
        // Arrange
        var context = new LearningRetrievalContext { TaskGoal = "Test task" };

        var learnings = new List<Learning>
        {
            CreateLearning("1", "Test learning", "Active", 0.8, 384)
        };

        _mockStorageService
            .Setup(x => x.GetEmbeddedLearningsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(learnings);

        _mockEmbeddingGenerator
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Embedding generation failed"));

        // Act
        var result = await _service.RetrieveLearningsAsync(context);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task RetrieveLearningsAsync_IncludesGlobalLearnings_WhenAgentIdSpecified()
    {
        // Arrange
        var context = new LearningRetrievalContext
        {
            TaskGoal = "Test task",
            AgentId = "agent-123",
            MinConfidence = 0.5,
            MinSimilarity = 0.3
        };

        var learnings = new List<Learning>
        {
            CreateLearning("1", "Agent-specific", "Active", 0.8, 384, sourceAgent: "agent-123"),
            CreateLearning("2", "Global learning", "Active", 0.9, 384, scope: "Global"),
            CreateLearning("3", "Other agent", "Active", 0.85, 384, scope: "Agent", sourceAgent: "agent-456")
        };

        _mockStorageService
            .Setup(x => x.GetEmbeddedLearningsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(learnings);

        _mockEmbeddingGenerator
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[384]);

        _vectorSimilarity.BatchScoreGenerator = i => 0.8f;

        // Act
        var result = await _service.RetrieveLearningsAsync(context);

        // Assert
        Assert.Equal(2, result.Count); // Agent-specific + Global, but not other agent
        Assert.Contains(result, r => r.Learning.LearningId == "1");
        Assert.Contains(result, r => r.Learning.LearningId == "2");
        Assert.DoesNotContain(result, r => r.Learning.LearningId == "3");
    }

    [Fact]
    public async Task RetrieveLearningsAsync_ReturnsEmpty_WhenRetrievalTimesOut()
    {
        // Arrange
        var context = new LearningRetrievalContext
        {
            TaskGoal = "Timeout task",
            MaxRetrievalTimeMs = 25,
            SlowRetrievalWarningMs = 10
        };

        var learnings = new List<Learning>
        {
            CreateLearning("1", "Slow retrieval learning", "Active", 0.9, 384)
        };

        _mockStorageService
            .Setup(x => x.GetEmbeddedLearningsAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken token) =>
            {
                await Task.Delay(250, token);
                return learnings;
            });

        // Act
        var result = await _service.RetrieveLearningsAsync(context);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task RetrieveLearningsAsync_CapsCandidatesForSimilarityCalculation()
    {
        // Arrange
        var context = new LearningRetrievalContext
        {
            TaskGoal = "Candidate cap task",
            MinConfidence = 0.5,
            MinSimilarity = 0.2,
            MaxCandidatesToScore = 2,
            MaxResults = 2
        };

        var learnings = new List<Learning>
        {
            CreateLearning("1", "Learning 1", "Active", 0.95, 384),
            CreateLearning("2", "Learning 2", "Active", 0.90, 384),
            CreateLearning("3", "Learning 3", "Active", 0.85, 384)
        };

        _mockStorageService
            .Setup(x => x.GetEmbeddedLearningsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(learnings);

        _mockEmbeddingGenerator
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[384]);

        _vectorSimilarity.BatchScoreGenerator = i => i switch
        {
            0 => 0.9f,
            1 => 0.8f,
            _ => 0.7f
        };

        // Act
        var result = await _service.RetrieveLearningsAsync(context);

        // Assert
        Assert.Equal(2, _vectorSimilarity.LastBatchVectorCount);
        Assert.Equal(2, result.Count);
    }

    #endregion

    #region Helper Methods

    private static Learning CreateLearning(
        string id,
        string title,
        string status,
        double confidence,
        int embeddingDimensions,
        string scope = "Global",
        string? sourceAgent = null)
    {
        var embedding = new float[embeddingDimensions];
        for (int i = 0; i < embeddingDimensions; i++)
        {
            embedding[i] = (float)i / embeddingDimensions;
        }

        var embeddingBytes = new byte[embeddingDimensions * sizeof(float)];
        Buffer.BlockCopy(embedding, 0, embeddingBytes, 0, embeddingBytes.Length);

        return new Learning
        {
            LearningId = id,
            Title = title,
            Description = $"Description for {title}",
            TriggerType = "Explicit",
            Scope = scope,
            SourceAgent = sourceAgent,
            Confidence = confidence,
            Status = status,
            EmbeddingBlob = embeddingBytes,
            EmbeddingDimensions = embeddingDimensions,
            TimesApplied = 0,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            CreatedBy = "system"
        };
    }

    #endregion
}

/// <summary>
/// Simple stub implementation of IVectorSimilarityService for testing.
/// Avoids M oq proxy issues with Span parameters and provides configurable similarity scores.
/// </summary>
public class SimpleVectorSimilarityStubForLearnings : IVectorSimilarityService
{
    public Func<int, float>? BatchScoreGenerator { get; set; }
    public int LastBatchVectorCount { get; private set; }

    public float CosineSimilarity(ReadOnlySpan<float> vector1, ReadOnlySpan<float> vector2)
    {
        return 0.5f;
    }

    public void BatchCosineSimilarity(
        ReadOnlySpan<float> queryVector,
        ReadOnlySpan<float> targetVectors,
        int vectorCount,
        int dimensions,
        Span<float> results)
    {
        LastBatchVectorCount = vectorCount;

        for (int i = 0; i < vectorCount; i++)
        {
            results[i] = BatchScoreGenerator != null ? BatchScoreGenerator(i) : 0.8f;
        }
    }

    public void Normalize(ReadOnlySpan<float> vector, Span<float> normalized)
    {
        // Mock implementation - just copy
        vector.CopyTo(normalized);
    }
}
