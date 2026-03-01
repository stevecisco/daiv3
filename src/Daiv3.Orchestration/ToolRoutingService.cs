using Daiv3.Mcp.Integration;
using Daiv3.Orchestration.Interfaces;
using Daiv3.Orchestration.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Daiv3.Orchestration;

/// <summary>
/// Default implementation of <see cref="IToolInvoker"/> providing intelligent tool routing.
/// </summary>
/// <remarks>
/// This implementation routes tool invocations to the appropriate backend based on the
/// tool's registered backend type. It prioritizes efficiency:
/// <list type="bullet">
/// <item><description>Direct C# tools are invoked through registered service interfaces</description></item>
/// <item><description>CLI tools are executed via process invocation</description></item>
/// <item><description>RestAPI tools are invoked via HTTP REST API calls</description></item>
/// <item><description>MCP tools are routed to the MCP tool provider</description></item>
/// </list>
/// 
/// <para>
/// The routing service tracks context token overhead for MCP and REST API tools and logs all routing
/// decisions for observability. It handles tool unavailability and backend failures gracefully.
/// </para>
/// </remarks>
public sealed class ToolRoutingService : IToolInvoker
{
    private readonly ILogger<ToolRoutingService> _logger;
    private readonly IToolRegistry _toolRegistry;
    private readonly IMcpToolProvider _mcpToolProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private int _invocationCounter = 0;

