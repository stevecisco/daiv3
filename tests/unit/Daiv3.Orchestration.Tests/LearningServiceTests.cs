using Daiv3.Knowledge.Embedding;
using Daiv3.Orchestration;
using Daiv3.Orchestration.Interfaces;
using Daiv3.Orchestration.Models;
using Daiv3.Persistence;
using Daiv3.Persistence.Entities;
using Daiv3.Persistence.Repositories;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Daiv3.Orchestration.Tests;

/// <summary>
/// Unit tests for LearningService (LM-REQ-001).
/// Tests learning creation triggers from various sources.
/// </summary>
public class LearningServiceTests
{
    private readonly Mock<ILogger<LearningService>> _mockLogger;
    private readonly Mock<LearningRepository> _mockRepository;
    private readonly Mock<IEmbeddingGenerator> _mockEmbeddingGenerator;
    private readonly ILearningService _service;

    public LearningServiceTests()
    {
        _mockLogger = new Mock<ILogger<LearningService>>();
        _mockRepository = new Mock<LearningRepository>(
            Mock.Of<IDatabaseContext>(),
            Mock.Of<ILogger<LearningRepository>>());
        _mockEmbeddingGenerator = new Mock<IEmbeddingGenerator>();

        // Setup default embedding generation
        _mockEmbeddingGenerator
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 0.1f, 0.2f, 0.3f });

        _service = new LearningService(
            _mockLogger.Object,
            _mockRepository.Object,
            _mockEmbeddingGenerator.Object);
    }

    #region General Learning Creation Tests

    [Fact]
    public async Task CreateLearningAsync_PopulatesAllLmReq002RequiredFields()
    {
        var context = new ExplicitTriggerContext
        {
            Title = "Learning record structure validation",
            Description = "Ensure learning includes all required LM-REQ-002 fields.",
            Scope = "Project",
            SourceAgent = "agent-lm-002",
            SourceTaskId = "task-lm-002",
            Tags = "lm,structure,validation",
            Confidence = 0.91,
            CreatedBy = "agent-lm-002"
        };

        _mockRepository
            .Setup(x => x.AddAsync(It.IsAny<Learning>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid().ToString());

        var result = await _service.CreateLearningAsync(context);

        Assert.False(string.IsNullOrWhiteSpace(result.LearningId));
        Assert.Equal("Learning record structure validation", result.Title);
        Assert.Equal("Ensure learning includes all required LM-REQ-002 fields.", result.Description);
        Assert.Equal("Explicit", result.TriggerType);
        Assert.Equal("Project", result.Scope);
        Assert.Equal("agent-lm-002", result.SourceAgent);
        Assert.Equal("task-lm-002", result.SourceTaskId);
        Assert.NotNull(result.EmbeddingBlob);
        Assert.Equal(3, result.EmbeddingDimensions);
        Assert.Equal("lm,structure,validation", result.Tags);
        Assert.Equal(0.91, result.Confidence);
        Assert.Equal("Active", result.Status);
        Assert.Equal(0, result.TimesApplied);
        Assert.True(result.CreatedAt > 0);
        Assert.True(result.UpdatedAt >= result.CreatedAt);
        Assert.Equal("agent-lm-002", result.CreatedBy);
    }

    [Fact]
    public async Task CreateLearningAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.CreateLearningAsync(null!));
    }

    [Fact]
    public async Task CreateLearningAsync_CreatesLearningWithEmbedding()
    {
        // Arrange
        var context = new ExplicitTriggerContext
        {
            Title = "Test Learning",
            Description = "Test description for learning",
            Scope = "Global",
            SourceAgent = "agent-123",
            SourceTaskId = "task-456",
            Tags = "test,learning",
            Confidence = 0.8,
            CreatedBy = "system"
        };

        Learning? capturedLearning = null;
        _mockRepository
            .Setup(x => x.AddAsync(It.IsAny<Learning>(), It.IsAny<CancellationToken>()))
            .Callback<Learning, CancellationToken>((l, _) => capturedLearning = l)
            .ReturnsAsync(Guid.NewGuid().ToString());

        // Act
        var result = await _service.CreateLearningAsync(context);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test Learning", result.Title);
        Assert.Equal("Test description for learning", result.Description);
        Assert.Equal("Explicit", result.TriggerType);
        Assert.Equal("Global", result.Scope);
        Assert.Equal("agent-123", result.SourceAgent);
        Assert.Equal("task-456", result.SourceTaskId);
        Assert.Equal("test,learning", result.Tags);
        Assert.Equal(0.8, result.Confidence);
        Assert.Equal("Active", result.Status);
        Assert.Equal(0, result.TimesApplied);
        Assert.NotNull(result.EmbeddingBlob);
        Assert.Equal(3, result.EmbeddingDimensions);

        _mockEmbeddingGenerator.Verify(
            x => x.GenerateEmbeddingAsync("Test description for learning", It.IsAny<CancellationToken>()),
            Times.Once);
        _mockRepository.Verify(
            x => x.AddAsync(It.IsAny<Learning>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateLearningAsync_HandlesEmbeddingGenerationFailure()
    {
        // Arrange
        var context = new ExplicitTriggerContext
        {
            Title = "Test Learning",
            Description = "Test description",
            Scope = "Global"
        };

        _mockEmbeddingGenerator
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Embedding generation failed"));

        _mockRepository
            .Setup(x => x.AddAsync(It.IsAny<Learning>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid().ToString());

        // Act
        var result = await _service.CreateLearningAsync(context);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.EmbeddingBlob);
        Assert.Empty(result.EmbeddingBlob);
        Assert.Null(result.EmbeddingDimensions);
    }

    [Fact]
    public async Task CreateLearningAsync_ThrowsInvalidOperationException_WhenRepositoryFails()
    {
        // Arrange
        var context = new ExplicitTriggerContext
        {
            Title = "Test Learning",
            Description = "Test description",
            Scope = "Global"
        };

        _mockRepository
            .Setup(x => x.AddAsync(It.IsAny<Learning>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateLearningAsync(context));

        Assert.Contains("Failed to create learning", exception.Message);
    }

    #endregion

    #region Self-Correction Learning Tests

    [Fact]
    public async Task CreateSelfCorrectionLearningAsync_CreatesLearningWithCorrectTriggerType()
    {
        // Arrange
        var context = new SelfCorrectionTriggerContext
        {
            Title = "Fixed incorrect API call",
            Description = "Learned correct API invocation pattern",
            FailedIteration = 1,
            FailedOutput = "Error: Invalid parameters",
            FailureReason = "Incorrect parameter format",
            SuccessIteration = 2,
            SuccessOutput = "Success: 200 OK",
            SuggestedCorrection = "Use JSON format",
            SourceAgent = "agent-123",
            Scope = "Agent"
        };

        _mockRepository
            .Setup(x => x.AddAsync(It.IsAny<Learning>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid().ToString());

        // Act
        var result = await _service.CreateSelfCorrectionLearningAsync(context);

        // Assert
        Assert.Equal("SelfCorrection", result.TriggerType);
        Assert.Equal(0.8, result.Confidence); // Default for self-correction
        Assert.Equal("Fixed incorrect API call", result.Title);
        Assert.Equal("Agent", result.Scope);
    }

    [Fact]
    public async Task CreateSelfCorrectionLearningAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.CreateSelfCorrectionLearningAsync(null!));
    }

    #endregion

    #region User Feedback Learning Tests

    [Fact]
    public async Task CreateUserFeedbackLearningAsync_CreatesLearningWithHighConfidence()
    {
        // Arrange
        var context = new UserFeedbackTriggerContext
        {
            Title = "User corrected output format",
            Description = "User prefers bullet-point summaries",
            OriginalOutput = "Long paragraph format...",
            CorrectedOutput = "- Point 1\n- Point 2",
            UserExplanation = "Too wordy, prefer concise format",
            Scope = "Global"
        };

        _mockRepository
            .Setup(x => x.AddAsync(It.IsAny<Learning>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid().ToString());

        // Act
        var result = await _service.CreateUserFeedbackLearningAsync(context);

        // Assert
        Assert.Equal("UserFeedback", result.TriggerType);
        Assert.Equal(0.95, result.Confidence); // High confidence for user feedback
        Assert.Equal("User corrected output format", result.Title);
    }

    [Fact]
    public async Task CreateUserFeedbackLearningAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.CreateUserFeedbackLearningAsync(null!));
    }

    #endregion

    #region Compilation Error Learning Tests

    [Fact]
    public async Task CreateCompilationErrorLearningAsync_CreatesLearningWithErrorDetails()
    {
        // Arrange
        var context = new CompilationErrorTriggerContext
        {
            Title = "FileStream disposal pattern",
            Description = "Always wrap FileStream in using statement",
            ErrorCode = "var fs = new FileStream(path, FileMode.Open);",
            ErrorMessage = "CS1061: File lock error",
            FixedCode = "using var fs = new FileStream(path, FileMode.Open);",
            Language = "csharp",
            Tags = "csharp,file-io",
            Scope = "Global"
        };

        _mockRepository
            .Setup(x => x.AddAsync(It.IsAny<Learning>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid().ToString());

        // Act
        var result = await _service.CreateCompilationErrorLearningAsync(context);

        // Assert
        Assert.Equal("CompilationError", result.TriggerType);
        Assert.Equal(0.85, result.Confidence); // Default for compilation errors
        Assert.Equal("FileStream disposal pattern", result.Title);
        Assert.Equal("csharp,file-io", result.Tags);
    }

    [Fact]
    public async Task CreateCompilationErrorLearningAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.CreateCompilationErrorLearningAsync(null!));
    }

    #endregion

    #region Tool Failure Learning Tests

    [Fact]
    public async Task CreateToolFailureLearningAsync_CreatesLearningWithToolDetails()
    {
        // Arrange
        var context = new ToolFailureTriggerContext
        {
            Title = "Correct REST API authentication",
            Description = "API requires Bearer token in Authorization header",
            ToolName = "RestApiTool",
            IncorrectInvocation = "{\"url\": \"...\", \"headers\": {}}",
            ToolError = "401 Unauthorized",
            CorrectInvocation = "{\"url\": \"...\", \"headers\": {\"Authorization\": \"Bearer token\"}}",
            Tags = "rest-api,auth",
            Scope = "Global"
        };

        _mockRepository
            .Setup(x => x.AddAsync(It.IsAny<Learning>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid().ToString());

        // Act
        var result = await _service.CreateToolFailureLearningAsync(context);

        // Assert
        Assert.Equal("ToolFailure", result.TriggerType);
        Assert.Equal(0.8, result.Confidence);
        Assert.Equal("Correct REST API authentication", result.Title);
        Assert.Equal("rest-api,auth", result.Tags);
    }

    [Fact]
    public async Task CreateToolFailureLearningAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.CreateToolFailureLearningAsync(null!));
    }

    #endregion

    #region Knowledge Conflict Learning Tests

    [Fact]
    public async Task CreateKnowledgeConflictLearningAsync_CreatesLearningWithConflictDetails()
    {
        // Arrange
        var context = new KnowledgeConflictTriggerContext
        {
            Title = "Updated API endpoint",
            Description = "API v2 endpoint changed from /api/v1 to /api/v2",
            PreviousBelief = "Use /api/v1/users",
            NewInformation = "API documentation shows /api/v2/users",
            ConflictSource = "Official API docs",
            Resolution = "Updated to use /api/v2/users endpoint",
            Scope = "Project",
            Tags = "api,endpoint"
        };

        _mockRepository
            .Setup(x => x.AddAsync(It.IsAny<Learning>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid().ToString());

        // Act
        var result = await _service.CreateKnowledgeConflictLearningAsync(context);

        // Assert
        Assert.Equal("KnowledgeConflict", result.TriggerType);
        Assert.Equal(0.6, result.Confidence); // Lower confidence for conflicts
        Assert.Equal("Updated API endpoint", result.Title);
        Assert.Equal("Project", result.Scope);
    }

    [Fact]
    public async Task CreateKnowledgeConflictLearningAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.CreateKnowledgeConflictLearningAsync(null!));
    }

    #endregion

    #region Explicit Learning Tests

    [Fact]
    public async Task CreateExplicitLearningAsync_CreatesLearningFromAgentCall()
    {
        // Arrange
        var context = new ExplicitTriggerContext
        {
            Title = "Efficient batch processing pattern",
            Description = "Use batch processing for large datasets",
            AgentReasoning = "Observed performance improvement with batching",
            SupportingEvidence = "Reduced processing time by 50%",
            SourceAgent = "optimization-agent",
            Scope = "Domain",
            Tags = "performance,batch-processing"
        };

        _mockRepository
            .Setup(x => x.AddAsync(It.IsAny<Learning>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid().ToString());

        // Act
        var result = await _service.CreateExplicitLearningAsync(context);

        // Assert
        Assert.Equal("Explicit", result.TriggerType);
        Assert.Equal(0.75, result.Confidence);
        Assert.Equal("Efficient batch processing pattern", result.Title);
        Assert.Equal("Domain", result.Scope);
        Assert.Equal("optimization-agent", result.SourceAgent);
    }

    [Fact]
    public async Task CreateExplicitLearningAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.CreateExplicitLearningAsync(null!));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task CreateLearningAsync_SetsDefaultValues_WhenOptionalFieldsNotProvided()
    {
        // Arrange
        var context = new ExplicitTriggerContext
        {
            Title = "Test",
            Description = "Test description"
            // Scope defaults to "Global", CreatedBy defaults to "system"
        };

        _mockRepository
            .Setup(x => x.AddAsync(It.IsAny<Learning>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid().ToString());

        // Act
        var result = await _service.CreateLearningAsync(context);

        // Assert
        Assert.Equal("Global", result.Scope);
        Assert.Equal("system", result.CreatedBy);
        Assert.Null(result.SourceAgent);
        Assert.Null(result.SourceTaskId);
        Assert.Null(result.Tags);
    }

    [Fact]
    public async Task CreateLearningAsync_GeneratesUniqueIds()
    {
        // Arrange
        var context1 = new ExplicitTriggerContext { Title = "Test 1", Description = "Desc 1" };
        var context2 = new ExplicitTriggerContext { Title = "Test 2", Description = "Desc 2" };

        _mockRepository
            .Setup(x => x.AddAsync(It.IsAny<Learning>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid().ToString());

        // Act
        var result1 = await _service.CreateLearningAsync(context1);
        var result2 = await _service.CreateLearningAsync(context2);

        // Assert
        Assert.NotEqual(result1.LearningId, result2.LearningId);
    }

    [Fact]
    public async Task CreateLearningAsync_SetsTimestamps()
    {
        // Arrange
        var context = new ExplicitTriggerContext { Title = "Test", Description = "Test" };
        var beforeCreation = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        _mockRepository
            .Setup(x => x.AddAsync(It.IsAny<Learning>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid().ToString());

        // Act
        var result = await _service.CreateLearningAsync(context);
        var afterCreation = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Assert
        Assert.True(result.CreatedAt >= beforeCreation && result.CreatedAt <= afterCreation);
        Assert.True(result.UpdatedAt >= beforeCreation && result.UpdatedAt <= afterCreation);
        Assert.Equal(result.CreatedAt, result.UpdatedAt);
    }

    #endregion
}
