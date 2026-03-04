using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Daiv3.Mcp.Integration;

/// <summary>
/// Default implementation of <see cref="IMcpToolProvider"/> providing connectivity to MCP tool servers.
/// </summary>
/// <remarks>
/// This implementation supports stdio-based MCP servers (the most common MCP server type).
/// HTTP and WebSocket transports can be added in future iterations if needed.
/// 
/// <para>
/// MCP Protocol Overview:
/// - JSON-RPC 2.0 over stdio
/// - Request: { "jsonrpc": "2.0", "id": "unique-id", "method": "method-name", "params": {...} }
/// - Response: { "jsonrpc": "2.0", "id": "unique-id", "result": {...} } or "error"
/// </para>
/// </remarks>
public sealed class McpToolProvider : IMcpToolProvider, IDisposable
{
    private readonly ILogger<McpToolProvider> _logger;
    private readonly Dictionary<string, McpServerConnection> _connections = new();
    private readonly object _lock = new();
    private int _requestIdCounter = 0;

    public McpToolProvider(ILogger<McpToolProvider> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> ConnectAsync(McpServerOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Endpoint);

        lock (_lock)
        {
            if (_connections.ContainsKey(options.Name))
            {
                _logger.LogWarning("MCP server '{ServerName}' is already connected", options.Name);
                return true;
            }
        }

        try
        {
            _logger.LogInformation("Connecting to MCP server '{ServerName}' at '{Endpoint}' via {Transport}",
                options.Name, options.Endpoint, options.TransportType);

            var connection = await CreateConnectionAsync(options, cancellationToken);

            lock (_lock)
            {
                _connections[options.Name] = connection;
            }

            _logger.LogInformation("Successfully connected to MCP server '{ServerName}'", options.Name);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to MCP server '{ServerName}': {ErrorMessage}",
                options.Name, ex.Message);
            return false;
        }
    }

    public async Task DisconnectAsync(string serverName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverName);

        McpServerConnection? connection = null;
        lock (_lock)
        {
            if (_connections.TryGetValue(serverName, out connection))
            {
                _connections.Remove(serverName);
            }
        }

        if (connection != null)
        {
            _logger.LogInformation("Disconnecting from MCP server '{ServerName}'", serverName);
            await connection.DisposeAsync();
        }
    }

    public async Task<IReadOnlyList<McpToolDescriptor>> DiscoverToolsAsync(
        string serverName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverName);

        var connection = GetConnection(serverName);
        if (connection == null)
        {
            _logger.LogWarning("Cannot discover tools: MCP server '{ServerName}' is not connected", serverName);
            return Array.Empty<McpToolDescriptor>();
        }

        try
        {
            _logger.LogDebug("Discovering tools from MCP server '{ServerName}'", serverName);

            var request = new McpRequest
            {
                Id = GenerateRequestId(),
                Method = "tools/list",
                Params = new McpToolsListRequest()
            };

            var response = await SendRequestAsync<McpToolsListResponse>(connection, request, cancellationToken);

            if (response == null || response.Tools == null)
            {
                _logger.LogWarning("Tool discovery returned null response from server '{ServerName}'", serverName);
                return Array.Empty<McpToolDescriptor>();
            }

            var tools = new List<McpToolDescriptor>();
            foreach (var toolInfo in response.Tools)
            {
                var descriptor = ConvertToDescriptor(serverName, toolInfo);
                tools.Add(descriptor);
            }

            _logger.LogInformation("Discovered {ToolCount} tools from MCP server '{ServerName}'",
                tools.Count, serverName);

            return tools;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discover tools from MCP server '{ServerName}': {ErrorMessage}",
                serverName, ex.Message);
            return Array.Empty<McpToolDescriptor>();
        }
    }

    public async Task<McpToolInvocationResult> InvokeToolAsync(
        string serverName,
        string toolId,
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverName);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolId);

        var connection = GetConnection(serverName);
        if (connection == null)
        {
            return new McpToolInvocationResult
            {
                Success = false,
                ErrorMessage = $"MCP server '{serverName}' is not connected",
                ErrorCode = "SERVER_NOT_CONNECTED"
            };
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            _logger.LogDebug("Invoking tool '{ToolId}' on MCP server '{ServerName}' with {ParamCount} parameters",
                toolId, serverName, parameters?.Count ?? 0);

            var request = new McpRequest
            {
                Id = GenerateRequestId(),
                Method = "tools/call",
                Params = new McpToolCallRequest
                {
                    Name = toolId,
                    Arguments = parameters
                }
            };

            var response = await SendRequestAsync<McpToolCallResponse>(connection, request, cancellationToken);
            stopwatch.Stop();

            if (response == null)
            {
                return new McpToolInvocationResult
                {
                    Success = false,
                    ErrorMessage = "Tool invocation returned null response",
                    ErrorCode = "NULL_RESPONSE",
                    DurationMs = stopwatch.ElapsedMilliseconds
                };
            }

            if (response.IsError == true)
            {
                var errorText = response.Content.FirstOrDefault()?.Text ?? "Unknown error";
                return new McpToolInvocationResult
                {
                    Success = false,
                    ErrorMessage = errorText,
                    ErrorCode = "TOOL_EXECUTION_ERROR",
                    DurationMs = stopwatch.ElapsedMilliseconds
                };
            }

            // Extract result from content
            var resultContent = response.Content.FirstOrDefault();
            var result = resultContent?.Text ?? resultContent?.Data;

            // Estimate context token cost (rough approximation)
            var tokenCost = EstimateTokenCost(parameters, result);

            _logger.LogInformation("Tool '{ToolId}' invoked successfully on server '{ServerName}' " +
                "in {DurationMs}ms (estimated {TokenCost} tokens)",
                toolId, serverName, stopwatch.ElapsedMilliseconds, tokenCost);

            return new McpToolInvocationResult
            {
                Success = true,
                Result = result,
                DurationMs = stopwatch.ElapsedMilliseconds,
                ContextTokenCost = tokenCost
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Failed to invoke tool '{ToolId}' on MCP server '{ServerName}': {ErrorMessage}",
                toolId, serverName, ex.Message);

            return new McpToolInvocationResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                ErrorCode = "INVOCATION_EXCEPTION",
                DurationMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    public bool IsConnected(string serverName)
    {
        lock (_lock)
        {
            return _connections.ContainsKey(serverName) && _connections[serverName].IsConnected;
        }
    }

    public IReadOnlyList<string> GetConnectedServers()
    {
        lock (_lock)
        {
            return _connections.Keys.ToList();
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var connection in _connections.Values)
            {
                connection.Dispose();
            }
            _connections.Clear();
        }
    }

    // Private helper methods

    private async Task<McpServerConnection> CreateConnectionAsync(
        McpServerOptions options,
        CancellationToken cancellationToken)
    {
        if (options.TransportType != McpTransportType.Stdio)
        {
            throw new NotSupportedException(
                $"Transport type {options.TransportType} is not currently supported. Only Stdio is implemented.");
        }

        // Parse command and arguments from Endpoint
        var parts = options.Endpoint.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var command = parts[0];
        var arguments = parts.Length > 1 ? parts[1] : string.Empty;

        var processStartInfo = new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        var process = Process.Start(processStartInfo);
        if (process == null)
        {
            throw new InvalidOperationException($"Failed to start MCP server process: {command}");
        }

        var connection = new McpServerConnection(options, process, _logger);

        // Wait for process to be ready (optional: could add handshake protocol)
        await Task.Delay(100, cancellationToken);

        return connection;
    }

    private McpServerConnection? GetConnection(string serverName)
    {
        lock (_lock)
        {
            return _connections.TryGetValue(serverName, out var connection) ? connection : null;
        }
    }

    private string GenerateRequestId()
    {
        var id = Interlocked.Increment(ref _requestIdCounter);
        return $"req-{id}";
    }

    private async Task<T?> SendRequestAsync<T>(
        McpServerConnection connection,
        McpRequest request,
        CancellationToken cancellationToken) where T : class
    {
        var requestJson = JsonSerializer.Serialize(request);
        await connection.WriteLineAsync(requestJson, cancellationToken);

        var responseJson = await connection.ReadLineAsync(
            connection.Options.InvocationTimeoutMs,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(responseJson))
        {
            throw new InvalidOperationException("Received empty response from MCP server");
        }

        var response = JsonSerializer.Deserialize<McpResponse>(responseJson);
        if (response == null)
        {
            throw new InvalidOperationException("Failed to deserialize MCP response");
        }

        if (response.Error != null)
        {
            throw new InvalidOperationException(
                $"MCP error {response.Error.Code}: {response.Error.Message}");
        }

        if (response.Result == null)
        {
            return null;
        }

        // Convert result to target type
        var resultJson = JsonSerializer.Serialize(response.Result);
        return JsonSerializer.Deserialize<T>(resultJson);
    }

    private McpToolDescriptor ConvertToDescriptor(string serverName, McpToolInfo toolInfo)
    {
        var parameters = new List<McpToolParameter>();
        var requiredParams = toolInfo.InputSchema?.Required ?? new List<string>();

        if (toolInfo.InputSchema?.Properties != null)
        {
            foreach (var (name, schema) in toolInfo.InputSchema.Properties)
            {
                parameters.Add(new McpToolParameter
                {
                    Name = name,
                    Description = schema.Description ?? string.Empty,
                    Type = schema.Type,
                    Required = requiredParams.Contains(name),
                    DefaultValue = schema.Default,
                    EnumValues = schema.Enum
                });
            }
        }

        // Estimate token cost based on tool description and parameters
        var tokenCost = EstimateDescriptorTokenCost(toolInfo, parameters);

        return new McpToolDescriptor
        {
            ToolId = toolInfo.Name,
            Name = toolInfo.Name,
            Description = toolInfo.Description ?? string.Empty,
            ServerName = serverName,
            Backend = ToolBackendType.MCP,
            Parameters = parameters,
            EstimatedTokenCost = tokenCost
        };
    }

    private int EstimateDescriptorTokenCost(McpToolInfo toolInfo, List<McpToolParameter> parameters)
    {
        // Rough estimate: ~4 characters per token
        var chars = (toolInfo.Name?.Length ?? 0) +
                   (toolInfo.Description?.Length ?? 0) +
                   parameters.Sum(p => (p.Name?.Length ?? 0) + (p.Description?.Length ?? 0));
        return Math.Max(chars / 4, 10); // Minimum 10 tokens
    }

    private int EstimateTokenCost(Dictionary<string, object>? parameters, object? result)
    {
        var paramChars = parameters != null ? JsonSerializer.Serialize(parameters).Length : 0;
        var resultChars = result != null ? JsonSerializer.Serialize(result).Length : 0;
        return Math.Max((paramChars + resultChars) / 4, 5); // Minimum 5 tokens
    }
}

