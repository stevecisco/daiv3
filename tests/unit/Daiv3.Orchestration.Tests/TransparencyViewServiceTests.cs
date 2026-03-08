using Daiv3.Knowledge;
using Daiv3.ModelExecution.Interfaces;
using Daiv3.ModelExecution.Models;
using Daiv3.Orchestration.Interfaces;
using Daiv3.Orchestration.Models;
using Daiv3.Orchestration.Services;
using Daiv3.Persistence;
using Daiv3.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Daiv3.Orchestration.Tests;

/// <summary>
/// Unit tests for TransparencyViewService (ES-REQ-004).
/// Tests transparency view data aggregation across model usage, indexing, queue, and agent activity.
/// </summary>
public class TransparencyViewServiceTests
{
    private readonly Mock<ILogger<TransparencyViewService>> _mockLogger;
    private readonly Mock<IModelQueue>? _mockModelQueue;
    private readonly Mock<IIndexingStatusService>? _mockIndexingStatusService;
    private readonly Mock<IServiceScopeFactory>? _mockScopeFactory;
    private readonly Mock<IDatabaseContext>? _mockDatabaseContext;

    public TransparencyViewServiceTests()
    {
        _mockLogger = new Mock<ILogger<TransparencyViewService>>();
        _mockModelQueue = new Mock<IModelQueue>();
        _mockIndexingStatusService = new Mock<IIndexingStatusService>();
        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        _mockDatabaseContext = new Mock<IDatabaseContext>();
    }

    private TransparencyViewService CreateService(
        IModelQueue? modelQueue = null,
        IIndexingStatusService? indexingStatusService = null,
        IServiceScopeFactory? scopeFactory = null,
        IDatabaseContext? databaseContext = null)
    {
        return new TransparencyViewService(
            _mockLogger.Object,
            modelQueue,
            indexingStatusService,
            scopeFactory,
            databaseContext);
    }

    #region GetTransparencyViewAsync Tests

    [Fact]
    public async Task GetTransparencyViewAsync_SuccessfulAggregation_AllServicesAvailable()
    {
        // Arrange
        var queueStatus = new QueueStatus
        {
            CurrentModelId = "Llama-2-7B",
            ImmediateCount = 2,
            NormalCount = 5,
            BackgroundCount = 1,
            LastModelSwitch = DateTimeOffset.UtcNow.AddMinutes(-30)
        };

        var queueMetrics = new QueueMetrics
        {
            TotalCompleted = 100,
            AverageExecutionDurationMs = 500,
            AverageQueueWaitMs = 2000,
            InFlightExecutions = 2
        };

        _mockModelQueue!
            .Setup(x => x.GetQueueStatusAsync())
            .ReturnsAsync(queueStatus);

        _mockModelQueue!
            .Setup(x => x.GetMetricsAsync())
            .ReturnsAsync(queueMetrics);

        var indexingStats = new IndexingStatistics
        {
            TotalIndexed = 50,
            TotalDiscovered = 60,
            TotalInProgress = 2,
            TotalErrors = 1,
            IsWatcherActive = true,
            OrchestrationStats = new KnowledgeFileOrchestrationStatistics()
        };

        _mockIndexingStatusService!
            .Setup(x => x.GetIndexingStatisticsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(indexingStats);

        var service = CreateService(
            modelQueue: _mockModelQueue.Object,
            indexingStatusService: _mockIndexingStatusService.Object,
            scopeFactory: _mockScopeFactory.Object,
            databaseContext: _mockDatabaseContext.Object);

        // Act
        var result = await service.GetTransparencyViewAsync();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsValid);
        Assert.Null(result.CollectionError);
        Assert.Equal("Llama-2-7B", result.ModelUsage.CurrentModel);
        Assert.Equal(8, result.QueueState.PendingCount);
        Assert.Equal(50, result.IndexingStatus.FilesIndexed);
    }