    public ToolRoutingService(
        ILogger<ToolRoutingService> logger,
        IToolRegistry toolRegistry,
        IMcpToolProvider mcpToolProvider,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _mcpToolProvider = mcpToolProvider ?? throw new ArgumentNullException(nameof(mcpToolProvider));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    public Task<ToolInvocationResult> InvokeToolAsync(
        string toolId,
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken = default)
    {
        return InvokeToolAsync(toolId, parameters, new ToolInvocationPreferences(), cancellationToken);
    }

    public async Task<ToolInvocationResult> InvokeToolAsync(
        string toolId,
        Dictionary<string, object> parameters,
        ToolInvocationPreferences preferences,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolId);
        ArgumentNullException.ThrowIfNull(preferences);

        var invocationId = Interlocked.Increment(ref _invocationCounter);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Look up tool in registry
            var tool = await _toolRegistry.GetToolAsync(toolId, cancellationToken);
            if (tool == null)
            {
                _logger.LogWarning("Tool '{ToolId}' not found in registry (invocation #{InvocationId})",
                    toolId, invocationId);

                return new ToolInvocationResult
                {
                    Success = false,
                    ErrorMessage = $"Tool '{toolId}' not found",
                    ErrorCode = "TOOL_NOT_FOUND",
                    BackendUsed = ToolBackendType.Direct, // Dummy value
                    DurationMs = stopwatch.ElapsedMilliseconds
                };
            }

            // Check availability
            if (!tool.IsAvailable)
            {
                _logger.LogWarning("Tool '{ToolId}' is not currently available (invocation #{InvocationId})",
                    toolId, invocationId);

                return new ToolInvocationResult
                {
                    Success = false,
                    ErrorMessage = $"Tool '{toolId}' is not currently available",
                    ErrorCode = "TOOL_UNAVAILABLE",
                    BackendUsed = tool.Backend,
                    DurationMs = stopwatch.ElapsedMilliseconds
                };
            }

            _logger.LogInformation("Routing tool '{ToolId}' invocation #{InvocationId} to {Backend} backend (source: {Source})",
                toolId, invocationId, tool.Backend, tool.Source);

            // Route to appropriate backend
            ToolInvocationResult result = tool.Backend switch
            {
                ToolBackendType.Direct => await InvokeDirectToolAsync(tool, parameters, invocationId, cancellationToken),
                ToolBackendType.CLI => await InvokeCliToolAsync(tool, parameters, invocationId, cancellationToken),
                ToolBackendType.RestAPI => await InvokeRestApiToolAsync(tool, parameters, invocationId, cancellationToken),
                ToolBackendType.MCP => await InvokeMcpToolAsync(tool, parameters, invocationId, cancellationToken),
                _ => throw new NotSupportedException($"Backend type {tool.Backend} is not supported")
            };

            stopwatch.Stop();
            result.DurationMs = stopwatch.ElapsedMilliseconds;

            // Check context token threshold
            if (result.ContextTokenCost > preferences.MaxContextTokenCost)
            {
                _logger.LogWarning("Tool invocation '{ToolId}' exceeded context token threshold: " +
                    "{ActualCost} > {Threshold} tokens (invocation #{InvocationId})",
                    toolId, result.ContextTokenCost, preferences.MaxContextTokenCost, invocationId);
            }

            _logger.LogInformation("Tool '{ToolId}' invocation #{InvocationId} completed: " +
                "success={Success}, backend={Backend}, duration={DurationMs}ms, tokens={TokenCost}",
                toolId, invocationId, result.Success, result.BackendUsed, result.DurationMs, result.ContextTokenCost);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Unexpected error invoking tool '{ToolId}' (invocation #{InvocationId}): {ErrorMessage}",
                toolId, invocationId, ex.Message);

            return new ToolInvocationResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                ErrorCode = "INVOCATION_EXCEPTION",
                BackendUsed = ToolBackendType.Direct, // Dummy value
                DurationMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    // Private helper methods for backend-specific invocation

    private async Task<ToolInvocationResult> InvokeDirectToolAsync(
        Models.ToolDescriptor tool,
        Dictionary<string, object> parameters,
        int invocationId,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Direct tool invocation for '{ToolId}' (invocation #{InvocationId}) - " +
            "implementation pending service registry integration",
            tool.ToolId, invocationId);

        // TODO: Implement direct C# tool invocation via service registry/factory pattern
        // This will be implemented when direct tools are registered (e.g., knowledge search, scheduling)
        // For now, return a placeholder indicating the feature is under development

        await Task.CompletedTask; // Suppress async warning

        return new ToolInvocationResult
        {
            Success = false,
            ErrorMessage = "Direct tool invocation not yet implemented - pending service registry integration",
            ErrorCode = "NOT_IMPLEMENTED",
            BackendUsed = ToolBackendType.Direct,
            ContextTokenCost = 0 // Direct tools have zero context overhead
        };
    }

    private async Task<ToolInvocationResult> InvokeCliToolAsync(
        Models.ToolDescriptor tool,
        Dictionary<string, object> parameters,
        int invocationId,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("CLI tool invocation for '{ToolId}' (invocation #{InvocationId}) - " +
            "implementation pending CLI executor integration",
            tool.ToolId, invocationId);

        // TODO: Implement CLI tool invocation via process execution
        // This will invoke the tool.Source as a command with marshalled parameters
        // For now, return a placeholder indicating the feature is under development

        await Task.CompletedTask; // Suppress async warning

        return new ToolInvocationResult
        {
            Success = false,
            ErrorMessage = "CLI tool invocation not yet implemented - pending CLI executor integration",
            ErrorCode = "NOT_IMPLEMENTED",
            BackendUsed = ToolBackendType.CLI,
            ContextTokenCost = 5 // CLI tools have minimal context overhead
        };
    }

    private async Task<ToolInvocationResult> InvokeRestApiToolAsync(
        Models.ToolDescriptor tool,
        Dictionary<string, object> parameters,
        int invocationId,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("REST API tool invocation for '{ToolId}' to '{BaseUrl}' (invocation #{InvocationId})",
            tool.ToolId, tool.Source, invocationId);

        try
        {
            // Parse REST API configuration from metadata
            var config = RestApiToolConfiguration.FromMetadata(tool.Metadata);
            if (config == null)
            {
                _logger.LogError("REST API configuration missing for tool '{ToolId}' (invocation #{InvocationId})",
                    tool.ToolId, invocationId);

                return new ToolInvocationResult
                {
                    Success = false,
                    ErrorMessage = "REST API configuration is missing or invalid",
                    ErrorCode = "INVALID_CONFIGURATION",
                    BackendUsed = ToolBackendType.RestAPI,
                    ContextTokenCost = 0
                };
            }

            // Build URL from template and parameters
            var url = BuildUrl(tool.Source, config.UrlTemplate, parameters);
            if (url == null)
            {
                return new ToolInvocationResult
                {
                    Success = false,
                    ErrorMessage = "Failed to construct URL from template and parameters",
                    ErrorCode = "URL_CONSTRUCTION_FAILED",
                    BackendUsed = ToolBackendType.RestAPI,
                    ContextTokenCost = 0
                };
            }

            // Create HTTP client with timeout
            var httpClient = _httpClientFactory.CreateClient("RestApiTool");
            httpClient.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);

            _logger.LogDebug("Preparing to send {Method} request to {Url} (invocation #{InvocationId})",
                config.HttpMethod, url, invocationId);

            // Execute request with retry
            HttpResponseMessage? response = null;
            Exception? lastException = null;
            var attemptCount = config.RetryCount + 1;
            HttpRequestMessage? lastRequest = null;

            for (int attempt = 0; attempt < attemptCount; attempt++)
            {
                try
                {
                    // Create a new request for each attempt (HttpRequestMessage can only be sent once)
                    var request = BuildHttpRequest(url, config.HttpMethod, config, parameters);
                    lastRequest = request;
                    
                    response = await httpClient.SendAsync(request, cancellationToken);
                    break; // Success, exit retry loop
                }
                catch (Exception ex) when (attempt < config.RetryCount)
                {
                    lastException = ex;
                    _logger.LogWarning(ex, "REST API request failed (attempt {Attempt}/{Total}), retrying...",
                        attempt + 1, attemptCount);
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), cancellationToken); // Exponential backoff
                }
            }

