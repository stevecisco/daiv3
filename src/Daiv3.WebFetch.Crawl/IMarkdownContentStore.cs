namespace Daiv3.WebFetch.Crawl;

/// <summary>
/// Represents metadata for stored Markdown content.
/// </summary>
public record StoredContentMetadata
{
    /// <summary>
    /// Gets the unique identifier for the stored content (typically a filename without extension).
    /// </summary>
    public required string ContentId { get; init; }

    /// <summary>
    /// Gets the source URL from which the content was fetched.
    /// </summary>
    public required string SourceUrl { get; init; }

    /// <summary>
    /// Gets the timestamp when the content was fetched.
    /// </summary>
    public required DateTime FetchedAt { get; init; }

    /// <summary>
    /// Gets the SHA256 hash of the content for change detection.
    /// </summary>
    public required string ContentHash { get; init; }

    /// <summary>
    /// Gets the file path where the content is stored.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Gets the size of the Markdown content in bytes.
    /// </summary>
    public long ContentSizeBytes { get; init; }

    /// <summary>
    /// Gets the timestamp when the content was stored.
    /// </summary>
    public DateTime StoredAt { get; init; }

    /// <summary>
    /// Gets an optional title extracted from the content or provided by the caller.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Gets an optional description of the content.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets an optional tag list for categorization.
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = [];
}

/// <summary>
/// Represents a result of storing Markdown content.
/// </summary>
public record StoreContentResult
{
    /// <summary>
    /// Gets the metadata of the stored content.
    /// </summary>
    public required StoredContentMetadata Metadata { get; init; }

    /// <summary>
    /// Gets a value indicating whether the content was newly stored or was an update.
    /// </summary>
    public bool IsNew { get; init; }

    /// <summary>
    /// Gets an optional message providing details about the operation.
    /// </summary>
    public string? Message { get; init; }
}

/// <summary>
/// Represents options for retrieving stored content.
/// </summary>
public record RetrieveContentOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to include the full content in the result.
    /// </summary>
    public bool IncludeContent { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to include metadata.
    /// </summary>
    public bool IncludeMetadata { get; set; } = true;
}

/// <summary>
/// Represents retrieved Markdown content with optional metadata.
/// </summary>
public record RetrievedContent
{
    /// <summary>
    /// Gets the unique identifier for the content.
    /// </summary>
    public required string ContentId { get; init; }

    /// <summary>
    /// Gets the Markdown content (may be null if not requested).
    /// </summary>
    public string? MarkdownContent { get; init; }

    /// <summary>
    /// Gets the metadata (may be null if not requested).
    /// </summary>
    public StoredContentMetadata? Metadata { get; init; }
}

/// <summary>
/// Interface for storing and retrieving Markdown content from fetched web pages.
/// </summary>
/// <remarks>
/// Provides functionality to persist HTML content converted to Markdown format,
/// with full metadata tracking including source URL, fetch date, and content hash.
/// </remarks>
public interface IMarkdownContentStore
{
    /// <summary>
    /// Stores Markdown content to disk with metadata.
    /// </summary>
    /// <param name="sourceUrl">The source URL from which the content was fetched.</param>
    /// <param name="markdownContent">The Markdown content to store.</param>
    /// <param name="title">Optional title for the content.</param>
    /// <param name="description">Optional description of the content.</param>
    /// <param name="tags">Optional tags for categorization.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that returns the result of the store operation, including metadata.</returns>
    /// <exception cref="ArgumentNullException">Thrown when sourceUrl or markdownContent is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when storage directory is not writable or another I/O error occurs.</exception>
    Task<StoreContentResult> StoreAsync(
        string sourceUrl,
        string markdownContent,
        string? title = null,
        string? description = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves stored Markdown content by its content ID.
    /// </summary>
    /// <param name="contentId">The unique identifier of the content to retrieve.</param>
    /// <param name="options">Options controlling what information to retrieve.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that returns the retrieved content, or null if not found.</returns>
    Task<RetrievedContent?> RetrieveAsync(
        string contentId,
        RetrieveContentOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all stored content metadata.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that returns a collection of all stored content metadata.</returns>
    Task<IEnumerable<StoredContentMetadata>> ListAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes stored content by its content ID.
    /// </summary>
    /// <param name="contentId">The unique identifier of the content to delete.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. Returns true if deleted, false if not found.</returns>
    Task<bool> DeleteAsync(string contentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if content with the given ID exists in the store.
    /// </summary>
    /// <param name="contentId">The unique identifier to check.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that returns true if the content exists, false otherwise.</returns>
    Task<bool> ExistsAsync(string contentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the configured storage directory.
    /// </summary>
    /// <returns>The full path to the storage directory.</returns>
    string GetStorageDirectory();
}
