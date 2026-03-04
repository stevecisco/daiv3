using Daiv3.Mcp.Integration;
using Daiv3.Orchestration;
using Daiv3.Orchestration.Interfaces;
using Daiv3.Orchestration.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Net.Http;
using Xunit;

namespace Daiv3.UnitTests.Orchestration;

/// <summary>
/// Unit tests for REST API tool invocation in ToolRoutingService.
/// Tests AST-REQ-009 functionality for invoking external REST APIs.
/// </summary>
public sealed class RestApiToolInvocationTests : IDisposable
{
    private readonly Mock<ILogger<ToolRoutingService>> _mockLogger;
    private readonly Mock<IToolRegistry> _mockRegistry;
    private readonly Mock<IMcpToolProvider> _mockMcpProvider;
    private readonly Mock<HttpMessageHandler> _mockHttpHandler;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ToolRoutingService _service;
    private readonly HttpClient _httpClient;

    public RestApiToolInvocationTests()
    {
        _mockLogger = new Mock<ILogger<ToolRoutingService>>();
        _mockRegistry = new Mock<IToolRegistry>();
        _mockMcpProvider = new Mock<IMcpToolProvider>();
        _mockHttpHandler = new Mock<HttpMessageHandler>();

        // Create HttpClientFactory with mocked handler
        _httpClient = new HttpClient(_mockHttpHandler.Object);
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient("RestApiTool")).Returns(_httpClient);
        _httpClientFactory = mockFactory.Object;

