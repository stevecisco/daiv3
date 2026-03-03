namespace Daiv3.WebFetch.Crawl;

/// <summary>
/// Represents metadata storage record for a web fetch operation.
/// </summary>
public record WebFetchMetadata
{
    /// <summary>
    /// Gets the unique identifier for this web fetch record.
    /// </summary>
    public required string WebFetchId { get; init; }

    /// <summary>
    /// Gets the source URL that was fetched.
    /// </summary>
    public required string SourceUrl { get; init; }

    /// <summary>
    /// Gets the content hash (SHA256) of the fetched content.
    /// Used for change detection on refetch.
    /// </summary>
    public required string ContentHash { get; init; }

    /// <summary>
    /// Gets the fetch date as Unix timestamp (seconds since epoch, UTC).
    /// </summary>
    public required long FetchDate { get; init; }

    /// <summary>
    /// Gets the page title extracted from the content (optional).
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Gets the page description extracted from the content (optional).
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the current status of the web fetch (active, stale, error, deleted).
    /// </summary>
    public string Status { get; init; } = "active";

    /// <summary>
    /// Gets the document ID this web fetch is associated with.
    /// </summary>
    public required string DocId { get; init; }
}

/// <summary>
/// Interface for managing web fetch metadata storage.
/// Handles storing source URLs, fetch dates, and content hashes for web fetched content.
/// Implements WFC-REQ-007: The system SHALL store source URL and fetch date as metadata.
/// </summary>
/// <remarks>
/// This service calculates content hashes and persists web fetch metadata to the database
/// including source URL, fetch date, and content hash for change detection.
/// </remarks>
public interface IWebFetchMetadataService
{
    /// <summary>
    /// Stores metadata for a successfully fetched web page.
    /// Calculates content hash and persists all required metadata to the database.
    /// </summary>
    /// <param name="sourceUrl">The original URL that was fetched.</param>
    /// <param name="docId">The document ID associated with this fetch (foreign key to documents table).</param>
    /// <param name="htmlContent">The raw HTML content that was fetched.</param>
    /// <param name="title">Optional page title extracted from the content.</param>
    /// <param name="description">Optional page description extracted from the content.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the stored metadata.</returns>
    /// <remarks>
    /// This method:
    /// 1. Calculates SHA256 hash of the HTML content
    /// 2. Creates a WebFetch record with source URL, fetch date (now), and content hash
    /// 3. Persists the record to the database
    /// 4. Returns the stored metadata including the generated web_fetch_id
    ///
    /// Satisfies WFC-REQ-007 by storing:
    /// - Source URL (sourceUrl parameter)
    /// - Fetch date (current timestamp)
    /// - Content hash (SHA256 of htmlContent)
    /// </remarks>
    Task<WebFetchMetadata> StoreMetadataAsync(
        string sourceUrl,
        string docId,
        string htmlContent,
        string? title = null,
        string? description = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates the SHA256 hash of HTML content.
    /// Used for detecting content changes on refetch operations.
    /// </summary>
    /// <param name="content">The content to hash.</param>
    /// <returns>The SHA256 hash as a hexadecimal string.</returns>
    /// <remarks>
    /// This is a utility method used internally but also exposed for testing
    /// and potential direct hash calculation use cases.
    /// </remarks>
    string CalculateContentHash(string content);
}
