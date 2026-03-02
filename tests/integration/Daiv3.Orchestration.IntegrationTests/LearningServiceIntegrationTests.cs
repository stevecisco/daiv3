using Daiv3.Knowledge.Embedding;
using Daiv3.Orchestration;
using Daiv3.Orchestration.Interfaces;
using Daiv3.Orchestration.Models;
using Daiv3.Persistence;
using Daiv3.Persistence.Repositories;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Daiv3.Orchestration.IntegrationTests;

/// <summary>
/// Integration tests for LearningService (LM-REQ-001).
/// Tests full flow with actual database and embedding generation.
/// </summary>
[Collection("Database")]
public class LearningServiceIntegrationTests : IAsyncLifetime
{
    private DatabaseContext? _dbContext;
    private LearningRepository? _repository;
    private ILearningService? _service;

    public async Task InitializeAsync()
    {
        // Create in-memory database
        _dbContext = new DatabaseContext(":memory:");
        await _dbContext.InitializeDatabaseAsync();

        // Create repository
        _repository = new LearningRepository(
            _dbContext,
            Mock.Of<ILogger<LearningRepository>>());

        // Create mock embedding generator
        var mockEmbeddingGenerator = new Mock<IEmbeddingGenerator>();
        mockEmbeddingGenerator
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string text, CancellationToken _) =>
            {
                // Generate simple deterministic embedding based on text length
                var embedding = new float[384];
                for (int i = 0; i < Math.Min(text.Length, 384); i++)
                {
                    embedding[i] = (float)text[i] / 1000f;
                }
                return embedding;
            });

        // Create service
        _service = new LearningService(
            Mock.Of<ILogger<LearningService>>(),
            _repository,
            mockEmbeddingGenerator.Object);
    }

    public async Task DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }
    }

    [Fact]
    public async Task CreateSelfCorrectionLearning_PersistsToDatabase()
    {
        // Arrange
        var context = new SelfCorrectionTriggerContext
        {
            Title = "Fixed API parameter order",
            Description = "API expects parameters in reverse order",
            FailedIteration = 1,
            FailedOutput = "Error: Invalid parameters",
            FailureReason = "Wrong parameter order",
            SuccessIteration = 2,
            SuccessOutput = "Success: 200 OK",
            SourceAgent = "test-agent",
            SourceTaskId = "task-123",
            Scope = "Agent",
            Tags = "api,parameters"
        };

        // Act
        var learning = await _service!.CreateSelfCorrectionLearningAsync(context);

        // Assert
        Assert.NotNull(learning);
        Assert.NotEmpty(learning.LearningId);

        // Verify it was persisted
        var retrieved = await _repository!.GetByIdAsync(learning.LearningId);
        Assert.NotNull(retrieved);
        Assert.Equal("Fixed API parameter order", retrieved.Title);
        Assert.Equal("SelfCorrection", retrieved.TriggerType);
        Assert.Equal(0.8, retrieved.Confidence);
        Assert.Equal("Agent", retrieved.Scope);
        Assert.Equal("test-agent", retrieved.SourceAgent);
        Assert.Equal("task-123", retrieved.SourceTaskId);
        Assert.Equal("api,parameters", retrieved.Tags);
        Assert.NotNull(retrieved.EmbeddingBlob);
        Assert.Equal(384, retrieved.EmbeddingDimensions);
        Assert.Equal("Active", retrieved.Status);
        Assert.Equal(0, retrieved.TimesApplied);
    }

    [Fact]
    public async Task CreateUserFeedbackLearning_PersistsWithHighConfidence()
    {
        // Arrange
        var context = new UserFeedbackTriggerContext
        {
            Title = "User prefers concise summaries",
            Description = "Use bullet points instead of paragraphs for summaries",
            OriginalOutput = "Long paragraph...",
            CorrectedOutput = "- Point 1\n- Point 2",
            UserExplanation = "Too wordy",
            Scope = "Global",
            CreatedBy = "user"
        };

        // Act
        var learning = await _service!.CreateUserFeedbackLearningAsync(context);

        //Assert
        var retrieved = await _repository!.GetByIdAsync(learning.LearningId);
        Assert.NotNull(retrieved);
        Assert.Equal("UserFeedback", retrieved.TriggerType);
        Assert.Equal(0.95, retrieved.Confidence); // High confidence
        Assert.Equal("Global", retrieved.Scope);
        Assert.Equal("user", retrieved.CreatedBy);
    }

    [Fact]
    public async Task CreateCompilationErrorLearning_StoresCodeDetails()
    {
        // Arrange
        var context = new CompilationErrorTriggerContext
        {
            Title = "Use using statement for FileStream",
            Description = "FileStream must be disposed to avoid file locks",
            ErrorCode = "var fs = new FileStream(path, FileMode.Open);",
            ErrorMessage = "File lock error",
            FixedCode = "using var fs = new FileStream(path, FileMode.Open);",
            Language = "csharp",
            Scope = "Global",
            Tags = "csharp,file-io,disposal"
        };

        // Act
        var learning = await _service!.CreateCompilationErrorLearningAsync(context);

        // Assert
        var retrieved = await _repository!.GetByIdAsync(learning.LearningId);
        Assert.NotNull(retrieved);
        Assert.Equal("CompilationError", retrieved.TriggerType);
        Assert.Equal(0.85, retrieved.Confidence);
        Assert.Equal("csharp,file-io,disposal", retrieved.Tags);
    }

    [Fact]
    public async Task CreateToolFailureLearning_TracksToolInvocationPattern()
    {
        // Arrange
        var context = new ToolFailureTriggerContext
        {
            Title = "REST API requires authentication header",
            Description = "API calls must include Bearer token",
            ToolName = "RestApiTool",
            IncorrectInvocation = "{\"url\": \"...\"}",
            ToolError = "401 Unauthorized",
            CorrectInvocation = "{\"url\": \"...\", \"headers\": {\"Authorization\": \"Bearer token\"}}",
            Scope = "Global",
            Tags = "rest-api,auth"
        };

        // Act
        var learning = await _service!.CreateToolFailureLearningAsync(context);

        // Assert
        var retrieved = await _repository!.GetByIdAsync(learning.LearningId);
        Assert.NotNull(retrieved);
        Assert.Equal("ToolFailure", retrieved.TriggerType);
        Assert.Equal(0.8, retrieved.Confidence);
    }

    [Fact]
    public async Task CreateKnowledgeConflictLearning_RecordsConflictResolution()
    {
        // Arrange
        var context = new KnowledgeConflictTriggerContext
        {
            Title = "API endpoint updated to v2",
            Description = "API migrated from v1 to v2 endpoints",
            PreviousBelief = "Use /api/v1/users",
            NewInformation = "Use /api/v2/users",
            ConflictSource = "API documentation",
            Resolution = "Updated to v2 endpoints",
            Scope = "Project",
            Tags = "api,migration"
        };

        // Act
        var learning = await _service!.CreateKnowledgeConflictLearningAsync(context);

        // Assert
        var retrieved = await _repository!.GetByIdAsync(learning.LearningId);
        Assert.NotNull(retrieved);
        Assert.Equal("KnowledgeConflict", retrieved.TriggerType);
        Assert.Equal(0.6, retrieved.Confidence); // Lower confidence for conflicts
        Assert.Equal("Project", retrieved.Scope);
    }

    [Fact]
    public async Task CreateExplicitLearning_AllowsAgentDefinedLearnings()
    {
        // Arrange
        var context = new ExplicitTriggerContext
        {
            Title = "Batch processing improves performance",
            Description = "Use batch sizes of 100 for optimal throughput",
            AgentReasoning = "Observed 50% performance improvement",
            SupportingEvidence = "Benchmark results: 100 items/sec vs 50 items/sec",
            SourceAgent = "optimization-agent",
            Scope = "Domain",
            Tags = "performance,batch"
        };

        // Act
        var learning = await _service!.CreateExplicitLearningAsync(context);

        // Assert
        var retrieved = await _repository!.GetByIdAsync(learning.LearningId);
        Assert.NotNull(retrieved);
        Assert.Equal("Explicit", retrieved.TriggerType);
        Assert.Equal(0.75, retrieved.Confidence);
        Assert.Equal("Domain", retrieved.Scope);
        Assert.Equal("optimization-agent", retrieved.SourceAgent);
    }

    [Fact]
    public async Task CreateMultipleLearnings_StoresAllIndependently()
    {
        // Arrange
        var context1 = new SelfCorrectionTriggerContext
        {
            Title = "Learning 1",
            Description = "Description 1",
            FailedIteration = 1,
            SuccessIteration = 2
        };

        var context2 = new UserFeedbackTriggerContext
        {
            Title = "Learning 2",
            Description = "Description 2"
        };

        var context3 = new CompilationErrorTriggerContext
        {
            Title = "Learning 3",
            Description = "Description 3",
            Language = "csharp"
        };

        // Act
        var learning1 = await _service!.CreateSelfCorrectionLearningAsync(context1);
        var learning2 = await _service.CreateUserFeedbackLearningAsync(context2);
        var learning3 = await _service.CreateCompilationErrorLearningAsync(context3);

        // Assert
        Assert.NotEqual(learning1.LearningId, learning2.LearningId);
        Assert.NotEqual(learning2.LearningId, learning3.LearningId);

        // Verify all are retrievable
        var retrieved1 = await _repository!.GetByIdAsync(learning1.LearningId);
        var retrieved2 = await _repository.GetByIdAsync(learning2.LearningId);
        var retrieved3 = await _repository.GetByIdAsync(learning3.LearningId);

        Assert.NotNull(retrieved1);
        Assert.NotNull(retrieved2);
        Assert.NotNull(retrieved3);
    }

    [Fact]
    public async Task CreatedLearning_HasProperProvenanceFields()
    {
        // Arrange
        var context = new SelfCorrectionTriggerContext
        {
            Title = "Provenance test",
            Description = "Testing provenance tracking",
            SourceAgent = "agent-456",
            SourceTaskId = "task-789",
            Scope = "Agent",
            CreatedBy = "agent-456",
            FailedIteration = 1,
            SuccessIteration = 2
        };

        // Act
        var learning = await _service!.CreateSelfCorrectionLearningAsync(context);

        // Assert (LM-DATA-001 provenance requirements)
        Assert.Equal("agent-456", learning.SourceAgent);
        Assert.Equal("task-789", learning.SourceTaskId);
        Assert.Equal("agent-456", learning.CreatedBy);
        Assert.True(learning.CreatedAt > 0);
        Assert.True(learning.UpdatedAt > 0);
        Assert.Equal(learning.CreatedAt, learning.UpdatedAt);
    }
}
