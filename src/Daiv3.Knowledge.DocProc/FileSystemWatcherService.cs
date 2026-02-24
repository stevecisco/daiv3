using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Daiv3.Knowledge.DocProc;

/// <summary>
/// Monitors file system directories for changes to trigger document processing.
/// Implements debouncing to avoid processing the same file multiple times on rapid changes.
/// </summary>
public sealed class FileSystemWatcherService : IFileSystemWatcher
{
    private readonly ILogger<FileSystemWatcherService> _logger;
    private readonly FileSystemWatcherOptions _options;
    private readonly ConcurrentDictionary<string, System.IO.FileSystemWatcher> _watchers = new();
    private readonly ConcurrentDictionary<string, Timer> _debounceTimers = new();
    private readonly ConcurrentDictionary<string, FileSystemChangeEventArgs> _pendingChanges = new();
    private bool _isRunning;
    private bool _disposed;

    /// <inheritdoc/>
    public event EventHandler<FileSystemChangeEventArgs>? FileChanged;

    /// <inheritdoc/>
    public event EventHandler<ErrorEventArgs>? Error;

    /// <inheritdoc/>
    public bool IsRunning => _isRunning;

    public FileSystemWatcherService(
        ILogger<FileSystemWatcherService> logger,
        IOptions<FileSystemWatcherOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken ct = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(FileSystemWatcherService));
        }

        if (_isRunning)
        {
            _logger.LogWarning("File system watcher is already running");
            return Task.CompletedTask;
        }

        _logger.LogInformation("Starting file system watcher service");

        _isRunning = true;

        // Start watching all configured paths
        foreach (var (path, recursive) in _options.WatchPaths)
        {
            AddWatchPath(path, recursive);
        }

        _logger.LogInformation("File system watcher service started. Watching {PathCount} paths", _watchers.Count);

        // Process existing files if configured
        if (_options.ProcessExistingFilesOnStart)
        {
            _ = Task.Run(() => ProcessExistingFilesAsync(ct), ct);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken ct = default)
    {
        if (!_isRunning)
        {
            return Task.CompletedTask;
        }

        _logger.LogInformation("Stopping file system watcher service");

        foreach (var watcher in _watchers.Values)
        {
            watcher.EnableRaisingEvents = false;
        }

        _isRunning = false;

        _logger.LogInformation("File system watcher service stopped");

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void AddWatchPath(string path, bool recursive = true)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(FileSystemWatcherService));
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be empty or whitespace", nameof(path));
        }

        if (!Path.IsPathRooted(path))
        {
            throw new ArgumentException($"Path must be absolute: {path}", nameof(path));
        }

        if (!Directory.Exists(path))
        {
            _logger.LogWarning("Directory does not exist, will not watch: {Path}", path);
            return;
        }

        if (_watchers.ContainsKey(path))
        {
            _logger.LogInformation("Path is already being watched: {Path}", path);
            return;
        }

        try
        {
            var watcher = new System.IO.FileSystemWatcher(path)
            {
                IncludeSubdirectories = recursive,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = _isRunning
            };

            // Set up event handlers
            watcher.Created += OnFileCreated;
            watcher.Changed += OnFileChanged;
            watcher.Deleted += OnFileDeleted;
            watcher.Renamed += OnFileRenamed;
            watcher.Error += OnError;

            _watchers[path] = watcher;

            _logger.LogInformation("Now watching path: {Path} (recursive: {Recursive})", path, recursive);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add watch path: {Path}", path);
            throw;
        }
    }

    /// <inheritdoc/>
    public void RemoveWatchPath(string path)
    {
        if (_watchers.TryRemove(path, out var watcher))
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
            _logger.LogInformation("Stopped watching path: {Path}", path);
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetWatchedPaths()
    {
        return _watchers.Keys.ToList();
    }

    private void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        if (_options.EnableVerboseLogging)
        {
            _logger.LogDebug("File created: {FilePath}", e.FullPath);
        }

        if (!ShouldProcessFile(e.FullPath))
        {
            return;
        }

        var eventArgs = new FileSystemChangeEventArgs
        {
            FilePath = e.FullPath,
            ChangeType = FileChangeType.Created
        };

        DebounceAndRaiseEvent(e.FullPath, eventArgs);
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        if (_options.EnableVerboseLogging)
        {
            _logger.LogDebug("File changed: {FilePath}", e.FullPath);
        }

        if (!ShouldProcessFile(e.FullPath))
        {
            return;
        }

        var eventArgs = new FileSystemChangeEventArgs
        {
            FilePath = e.FullPath,
            ChangeType = FileChangeType.Modified
        };

        DebounceAndRaiseEvent(e.FullPath, eventArgs);
    }

    private void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        if (_options.EnableVerboseLogging)
        {
            _logger.LogDebug("File deleted: {FilePath}", e.FullPath);
        }

        if (!ShouldProcessFile(e.FullPath))
        {
            return;
        }

        var eventArgs = new FileSystemChangeEventArgs
        {
            FilePath = e.FullPath,
            ChangeType = FileChangeType.Deleted
        };

        // Don't debounce delete events - process immediately
        RaiseFileChangedEvent(eventArgs);
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        if (_options.EnableVerboseLogging)
        {
            _logger.LogDebug("File renamed: {OldPath} -> {NewPath}", e.OldFullPath, e.FullPath);
        }

        if (!ShouldProcessFile(e.FullPath) && !ShouldProcessFile(e.OldFullPath))
        {
            return;
        }

        var eventArgs = new FileSystemChangeEventArgs
        {
            FilePath = e.FullPath,
            OldFilePath = e.OldFullPath,
            ChangeType = FileChangeType.Renamed
        };

        RaiseFileChangedEvent(eventArgs);
    }

    private void OnError(object sender, ErrorEventArgs e)
    {
        _logger.LogError(e.GetException(), "File system watcher error occurred");
        Error?.Invoke(this, e);
    }

    private bool ShouldProcessFile(string filePath)
    {
        try
        {
            // Check if file exists (for non-delete operations)
            if (File.Exists(filePath))
            {
                var fileInfo = new FileInfo(filePath);

                // Check file size
                if (fileInfo.Length > _options.MaxFileSizeBytes)
                {
                    _logger.LogWarning("File exceeds max size ({Size} bytes), skipping: {FilePath}",
                        fileInfo.Length, filePath);
                    return false;
                }
            }

            var fileName = Path.GetFileName(filePath);
            var directory = Path.GetDirectoryName(filePath) ?? string.Empty;

            // Check if in excluded directory
            if (_options.ExcludeDirectories.Any(excluded =>
                directory.Contains(Path.DirectorySeparatorChar + excluded + Path.DirectorySeparatorChar) ||
                directory.EndsWith(Path.DirectorySeparatorChar + excluded)))
            {
                if (_options.EnableVerboseLogging)
                {
                    _logger.LogDebug("File in excluded directory, skipping: {FilePath}", filePath);
                }
                return false;
            }

            // Check if file matches exclude patterns
            if (_options.ExcludePatterns.Any(pattern =>
                MatchesPattern(fileName, pattern)))
            {
                if (_options.EnableVerboseLogging)
                {
                    _logger.LogDebug("File matches exclude pattern, skipping: {FilePath}", filePath);
                }
                return false;
            }

            // Check if file matches include patterns (if any specified)
            if (_options.IncludePatterns.Count > 0 &&
                !_options.IncludePatterns.Any(pattern => MatchesPattern(fileName, pattern)))
            {
                if (_options.EnableVerboseLogging)
                {
                    _logger.LogDebug("File does not match any include patterns, skipping: {FilePath}", filePath);
                }
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking if file should be processed: {FilePath}", filePath);
            return false;
        }
    }

    private static bool MatchesPattern(string fileName, string pattern)
    {
        // Simple wildcard matching (* and ?)
        var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";

        return System.Text.RegularExpressions.Regex.IsMatch(fileName, regexPattern,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private void DebounceAndRaiseEvent(string filePath, FileSystemChangeEventArgs eventArgs)
    {
        // Store the latest event for this file
        _pendingChanges[filePath] = eventArgs;

        // Cancel existing timer if any
        if (_debounceTimers.TryRemove(filePath, out var existingTimer))
        {
            existingTimer.Dispose();
        }

        // Create new timer
        var timer = new Timer(
            callback: _ =>
            {
                if (_pendingChanges.TryRemove(filePath, out var pendingEvent))
                {
                    RaiseFileChangedEvent(pendingEvent);
                }

                if (_debounceTimers.TryRemove(filePath, out var t))
                {
                    t.Dispose();
                }
            },
            state: null,
            dueTime: _options.DebounceDelayMs,
            period: Timeout.Infinite);

        _debounceTimers[filePath] = timer;
    }

    private void RaiseFileChangedEvent(FileSystemChangeEventArgs eventArgs)
    {
        try
        {
            _logger.LogInformation("File {ChangeType}: {FilePath}",
                eventArgs.ChangeType, eventArgs.FilePath);

            FileChanged?.Invoke(this, eventArgs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error raising file changed event for: {FilePath}", eventArgs.FilePath);
        }
    }

    private async Task ProcessExistingFilesAsync(CancellationToken ct)
    {
        _logger.LogInformation("Processing existing files in watched directories");

        var processedCount = 0;

        foreach (var (path, recursive) in _options.WatchPaths)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    continue;
                }

                var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

                foreach (var pattern in _options.IncludePatterns)
                {
                    if (ct.IsCancellationRequested)
                    {
                        break;
                    }

                    foreach (var file in Directory.EnumerateFiles(path, pattern, searchOption))
                    {
                        if (ct.IsCancellationRequested)
                        {
                            break;
                        }

                        if (ShouldProcessFile(file))
                        {
                            var eventArgs = new FileSystemChangeEventArgs
                            {
                                FilePath = file,
                                ChangeType = FileChangeType.Created
                            };

                            RaiseFileChangedEvent(eventArgs);
                            processedCount++;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing existing files in directory: {Path}", path);
            }
        }

        _logger.LogInformation("Finished processing {Count} existing files", processedCount);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await StopAsync().ConfigureAwait(false);

        // Dispose all timers
        foreach (var timer in _debounceTimers.Values)
        {
            timer.Dispose();
        }
        _debounceTimers.Clear();

        // Dispose all watchers
        foreach (var watcher in _watchers.Values)
        {
            watcher.Created -= OnFileCreated;
            watcher.Changed -= OnFileChanged;
            watcher.Deleted -= OnFileDeleted;
            watcher.Renamed -= OnFileRenamed;
            watcher.Error -= OnError;
            watcher.Dispose();
        }
        _watchers.Clear();

        _pendingChanges.Clear();

        _disposed = true;

        _logger.LogInformation("File system watcher service disposed");
    }
}
