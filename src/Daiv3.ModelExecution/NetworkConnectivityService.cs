using System.Net.NetworkInformation;
using Daiv3.ModelExecution.Interfaces;
using Microsoft.Extensions.Logging;

namespace Daiv3.ModelExecution;

/// <summary>
/// Default implementation of network connectivity checks.
/// </summary>
/// <remarks>
/// Implements MQ-REQ-013: Detects offline status for queueing online tasks.
/// Uses NetworkInterface to check connectivity and DNS resolution for endpoint checks.
/// </remarks>
public class NetworkConnectivityService : INetworkConnectivityService, IDisposable
{
    private readonly ILogger<NetworkConnectivityService> _logger;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public NetworkConnectivityService(
        ILogger<NetworkConnectivityService> logger,
        IHttpClientFactory? httpClientFactory = null)
    {
        _logger = logger;
        if (httpClientFactory != null)
        {
            _httpClient = httpClientFactory.CreateClient("ConnectivityCheck");
            _ownsHttpClient = false;
        }
        else
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            _ownsHttpClient = true;
        }
    }

    public Task<bool> IsOnlineAsync(CancellationToken ct = default)
    {
        try
        {
            // Check if any network interface is up and not loopback
            var isOnline = NetworkInterface.GetIsNetworkAvailable() &&
                           NetworkInterface.GetAllNetworkInterfaces()
                               .Any(ni => ni.OperationalStatus == OperationalStatus.Up &&
                                         ni.NetworkInterfaceType != NetworkInterfaceType.Loopback);

            _logger.LogDebug("Network availability check: {IsOnline}", isOnline);
            return Task.FromResult(isOnline);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking network availability, assuming offline");
            return Task.FromResult(false);
        }
    }

    public async Task<bool> IsEndpointReachableAsync(string endpoint, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);

        try
        {
            // First check basic network availability
            if (!await IsOnlineAsync(ct))
            {
                return false;
            }

            // Try to resolve DNS and make a HEAD request
            // Use HTTPS by default
            var url = endpoint.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? endpoint
                : $"https://{endpoint}";

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            using var response = await _httpClient.SendAsync(request, cts.Token);

            var isReachable = response.IsSuccessStatusCode ||
                            response.StatusCode == System.Net.HttpStatusCode.Unauthorized || // API requires auth
                            response.StatusCode == System.Net.HttpStatusCode.Forbidden;     // API exists but access denied

            _logger.LogDebug(
                "Endpoint reachability check for {Endpoint}: {IsReachable} (Status: {StatusCode})",
                endpoint, isReachable, response.StatusCode);

            return isReachable;
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Endpoint check timed out for {Endpoint}, assuming unreachable", endpoint);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking endpoint reachability for {Endpoint}, assuming unreachable", endpoint);
            return false;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing && _ownsHttpClient)
        {
            _httpClient?.Dispose();
        }
    }
}