            if (response == null)
            {
                throw lastException ?? new HttpRequestException("Request failed without exception");
            }

            // Read response
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            // Check if status code is expected
            var isSuccess = IsSuccessStatusCode(response.StatusCode, config.ExpectedStatusCodes);

            if (isSuccess)
            {
                _logger.LogInformation("REST API tool '{ToolId}' completed successfully: {StatusCode} (invocation #{InvocationId})",
                    tool.ToolId, (int)response.StatusCode, invocationId);
            }
            else
            {
                _logger.LogWarning("REST API tool '{ToolId}' returned non-success status: {StatusCode} (invocation #{InvocationId})",
                    tool.ToolId, (int)response.StatusCode, invocationId);
            }

            // Estimate context token cost (headers + body)
            var contextTokenCost = EstimateRestApiTokenCost(lastRequest!, responseBody);

            return new ToolInvocationResult
            {
                Success = isSuccess,
                Result = responseBody,
                ErrorMessage = isSuccess ? null : $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}",
                ErrorCode = isSuccess ? null : $"HTTP_{(int)response.StatusCode}",
                BackendUsed = ToolBackendType.RestAPI,
                ContextTokenCost = contextTokenCost,
                Metadata = new Dictionary<string, string>
                {
                    ["HttpStatusCode"] = ((int)response.StatusCode).ToString(),
                    ["HttpReasonPhrase"] = response.ReasonPhrase ?? string.Empty
                }
            };
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "REST API tool '{ToolId}' timed out (invocation #{InvocationId})",
                tool.ToolId, invocationId);

            return new ToolInvocationResult
            {
                Success = false,
                ErrorMessage = "Request timed out",
                ErrorCode = "TIMEOUT",
                BackendUsed = ToolBackendType.RestAPI,
                ContextTokenCost = tool.EstimatedTokenCost
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "REST API tool '{ToolId}' request failed (invocation #{InvocationId}): {ErrorMessage}",
                tool.ToolId, invocationId, ex.Message);

            return new ToolInvocationResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                ErrorCode = "HTTP_REQUEST_FAILED",
                BackendUsed = ToolBackendType.RestAPI,
                ContextTokenCost = tool.EstimatedTokenCost
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error invoking REST API tool '{ToolId}' (invocation #{InvocationId}): {ErrorMessage}",
                tool.ToolId, invocationId, ex.Message);

            return new ToolInvocationResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                ErrorCode = "RESTAPI_INVOCATION_FAILED",
                BackendUsed = ToolBackendType.RestAPI,
                ContextTokenCost = tool.EstimatedTokenCost
            };
        }
    }

    private async Task<ToolInvocationResult> InvokeMcpToolAsync(
        Models.ToolDescriptor tool,
        Dictionary<string, object> parameters,
        int invocationId,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("MCP tool invocation for '{ToolId}' from server '{ServerName}' (invocation #{InvocationId})",
            tool.ToolId, tool.Source, invocationId);

        try
        {
            var mcpResult = await _mcpToolProvider.InvokeToolAsync(
                tool.Source, // Source is the server name for MCP tools
                tool.ToolId,
                parameters,
                cancellationToken);

            return new ToolInvocationResult
            {
                Success = mcpResult.Success,
                Result = mcpResult.Result,
                ErrorMessage = mcpResult.ErrorMessage,
                ErrorCode = mcpResult.ErrorCode,
                BackendUsed = ToolBackendType.MCP,
                DurationMs = mcpResult.DurationMs,
                ContextTokenCost = mcpResult.ContextTokenCost,
                Metadata = mcpResult.Metadata
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to invoke MCP tool '{ToolId}' from server '{ServerName}' " +
                "(invocation #{InvocationId}): {ErrorMessage}",
                tool.ToolId, tool.Source, invocationId, ex.Message);

            return new ToolInvocationResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                ErrorCode = "MCP_INVOCATION_FAILED",
                BackendUsed = ToolBackendType.MCP,
                ContextTokenCost = tool.EstimatedTokenCost
            };
        }
    }

    // Helper methods for REST API invocation

    private static HttpRequestMessage BuildHttpRequest(
        string url,
        string httpMethod,
        RestApiToolConfiguration config,
        Dictionary<string, object> parameters)
    {
        var request = new HttpRequestMessage
        {
            Method = new HttpMethod(httpMethod),
            RequestUri = new Uri(url)
        };

        // Add custom headers
        foreach (var header in config.Headers)
        {
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        // Add authentication
        ApplyAuthentication(request, config);

        // Add request body for POST/PUT/PATCH
        if (config.AutoSerializeBody && 
            (httpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) ||
             httpMethod.Equals("PUT", StringComparison.OrdinalIgnoreCase) ||
             httpMethod.Equals("PATCH", StringComparison.OrdinalIgnoreCase)))
        {
            var bodyParameters = GetBodyParameters(parameters, config.UrlTemplate);
            if (bodyParameters.Count > 0)
            {
                var jsonBody = JsonSerializer.Serialize(bodyParameters);
                request.Content = new StringContent(jsonBody, Encoding.UTF8, config.RequestContentType);
            }
        }

        return request;
    }

    private static string? BuildUrl(string baseUrl, string? urlTemplate, Dictionary<string, object> parameters)
    {
        try
        {
            var url = baseUrl.TrimEnd('/');

            if (!string.IsNullOrWhiteSpace(urlTemplate))
            {
                var template = urlTemplate.TrimStart('/');

                // Replace parameters in template
                foreach (var param in parameters)
                {
                    var placeholder = $"{{{param.Key}}}";
                    if (template.Contains(placeholder, StringComparison.OrdinalIgnoreCase))
                    {
                        template = template.Replace(placeholder, Uri.EscapeDataString(param.Value?.ToString() ?? ""),
                            StringComparison.OrdinalIgnoreCase);
                    }
                }

                url = $"{url}/{template}";
            }

            return url;
        }
        catch
        {
            return null;
        }
    }

    private static void ApplyAuthentication(HttpRequestMessage request, RestApiToolConfiguration config)
    {
        switch (config.AuthenticationType)
        {
            case RestApiAuthenticationType.ApiKey:
                if (config.AuthenticationConfig.TryGetValue("HeaderName", out var headerName) &&
                    config.AuthenticationConfig.TryGetValue("Value", out var apiKey))
                {
                    request.Headers.TryAddWithoutValidation(headerName, apiKey);
                }
                break;

            case RestApiAuthenticationType.Bearer:
                if (config.AuthenticationConfig.TryGetValue("Token", out var token))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }
                break;

            case RestApiAuthenticationType.Basic:
                if (config.AuthenticationConfig.TryGetValue("Username", out var username) &&
                    config.AuthenticationConfig.TryGetValue("Password", out var password))
                {
                    var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
                    request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
                }
                break;

            case RestApiAuthenticationType.None:
            default:
                // No authentication
                break;
        }
    }

    private static Dictionary<string, object> GetBodyParameters(Dictionary<string, object> allParameters, string? urlTemplate)
    {
        if (string.IsNullOrWhiteSpace(urlTemplate))
        {
            return allParameters;
        }

        // Extract parameters used in URL template
        var usedInUrl = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var placeholderPattern = new System.Text.RegularExpressions.Regex(@"\{([^}]+)\}");
        var matches = placeholderPattern.Matches(urlTemplate);
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            usedInUrl.Add(match.Groups[1].Value);
        }

        // Return parameters not used in URL (these go in body)
        return allParameters
            .Where(p => !usedInUrl.Contains(p.Key))
            .ToDictionary(p => p.Key, p => p.Value);
    }

    private static bool IsSuccessStatusCode(System.Net.HttpStatusCode statusCode, List<int> expectedCodes)
    {
        var code = (int)statusCode;

        // If expected codes are specified, check against them
        if (expectedCodes.Count > 0)
        {
            return expectedCodes.Contains(code);
        }

        // Default: 200-299 range
        return code >= 200 && code < 300;
    }

    private static int EstimateRestApiTokenCost(HttpRequestMessage request, string responseBody)
    {
        // Estimate token cost based on headers and body
        // Rough approximation: 4 characters = 1 token
        var headerSize = request.Headers.ToString().Length;
        var bodySize = responseBody?.Length ?? 0;
        var totalChars = headerSize + bodySize;
        return totalChars / 4;
    }
}
