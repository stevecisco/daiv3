using Daiv3.Knowledge;
using Daiv3.Orchestration.Interfaces;
using Daiv3.WebFetch.Crawl;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daiv3.Orchestration;

/// <summary>
/// Integrates web-fetched markdown content into the knowledge ingestion pipeline.
/// Monitors the markdown content store directory and automatically processes new files.
/// </summary>
public class WebContentIngestionService : IWebContentIngestionService, IDisposable
{
    private readonly ILogger<WebContentIngestionService> _logger;
    private readonly IMarkdownContentStore _contentStore;
    private readonly IKnowledgeDocumentProcessor _documentProcessor;
    private readonly WebContentIngestionOptions _options;
    private readonly ConcurrentDictionary<string, (long timestamp, long fileSize)> _ingestedFiles = new();
    private FileSystemWatcher? _watcher;
    private CancellationTokenSource? _monitoringCts;
    private Task? _monitoringTask;
    private int _totalFilesDetected;
    private int _filesIngested;
    private int _filesSkipped;
    private int _filesWithErrors;
    private long _totalChunksCreated;
    private long _totalTokensProcessed;
    private long _totalIngestionTimeMs;
    private bool _isMonitoring;
    private bool _disposed;
    private readonly SemaphoreSlim _concurrencyLimiter;
    private readonly Dictionary<string, Task<WebContentIngestionResult>> _pendingIngestions = new();
    private readonly object _pendingLock = new();

    public WebContentIngestionService(
        ILogger<WebContentIngestionService> logger,
        IMarkdownContentStore contentStore,
        IKnowledgeDocumentProcessor documentProcessor,
        IOptions<WebContentIngestionOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _contentStore = contentStore ?? throw new ArgumentNullException(nameof(contentStore));
        _documentProcessor = documentProcessor ?? throw new ArgumentNullException(nameof(documentProcessor));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _concurrencyLimiter = new SemaphoreSlim(_options.MaxConcurrentIngestions);

        _logger.LogInformation("WebContentIngestionService initialized. AutoMonitoring={AutoMonitoring}, MaxConcurrent={MaxConcurrent}",
            _options.EnableAutoMonitoring, _options.MaxConcurrentIngestions);
    }

    public async Task<WebContentIngestionResult> IngestContentAsync(
        string filePath,
        string? sourceUrl = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentNullException(nameof(filePath));
        }

        var stopwatch = Stopwatch.StartNew();
        var result = new WebContentIngestionResult
        {
            FilePath = filePath,
            SourceUrl = sourceUrl ?? string.Empty,
            Success = false
        };

