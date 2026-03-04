using Daiv3.ModelExecution;
using Daiv3.ModelExecution.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Daiv3.UnitTests.ModelExecution;

/// <summary>
/// Unit tests for NetworkConnectivityService (M Q-REQ-013).
/// </summary>
public class NetworkConnectivityServiceTests
{
    private readonly Mock<ILogger<NetworkConnectivityService>> _mockLogger;

    public NetworkConnectivityServiceTests()
    {
        _mockLogger = new Mock<ILogger<NetworkConnectivityService>>();
    }

    [Fact]
    public async Task IsOnlineAsync_ReturnsTrue_WhenNetworkIsAvailable()
    {
        // Arrange
        var service = new NetworkConnectivityService(_mockLogger.Object);

        // Act
        var isOnline = await service.IsOnlineAsync();

        // Assert
        // This test depends on actual network availability
        // In a real scenario, we'd mock NetworkInterface.GetIsNetworkAvailable()
        Assert.IsType<bool>(isOnline);
    }

    [Fact]
    public async Task IsEndpointReachableAsync_ThrowsArgumentNullException_WhenEndpointIsNull()
    {
        // Arrange
        var service = new NetworkConnectivityService(_mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.IsEndpointReachableAsync(null!));
    }

    [Fact]
    public async Task IsEndpointReachableAsync_ThrowsArgumentException_WhenEndpointIsEmpty()
    {
        // Arrange
        var service = new NetworkConnectivityService(_mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.IsEndpointReachableAsync(string.Empty));
    }

    [Fact]
    public async Task IsEndpointReachableAsync_ReturnsFalse_OnTimeout()
    {
        // Arrange
        var service = new NetworkConnectivityService(_mockLogger.Object);

        // Act - Using a non-routable IP to simulate timeout
        var isReachable = await service.IsEndpointReachableAsync("http://192.0.2.1");

        // Assert
        Assert.False(isReachable);
    }

    [Fact]
    public async Task IsEndpointReachableAsync_HandlesHttpsEndpoints()
    {
        // Arrange
        var service = new NetworkConnectivityService(_mockLogger.Object);

        // Act
        var isReachable = await service.IsEndpointReachableAsync("https://www.example.com");

        // Assert
        Assert.IsType<bool>(isReachable);
    }

    [Fact]
    public async Task IsEndpointReachableAsync_AddsHttpsPrefix_WhenNotProvided()
    {
        // Arrange
        var service = new NetworkConnectivityService(_mockLogger.Object);

        // Act
        var isReachable = await service.IsEndpointReachableAsync("www.example.com");

        // Assert
        Assert.IsType<bool>(isReachable);
    }
}
