namespace Daiv3.Knowledge;

/// <summary>
/// Orchestrates automatic document processing based on file system changes.
/// Monitors files via IFileSystemWatcher and triggers document processing
/// (indexing, updating, deletion) via IKnowledgeDocumentProcessor.
/// </summary>
public interface IKnowledgeFileOrchestrationService : IAsyncDisposable
{
    /// <summary>
    /// Starts monitoring files and processing changes automatically.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops monitoring and processing file changes.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets whether the orchestration service is currently running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Gets statistics about processed files.
    /// </summary>
    KnowledgeFileOrchestrationStatistics GetStatistics();
}

/// <summary>
/// Statistics for knowledge file orchestration operations.
/// </summary>
public sealed class KnowledgeFileOrchestrationStatistics
{
    /// <summary>
    /// Total files processed (created/modified).
    /// </summary>
    public int FilesProcessed { get; set; }

    /// <summary>
    /// Total files deleted.
    /// </summary>
    public int FilesDeleted { get; set; }

    /// <summary>
    /// Total processing errors.
    /// </summary>
    public int ProcessingErrors { get; set; }

    /// <summary>
    /// Total deletion errors.
    /// </summary>
    public int DeletionErrors { get; set; }
}
