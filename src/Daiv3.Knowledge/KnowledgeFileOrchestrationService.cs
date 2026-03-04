using Daiv3.Knowledge.DocProc;
using Microsoft.Extensions.Logging;

namespace Daiv3.Knowledge;

/// <summary>
/// Orchestrates automatic document processing based on file system changes.
/// Monitors files via IFileSystemWatcher and triggers document processing
/// (indexing, updating, deletion) via IKnowledgeDocumentProcessor.
/// </summary>
public sealed class KnowledgeFileOrchestrationService : IKnowledgeFileOrchestrationService
{
    private readonly IFileSystemWatcher _fileSystemWatcher;
    private readonly IKnowledgeDocumentProcessor _documentProcessor;
    private readonly ILogger<KnowledgeFileOrchestrationService> _logger;
    private readonly SemaphoreSlim _processingLock = new(1, 1);

    private int _filesProcessed;
    private int _filesDeleted;
    private int _processingErrors;
    private int _deletionErrors;
    private bool _isRunning;
    private bool _disposed;

    public KnowledgeFileOrchestrationService(
        IFileSystemWatcher fileSystemWatcher,
        IKnowledgeDocumentProcessor documentProcessor,
        ILogger<KnowledgeFileOrchestrationService> logger)
    {
        _fileSystemWatcher = fileSystemWatcher ?? throw new ArgumentNullException(nameof(fileSystemWatcher));
        _documentProcessor = documentProcessor ?? throw new ArgumentNullException(nameof(documentProcessor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public bool IsRunning => _isRunning;

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(KnowledgeFileOrchestrationService));
        }

        if (_isRunning)
        {
            _logger.LogWarning("Knowledge file orchestration service is already running");
            return;
        }

        _logger.LogInformation("Starting knowledge file orchestration service");

        // Subscribe to file system watcher events
        _fileSystemWatcher.FileChanged += OnFileChanged;

        // Start the file system watcher if not already running
        if (!_fileSystemWatcher.IsRunning)
        {
            await _fileSystemWatcher.StartAsync(cancellationToken).ConfigureAwait(false);
        }

        _isRunning = true;

        _logger.LogInformation("Knowledge file orchestration service started");
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_isRunning)
        {
            return;
        }

        _logger.LogInformation("Stopping knowledge file orchestration service");

        // Unsubscribe from file system watcher events
        _fileSystemWatcher.FileChanged -= OnFileChanged;

        // Wait for any in-progress operations
        await _processingLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _isRunning = false;
        }
        finally
        {
            _processingLock.Release();
        }

        _logger.LogInformation("Knowledge file orchestration service stopped");
    }

    /// <inheritdoc/>
    public KnowledgeFileOrchestrationStatistics GetStatistics()
    {
        return new KnowledgeFileOrchestrationStatistics
        {
            FilesProcessed = _filesProcessed,
            FilesDeleted = _filesDeleted,
            ProcessingErrors = _processingErrors,
            DeletionErrors = _deletionErrors
        };
    }

    private void OnFileChanged(object? sender, FileSystemChangeEventArgs e)
    {
        if (!_isRunning)
        {
            return;
        }

        // Process file changes asynchronously without blocking the event handler
        _ = Task.Run(async () => await ProcessFileChangeAsync(e).ConfigureAwait(false));
    }

    private async Task ProcessFileChangeAsync(FileSystemChangeEventArgs args)
    {
        await _processingLock.WaitAsync().ConfigureAwait(false);
        try
        {
            switch (args.ChangeType)
            {
                case FileChangeType.Created:
                case FileChangeType.Modified:
                    await ProcessOrUpdateDocumentAsync(args.FilePath).ConfigureAwait(false);
                    break;

                case FileChangeType.Deleted:
                    await DeleteDocumentAsync(args.FilePath).ConfigureAwait(false);
                    break;

                case FileChangeType.Renamed:
                    // Handle rename as delete old + create new
                    if (!string.IsNullOrEmpty(args.OldFilePath))
                    {
                        await DeleteDocumentAsync(args.OldFilePath).ConfigureAwait(false);
                    }
                    await ProcessOrUpdateDocumentAsync(args.FilePath).ConfigureAwait(false);
                    break;

                default:
                    _logger.LogWarning("Unknown file change type: {ChangeType} for file: {FilePath}",
                        args.ChangeType, args.FilePath);
                    break;
            }
        }
        finally
        {
            _processingLock.Release();
        }
    }

    private async Task ProcessOrUpdateDocumentAsync(string filePath)
    {
        try
        {
            _logger.LogInformation("Processing document: {FilePath}", filePath);

            var result = await _documentProcessor.ProcessDocumentAsync(
                filePath,
                CancellationToken.None).ConfigureAwait(false);

            if (result.Success)
            {
                Interlocked.Increment(ref _filesProcessed);
                _logger.LogInformation(
                    "Successfully processed document: {FilePath} ({ChunkCount} chunks)",
                    filePath, result.ChunkCount);
            }
            else
            {
                Interlocked.Increment(ref _processingErrors);
                _logger.LogWarning(
                    "Failed to process document: {FilePath}. Error: {Error}",
                    filePath, result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _processingErrors);
            _logger.LogError(ex, "Error processing document: {FilePath}", filePath);
        }
    }

    private async Task DeleteDocumentAsync(string filePath)
    {
        try
        {
            _logger.LogInformation("Deleting document: {FilePath}", filePath);

            var success = await _documentProcessor.RemoveDocumentAsync(
                filePath,
                CancellationToken.None).ConfigureAwait(false);

            if (success)
            {
                Interlocked.Increment(ref _filesDeleted);
                _logger.LogInformation("Successfully deleted document: {FilePath}", filePath);
            }
            else
            {
                Interlocked.Increment(ref _deletionErrors);
                _logger.LogWarning("Failed to delete document (not found): {FilePath}", filePath);
            }
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _deletionErrors);
            _logger.LogError(ex, "Error deleting document: {FilePath}", filePath);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await StopAsync().ConfigureAwait(false);
        _processingLock.Dispose();
        _disposed = true;

        _logger.LogInformation("Knowledge file orchestration service disposed");
    }
}
