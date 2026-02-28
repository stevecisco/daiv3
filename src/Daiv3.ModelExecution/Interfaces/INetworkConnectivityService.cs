namespace Daiv3.ModelExecution.Interfaces;

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
    /// Checks if a specific endpoint is reachable.
    /// </summary>
    /// <param name="endpoint">The endpoint to check (e.g., "api.openai.com")</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if reachable, false otherwise</returns>
    Task<bool> IsEndpointReachableAsync(string endpoint, CancellationToken ct = default);
}