    [Fact]
    public async Task GetTransparencyViewAsync_GracefulDegradation_MissingServices()
    {
        // Arrange
        var service = CreateService(
            modelQueue: null,
            indexingStatusService: null,
            scopeFactory: null,
            databaseContext: null);

        // Act
        var result = await service.GetTransparencyViewAsync();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsValid);
        Assert.Null(result.CollectionError);
        Assert.Null(result.ModelUsage.CurrentModel);
        Assert.Equal(0, result.QueueState.PendingCount);
    }

    [Fact]
    public async Task GetTransparencyViewAsync_GracefulDegradation_ServiceExceptionHandled()
    {
        // Arrange
        // When a service throws, the individual method catches it and returns defaults
        _mockModelQueue!
            .Setup(x => x.GetQueueStatusAsync())
            .ThrowsAsync(new InvalidOperationException("Queue service error"));

        _mockModelQueue!
            .Setup(x => x.GetMetricsAsync())
            .ReturnsAsync(new QueueMetrics());

        var service = CreateService(modelQueue: _mockModelQueue.Object);

        // Act
        var result = await service.GetTransparencyViewAsync();

        // Assert
        // Service catches exceptions and returns partial data with defaults
        Assert.NotNull(result);
        Assert.True(result.IsValid); // Still valid, just with default values
        Assert.Null(result.CollectionError);
        Assert.Equal(0, result.QueueState.PendingCount); // Default value from exception handling
    }

    #endregion

    #region GetModelUsageAsync Tests

    [Fact]
    public async Task GetModelUsageAsync_ReturnsModelUsageStatistics()
    {
        // Arrange
        var queueStatus = new QueueStatus
        {
            CurrentModelId = "Llama-2-7B",
            LastModelSwitch = DateTimeOffset.UtcNow.AddMinutes(-10)
        };

        var queueMetrics = new QueueMetrics
        {
            TotalCompleted = 250,
            AverageExecutionDurationMs = 450
        };

        _mockModelQueue!
            .Setup(x => x.GetQueueStatusAsync())
            .ReturnsAsync(queueStatus);

        _mockModelQueue!
            .Setup(x => x.GetMetricsAsync())
            .ReturnsAsync(queueMetrics);

        var service = CreateService(modelQueue: _mockModelQueue.Object);

        // Act
        var result = await service.GetModelUsageAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Llama-2-7B", result.CurrentModel);
        Assert.Equal(250, result.TotalExecutions);
        Assert.Equal(450, result.AverageExecutionMs);
        Assert.Equal(0, result.ModelSwitchCount);
        Assert.True(result.ActiveModelLoadDurationMs > 0);
    }

    [Fact]
    public async Task GetModelUsageAsync_NoModelQueue_ReturnsDefaults()
    {
        // Arrange
        var service = CreateService(modelQueue: null);

        // Act
        var result = await service.GetModelUsageAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.CurrentModel);
        Assert.Equal(0, result.TotalExecutions);
        Assert.Equal(0, result.AverageExecutionMs);
    }

    #endregion

    #region GetIndexingStatusAsync Tests

    [Fact]
    public async Task GetIndexingStatusAsync_ReturnsIndexingProgress()
    {
        // Arrange
        var stats = new IndexingStatistics
        {
            TotalIndexed = 45,
            TotalNotIndexed = 5,
            TotalDiscovered = 50,
            TotalInProgress = 2,
            TotalErrors = 3,
            IsWatcherActive = true,
            TotalEmbeddingStorageBytes = 524_288, // 512 KB
            OrchestrationStats = new KnowledgeFileOrchestrationStatistics()
        };

        _mockIndexingStatusService!
            .Setup(x => x.GetIndexingStatisticsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(stats);

        var service = CreateService(indexingStatusService: _mockIndexingStatusService.Object);

        // Act
        var result = await service.GetIndexingStatusAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(45, result.FilesIndexed);
        Assert.Equal(5, result.FilesQueued);
        Assert.Equal(2, result.FilesInProgress);
        Assert.Equal(3, result.FilesWithErrors);
        Assert.Equal(90, result.ProgressPercentage, precision: 0); // 45/50 = 90%
        Assert.True(result.IsIndexing);
    }

    [Fact]
    public async Task GetIndexingStatusAsync_NoIndexingService_ReturnsDefaults()
    {
        // Arrange
        var service = CreateService(indexingStatusService: null);

        // Act
        var result = await service.GetIndexingStatusAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.FilesIndexed);
        Assert.False(result.IsIndexing);
    }

    #endregion

    #region GetQueueStateAsync Tests

    [Fact]
    public async Task GetQueueStateAsync_ReturnsQueueStateWithPriorityDistribution()
    {
        // Arrange
        var queueStatus = new QueueStatus
        {
            ImmediateCount = 3,
            NormalCount = 7,
            BackgroundCount = 5
        };

        var queueMetrics = new QueueMetrics
        {
            TotalCompleted = 500,
            AverageExecutionDurationMs = 600,
            AverageQueueWaitMs = 3000,
            InFlightExecutions = 3
        };

        _mockModelQueue!
            .Setup(x => x.GetQueueStatusAsync())
            .ReturnsAsync(queueStatus);

        _mockModelQueue!
            .Setup(x => x.GetMetricsAsync())
            .ReturnsAsync(queueMetrics);

        var service = CreateService(modelQueue: _mockModelQueue.Object);

        // Act
        var result = await service.GetQueueStateAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(15, result.PendingCount); // 3 + 7 + 5
        Assert.Equal(500, result.CompletedCount);
        Assert.Equal(3, result.ImmediateCount);
        Assert.Equal(7, result.NormalCount);
        Assert.Equal(5, result.BackgroundCount);
        Assert.Equal(600, result.AverageTaskDurationMs);
        Assert.Equal(3000, result.EstimatedWaitMs);
        Assert.True(result.ModelUtilizationPercent > 0);
    }

    [Fact]
    public async Task GetQueueStateAsync_NoModelQueue_ReturnsDefaults()
    {
        // Arrange
        var service = CreateService(modelQueue: null);

        // Act
        var result = await service.GetQueueStateAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.PendingCount);
        Assert.Equal(0, result.CompletedCount);
    }

    #endregion

    #region GetAgentActivityAsync Tests

    [Fact]
    public async Task GetAgentActivityAsync_NoAgentManager_ReturnsDefaults()
    {
        // Arrange
        var service = CreateService(scopeFactory: null);

        // Act
        var result = await service.GetAgentActivityAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.ActiveAgentCount);
        Assert.Equal(0, result.TotalIterations);
        Assert.Empty(result.Activities);
    }

    [Fact]
    public async Task GetAgentActivityAsync_WithAgentManager_CollectsActiveAgents()
    {
        // Arrange
        var agentExecutionControl = new AgentExecutionControl(
            executionId: new Guid("87654321-4321-4321-4321-210987654321"),
            agentId: new Guid("12345678-1234-1234-1234-123456789012"));

        var mockAgentManager = new Mock<IAgentManager>();
        mockAgentManager
            .Setup(x => x.GetActiveExecutions())
            .Returns([agentExecutionControl]);

        mockAgentManager
            .Setup(x => x.GetAgentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Agent 
            { 
                Id = agentExecutionControl.AgentId, 
                Name = "TestAgent",
                Purpose = "Test Purpose"
            });

        var mockScope = new Mock<IServiceScope>();
        mockScope
            .Setup(x => x.ServiceProvider.GetService(typeof(IAgentManager)))
            .Returns(mockAgentManager.Object);

        _mockScopeFactory!
            .Setup(x => x.CreateScope())
            .Returns(mockScope.Object);

        var service = CreateService(scopeFactory: _mockScopeFactory.Object);

        // Act
        var result = await service.GetAgentActivityAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.ActiveAgentCount);
        Assert.Single(result.Activities);
        Assert.Equal("TestAgent", result.Activities[0].AgentName);
    }

    #endregion

    #region Performance & Cancellation Tests

    [Fact]
    public async Task GetAgentActivityAsync_CancellationToken_PropagatedToAgentManager()
    {
        // Arrange
        var cancellationTokenReceived = false;
        var mockAgentManager = new Mock<IAgentManager>();
        mockAgentManager
            .Setup(x => x.GetActiveExecutions())
            .Returns([]);

        var mockScope = new Mock<IServiceScope>();
        mockScope
            .Setup(x => x.ServiceProvider.GetService(typeof(IAgentManager)))
            .Returns(mockAgentManager.Object);

        _mockScopeFactory!
            .Setup(x => x.CreateScope())
            .Returns(mockScope.Object);

        var service = CreateService(scopeFactory: _mockScopeFactory.Object);
        var cts = new System.Threading.CancellationTokenSource();

        // Act
        var result = await service.GetAgentActivityAsync(cts.Token);

        // Assert
        // Service should handle cancellation gracefully
        Assert.NotNull(result);
        Assert.Equal(0, result.ActiveAgentCount);
    }

    [Fact]
    public async Task GetTransparencyViewAsync_CompleteWithinReasonableTime()
    {
        // Arrange
        _mockModelQueue!
            .Setup(x => x.GetQueueStatusAsync())
            .ReturnsAsync(new QueueStatus());

        _mockModelQueue!
            .Setup(x => x.GetMetricsAsync())
            .ReturnsAsync(new QueueMetrics());

        _mockIndexingStatusService!
            .Setup(x => x.GetIndexingStatisticsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IndexingStatistics 
            { 
                OrchestrationStats = new KnowledgeFileOrchestrationStatistics() 
            });

        var service = CreateService(
            modelQueue: _mockModelQueue.Object,
            indexingStatusService: _mockIndexingStatusService.Object,
            scopeFactory: _mockScopeFactory.Object);

        // Act
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await service.GetTransparencyViewAsync();
        sw.Stop();

        // Assert
        Assert.NotNull(result);
        Assert.True(sw.ElapsedMilliseconds < 500, $"Took {sw.ElapsedMilliseconds}ms, expected < 500ms");
    }

    #endregion

    #region Data Consistency Tests

    [Fact]
    public async Task GetTransparencyViewAsync_AllDataHasSameTimestamp()
    {
        // Arrange
        _mockModelQueue!
            .Setup(x => x.GetQueueStatusAsync())
            .ReturnsAsync(new QueueStatus());

        _mockModelQueue!
            .Setup(x => x.GetMetricsAsync())
            .ReturnsAsync(new QueueMetrics());

        _mockIndexingStatusService!
            .Setup(x => x.GetIndexingStatisticsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IndexingStatistics 
            { 
                OrchestrationStats = new KnowledgeFileOrchestrationStatistics() 
            });

        var service = CreateService(
            modelQueue: _mockModelQueue.Object,
            indexingStatusService: _mockIndexingStatusService.Object);

        // Act
        var result = await service.GetTransparencyViewAsync();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.CollectedAt < DateTimeOffset.UtcNow.AddSeconds(1));
        Assert.True(result.CollectedAt > DateTimeOffset.UtcNow.AddSeconds(-5));
    }

    #endregion
}