/// <summary>
/// Represents a connection to an MCP server process.
/// </summary>
internal sealed class McpServerConnection : IDisposable, IAsyncDisposable
{
    private readonly Process _process;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public McpServerConnection(McpServerOptions options, Process process, ILogger logger)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        _process = process ?? throw new ArgumentNullException(nameof(process));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public McpServerOptions Options { get; }

    public bool IsConnected => _process != null && !_process.HasExited;

    public async Task WriteLineAsync(string message, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            if (_process.StandardInput != null)
            {
                await _process.StandardInput.WriteLineAsync(message.AsMemory(), cancellationToken);
                await _process.StandardInput.FlushAsync(cancellationToken);
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<string> ReadLineAsync(int timeoutMs, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeoutMs);

        try
        {
            if (_process.StandardOutput != null)
            {
                var line = await _process.StandardOutput.ReadLineAsync(cts.Token);
                return line ?? string.Empty;
            }
            return string.Empty;
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException($"MCP server read operation timed out after {timeoutMs}ms");
        }
    }

    public void Dispose()
    {
        _writeLock.Dispose();
        if (_process != null && !_process.HasExited)
        {
            try
            {
                _process.Kill(entireProcessTree: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error killing MCP server process");
            }
        }
        _process?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        Dispose();
        await Task.CompletedTask;
    }
}
