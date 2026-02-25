namespace Daiv3.Knowledge.Embedding;

/// <summary>
/// Provides functionality for downloading embedding models from remote sources.
/// </summary>
public interface IEmbeddingModelDownloadService
{
    /// <summary>
    /// Ensures the embedding model exists at the specified path.
    /// If not present, downloads from the configured URL with progress reporting.
    /// </summary>
    /// <param name="destinationPath">Full path where the model should be saved.</param>
    /// <param name="downloadUrl">URL to download the model from.</param>
    /// <param name="progress">Optional progress reporter for download status.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if model already exists or download succeeded, false if download failed.</returns>
    Task<bool> EnsureModelExistsAsync(
        string destinationPath,
        string downloadUrl,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads the embedding model from the specified URL.
    /// </summary>
    /// <param name="downloadUrl">URL to download the model from.</param>
    /// <param name="destinationPath">Full path where the model should be saved.</param>
    /// <param name="progress">Optional progress reporter for download status.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DownloadModelAsync(
        string downloadUrl,
        string destinationPath,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents download progress information.
/// </summary>
public record DownloadProgress
{
    /// <summary>
    /// Number of bytes downloaded so far.
    /// </summary>
    public long BytesDownloaded { get; init; }

    /// <summary>
    /// Total size of the file being downloaded, or null if unknown.
    /// </summary>
    public long? TotalBytes { get; init; }

    /// <summary>
    /// Percentage of download completed (0-100), or null if total size is unknown.
    /// </summary>
    public double? PercentComplete => TotalBytes.HasValue && TotalBytes.Value > 0
        ? (double)BytesDownloaded / TotalBytes.Value * 100.0
        : null;

    /// <summary>
    /// Human-readable status message.
    /// </summary>
    public string Status { get; init; } = string.Empty;
}
