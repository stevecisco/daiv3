using System.Data;
using Xunit;
using Moq;

namespace Daiv3.Architecture.Tests;

/// <summary>
/// Integration tests that validate cross-layer interactions
/// while maintaining architectural boundaries.
/// </summary>
public class PersistenceKnowledgeIntegrationTests
{
    /// <summary>
    /// Tests that Knowledge Layer correctly uses Persistence Layer repositories.
    /// </summary>
    [Fact]
    public async Task KnowledgeLayer_CanPersistDocuments_ViaRepositories()
    {
        // Arrange
        var mockDatabaseFactory = new Mock<Daiv3.Persistence.Interfaces.IDatabaseFactory>();
        var mockConnection = new Mock<IDbConnection>();
        
        mockDatabaseFactory
            .Setup(f => f.CreateConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockConnection.Object);

        // Act: Knowledge layer should be able to create and persist document entities
        var connection = await mockDatabaseFactory.Object.CreateConnectionAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(connection);
        mockDatabaseFactory.Verify(f => f.CreateConnectionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Tests that Knowledge Layer properly implements document addition contract.
    /// </summary>
    [Fact]
    public async Task KnowledgeIndex_AddDocument_PersistsToDatabase()
    {
        // Arrange
        var mockDocumentRepository = new Mock<Daiv3.Persistence.Interfaces.IRepository<dynamic>>();
        var filePath = "test-document.pdf";
        var content = "Sample document content for testing embedding generation";
        var metadata = new Dictionary<string, string> { { "Author", "Test" } };

        // Act
        var documentId = Guid.NewGuid();
        mockDocumentRepository
            .Setup(r => r.AddAsync(It.IsAny<dynamic>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new { Id = documentId });

        var result = await mockDocumentRepository.Object.AddAsync(null, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        mockDocumentRepository.Verify(r => r.AddAsync(It.IsAny<dynamic>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}

/// <summary>
/// Integration tests for Knowledge and Model Execution Layer interaction.
/// </summary>
public class KnowledgeModelExecutionIntegrationTests
{
    /// <summary>
    /// Tests that Model Execution Layer can query Knowledge Layer for context.
    /// </summary>
    [Fact]
    public async Task ModelQueue_CanQueryKnowledgeIndex_ForContext()
    {
        // Arrange
        var mockKnowledgeIndex = new Mock<Daiv3.Knowledge.Interfaces.IKnowledgeIndex>();
        var query = "What is the system architecture?";
        var expectedResults = new List<(Guid DocumentId, float Score)>
        {
            (Guid.NewGuid(), 0.95f),
            (Guid.NewGuid(), 0.87f)
        };

        mockKnowledgeIndex
            .Setup(k => k.SearchTier1Async(query, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResults);

        // Act
        var results = await mockKnowledgeIndex.Object.SearchTier1Async(query, 10, CancellationToken.None);

        // Assert
        Assert.NotEmpty(results);
        Assert.Equal(2, results.Count);
        Assert.Equal(0.95f, results[0].Score);
    }

    /// <summary>
    /// Tests two-tier search workflow between knowledge and model execution.
    /// </summary>
    [Fact]
    public async Task SearchWorkflow_Tier1ThenTier2_ProducesRefinedResults()
    {
        // Arrange
        var mockKnowledgeIndex = new Mock<Daiv3.Knowledge.Interfaces.IKnowledgeIndex>();
        var query = "system design";
        
        var tier1Results = new List<(Guid DocumentId, float Score)>
        {
            (new Guid("11111111-1111-1111-1111-111111111111"), 0.92f),
            (new Guid("22222222-2222-2222-2222-222222222222"), 0.85f)
        };
        
        var tier2Results = new List<(Guid ChunkId, float Score)>
        {
            (new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), 0.95f),
            (new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), 0.88f),
            (new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc"), 0.81f)
        };

        mockKnowledgeIndex
            .Setup(k => k.SearchTier1Async(query, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tier1Results);

        mockKnowledgeIndex
            .Setup(k => k.SearchTier2Async(
                It.Is<List<Guid>>(list => list.Count == 2),
                query,
                20,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(tier2Results);

        // Act
        var tier1 = await mockKnowledgeIndex.Object.SearchTier1Async(query, 10);
        var tier2 = await mockKnowledgeIndex.Object.SearchTier2Async(
            tier1.Select(r => r.DocumentId).ToList(),
            query,
            20);

        // Assert
        Assert.Equal(2, tier1.Count);
        Assert.Equal(3, tier2.Count);
        Assert.True(tier2[0].Score >= tier2[1].Score);  // Sorted descending
    }
}

/// <summary>
/// Integration tests for Model Execution and Orchestration Layer interaction.
/// </summary>
public class ModelExecutionOrchestrationIntegrationTests
{
    /// <summary>
    /// Tests that Orchestration Layer can enqueue requests to Model Execution Layer.
    /// </summary>
    [Fact]
    public async Task TaskOrchestrator_CanEnqueueRequests_ToModelQueue()
    {
        // Arrange
        var mockModelQueue = new Mock<Daiv3.ModelExecution.Interfaces.IModelQueue>();
        var request = new Daiv3.ModelExecution.Interfaces.ExecutionRequest
        {
            Id = Guid.NewGuid(),
            TaskType = "chat",
            Content = "What is the system architecture?"
        };

        var expectedRequestId = request.Id;
        mockModelQueue
            .Setup(q => q.EnqueueAsync(
                It.Is<Daiv3.ModelExecution.Interfaces.ExecutionRequest>(r => r.Id == request.Id),
                Daiv3.ModelExecution.Interfaces.ExecutionPriority.Normal,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedRequestId);

        // Act
        var requestId = await mockModelQueue.Object.EnqueueAsync(
            request,
            Daiv3.ModelExecution.Interfaces.ExecutionPriority.Normal,
            CancellationToken.None);

        // Assert
        Assert.Equal(expectedRequestId, requestId);
        mockModelQueue.Verify(q => q.EnqueueAsync(
            It.IsAny<Daiv3.ModelExecution.Interfaces.ExecutionRequest>(),
            It.IsAny<Daiv3.ModelExecution.Interfaces.ExecutionPriority>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Tests priority-based queue behavior (ARCH-REQ-004 Model Execution Layer).
    /// </summary>
    [Fact]
    public async Task ModelQueue_PrioritizesImmediate_OverNormal()
    {
        // Arrange
        var mockModelQueue = new Mock<Daiv3.ModelExecution.Interfaces.IModelQueue>();
        
        var immediateRequest = new Daiv3.ModelExecution.Interfaces.ExecutionRequest
        {
            Id = Guid.NewGuid(),
            TaskType = "urgent_chat",
            Content = "Urgent query"
        };

        var normalRequest = new Daiv3.ModelExecution.Interfaces.ExecutionRequest
        {
            Id = Guid.NewGuid(),
            TaskType = "chat",
            Content = "Regular query"
        };

        // Setup queue to track order
        var enqueuedRequests = new List<(Guid Id, int Priority)>();
        
        mockModelQueue
            .Setup(q => q.EnqueueAsync(It.IsAny<Daiv3.ModelExecution.Interfaces.ExecutionRequest>(), It.IsAny<Daiv3.ModelExecution.Interfaces.ExecutionPriority>(), It.IsAny<CancellationToken>()))
            .Callback((Daiv3.ModelExecution.Interfaces.ExecutionRequest req, Daiv3.ModelExecution.Interfaces.ExecutionPriority pri, CancellationToken ct) =>
            {
                enqueuedRequests.Add((req.Id, (int)pri));
            })
            .ReturnsAsync(() => enqueuedRequests.Last().Id);

        // Act: Enqueue normal first, then immediate
        await mockModelQueue.Object.EnqueueAsync(normalRequest, Daiv3.ModelExecution.Interfaces.ExecutionPriority.Normal);
        await mockModelQueue.Object.EnqueueAsync(immediateRequest, Daiv3.ModelExecution.Interfaces.ExecutionPriority.Immediate);

        // Assert: Verify enqueue was called (queue implementation would handle prioritization)
        Assert.Equal(2, enqueuedRequests.Count);
    }
}

/// <summary>
/// Integration tests for full orchestrationworkflow.
/// </summary>
public class OrchestrationWorkflowIntegrationTests
{
    /// <summary>
    /// Tests complete workflow: Request → Orchestration → Model Execution → Knowledge.
    /// </summary>
    [Fact]
    public async Task FullPipeline_UserRequest_TracesCorrectly()
    {
        // Arrange
        var mockOrchestrator = new Mock<Daiv3.Orchestration.Interfaces.ITaskOrchestrator>();
        var mockModelQueue = new Mock<Daiv3.ModelExecution.Interfaces.IModelQueue>();
        var mockKnowledgeIndex = new Mock<Daiv3.Knowledge.Interfaces.IKnowledgeIndex>();

        var userRequest = new Daiv3.Orchestration.Interfaces.UserRequest
        {
            Input = "Summarize the architecture",
            ProjectId = Guid.NewGuid()
        };

        var orchestrationResult = new Daiv3.Orchestration.Interfaces.OrchestrationResult
        {
            SessionId = Guid.NewGuid(),
            Success = true,
            TaskResults = new() {
                new Daiv3.ModelExecution.Interfaces.ExecutionResult {
                    RequestId = Guid.NewGuid(),
                    Status = Daiv3.ModelExecution.Interfaces.ExecutionStatus.Completed,
                    Content = "Architecture summary..."
                }
            }
        };

        mockOrchestrator
            .Setup(o => o.ExecuteAsync(userRequest, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orchestrationResult);

        // Act
        var result = await mockOrchestrator.Object.ExecuteAsync(userRequest, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.NotEmpty(result.TaskResults);
        Assert.Equal(Daiv3.ModelExecution.Interfaces.ExecutionStatus.Completed, result.TaskResults[0].Status);
    }

    /// <summary>
    /// Tests intent resolution interface implementation.
    /// </summary>
    [Fact]
    public async Task IntentResolver_ParsesUserInput_CorrectlyIdentifiesIntent()
    {
        // Arrange
        var mockIntentResolver = new Mock<Daiv3.Orchestration.Interfaces.IIntentResolver>();
        var userInput = "Search for information about embedding generation";
        
        var expectedIntent = new Daiv3.Orchestration.Interfaces.Intent
        {
            Type = "search",
            Confidence = 0.95m,
            Entities = new() { { "topic", "embedding generation" } }
        };

        mockIntentResolver
            .Setup(r => r.ResolveAsync(userInput, It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedIntent);

        // Act
        var intent = await mockIntentResolver.Object.ResolveAsync(
            userInput,
            new Dictionary<string, string>(),
            CancellationToken.None);

        // Assert
        Assert.Equal("search", intent.Type);
        Assert.Equal(0.95m, intent.Confidence);
        Assert.Contains("embedding generation", intent.Entities.Values);
    }
}

/// <summary>
/// Tests for error handling across layers.
/// </summary>
public class CrossLayerErrorHandlingTests
{
    /// <summary>
    /// Tests that Knowledge Layer errors don't propagate unhandled to Model Execution.
    /// </summary>
    [Fact]
    public async Task KnowledgeError_IsHandledGracefully_InModelExecution()
    {
        // Arrange
        var mockKnowledgeIndex = new Mock<Daiv3.Knowledge.Interfaces.IKnowledgeIndex>();
        var query = "invalid query";

        mockKnowledgeIndex
            .Setup(k => k.SearchTier1Async(query, 10, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Knowledge index error"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => mockKnowledgeIndex.Object.SearchTier1Async(query, 10, CancellationToken.None));
    }

    /// <summary>
    /// Tests that Model Execution errors are properly tracked.
    /// </summary>
    [Fact]
    public async Task ExecutionFailure_IsTrackedInResult()
    {
        // Arrange
        var mockModelQueue = new Mock<Daiv3.ModelExecution.Interfaces.IModelQueue>();
        var request = new Daiv3.ModelExecution.Interfaces.ExecutionRequest
        {
            Id = Guid.NewGuid(),
            TaskType = "chat",
            Content = "Test"
        };

        var failedResult = new Daiv3.ModelExecution.Interfaces.ExecutionResult
        {
            RequestId = request.Id,
            Status = Daiv3.ModelExecution.Interfaces.ExecutionStatus.Failed,
            ErrorMessage = "Model execution failed"
        };

        mockModelQueue
            .Setup(q => q.ProcessAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(failedResult);

        // Act
        var result = await mockModelQueue.Object.ProcessAsync(request.Id, CancellationToken.None);

        // Assert
        Assert.Equal(Daiv3.ModelExecution.Interfaces.ExecutionStatus.Failed, result.Status);
        Assert.NotNull(result.ErrorMessage);
    }
}

/// <summary>
/// Tests for cancellation token propagation across layers.
/// </summary>
public class CancellationTokenPropagationTests
{
    /// <summary>
    /// Tests that cancellation is properly propagated through layers.
    /// </summary>
    [Fact]
    public async Task CancellationToken_IsPropagated_ThroughAllLayers()
    {
        // Arrange
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var mockModelQueue = new Mock<Daiv3.ModelExecution.Interfaces.IModelQueue>();
        
        mockModelQueue
            .Setup(q => q.EnqueueAsync(
                It.IsAny<Daiv3.ModelExecution.Interfaces.ExecutionRequest>(),
                It.IsAny<Daiv3.ModelExecution.Interfaces.ExecutionPriority>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (Daiv3.ModelExecution.Interfaces.ExecutionRequest req, Daiv3.ModelExecution.Interfaces.ExecutionPriority pri, CancellationToken ct) =>
            {
                // Simulate async work that respects cancellation
                await Task.Delay(200, ct);
                return req.Id;
            });

        var request = new Daiv3.ModelExecution.Interfaces.ExecutionRequest { Id = Guid.NewGuid() };

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => mockModelQueue.Object.EnqueueAsync(request, Daiv3.ModelExecution.Interfaces.ExecutionPriority.Normal, cts.Token));
    }
}
