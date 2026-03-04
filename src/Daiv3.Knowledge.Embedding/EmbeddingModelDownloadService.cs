using Microsoft.Extensions.Logging;

namespace Daiv3.Knowledge.Embedding;

/// <summary>
/// Service for downloading embedding models from Azure Blob Storage or other HTTP sources.
/// </summary>
public class EmbeddingModelDownloadService : IEmbeddingModelDownloadService
{
    private readonly ILogger<EmbeddingModelDownloadService> _logger;
    private readonly HttpClient _httpClient;
    private const int DownloadTimeoutSeconds = 1800; // 30 minutes for large model downloads

    public EmbeddingModelDownloadService(
        ILogger<EmbeddingModelDownloadService> logger,
        HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
    }

    /// <inheritdoc/>
    public async Task<bool> EnsureModelExistsAsync(
        string destinationPath,
        string downloadUrl,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Create directory if it doesn't exist
            var directory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                _logger.LogInformation("Created model directory: {Directory}", directory);
            }

            // Check if model already exists
            if (File.Exists(destinationPath))
            {
                var fileInfo = new FileInfo(destinationPath);
                _logger.LogInformation("Embedding model already exists at {Path} ({Size:N0} bytes)",
                    destinationPath, fileInfo.Length);

                progress?.Report(new DownloadProgress
                {
                    BytesDownloaded = fileInfo.Length,
                    TotalBytes = fileInfo.Length,
                    Status = "Model already exists"
                });

                return true;
            }

            // Download the model
            _logger.LogInformation("Model not found, downloading from {Url}", downloadUrl);
            await DownloadModelAsync(downloadUrl, destinationPath, progress, cancellationToken);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure embedding model exists at {Path}", destinationPath);

            progress?.Report(new DownloadProgress
            {
                Status = $"Error: {ex.Message}"
            });

            return false;
        }
    }

    /// <inheritdoc/>
    public async Task DownloadModelAsync(
        string downloadUrl,
        string destinationPath,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting download from {Url} to {Path}", downloadUrl, destinationPath);

            progress?.Report(new DownloadProgress
            {
                BytesDownloaded = 0,
                Status = "Initiating download..."
            });

            // Create a timeout cancellation token (reuse passed cancellationToken if provided)
            using var downloadCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            downloadCts.CancelAfter(TimeSpan.FromSeconds(DownloadTimeoutSeconds));

            using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, downloadCts.Token);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength;
            _logger.LogInformation("Download started, total size: {Size:N0} bytes ({SizeMB:F2} MB)",
                totalBytes, totalBytes / 1024.0 / 1024.0);

            // Create staging directory for downloads
            var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var stagingDir = Path.Combine(baseDir, "Daiv3", "models", "embeddings", "downloads");
            if (!Directory.Exists(stagingDir))
            {
                Directory.CreateDirectory(stagingDir);
                _logger.LogInformation("Created staging directory: {Directory}", stagingDir);
            }

            // Download to staging directory with unique filename
            var stagingFileName = $"{Path.GetFileNameWithoutExtension(destinationPath)}_{Guid.NewGuid():N}{Path.GetExtension(destinationPath)}";
            var stagingFilePath = Path.Combine(stagingDir, stagingFileName);

            try
            {
                long totalBytesRead;

                // Download to staging file - enclosed in a scope to ensure proper disposal before moving
                {
                    await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    await using var fileStream = new FileStream(stagingFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                    var buffer = new byte[8192];
                    totalBytesRead = 0;
                    int bytesRead;
                    var lastProgressReport = DateTime.UtcNow;

                    while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                        totalBytesRead += bytesRead;

                        // Report progress every 500ms to avoid excessive updates
                        var now = DateTime.UtcNow;
                        if (progress != null && (now - lastProgressReport).TotalMilliseconds >= 500)
                        {
                            var percentComplete = totalBytes.HasValue
                                ? (double)totalBytesRead / totalBytes.Value * 100.0
                                : (double?)null;

                            var statusMessage = totalBytes.HasValue
                                ? $"Downloading: {totalBytesRead:N0} / {totalBytes.Value:N0} bytes ({percentComplete:F1}%)"
                                : $"Downloading: {totalBytesRead:N0} bytes";

                            progress.Report(new DownloadProgress
                            {
                                BytesDownloaded = totalBytesRead,
                                TotalBytes = totalBytes,
                                Status = statusMessage
                            });

                            lastProgressReport = now;
                        }
                    }

                    // Ensure all data is written to disk
                    await fileStream.FlushAsync(cancellationToken);

                } // Dispose streams here before moving file

                // Final progress report
                progress?.Report(new DownloadProgress
                {
                    BytesDownloaded = totalBytesRead,
                    TotalBytes = totalBytes ?? totalBytesRead,
                    Status = "Download complete, moving to final location..."
                });

                _logger.LogInformation("Download completed to staging, {Bytes:N0} bytes downloaded", totalBytesRead);

                // Ensure destination directory exists
                var destDirectory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(destDirectory) && !Directory.Exists(destDirectory))
                {
                    Directory.CreateDirectory(destDirectory);
                    _logger.LogInformation("Created destination directory: {Directory}", destDirectory);
                }

                // Move from staging to final destination - streams are now disposed
                if (File.Exists(destinationPath))
                {
                    File.Delete(destinationPath);
                }
                File.Move(stagingFilePath, destinationPath);

                progress?.Report(new DownloadProgress
                {
                    BytesDownloaded = totalBytesRead,
                    TotalBytes = totalBytesRead,
                    Status = "Model ready"
                });

                _logger.LogInformation("Model successfully moved from staging to {Path}", destinationPath);
            }
            catch
            {
                // Clean up staging file on error
                if (File.Exists(stagingFilePath))
                {
                    try
                    {
                        File.Delete(stagingFilePath);
                        _logger.LogWarning("Deleted staging file after error: {Path}", stagingFilePath);
                    }
                    catch (Exception deleteEx)
                    {
                        _logger.LogWarning(deleteEx, "Failed to delete staging file {Path}", stagingFilePath);
                    }
                }
                throw;
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error downloading model from {Url}", downloadUrl);
            throw new InvalidOperationException($"Failed to download model: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Download cancelled or timed out");
            throw new InvalidOperationException("Download cancelled or timed out", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error downloading model from {Url}", downloadUrl);
            throw;
        }
    }
}
