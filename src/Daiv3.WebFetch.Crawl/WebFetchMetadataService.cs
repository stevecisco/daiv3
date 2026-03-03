using System.Security.Cryptography;
using System.Text;
using Daiv3.Persistence.Entities;
using Daiv3.Persistence.Repositories;
using Microsoft.Extensions.Logging;

namespace Daiv3.WebFetch.Crawl;

/// <summary>
/// Implements web fetch metadata storage with SHA256 content hashing.
/// Persists source URL, fetch date, and content hash to the database.
/// </summary>
/// <remarks>
/// Implements WFC-REQ-007: The system SHALL store source URL and fetch date as metadata.
/// Also supports WFC-DATA-001 requirements (content hash for change detection).
/// </remarks>
public class WebFetchMetadataService : IWebFetchMetadataService
{
    private readonly IWebFetchRepository _webFetchRepository;
    private readonly ILogger<WebFetchMetadataService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebFetchMetadataService"/> class.
    /// </summary>
    /// <param name="webFetchRepository">The repository for persisting web fetch metadata.</param>
    /// <param name="logger">The logger instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public WebFetchMetadataService(
        IWebFetchRepository webFetchRepository,
        ILogger<WebFetchMetadataService> logger)
    {
        _webFetchRepository = webFetchRepository ?? throw new ArgumentNullException(nameof(webFetchRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

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
    public async Task<WebFetchMetadata> StoreMetadataAsync(
        string sourceUrl,
        string docId,
        string htmlContent,
        string? title = null,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceUrl))
            throw new ArgumentNullException(nameof(sourceUrl));
        if (string.IsNullOrWhiteSpace(docId))
            throw new ArgumentNullException(nameof(docId));
        if (string.IsNullOrWhiteSpace(htmlContent))
            throw new ArgumentException("HTML content cannot be empty.", nameof(htmlContent));

        try
        {
            _logger.LogInformation("Storing metadata for URL: {SourceUrl}, DocId: {DocId}", sourceUrl, docId);

            // Calculate content hash
            var contentHash = CalculateContentHash(htmlContent);
            _logger.LogDebug("Calculated content hash: {ContentHash} for {SourceUrl}", contentHash, sourceUrl);

            // Create the web fetch record
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var webFetch = new Daiv3.Persistence.Entities.WebFetch
            {
                WebFetchId = Guid.NewGuid().ToString(),
                SourceUrl = sourceUrl,
                DocId = docId,
                ContentHash = contentHash,
                FetchDate = now,
                Title = title,
                Description = description,
                Status = "active",
                CreatedAt = now,
                UpdatedAt = now
            };

            // Persist to database
            _logger.LogDebug("Persisting WebFetch record: {WebFetchId}", webFetch.WebFetchId);
            await _webFetchRepository.AddAsync(webFetch, cancellationToken);

            _logger.LogInformation(
                "Successfully stored metadata for {SourceUrl} with hash {ContentHash}",
                sourceUrl,
                contentHash);

            // Return the stored metadata
            return new WebFetchMetadata
            {
                WebFetchId = webFetch.WebFetchId,
                SourceUrl = webFetch.SourceUrl,
                ContentHash = webFetch.ContentHash,
                FetchDate = webFetch.FetchDate,
                Title = webFetch.Title,
                Description = webFetch.Description,
                Status = webFetch.Status,
                DocId = webFetch.DocId
            };
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid argument when storing metadata for {SourceUrl}", sourceUrl);
            throw;
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "Metadata storage operation cancelled for {SourceUrl}", sourceUrl);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing metadata for {SourceUrl}", sourceUrl);
            throw new InvalidOperationException($"Failed to store metadata for {sourceUrl}", ex);
        }
    }

    /// <summary>
    /// Calculates the SHA256 hash of HTML content.
    /// Used for detecting content changes on refetch operations.
    /// </summary>
    /// <param name="content">The content to hash.</param>
    /// <returns>The SHA256 hash as a hexadecimal string.</returns>
    public string CalculateContentHash(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Content cannot be empty.", nameof(content));

        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
        return BitConverter.ToString(hashedBytes).Replace("-", "").ToLowerInvariant();
    }
}
