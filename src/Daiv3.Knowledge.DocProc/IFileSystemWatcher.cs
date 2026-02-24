namespace Daiv3.Knowledge.DocProc;

/// <summary>
/// Event arguments for file system change notifications.
/// </summary>
public sealed class FileSystemChangeEventArgs : EventArgs
{
    /// <summary>
    /// Gets the full path of the file that changed.
    /// </summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the type of change that occurred.
    /// </summary>
    public FileChangeType ChangeType { get; init; }

    /// <summary>
    /// Gets the old path if the file was renamed.
    /// </summary>
    public string? OldFilePath { get; init; }

    /// <summary>
    /// Gets the timestamp when the change was detected.
    /// </summary>
    public DateTimeOffset DetectedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Type of file system change.
/// </summary>
public enum FileChangeType
{
    /// <summary>
    /// A new file was created.
    /// </summary>
    Created,

    /// <summary>
    /// An existing file was modified.
    /// </summary>
    Modified,

    /// <summary>
    /// A file was deleted.
    /// </summary>
    Deleted,

    /// <summary>
    /// A file was renamed.
    /// </summary>
    Renamed
}

/// <summary>
/// Monitors file system directories for changes to trigger document processing.
/// </summary>
public interface IFileSystemWatcher : IAsyncDisposable
{
    /// <summary>
    /// Event raised when a file is created, modified, deleted, or renamed.
    /// </summary>
    event EventHandler<FileSystemChangeEventArgs>? FileChanged;

    /// <summary>
    /// Event raised when an error occurs during file monitoring.
    /// </summary>
    event EventHandler<ErrorEventArgs>? Error;

    /// <summary>
    /// Starts monitoring the configured directories.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    Task StartAsync(CancellationToken ct = default);

    /// <summary>
    /// Stops monitoring all directories.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    Task StopAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets whether the watcher is currently running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Adds a directory to the watch list.
    /// </summary>
    /// <param name="path">Directory path to watch</param>
    /// <param name="recursive">Whether to watch subdirectories</param>
    void AddWatchPath(string path, bool recursive = true);

    /// <summary>
    /// Removes a directory from the watch list.
    /// </summary>
    /// <param name="path">Directory path to stop watching</param>
    void RemoveWatchPath(string path);

    /// <summary>
    /// Gets all currently watched paths.
    /// </summary>
    IReadOnlyList<string> GetWatchedPaths();
}
