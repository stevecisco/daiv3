namespace Daiv3.Knowledge.DocProc;

/// <summary>
/// Configuration options for file system monitoring.
/// </summary>
public sealed class FileSystemWatcherOptions
{
    /// <summary>
    /// Gets or sets the directories to watch for changes.
    /// Key: Directory path, Value: Whether to watch subdirectories recursively
    /// </summary>
    public Dictionary<string, bool> WatchPaths { get; set; } = new();

    /// <summary>
    /// Gets or sets file patterns to include (e.g., "*.pdf", "*.docx").
    /// Empty list means all files are included.
    /// </summary>
    public List<string> IncludePatterns { get; set; } = new()
    {
        "*.pdf",
        "*.docx",
        "*.doc",
        "*.html",
        "*.htm",
        "*.md",
        "*.txt",
        "*.cs",
        "*.js",
        "*.ts",
        "*.py",
        "*.java",
        "*.cpp",
        "*.c",
        "*.h",
        "*.json",
        "*.xml",
        "*.yaml",
        "*.yml"
    };

    /// <summary>
    /// Gets or sets directory patterns to exclude (e.g., "node_modules", ".git").
    /// </summary>
    public List<string> ExcludeDirectories { get; set; } = new()
    {
        "node_modules",
        ".git",
        ".vs",
        ".vscode",
        "bin",
        "obj",
        ".idea",
        "__pycache__",
        ".pytest_cache",
        "build",
        "dist",
        "target",
        ".gradle"
    };

    /// <summary>
    /// Gets or sets file patterns to exclude (e.g., "*.tmp", "*.log").
    /// </summary>
    public List<string> ExcludePatterns { get; set; } = new()
    {
        "*.tmp",
        "*.temp",
        "*.log",
        "*.bak",
        "*.swp",
        "*.swo",
        "*~",
        ".DS_Store",
        "Thumbs.db"
    };

    /// <summary>
    /// Gets or sets the debounce delay in milliseconds to wait before processing a change.
    /// This prevents processing the same file multiple times when it receives rapid changes.
    /// Default: 500ms
    /// </summary>
    public int DebounceDelayMs { get; set; } = 500;

    /// <summary>
    /// Gets or sets whether to automatically start watching when the service starts.
    /// Default: false (manual start required)
    /// </summary>
    public bool AutoStart { get; set; } = false;

    /// <summary>
    /// Gets or sets the maximum file size in bytes to process.
    /// Files larger than this will be skipped.
    /// Default: 100MB
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 100 * 1024 * 1024; // 100MB

    /// <summary>
    /// Gets or sets whether to process existing files on startup.
    /// Default: false
    /// </summary>
    public bool ProcessExistingFilesOnStart { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to enable detailed logging of file system events.
    /// Default: false
    /// </summary>
    public bool EnableVerboseLogging { get; set; } = false;

    /// <summary>
    /// Validates the configuration options.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if configuration is invalid</exception>
    public void Validate()
    {
        // Validate basic constraints first
        if (DebounceDelayMs < 0)
        {
            throw new InvalidOperationException("DebounceDelayMs must be non-negative.");
        }

        if (MaxFileSizeBytes <= 0)
        {
            throw new InvalidOperationException("MaxFileSizeBytes must be positive.");
        }

        // Validate watch paths if any are configured
        if (WatchPaths.Count == 0 && !AutoStart)
        {
            // No paths configured is valid if AutoStart is false (paths can be added later)
            return;
        }

        foreach (var (path, _) in WatchPaths)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new InvalidOperationException("Watch path cannot be empty or whitespace.");
            }

            if (!Path.IsPathRooted(path))
            {
                throw new InvalidOperationException($"Watch path must be an absolute path: {path}");
            }
        }
    }
}
