using Daiv3.Persistence.Entities;

namespace Daiv3.Persistence.Repositories;

/// <summary>
/// Repository interface for managing WebFetch entities.
/// Provides data access for web fetch metadata including source URL, fetch date, and content hash.
/// Implements WFC-DATA-001: Metadata SHALL include source URL, fetch date, and content hash.
/// </summary>
public interface IWebFetchRepository : IRepository<WebFetch>
{
    /// <summary>
    /// Gets a web fetch by source URL.
    /// </summary>
    /// <param name="sourceUrl">The source URL to search for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The web fetch record if found; null otherwise.</returns>
    Task<WebFetch?> GetBySourceUrlAsync(string sourceUrl, CancellationToken ct = default);

    /// <summary>
    /// Gets all web fetch records for a specific document.
    /// </summary>
    /// <param name="docId">The document ID to search for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All web fetch records for the document.</returns>
    Task<IReadOnlyList<WebFetch>> GetByDocIdAsync(string docId, CancellationToken ct = default);

    /// <summary>
    /// Gets web fetch records by status.
    /// </summary>
    /// <param name="status">The status to filter by (active, stale, error, deleted).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All web fetch records with the specified status.</returns>
    Task<IReadOnlyList<WebFetch>> GetByStatusAsync(string status, CancellationToken ct = default);

    /// <summary>
    /// Gets web fetch records that were fetched before a certain date.
    /// Useful for identifying stale content that needs refetch.
    /// </summary>
    /// <param name="beforeDate">Unix timestamp - fetch records before this date are returned.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All web fetch records fetched before the specified date.</returns>
    Task<IReadOnlyList<WebFetch>> GetFetchedBeforeDateAsync(long beforeDate, CancellationToken ct = default);

    /// <summary>
    /// Gets web fetch records fetched after a certain date.
    /// </summary>
    /// <param name="afterDate">Unix timestamp - fetch records after this date are returned.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All web fetch records fetched after the specified date.</returns>
    Task<IReadOnlyList<WebFetch>> GetFetchedAfterDateAsync(long afterDate, CancellationToken ct = default);

    /// <summary>
    /// Gets web fetch records by content hash.
    /// Useful for detecting duplicate content from different sources.
    /// </summary>
    /// <param name="contentHash">The content hash to search for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All web fetch records with the specified content hash.</returns>
    Task<IReadOnlyList<WebFetch>> GetByContentHashAsync(string contentHash, CancellationToken ct = default);

    /// <summary>
    /// Gets the most recent web fetch for a given source URL.
    /// </summary>
    /// <param name="sourceUrl">The source URL to search for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The most recent web fetch record for the URL, or null if not found.</returns>
    Task<WebFetch?> GetMostRecentBySourceUrlAsync(string sourceUrl, CancellationToken ct = default);

    /// <summary>
    /// Updates the status and error message for a web fetch.
    /// Used when a refetch attempt fails.
    /// </summary>
    /// <param name="webFetchId">The ID of the web fetch to update.</param>
    /// <param name="status">The new status value.</param>
    /// <param name="errorMessage">Optional error message if status is 'error'.</param>
    /// <param name="ct">Cancellation token.</param>
    Task UpdateStatusAsync(string webFetchId, string status, string? errorMessage = null, CancellationToken ct = default);

    /// <summary>
    /// Updates content hash and fetch date for a web fetch (refetch operation).
    /// </summary>
    /// <param name="webFetchId">The ID of the web fetch to update.</param>
    /// <param name="newContentHash">The new content hash after refetch.</param>
    /// <param name="newFetchDate">The new fetch date (Unix timestamp).</param>
    /// <param name="ct">Cancellation token.</param>
    Task UpdateContentAsync(string webFetchId, string newContentHash, long newFetchDate, CancellationToken ct = default);
}
