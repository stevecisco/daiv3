using System.Text.Json;

namespace Daiv3.Orchestration.Models;

/// <summary>
/// Configuration for REST API tool invocation.
/// </summary>
/// <remarks>
/// This configuration is stored in the ToolDescriptor.Metadata dictionary as JSON.
/// It defines how to construct and execute HTTP requests to external REST APIs.
/// </remarks>
public sealed class RestApiToolConfiguration
{
    /// <summary>
    /// Gets or sets the HTTP method for the request.
    /// </summary>
    /// <remarks>
    /// Supported values: GET, POST, PUT, DELETE, PATCH, HEAD, OPTIONS.
    /// </remarks>
    public required string HttpMethod { get; set; }

    /// <summary>
    /// Gets or sets the URL template with parameter placeholders.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Parameter placeholders use curly braces: "/api/users/{userId}/posts/{postId}"
    /// Parameters are substituted from the invocation parameters dictionary.
    /// </para>
    /// <para>
    /// For query parameters in GET requests, use the template:
    /// "/api/search?q={query}&amp;limit={limit}"
    /// </para>
    /// </remarks>
    public string? UrlTemplate { get; set; }

    /// <summary>
    /// Gets or sets custom HTTP headers to include in the request.
    /// </summary>
    /// <remarks>
    /// Common headers: Content-Type, Accept, User-Agent, X-Api-Key.
    /// Authentication headers are set separately via AuthenticationConfig.
    /// </remarks>
    public Dictionary<string, string> Headers { get; set; } = new();

    /// <summary>
    /// Gets or sets the authentication type for this API.
    /// </summary>
    public RestApiAuthenticationType AuthenticationType { get; set; } = RestApiAuthenticationType.None;

    /// <summary>
    /// Gets or sets the authentication configuration.
    /// </summary>
    /// <remarks>
    /// The structure depends on AuthenticationType:
    /// <list type="bullet">
    /// <item><description>ApiKey: { "HeaderName": "X-API-Key", "Value": "key123" }</description></item>
    /// <item><description>Bearer: { "Token": "eyJ..." }</description></item>
    /// <item><description>Basic: { "Username": "user", "Password": "pass" }</description></item>
    /// </list>
    /// </remarks>
    public Dictionary<string, string> AuthenticationConfig { get; set; } = new();

    /// <summary>
    /// Gets or sets the request timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the number of retry attempts on transient failures.
    /// </summary>
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// Gets or sets the expected HTTP status codes that indicate success.
    /// </summary>
    /// <remarks>
    /// If not specified, defaults to 200-299 range.
    /// Use this to customize success criteria (e.g., accepting 404 as valid).
    /// </remarks>
    public List<int> ExpectedStatusCodes { get; set; } = new();

    /// <summary>
    /// Gets or sets the content type for POST/PUT/PATCH request bodies.
    /// </summary>
    /// <remarks>
    /// Defaults to "application/json". Other common values: "application/xml", "application/x-www-form-urlencoded".
    /// </remarks>
    public string RequestContentType { get; set; } = "application/json";

    /// <summary>
    /// Gets or sets whether to automatically serialize parameters as JSON body for POST/PUT/PATCH.
    /// </summary>
    /// <remarks>
    /// When true, parameters not used in URL template are serialized as JSON request body.
    /// When false, all parameters must be specified in URL template.
    /// </remarks>
    public bool AutoSerializeBody { get; set; } = true;