        try
        {
            // Check if file exists
            if (!File.Exists(filePath))
            {
                result.ErrorMessage = $"File not found: {filePath}";
                _logger.LogWarning("Attempted to ingest non-existent file: {FilePath}", filePath);
                Interlocked.Increment(ref _filesWithErrors);
                return result;
            }

            // Extract source URL from metadata if not provided
            if (string.IsNullOrWhiteSpace(sourceUrl))
            {
                sourceUrl = ExtractSourceUrlFromMetadata(filePath);
                result.SourceUrl = sourceUrl ?? filePath;
            }

            // Check if already ingested
            var fileInfo = new FileInfo(filePath);
            var fileKey = GetFileKey(filePath);

            if (_options.SkipAlreadyIngestedFiles && _ingestedFiles.TryGetValue(fileKey, out var cached))
            {
                if (cached.fileSize == fileInfo.Length)
                {
                    _logger.LogDebug("Skipping already ingested file: {FilePath}", filePath);
                    Interlocked.Increment(ref _filesSkipped);
                    stopwatch.Stop();
                    result.IngestionTimeMs = stopwatch.ElapsedMilliseconds;
                    return result;
                }
            }

            // Process through knowledge document processor
            _logger.LogInformation("Ingesting web content from {SourceUrl} via {FilePath}", sourceUrl, filePath);

            var processingResult = await _documentProcessor.ProcessDocumentAsync(filePath, cancellationToken);

            if (processingResult.Success)
            {
                result.Success = true;
                result.ChunkCount = processingResult.ChunkCount;
                result.TotalTokens = processingResult.TotalTokens;
                result.FetchedAt = FormatFetchDate(fileInfo.CreationTimeUtc);

                // Track successful ingestion
                _ingestedFiles.AddOrUpdate(
                    fileKey,
                    (DateTime.UtcNow.Ticks, fileInfo.Length),
                    (_, _) => (DateTime.UtcNow.Ticks, fileInfo.Length));

                Interlocked.Increment(ref _filesIngested);
                Interlocked.Add(ref _totalChunksCreated, processingResult.ChunkCount);
                Interlocked.Add(ref _totalTokensProcessed, processingResult.TotalTokens);

                _logger.LogInformation("Successfully ingested web content. Chunks={Chunks}, Tokens={Tokens}, Source={Source}",
                    processingResult.ChunkCount, processingResult.TotalTokens, sourceUrl);
            }
            else
            {
                result.ErrorMessage = $"Document processor failed: {processingResult.ErrorMessage}";
                Interlocked.Increment(ref _filesWithErrors);
                _logger.LogError("Failed to process document {FilePath}: {Error}", filePath, processingResult.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
            Interlocked.Increment(ref _filesWithErrors);
            _logger.LogError(ex, "Error ingesting web content from {FilePath}", filePath);
        }
        finally
        {
            stopwatch.Stop();
            result.IngestionTimeMs = stopwatch.ElapsedMilliseconds;
            Interlocked.Add(ref _totalIngestionTimeMs, stopwatch.ElapsedMilliseconds);
            Interlocked.Increment(ref _totalFilesDetected);
        }

        return result;
    }

    public async Task<IReadOnlyList<WebContentIngestionResult>> IngestPendingContentAsync(
        IProgress<(int Processed, int Total, string CurrentFile)>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogDebug("Web content ingestion is disabled");
            return Array.Empty<WebContentIngestionResult>();
        }

        var results = new List<WebContentIngestionResult>();
        var storageDir = _contentStore.GetStorageDirectory();

        try
        {
            if (!Directory.Exists(storageDir))
            {
                _logger.LogInformation("Content store directory does not exist: {StorageDirectory}", storageDir);
                return results;
            }

            // Find all markdown files
            var markdownFiles = Directory.EnumerateFiles(storageDir, "*.md", SearchOption.AllDirectories)
                .Where(f => !Path.GetFileName(f).EndsWith(".metadata.json", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(f => new FileInfo(f).LastWriteTimeUtc)
                .ToList();

            _logger.LogInformation("Found {FileCount} markdown files in content store", markdownFiles.Count);

            var processed = 0;
            foreach (var filePath in markdownFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                progressCallback?.Report((processed, markdownFiles.Count, Path.GetFileName(filePath)));

                try
                {
                    await _concurrencyLimiter.WaitAsync(cancellationToken);
                    try
                    {
                        var result = await IngestContentAsync(filePath, null, cancellationToken);
                        results.Add(result);
                    }
                    finally
                    {
                        _concurrencyLimiter.Release();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception during ingestion of {FilePath}", filePath);
                    results.Add(new WebContentIngestionResult
                    {
                        FilePath = filePath,
                        Success = false,
                        ErrorMessage = $"Exception: {ex.Message}",
                        IngestionTimeMs = 0
                    });
                }

                processed++;
            }

            _logger.LogInformation("Completed ingestion scan. Results: {Ingested} ingested, {Skipped} skipped, {Errors} errors",
                results.Count(r => r.Success), results.Count(r => !r.Success && string.IsNullOrEmpty(r.ErrorMessage)), results.Count(r => !r.Success && !string.IsNullOrEmpty(r.ErrorMessage)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning content store for ingestion");
        }

        return results;
    }

    public async Task StartMonitoringAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogDebug("Web content ingestion is disabled, not starting monitoring");
            return;
        }

        if (!_options.EnableAutoMonitoring)
        {
            _logger.LogDebug("Auto-monitoring is disabled, not starting file watcher");
            return;
        }

        if (_isMonitoring)
        {
            _logger.LogDebug("Monitoring is already active");
            return;
        }

        var storageDir = _contentStore.GetStorageDirectory();

        if (!Directory.Exists(storageDir))
        {
            _logger.LogWarning("Content store directory does not exist, creating: {StorageDirectory}", storageDir);
            try
            {
                Directory.CreateDirectory(storageDir);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create content store directory");
                return;
            }
        }

        try
        {
            // Dispose previous instances before reassignment
            _monitoringCts?.Dispose();
            _watcher?.Dispose();

            _monitoringCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _watcher = new FileSystemWatcher(storageDir, "*.md")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                IncludeSubdirectories = true
            };

            _watcher.Created += OnFileCreated;
            _watcher.Changed += OnFileChanged;
            _watcher.Error += OnWatcherError;

            _watcher.EnableRaisingEvents = true;
            _isMonitoring = true;

            _logger.LogInformation("Started monitoring content store directory: {StorageDirectory}", storageDir);

            // Run monitoring task that completes when cancellation is requested
            _monitoringTask = Task.Delay(Timeout.Infinite, _monitoringCts.Token).ContinueWith(_ =>
            {
                _logger.LogInformation("Monitoring task completed");
            }, TaskScheduler.Default);

            await _monitoringTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting monitoring");
            _isMonitoring = false;
            throw;
        }
    }

    public async Task StopMonitoringAsync()
    {
        if (!_isMonitoring)
        {
            return;
        }

        _logger.LogInformation("Stopping web content ingestion monitoring");

        try
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Dispose();
                _watcher = null;
            }

            _monitoringCts?.Cancel();
            _monitoringCts?.Dispose();

            if (_monitoringTask != null)
            {
                try
                {
                    await _monitoringTask;
                }
                catch (OperationCanceledException)
                {
                    // Expected
                }
            }

            _isMonitoring = false;
            _logger.LogInformation("Stopped monitoring");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping monitoring");
        }
    }

