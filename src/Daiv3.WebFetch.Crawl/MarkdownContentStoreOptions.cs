namespace Daiv3.WebFetch.Crawl;

/// <summary>
/// Configuration options for the Markdown content store.
/// </summary>
public class MarkdownContentStoreOptions
{
    /// <summary>
    /// Gets or sets the root directory where Markdown content will be stored.
    /// Defaults to %LOCALAPPDATA%\Daiv3\content\markdown.
    /// </summary>
    public string StorageDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Daiv3",
        "content",
        "markdown");

    /// <summary>
    /// Gets or sets the maximum size in bytes for a single Markdown file.
    /// Defaults to 10 MB (10,485,760 bytes).
    /// </summary>
    public long MaxContentSizeBytes { get; set; } = 10 * 1024 * 1024; // 10 MB

    /// <summary>
    /// Gets or sets a value indicating whether to create subdirectories based on domain.
    /// When true, content from example.com will be stored in storage/example.com/...
    /// Defaults to true.
    /// </summary>
    public bool OrganizeByDomain { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to store metadata in sidecar JSON files.
    /// When true, each Markdown file will have a corresponding .metadata.json file.
    /// Defaults to true.
    /// </summary>
    public bool StoreSidecarMetadata { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to automatically create the storage directory if it doesn't exist.
    /// Defaults to true.
    /// </summary>
    public bool CreateDirectoryIfNotExists { get; set; } = true;

    /// <summary>
    /// Gets or sets the encoding to use when writing Markdown content.
    /// Defaults to UTF-8.
    /// </summary>
    public System.Text.Encoding ContentEncoding { get; set; } = System.Text.Encoding.UTF8;

    /// <summary>
    /// Gets or sets a value indicating whether to include a front matter block with metadata at the start of Markdown files.
    /// When true, files will start with YAML front matter containing metadata.
    /// Defaults to true.
    /// </summary>
    public bool IncludeFrontMatter { get; set; } = true;

    /// <summary>
    /// Validates the configuration options.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when configuration is invalid.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(StorageDirectory))
        {
            throw new ArgumentException("StorageDirectory cannot be null or empty.", nameof(StorageDirectory));
        }

        if (MaxContentSizeBytes <= 0)
        {
            throw new ArgumentException("MaxContentSizeBytes must be greater than 0.", nameof(MaxContentSizeBytes));
        }

        if (ContentEncoding == null)
        {
            throw new ArgumentException("ContentEncoding cannot be null.", nameof(ContentEncoding));
        }
    }
}