    /// <summary>
    /// Parses REST API configuration from ToolDescriptor.Metadata dictionary.
    /// </summary>
    /// <param name="metadata">Metadata dictionary from ToolDescriptor.</param>
    /// <returns>Parsed configuration or null if metadata is incomplete.</returns>
    public static RestApiToolConfiguration? FromMetadata(Dictionary<string, string> metadata)
    {
        if (!metadata.TryGetValue("HttpMethod", out var httpMethod))
        {
            return null;
        }

        var config = new RestApiToolConfiguration
        {
            HttpMethod = httpMethod
        };

        if (metadata.TryGetValue("UrlTemplate", out var urlTemplate))
        {
            config.UrlTemplate = urlTemplate;
        }

        if (metadata.TryGetValue("TimeoutSeconds", out var timeoutStr) &&
            int.TryParse(timeoutStr, out var timeout))
        {
            config.TimeoutSeconds = timeout;
        }

        if (metadata.TryGetValue("RetryCount", out var retryStr) &&
            int.TryParse(retryStr, out var retry))
        {
            config.RetryCount = retry;
        }

        if (metadata.TryGetValue("AuthenticationType", out var authTypeStr) &&
            Enum.TryParse<RestApiAuthenticationType>(authTypeStr, true, out var authType))
        {
            config.AuthenticationType = authType;
        }

        if (metadata.TryGetValue("RequestContentType", out var contentType))
        {
            config.RequestContentType = contentType;
        }

        if (metadata.TryGetValue("AutoSerializeBody", out var autoSerializeStr) &&
            bool.TryParse(autoSerializeStr, out var autoSerialize))
        {
            config.AutoSerializeBody = autoSerialize;
        }

        // Parse headers (prefixed with "Header.")
        foreach (var kvp in metadata.Where(m => m.Key.StartsWith("Header.", StringComparison.OrdinalIgnoreCase)))
        {
            var headerName = kvp.Key.Substring(7); // Remove "Header." prefix
            config.Headers[headerName] = kvp.Value;
        }

        // Parse authentication config (prefixed with "Auth.")
        foreach (var kvp in metadata.Where(m => m.Key.StartsWith("Auth.", StringComparison.OrdinalIgnoreCase)))
        {
            var configKey = kvp.Key.Substring(5); // Remove "Auth." prefix
            config.AuthenticationConfig[configKey] = kvp.Value;
        }

        // Parse expected status codes
        if (metadata.TryGetValue("ExpectedStatusCodes", out var statusCodesStr))
        {
            var codes = statusCodesStr.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var code in codes)
            {
                if (int.TryParse(code.Trim(), out var statusCode))
                {
                    config.ExpectedStatusCodes.Add(statusCode);
                }
            }
        }

        return config;
    }

    /// <summary>
    /// Converts this configuration to metadata dictionary for storage in ToolDescriptor.
    /// </summary>
    /// <returns>Metadata dictionary.</returns>
    public Dictionary<string, string> ToMetadata()
    {
        var metadata = new Dictionary<string, string>
        {
            ["HttpMethod"] = HttpMethod,
            ["TimeoutSeconds"] = TimeoutSeconds.ToString(),
            ["RetryCount"] = RetryCount.ToString(),
            ["AuthenticationType"] = AuthenticationType.ToString(),
            ["RequestContentType"] = RequestContentType,
            ["AutoSerializeBody"] = AutoSerializeBody.ToString()
        };

        if (!string.IsNullOrWhiteSpace(UrlTemplate))
        {
            metadata["UrlTemplate"] = UrlTemplate;
        }

        // Add headers with "Header." prefix
        foreach (var kvp in Headers)
        {
            metadata[$"Header.{kvp.Key}"] = kvp.Value;
        }

        // Add authentication config with "Auth." prefix
        foreach (var kvp in AuthenticationConfig)
        {
            metadata[$"Auth.{kvp.Key}"] = kvp.Value;
        }

        // Add expected status codes as comma-separated list
        if (ExpectedStatusCodes.Count > 0)
        {
            metadata["ExpectedStatusCodes"] = string.Join(",", ExpectedStatusCodes);
        }

        return metadata;
    }
}

/// <summary>
/// Specifies the authentication type for REST API invocation.
/// </summary>
public enum RestApiAuthenticationType
{
    /// <summary>
    /// No authentication required.
    /// </summary>
    None = 0,

    /// <summary>
    /// API key in custom header.
    /// </summary>
    ApiKey = 1,

    /// <summary>
    /// Bearer token in Authorization header.
    /// </summary>
    Bearer = 2,

    /// <summary>
    /// Basic authentication with username and password.
    /// </summary>
    Basic = 3
}