        _service = new ToolRoutingService(
            _mockLogger.Object,
            _mockRegistry.Object,
            _mockMcpProvider.Object,
            _httpClientFactory);
    }

    [Fact]
    public async Task InvokeRestApiTool_WithValidGet_ReturnsSuccessResult()
    {
        // Arrange
        var tool = CreateTestRestApiTool(
            "weather-api",
            "https://api.example.com",
            "GET",
            "/weather?city={city}");

        _mockRegistry.Setup(r => r.GetToolAsync("weather-api", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tool);

        SetupHttpResponse(HttpStatusCode.OK, "{\"temperature\": 72, \"condition\": \"sunny\"}");

        var parameters = new Dictionary<string, object>
        {
            ["city"] = "Seattle"
        };

        // Act
        var result = await _service.InvokeToolAsync("weather-api", parameters);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(ToolBackendType.RestAPI, result.BackendUsed);
        Assert.NotNull(result.Result);
        var resultStr = result.Result.ToString() ?? string.Empty;
        Assert.Contains("temperature", resultStr);
        Assert.Contains("72", resultStr);
        Assert.True(result.ContextTokenCost > 0);
    }

    [Fact]
    public async Task InvokeRestApiTool_WithPost_SendsBodyCorrectly()
    {
        // Arrange
        var tool = CreateTestRestApiTool(
            "create-user",
            "https://api.example.com",
            "POST",
            "/users");

        _mockRegistry.Setup(r => r.GetToolAsync("create-user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tool);

        HttpRequestMessage? capturedRequest = null;
        SetupHttpResponse(HttpStatusCode.Created, "{\"id\": \"123\", \"status\": \"created\"}",
            req => capturedRequest = req);

        var parameters = new Dictionary<string, object>
        {
            ["name"] = "John Doe",
            ["email"] = "john@example.com"
        };

        // Act
        var result = await _service.InvokeToolAsync("create-user", parameters);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(ToolBackendType.RestAPI, result.BackendUsed);
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest.Method);
        Assert.NotNull(capturedRequest.Content);
    }

    [Fact]
    public async Task InvokeRestApiTool_WithAuthentication_InjectsHeaders()
    {
        // Arrange
        var tool = CreateTestRestApiTool(
            "auth-api",
            "https://api.example.com",
            "GET",
            "/protected");

        tool.Metadata["Auth.HeaderName"] = "X-API-Key";
        tool.Metadata["Auth.Value"] = "secret-key-123";
        tool.Metadata["AuthenticationType"] = "ApiKey";

        _mockRegistry.Setup(r => r.GetToolAsync("auth-api", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tool);

        HttpRequestMessage? capturedRequest = null;
        SetupHttpResponse(HttpStatusCode.OK, "{\"data\": \"protected content\"}",
            req => capturedRequest = req);

        // Act
        var result = await _service.InvokeToolAsync("auth-api", new Dictionary<string, object>());

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(capturedRequest);
        Assert.Contains(capturedRequest.Headers, h => h.Key == "X-API-Key");
    }

    [Fact]
    public async Task InvokeRestApiTool_WithBearerAuth_SetsAuthorizationHeader()
    {
        // Arrange
        var tool = CreateTestRestApiTool(
            "bearer-api",
            "https://api.example.com",
            "GET",
            "/data");

        tool.Metadata["Auth.Token"] = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...";
        tool.Metadata["AuthenticationType"] = "Bearer";

        _mockRegistry.Setup(r => r.GetToolAsync("bearer-api", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tool);

        HttpRequestMessage? capturedRequest = null;
        SetupHttpResponse(HttpStatusCode.OK, "{\"data\": \"value\"}",
            req => capturedRequest = req);

        // Act
        var result = await _service.InvokeToolAsync("bearer-api", new Dictionary<string, object>());

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(capturedRequest);
        Assert.NotNull(capturedRequest.Headers.Authorization);
        Assert.Equal("Bearer", capturedRequest.Headers.Authorization.Scheme);
    }

    [Fact]
    public async Task InvokeRestApiTool_WithUrlTemplate_SubstitutesParameters()
    {
        // Arrange
        var tool = CreateTestRestApiTool(
            "user-api",
            "https://api.example.com",
            "GET",
            "/users/{userId}/posts/{postId}");

        _mockRegistry.Setup(r => r.GetToolAsync("user-api", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tool);

        HttpRequestMessage? capturedRequest = null;
        SetupHttpResponse(HttpStatusCode.OK, "{\"post\": \"data\"}",
            req => capturedRequest = req);

        var parameters = new Dictionary<string, object>
        {
            ["userId"] = "42",
            ["postId"] = "789"
        };

        // Act
        var result = await _service.InvokeToolAsync("user-api", parameters);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(capturedRequest);
        Assert.Contains("/users/42/posts/789", capturedRequest.RequestUri?.ToString());
    }

    [Fact]
    public async Task InvokeRestApiTool_WithTimeout_HandlesGracefully()
    {
        // Arrange
        var tool = CreateTestRestApiTool(
            "slow-api",
            "https://api.example.com",
            "GET",
            "/slow");

        tool.Metadata["TimeoutSeconds"] = "1";

        _mockRegistry.Setup(r => r.GetToolAsync("slow-api", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tool);

        // Simulate timeout
        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Request timed out"));

        // Act
        var result = await _service.InvokeToolAsync("slow-api", new Dictionary<string, object>());

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("TIMEOUT", result.ErrorCode);
        Assert.Equal(ToolBackendType.RestAPI, result.BackendUsed);
    }

    [Fact]
    public async Task InvokeRestApiTool_WithNonSuccessStatus_ReturnsError()
    {
        // Arrange
        var tool = CreateTestRestApiTool(
            "not-found-api",
            "https://api.example.com",
            "GET",
            "/missing");

        _mockRegistry.Setup(r => r.GetToolAsync("not-found-api", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tool);

        SetupHttpResponse(HttpStatusCode.NotFound, "{\"error\": \"Not found\"}");

        // Act
        var result = await _service.InvokeToolAsync("not-found-api", new Dictionary<string, object>());

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("HTTP_404", result.ErrorCode);
        Assert.Equal(ToolBackendType.RestAPI, result.BackendUsed);
        Assert.Contains("404", result.ErrorMessage);
    }

    [Fact]
    public async Task InvokeRestApiTool_WithCustomExpectedStatus_AcceptsStatusCode()
    {
        // Arrange
        var tool = CreateTestRestApiTool(
            "custom-status-api",
            "https://api.example.com",
            "GET",
            "/data");

        tool.Metadata["ExpectedStatusCodes"] = "200,404"; // Accept both 200 and 404

        _mockRegistry.Setup(r => r.GetToolAsync("custom-status-api", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tool);

        SetupHttpResponse(HttpStatusCode.NotFound, "{\"data\": null}");

        // Act
        var result = await _service.InvokeToolAsync("custom-status-api", new Dictionary<string, object>());

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success); // 404 is accepted as success
        Assert.Equal(ToolBackendType.RestAPI, result.BackendUsed);
    }

    [Fact]
    public async Task InvokeRestApiTool_WithNetworkFailure_ReturnsError()
    {
        // Arrange
        var tool = CreateTestRestApiTool(
            "network-fail-api",
            "https://api.example.com",
            "GET",
            "/data");

        _mockRegistry.Setup(r => r.GetToolAsync("network-fail-api", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tool);

        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        var result = await _service.InvokeToolAsync("network-fail-api", new Dictionary<string, object>());

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("HTTP_REQUEST_FAILED", result.ErrorCode);
        Assert.Equal(ToolBackendType.RestAPI, result.BackendUsed);
        Assert.Contains("Network error", result.ErrorMessage);
    }

    [Fact]
    public async Task InvokeRestApiTool_WithMissingConfiguration_ReturnsError()
    {
        // Arrange
        var tool = new ToolDescriptor
        {
            ToolId = "invalid-config-api",
            Name = "Invalid Config API",
            Description = "API with invalid config",
            Backend = ToolBackendType.RestAPI,
            Source = "https://api.example.com",
            Metadata = new Dictionary<string, string>() // Missing HttpMethod
        };

        _mockRegistry.Setup(r => r.GetToolAsync("invalid-config-api", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tool);

        // Act
        var result = await _service.InvokeToolAsync("invalid-config-api", new Dictionary<string, object>());

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("INVALID_CONFIGURATION", result.ErrorCode);
        Assert.Equal(ToolBackendType.RestAPI, result.BackendUsed);
    }

    [Fact]
    public async Task InvokeRestApiTool_WithRetry_RetriesOnFailure()
    {
        // Arrange
        var tool = CreateTestRestApiTool(
            "retry-api",
            "https://api.example.com",
            "GET",
            "/data");

        tool.Metadata["RetryCount"] = "2";

        _mockRegistry.Setup(r => r.GetToolAsync("retry-api", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tool);

        var attemptCount = 0;
        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                attemptCount++;
                if (attemptCount < 3)
                {
                    throw new HttpRequestException("Transient error");
                }
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"data\": \"success\"}")
                };
            });

        // Act
        var result = await _service.InvokeToolAsync("retry-api", new Dictionary<string, object>());

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(3, attemptCount); // Initial + 2 retries
        Assert.Equal(ToolBackendType.RestAPI, result.BackendUsed);
    }

    [Fact]
    public async Task InvokeRestApiTool_WithCustomHeaders_AddsHeaders()
    {
        // Arrange
        var tool = CreateTestRestApiTool(
            "custom-header-api",
            "https://api.example.com",
            "GET",
            "/data");

        tool.Metadata["Header.X-Custom-Header"] = "custom-value";
        tool.Metadata["Header.Accept"] = "application/json";

        _mockRegistry.Setup(r => r.GetToolAsync("custom-header-api", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tool);

        HttpRequestMessage? capturedRequest = null;
        SetupHttpResponse(HttpStatusCode.OK, "{\"data\": \"value\"}",
            req => capturedRequest = req);

        // Act
        var result = await _service.InvokeToolAsync("custom-header-api", new Dictionary<string, object>());

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(capturedRequest);
        Assert.Contains(capturedRequest.Headers, h => h.Key == "X-Custom-Header");
    }

    [Fact]
    public async Task InvokeRestApiTool_WithPutMethod_SendsCorrectMethod()
    {
        // Arrange
        var tool = CreateTestRestApiTool(
            "update-api",
            "https://api.example.com",
            "PUT",
            "/users/{id}");

        _mockRegistry.Setup(r => r.GetToolAsync("update-api", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tool);

        HttpRequestMessage? capturedRequest = null;
        SetupHttpResponse(HttpStatusCode.OK, "{\"updated\": true}",
            req => capturedRequest = req);

        var parameters = new Dictionary<string, object>
        {
            ["id"] = "123",
            ["name"] = "Updated Name"
        };

        // Act
        var result = await _service.InvokeToolAsync("update-api", parameters);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Put, capturedRequest.Method);
    }

    [Fact]
    public async Task InvokeRestApiTool_WithDeleteMethod_SendsCorrectMethod()
    {
        // Arrange
        var tool = CreateTestRestApiTool(
            "delete-api",
            "https://api.example.com",
            "DELETE",
            "/users/{id}");

        _mockRegistry.Setup(r => r.GetToolAsync("delete-api", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tool);

        HttpRequestMessage? capturedRequest = null;
        SetupHttpResponse(HttpStatusCode.NoContent, string.Empty,
            req => capturedRequest = req);

        var parameters = new Dictionary<string, object>
        {
            ["id"] = "123"
        };

        // Act
        var result = await _service.InvokeToolAsync("delete-api", parameters);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Delete, capturedRequest.Method);
    }

    [Fact]
    public async Task InvokeRestApiTool_EstimatesContextTokenCost()
    {
        // Arrange
        var tool = CreateTestRestApiTool(
            "token-cost-api",
            "https://api.example.com",
            "GET",
            "/data");

        _mockRegistry.Setup(r => r.GetToolAsync("token-cost-api", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tool);

        var largeResponse = new string('x', 4000); // ~1000 tokens
        SetupHttpResponse(HttpStatusCode.OK, largeResponse);

        // Act
        var result = await _service.InvokeToolAsync("token-cost-api", new Dictionary<string, object>());

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.True(result.ContextTokenCost > 500); // Should estimate significant token cost
        Assert.Equal(ToolBackendType.RestAPI, result.BackendUsed);
    }

    // Helper methods

    private ToolDescriptor CreateTestRestApiTool(
        string toolId,
        string baseUrl,
        string httpMethod,
        string? urlTemplate = null)
    {
        var metadata = new Dictionary<string, string>
        {
            ["HttpMethod"] = httpMethod
        };

        if (!string.IsNullOrWhiteSpace(urlTemplate))
        {
            metadata["UrlTemplate"] = urlTemplate;
        }

        return new ToolDescriptor
        {
            ToolId = toolId,
            Name = $"{toolId}-name",
            Description = $"{toolId}-description",
            Backend = ToolBackendType.RestAPI,
            Source = baseUrl,
            Metadata = metadata,
            IsAvailable = true
        };
    }

    private void SetupHttpResponse(
        HttpStatusCode statusCode,
        string content,
        Action<HttpRequestMessage>? captureRequest = null)
    {
        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken ct) =>
            {
                captureRequest?.Invoke(req);
                return new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(content)
                };
            });
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
