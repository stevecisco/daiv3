using Daiv3.ModelExecution;
using Daiv3.ModelExecution.Interfaces;
using Daiv3.ModelExecution.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

#pragma warning disable IDISP025 // Class with no virtual dispose method should be sealed

namespace Daiv3.UnitTests.ModelExecution;

/// <summary>
/// Unit tests for OnlineProviderRouter offline queueing functionality (MQ-REQ-013).
/// </summary>
public class OnlineProviderRouterOfflineQueueingTests : IDisposable
{
    private readonly Mock<ILogger<OnlineProviderRouter>> _mockLogger;
    private readonly Mock<INetworkConnectivityService> _mockConnectivityService;
    private readonly Mock<IModelQueueRepository> _mockQueueRepository;
    private readonly OnlineProviderOptions _options;
    private readonly TaskToModelMappingConfiguration _mappingConfig;

    public OnlineProviderRouterOfflineQueueingTests()
    {
        _mockLogger = new Mock<ILogger<OnlineProviderRouter>>();
        _mockConnectivityService = new Mock<INetworkConnectivityService>();
        _mockQueueRepository = new Mock<IModelQueueRepository>();

        _options = new OnlineProviderOptions
        {
            Providers = new Dictionary<string, ProviderConfig>
            {
                ["openai"] = new()
                {
                    DailyInputTokenLimit = 100000,
                    DailyOutputTokenLimit = 100000,
                    MonthlyInputTokenLimit = 1000000,
                    MonthlyOutputTokenLimit = 1000000
                }
            }
        };

        _mappingConfig = new TaskToModelMappingConfiguration();
    }

    [Fact]
    public async Task ExecuteAsync_WhenOnline_ExecutesNormally()
    {
        // Arrange
        _mockConnectivityService.Setup(x => x.IsOnlineAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var router = new OnlineProviderRouter(
            Options.Create(_options),
            Options.Create(_mappingConfig),
            _mockLogger.Object,
            _mockConnectivityService.Object,
            _mockQueueRepository.Object);

        var request = new ExecutionRequest
        {
            Id = Guid.NewGuid(),
            TaskType = "chat",
            Content = "Test message"
        };

        // Act
        var result = await router.ExecuteAsync(request);

        // Assert
        Assert.Equal(ExecutionStatus.Completed, result.Status);
        Assert.Equal(request.Id, result.RequestId);
        _mockQueueRepository.Verify(
            x => x.SavePendingRequestAsync(It.IsAny<ExecutionRequest>(), It.IsAny<ExecutionPriority>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOffline_QueuesPendingRequest()
    {
        // Arrange
        _mockConnectivityService.Setup(x => x.IsOnlineAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockQueueRepository.Setup(x => x.SavePendingRequestAsync(
            It.IsAny<ExecutionRequest>(),
            It.IsAny<ExecutionPriority>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var router = new OnlineProviderRouter(
            Options.Create(_options),
            Options.Create(_mappingConfig),
            _mockLogger.Object,
            _mockConnectivityService.Object,
            _mockQueueRepository.Object);

        var request = new ExecutionRequest
        {
            Id = Guid.NewGuid(),
            TaskType = "chat",
            Content = "Test message"
        };

        // Act
        var result = await router.ExecuteAsync(request);

        // Assert
        Assert.Equal(ExecutionStatus.Pending, result.Status);
        Assert.Equal(request.Id, result.RequestId);
        Assert.Contains("offline", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        _mockQueueRepository.Verify(
            x => x.SavePendingRequestAsync(
                It.Is<ExecutionRequest>(r => r.Id == request.Id),
                ExecutionPriority.Normal,
                "openai",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutConnectivityService_ExecutesNormally()
    {
        // Arrange - No connectivity service provided
        var router = new OnlineProviderRouter(
            Options.Create(_options),
            Options.Create(_mappingConfig),
            _mockLogger.Object,
            null, // No connectivity service
            null); // No queue repository

        var request = new ExecutionRequest
        {
            Id = Guid.NewGuid(),
            TaskType = "chat",
            Content = "Test message"
        };

        // Act
        var result = await router.ExecuteAsync(request);

        // Assert
        Assert.Equal(ExecutionStatus.Completed, result.Status);
    }

    [Fact]
    public async Task RetryPendingRequestsAsync_WhenOffline_ReturnsZero()
    {
        // Arrange
        _mockConnectivityService.Setup(x => x.IsOnlineAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var router = new OnlineProviderRouter(
            Options.Create(_options),
            Options.Create(_mappingConfig),
            _mockLogger.Object,
            _mockConnectivityService.Object,
            _mockQueueRepository.Object);

        // Act
        var retriedCount = await router.RetryPendingRequestsAsync();

        // Assert
        Assert.Equal(0, retriedCount);
        _mockQueueRepository.Verify(
            x => x.GetPendingRequestsAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RetryPendingRequestsAsync_WithNoPendingRequests_ReturnsZero()
    {
        // Arrange
        _mockConnectivityService.Setup(x => x.IsOnlineAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockQueueRepository.Setup(x => x.GetPendingRequestsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(ExecutionRequest Request, ExecutionPriority Priority, string ModelId)>());

        var router = new OnlineProviderRouter(
            Options.Create(_options),
            Options.Create(_mappingConfig),
            _mockLogger.Object,
            _mockConnectivityService.Object,
            _mockQueueRepository.Object);

        // Act
        var retriedCount = await router.RetryPendingRequestsAsync();

        // Assert
        Assert.Equal(0, retriedCount);
    }

    [Fact]
    public async Task RetryPendingRequestsAsync_WithPendingRequests_RetriesAndUpdatesStatus()
    {
        // Arrange
        _mockConnectivityService.Setup(x => x.IsOnlineAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var pendingRequest = new ExecutionRequest
        {
            Id = Guid.NewGuid(),
            TaskType = "chat",
            Content = "Pending message"
        };

        _mockQueueRepository.Setup(x => x.GetPendingRequestsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(ExecutionRequest Request, ExecutionPriority Priority, string ModelId)>
            {
                (pendingRequest, ExecutionPriority.Normal, "openai")
            });

        _mockQueueRepository.Setup(x => x.UpdateRequestStatusAsync(
            It.IsAny<Guid>(),
            It.IsAny<ExecutionStatus>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var router = new OnlineProviderRouter(
            Options.Create(_options),
            Options.Create(_mappingConfig),
            _mockLogger.Object,
            _mockConnectivityService.Object,
            _mockQueueRepository.Object);

        // Act
        var retriedCount = await router.RetryPendingRequestsAsync();

        // Assert
        Assert.Equal(1, retriedCount);

        // Verify status was updated to Queued (retry started)
        _mockQueueRepository.Verify(
            x => x.UpdateRequestStatusAsync(
                pendingRequest.Id,
                ExecutionStatus.Queued,
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify final status was updated to Completed
        _mockQueueRepository.Verify(
            x => x.UpdateRequestStatusAsync(
                pendingRequest.Id,
                ExecutionStatus.Completed,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RetryPendingRequestsAsync_WithoutServices_ReturnsZero()
    {
        // Arrange - No connectivity or queue service
        var router = new OnlineProviderRouter(
            Options.Create(_options),
            Options.Create(_mappingConfig),
            _mockLogger.Object,
            null,
            null);

        // Act
        var retriedCount = await router.RetryPendingRequestsAsync();

        // Assert
        Assert.Equal(0, retriedCount);
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}
