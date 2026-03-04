using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Daiv3.WebFetch.Crawl;

/// <summary>
/// Implementation of IMarkdownContentStore that stores content to the file system.
/// </summary>
internal class MarkdownContentStore : IMarkdownContentStore
{
    private readonly MarkdownContentStoreOptions _options;
    private readonly ILogger<MarkdownContentStore> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public MarkdownContentStore(
        IOptions<MarkdownContentStoreOptions> options,
        ILogger<MarkdownContentStore> logger)
    {
        _options = options.Value;
        _options.Validate();
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Create storage directory if configured
        if (_options.CreateDirectoryIfNotExists && !Directory.Exists(_options.StorageDirectory))
        {
            try
            {
                Directory.CreateDirectory(_options.StorageDirectory);
                _logger.LogInformation("Created content storage directory: {StorageDirectory}", _options.StorageDirectory);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create content storage directory: {StorageDirectory}", _options.StorageDirectory);
                throw;
            }
        }
    }

    public async Task<StoreContentResult> StoreAsync(
        string sourceUrl,
        string markdownContent,
        string? title = null,
        string? description = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceUrl))
        {
            throw new ArgumentNullException(nameof(sourceUrl), "Source URL cannot be null or empty.");
        }

        if (string.IsNullOrWhiteSpace(markdownContent))
        {
            throw new ArgumentNullException(nameof(markdownContent), "Markdown content cannot be null or empty.");
        }

        if (_options.ContentEncoding.GetByteCount(markdownContent) > _options.MaxContentSizeBytes)
        {
            throw new InvalidOperationException(
                $"Content size exceeds maximum allowed ({_options.MaxContentSizeBytes} bytes).");
        }

        try
        {
            var contentId = GenerateContentId(sourceUrl);
            var contentHash = ComputeContentHash(markdownContent);
            var now = DateTime.UtcNow;
            var tagsList = tags?.ToList() ?? [];

            // Determine storage path
            var storageDir = _options.StorageDirectory;
            if (_options.OrganizeByDomain)
            {
                var domain = ExtractDomain(sourceUrl);
                storageDir = Path.Combine(storageDir, domain);
            }

            // Create subdirectory if needed
            if (!Directory.Exists(storageDir))
            {
                Directory.CreateDirectory(storageDir);
            }

            var filePath = Path.Combine(storageDir, $"{contentId}.md");
            var metadataPath = Path.Combine(storageDir, $"{contentId}.metadata.json");

            // Check if content already exists and if it's identical
            var isNew = !File.Exists(filePath);
            var existingHash = isNew ? null : await ComputeFileHashAsync(filePath, cancellationToken);
            var isUpdate = !isNew && existingHash != contentHash;

            // Prepare metadata
            var metadata = new StoredContentMetadata
            {
                ContentId = contentId,
                SourceUrl = sourceUrl,
                FetchedAt = now,
                ContentHash = contentHash,
                FilePath = filePath,
                ContentSizeBytes = _options.ContentEncoding.GetByteCount(markdownContent),
                StoredAt = now,
                Title = title,
                Description = description,
                Tags = tagsList
            };

            // Prepare content to write
            var contentToWrite = markdownContent;

            if (_options.IncludeFrontMatter)
            {
                var frontMatter = GenerateFrontMatter(metadata);
                contentToWrite = $"{frontMatter}\n\n{markdownContent}";
            }

            // Write content to file
            await File.WriteAllTextAsync(filePath, contentToWrite, _options.ContentEncoding, cancellationToken);
            _logger.LogInformation("Stored content with ID {ContentId} from {SourceUrl} to {FilePath}",
                contentId, sourceUrl, filePath);

            // Write metadata sidecar if configured
            if (_options.StoreSidecarMetadata)
            {
                var metadataJson = JsonSerializer.Serialize(metadata, _jsonOptions);
                await File.WriteAllTextAsync(metadataPath, metadataJson, _options.ContentEncoding, cancellationToken);
                _logger.LogDebug("Wrote metadata sidecar for {ContentId} to {MetadataPath}", contentId, metadataPath);
            }

            var message = isNew
                ? $"New content stored with ID {contentId}"
                : isUpdate
                    ? $"Content updated with ID {contentId}"
                    : $"Content already exists with ID {contentId} (unchanged)";

            return new StoreContentResult
            {
                Metadata = metadata,
                IsNew = isNew,
                Message = message
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing content from {SourceUrl}", sourceUrl);
            throw;
        }
    }

    public async Task<RetrievedContent?> RetrieveAsync(
        string contentId,
        RetrieveContentOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contentId))
        {
            throw new ArgumentNullException(nameof(contentId), "Content ID cannot be null or empty.");
        }

        options ??= new RetrieveContentOptions();

        try
        {
            var filePath = FindContentFile(contentId);
            if (!filePath.Exists)
            {
                _logger.LogDebug("Content with ID {ContentId} not found", contentId);
                return null;
            }

            var retrieved = new RetrievedContent { ContentId = contentId };

            if (options.IncludeContent)
            {
                var content = await File.ReadAllTextAsync(filePath.FullName, _options.ContentEncoding, cancellationToken);

                // Strip front matter if present
                if (_options.IncludeFrontMatter && content.StartsWith("---"))
                {
                    var endMarker = content.IndexOf("\n---\n", StringComparison.Ordinal);
                    if (endMarker > 0)
                    {
                        content = content.Substring(endMarker + 5).TrimStart();
                    }
                }

                retrieved = retrieved with { MarkdownContent = content };
            }

            if (options.IncludeMetadata)
            {
                var metadata = await LoadMetadataAsync(filePath.DirectoryName!, contentId, cancellationToken);
                retrieved = retrieved with { Metadata = metadata };
            }

            return retrieved;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving content with ID {ContentId}", contentId);
            throw;
        }
    }

    public async Task<IEnumerable<StoredContentMetadata>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        var allMetadata = new List<StoredContentMetadata>();

        try
        {
            if (!Directory.Exists(_options.StorageDirectory))
            {
                _logger.LogDebug("Storage directory {StorageDirectory} does not exist", _options.StorageDirectory);
                return allMetadata;
            }

            var directory = new DirectoryInfo(_options.StorageDirectory);
            var mdFiles = directory.GetFiles("*.md", SearchOption.AllDirectories);

            foreach (var mdFile in mdFiles)
            {
                try
                {
                    var contentId = Path.GetFileNameWithoutExtension(mdFile.Name);
                    var metadata = await LoadMetadataAsync(mdFile.DirectoryName!, contentId, cancellationToken);
                    if (metadata != null)
                    {
                        allMetadata.Add(metadata);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load metadata for file {FileName}", mdFile.Name);
                    continue;
                }
            }

            return allMetadata;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing all stored content");
            throw;
        }
    }

    public async Task<bool> DeleteAsync(string contentId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contentId))
        {
            throw new ArgumentNullException(nameof(contentId), "Content ID cannot be null or empty.");
        }

        try
        {
            var filePath = FindContentFile(contentId);
            if (!filePath.Exists)
            {
                _logger.LogDebug("Content with ID {ContentId} not found for deletion", contentId);
                return false;
            }

            // Delete markdown file
            File.Delete(filePath.FullName);
            _logger.LogInformation("Deleted content file for {ContentId}: {FilePath}", contentId, filePath.FullName);

            // Delete metadata sidecar if it exists
            var metadataPath = Path.Combine(filePath.DirectoryName!, $"{contentId}.metadata.json");
            if (File.Exists(metadataPath))
            {
                File.Delete(metadataPath);
                _logger.LogDebug("Deleted metadata sidecar for {ContentId}: {MetadataPath}", contentId, metadataPath);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting content with ID {ContentId}", contentId);
            throw;
        }
    }

    public async Task<bool> ExistsAsync(string contentId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contentId))
        {
            throw new ArgumentNullException(nameof(contentId), "Content ID cannot be null or empty.");
        }

        try
        {
            var filePath = FindContentFile(contentId);
            return filePath.Exists;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking existence of content with ID {ContentId}", contentId);
            throw;
        }
    }

    public string GetStorageDirectory() => _options.StorageDirectory;

    // Private helper methods

    /// <summary>
    /// Generates a safe content ID from a URL.
    /// </summary>
    private static string GenerateContentId(string sourceUrl)
    {
        var uri = new Uri(sourceUrl);
        var path = uri.LocalPath.Trim('/');

        // If path is empty, use domain as base
        if (string.IsNullOrEmpty(path))
        {
            path = uri.Host;
        }

        // Replace problematic characters and keep it reasonable length
        var sanitized = System.Text.RegularExpressions.Regex.Replace(path, @"[^\w\-_]", "-")
            .Replace("--", "-")
            .Trim('-');

        // Hash the full URL to ensure uniqueness and limit length
        using (var sha = SHA256.Create())
        {
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(sourceUrl));
            var hashStr = Convert.ToHexString(hash)[..8]; // First 8 chars of hash

            if (string.IsNullOrEmpty(sanitized))
            {
                return hashStr;
            }

            // Combine sanitized path with hash for human-readability and uniqueness
            return $"{sanitized[..Math.Min(30, sanitized.Length)]}-{hashStr}".ToLowerInvariant();
        }
    }

    /// <summary>
    /// Extracts the domain from a URL.
    /// </summary>
    private static string ExtractDomain(string sourceUrl)
    {
        try
        {
            var uri = new Uri(sourceUrl);
            return uri.Host.Replace("www.", "");
        }
        catch
        {
            return "other";
        }
    }

    /// <summary>
    /// Computes the SHA256 hash of content.
    /// </summary>
    private static string ComputeContentHash(string content)
    {
        using (var sha = SHA256.Create())
        {
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(content));
            return Convert.ToHexString(hash);
        }
    }

    /// <summary>
    /// Computes the SHA256 hash of a file asynchronously.
    /// </summary>
    private static async Task<string> ComputeFileHashAsync(string filePath, CancellationToken cancellationToken)
    {
        using (var sha = SHA256.Create())
        using (var stream = File.OpenRead(filePath))
        {
            var hash = await Task.Run(() => sha.ComputeHash(stream), cancellationToken);
            return Convert.ToHexString(hash);
        }
    }

    /// <summary>
    /// Generates YAML front matter from metadata.
    /// </summary>
    private string GenerateFrontMatter(StoredContentMetadata metadata)
    {
        var lines = new List<string>
        {
            "---",
            $"title: {EscapeYamlString(metadata.Title ?? metadata.ContentId)}",
            $"source_url: {EscapeYamlString(metadata.SourceUrl)}",
            $"fetched_at: {metadata.FetchedAt:O}",
            $"stored_at: {metadata.StoredAt:O}",
            $"content_hash: {metadata.ContentHash}"
        };

        if (!string.IsNullOrEmpty(metadata.Description))
        {
            lines.Add($"description: {EscapeYamlString(metadata.Description)}");
        }

        if (metadata.Tags.Any())
        {
            var tagYaml = string.Join(", ", metadata.Tags.Select(t => $"\"{t}\""));
            lines.Add($"tags: [{tagYaml}]");
        }

        lines.Add("---");
        return string.Join("\n", lines);
    }

    /// <summary>
    /// Escapes a string for use in YAML.
    /// </summary>
    private static string EscapeYamlString(string value) =>
        value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\\\"")}\"" 
            : value;

    /// <summary>
    /// Finds a content file by content ID, searching in both organized and flat locations.
    /// </summary>
    private FileInfo FindContentFile(string contentId)
    {
        if (!Directory.Exists(_options.StorageDirectory))
        {
            return new FileInfo(Path.Combine(_options.StorageDirectory, $"{contentId}.md"));
        }

        // Search for the file recursively (in case it's organized by domain)
        var directory = new DirectoryInfo(_options.StorageDirectory);
        var matches = directory.GetFiles($"{contentId}.md", SearchOption.AllDirectories);

        return matches.Length > 0 ? matches[0] : new FileInfo(Path.Combine(_options.StorageDirectory, $"{contentId}.md"));
    }

    /// <summary>
    /// Loads metadata from sidecar file or front matter.
    /// </summary>
    private async Task<StoredContentMetadata?> LoadMetadataAsync(
        string directory,
        string contentId,
        CancellationToken cancellationToken)
    {
        var metadataPath = Path.Combine(directory, $"{contentId}.metadata.json");

        // Try to load from sidecar file first
        if (File.Exists(metadataPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(metadataPath, _options.ContentEncoding, cancellationToken);
                return JsonSerializer.Deserialize<StoredContentMetadata>(json, _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize metadata from {MetadataPath}", metadataPath);
            }
        }

        // Fallback: Create minimal metadata from file info
        var mdPath = Path.Combine(directory, $"{contentId}.md");
        if (File.Exists(mdPath))
        {
            var fileInfo = new FileInfo(mdPath);
            return new StoredContentMetadata
            {
                ContentId = contentId,
                SourceUrl = $"file://{mdPath}",
                FetchedAt = fileInfo.LastWriteTimeUtc,
                StoredAt = fileInfo.LastWriteTimeUtc,
                ContentHash = "unknown",
                FilePath = mdPath,
                ContentSizeBytes = fileInfo.Length
            };
        }

        return null;
    }
}
