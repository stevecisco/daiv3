namespace Daiv3.ModelExecution.Interfaces;

/// <summary>
/// Represents different levels of network connectivity.
/// </summary>
/// <remarks>
/// Used by ES-ACC-001 to automatically adjust system behavior based on connectivity:
/// - None: All operations local-only, queue online requests
/// - LocalOnly: Network active but no internet access (local network features only)
/// - Internet: Full internet connectivity available
/// </remarks>
public enum ConnectivityLevel
{
    /// <summary>
    /// No network connectivity (airplane mode, no adapters, etc.)
    /// </summary>
    None = 0,

    /// <summary>
    /// Local network connected (WiFi/Ethernet) but no internet access
    /// </summary>
    LocalOnly = 1,

    /// <summary>
    /// Full internet connectivity confirmed
    /// </summary>
    Internet = 2
}

/// <summary>
/// Service for checking network connectivity status.
/// </summary>
public interface INetworkConnectivityService
{
    /// <summary>
    /// Checks if the system is currently online.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if online, false if offline</returns>
    Task<bool> IsOnlineAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the current connectivity level with granular detection.
    /// </summary>
    /// <remarks>
    /// Implements ES-ACC-001: Detects multiple levels of internet connectivity.
    /// Tests for:
    /// - None: No network interfaces active
    /// - LocalOnly: Network connected but internet unreachable
    /// - Internet: Full internet access confirmed via well-known endpoints
    /// </remarks>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Current connectivity level</returns>
    Task<ConnectivityLevel> GetConnectivityLevelAsync(CancellationToken ct = default);

    /// <summary>
    /// Checks if a specific endpoint is reachable.
    /// </summary>
    /// <param name="endpoint">The endpoint to check (e.g., "api.openai.com")</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if reachable, false otherwise</returns>
    Task<bool> IsEndpointReachableAsync(string endpoint, CancellationToken ct = default);
}