    public WebContentIngestionStatistics GetStatistics()
    {
        return new WebContentIngestionStatistics
        {
            TotalFilesDetected = _totalFilesDetected,
            FilesIngested = _filesIngested,
            FilesSkipped = _filesSkipped,
            FilesWithErrors = _filesWithErrors,
            TotalChunksCreated = (int)_totalChunksCreated,
            TotalTokensProcessed = (int)_totalTokensProcessed,
            TotalIngestionTimeMs = _totalIngestionTimeMs,
            IsMonitoring = _isMonitoring
        };
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            if (_isMonitoring)
            {
                try
                {
                    StopMonitoringAsync().GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error during disposal while stopping monitoring");
                }
            }

            _watcher?.Dispose();
            _monitoringCts?.Dispose();
            _concurrencyLimiter?.Dispose();
        }

        _disposed = true;
    }

    private void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        if (e.FullPath.EndsWith(".metadata.json", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _logger.LogDebug("Detected new markdown file: {FilePath}", e.FullPath);

        // Delay to avoid processing incomplete files
        _ = Task.Delay(_options.FileDetectionDelayMs).ContinueWith(async _ =>
        {
            try
            {
                await _concurrencyLimiter.WaitAsync();
                try
                {
                    if (File.Exists(e.FullPath))
                    {
                        await IngestContentAsync(e.FullPath, null);
                    }
                }
                finally
                {
                    _concurrencyLimiter.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing detected file: {FilePath}", e.FullPath);
            }
        });
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        if (e.FullPath.EndsWith(".metadata.json", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _logger.LogDebug("Detected changed markdown file: {FilePath}", e.FullPath);

        // For changed files, only reprocess if skip-unchanged is disabled
        if (!_options.SkipAlreadyIngestedFiles)
        {
            // Remove from cache to force reprocessing
            _ingestedFiles.TryRemove(GetFileKey(e.FullPath), out _);

            _ = Task.Delay(_options.FileDetectionDelayMs).ContinueWith(async _ =>
            {
                try
                {
                    await _concurrencyLimiter.WaitAsync();
                    try
                    {
                        if (File.Exists(e.FullPath))
                        {
                            await IngestContentAsync(e.FullPath, null);
                        }
                    }
                    finally
                    {
                        _concurrencyLimiter.Release();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing changed file: {FilePath}", e.FullPath);
                }
            });
        }
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        var exception = e.GetException();
        if (exception != null)
        {
            _logger.LogError(exception, "FileSystemWatcher error");
        }
    }

    private string? ExtractSourceUrlFromMetadata(string filePath)
    {
        try
        {
            var metadataPath = Path.Combine(
                Path.GetDirectoryName(filePath) ?? string.Empty,
                Path.GetFileNameWithoutExtension(filePath) + ".metadata.json");

            if (File.Exists(metadataPath))
            {
                var json = File.ReadAllText(metadataPath);
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("sourceUrl", out var urlElement))
                {
                    return urlElement.GetString();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to extract source URL from metadata");
        }

        return null;
    }

    private string GetFileKey(string filePath)
    {
        return filePath.ToLowerInvariant();
    }

    private string FormatFetchDate(DateTime utcDateTime)
    {
        return utcDateTime.ToString("o"); // ISO 8601 format
    }
}
