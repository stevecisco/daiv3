namespace FoundryLocal.Management;

using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

internal sealed class ServiceCatalogClient : IAsyncDisposable
{
    private static readonly Regex StatusRegex = new("is running on (http://.*)\\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private HttpClient? _httpClient;
    private Uri? _serviceUri;

    public async Task<IReadOnlyList<ServiceModelInfo>> ListModelsAsync(CancellationToken ct)
    {
        await EnsureServiceAsync(ct).ConfigureAwait(false);

        using var response = await _httpClient!.GetAsync("/foundry/list", ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        var models = JsonSerializer.Deserialize(json, ServiceModelJsonContext.Default.ListServiceModelInfo);
        return models ?? [];
    }

    public async Task<IReadOnlyList<string>> GetCachedModelIdsAsync(CancellationToken ct)
    {
        var cacheDir = await GetCacheDirectoryAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrEmpty(cacheDir) || !Directory.Exists(cacheDir))
        {
            return [];
        }

        try
        {
            var modelIds = new List<string>();

            // Get all model directories (they're under publisher folders like Microsoft\)
            // and have version suffixes like -1, -2, etc.
            var allDirs = Directory.GetDirectories(cacheDir, "*", SearchOption.AllDirectories)
                .Where(d => {
                    var parent = Path.GetFileName(Path.GetDirectoryName(d));
                    // Model directories are under publisher folders, not directly under cache/models
                    return !string.IsNullOrEmpty(parent) && !parent.Equals("models", StringComparison.OrdinalIgnoreCase);
                })
                .ToList();

            foreach (var dir in allDirs)
            {
                // Check if directory has files (not empty)
                if (Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Any())
                {
                    var dirName = Path.GetFileName(dir);
                    // Remove version suffix like "-1", "-2" to get the cached model name
                    var cachedName = System.Text.RegularExpressions.Regex.Replace(dirName ?? "", @"-\d+$", "");
                    
                    if (!string.IsNullOrEmpty(cachedName) && !modelIds.Contains(cachedName, StringComparer.OrdinalIgnoreCase))
                    {
                        modelIds.Add(cachedName);
                    }
                }
            }

            return modelIds;
        }
        catch
        {
            return [];
        }
    }

    public async Task<bool> DeleteModelAsync(string cachedModelName, CancellationToken ct)
    {
        var cacheDir = await GetCacheDirectoryAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrEmpty(cacheDir))
        {
            return false;
        }

        // Search for the cached model directory recursively (models are under subdirs like Microsoft\)
        try
        {
            var matchingDirs = Directory.GetDirectories(cacheDir, cachedModelName, SearchOption.AllDirectories);
            if (matchingDirs.Length == 0)
            {
                return false;
            }

            // Delete all matching directories (there should typically be only one)
            foreach (var dir in matchingDirs)
            {
                Directory.Delete(dir, recursive: true);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string?> GetCacheDirectoryAsync(CancellationToken ct)
    {
        await EnsureServiceAsync(ct).ConfigureAwait(false);

        using var response = await _httpClient!.GetAsync("/openai/status", ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("modelDirPath", out var pathElement))
        {
            return pathElement.GetString();
        }

        return null;
    }

    public async Task DownloadModelAsync(ServiceModelInfo model, IProgress<float>? progress, CancellationToken ct)
    {
        await EnsureServiceAsync(ct).ConfigureAwait(false);

        var payload = new ServiceDownloadRequest
        {
            Model = new ServiceDownloadRequest.ServiceModelInfo
            {
                Name = model.ModelId,
                Uri = model.Uri,
                Publisher = model.Publisher ?? string.Empty,
                ProviderType = string.Concat(model.ProviderType, "Local"),
                PromptTemplate = model.PromptTemplate
            },
            Token = string.Empty,
            IgnorePipeReport = true
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/openai/download");
        request.Content = JsonContent.Create(payload);

        using var response = await _httpClient!.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync(ct).ConfigureAwait(false)) != null)
        {
            if (line.StartsWith("Total", StringComparison.OrdinalIgnoreCase) && line.Contains('%'))
            {
                var percentStr = line.Split('%')[0].Split(' ').LastOrDefault();
                if (double.TryParse(percentStr, out var percentage))
                {
                    progress?.Report((float)percentage);
                }
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        _httpClient?.Dispose();
        _httpClient = null;
        _serviceUri = null;
        return ValueTask.CompletedTask;
    }

    private async Task EnsureServiceAsync(CancellationToken ct)
    {
        if (_serviceUri != null)
        {
            return;
        }

        await StartServiceAsync(ct).ConfigureAwait(false);
        var endpoint = await GetStatusEndpointAsync(ct).ConfigureAwait(false);
        if (endpoint == null)
        {
            throw new InvalidOperationException("Foundry service is not running.");
        }

        _serviceUri = endpoint;
        _httpClient?.Dispose();
        _httpClient = new HttpClient
        {
            BaseAddress = _serviceUri,
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    private static async Task StartServiceAsync(CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "foundry",
            Arguments = "service start",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.Environment["DOTNET_ENVIRONMENT"] = null;

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        await process.WaitForExitAsync(ct).ConfigureAwait(false);
    }

    private static async Task<Uri?> GetStatusEndpointAsync(CancellationToken ct)
    {
        var statusOutput = await InvokeFoundryAsync("service status", ct).ConfigureAwait(false);
        var match = StatusRegex.Match(statusOutput);
        if (!match.Success)
        {
            return null;
        }

        var uri = new Uri(match.Groups[1].Value);
        var builder = new UriBuilder { Scheme = uri.Scheme, Host = uri.Host, Port = uri.Port };
        return builder.Uri;
    }

    private static async Task<string> InvokeFoundryAsync(string args, CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "foundry",
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.Environment["DOTNET_ENVIRONMENT"] = null;

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the foundry process.");

        var output = new System.Text.StringBuilder();
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                output.AppendLine(e.Data);
            }
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(ct).ConfigureAwait(false);
        return output.ToString();
    }
}

internal sealed record ServiceRuntime(
    [property: JsonPropertyName("deviceType")] string? DeviceType,
    [property: JsonPropertyName("executionProvider")] string? ExecutionProvider);

internal sealed record ServicePromptTemplate(
    [property: JsonPropertyName("assistant")] string? Assistant,
    [property: JsonPropertyName("prompt")] string? Prompt);

internal sealed record ServiceModelInfo(
    [property: JsonPropertyName("name")] string ModelId,
    [property: JsonPropertyName("alias")] string Alias,
    [property: JsonPropertyName("displayName")] string? DisplayName,
    [property: JsonPropertyName("providerType")] string ProviderType,
    [property: JsonPropertyName("uri")] string Uri,
    [property: JsonPropertyName("publisher")] string? Publisher,
    [property: JsonPropertyName("promptTemplate")] ServicePromptTemplate? PromptTemplate,
    [property: JsonPropertyName("runtime")] ServiceRuntime? Runtime,
    [property: JsonPropertyName("fileSizeMb")] long? FileSizeMb);

internal sealed class ServiceDownloadRequest
{
    internal sealed class ServiceModelInfo
    {
        [JsonPropertyName("Name")]
        public required string Name { get; set; }

        [JsonPropertyName("Uri")]
        public required string Uri { get; set; }

        [JsonPropertyName("Publisher")]
        public required string Publisher { get; set; }

        [JsonPropertyName("ProviderType")]
        public required string ProviderType { get; set; }

        [JsonPropertyName("PromptTemplate")]
        public ServicePromptTemplate? PromptTemplate { get; set; }
    }

    [JsonPropertyName("Model")]
    public required ServiceModelInfo Model { get; set; }

    [JsonPropertyName("Token")]
    public required string Token { get; set; }

    [JsonPropertyName("IgnorePipeReport")]
    public required bool IgnorePipeReport { get; set; }
}

[JsonSerializable(typeof(ServiceModelInfo))]
[JsonSerializable(typeof(List<ServiceModelInfo>))]
[JsonSerializable(typeof(List<string>))]
internal partial class ServiceModelJsonContext : JsonSerializerContext
{
}
