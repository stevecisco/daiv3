using Microsoft.Extensions.Logging;
using Daiv3.Knowledge.Embedding;

namespace Daiv3.App.Maui.Services;

/// <summary>
/// Bootstraps both Tier 1 and Tier 2 embedding models by downloading them from Azure Blob Storage on first launch.
/// </summary>
public class EmbeddingModelBootstrapService
{
	private readonly ILogger<EmbeddingModelBootstrapService> _logger;
	private readonly IEmbeddingModelDownloadService _downloadService;
	
	// Tier 1: all-MiniLM-L6-v2 (384 dimensions) - Topic/Summary level
	private const string Tier1ModelDownloadUrl = "https://stdaiv3.blob.core.windows.net/models/embedding/onnx/all-MiniLM-L6-v2/model.onnx";
	
	// Tier 2: nomic-embed-text-v1.5 (768 dimensions) - Chunk level
	private const string Tier2ModelDownloadUrl = "https://stdaiv3.blob.core.windows.net/models/embedding/onnx/nomic-embed-text-v1.5/model.onnx";

	public EmbeddingModelBootstrapService(
		ILogger<EmbeddingModelBootstrapService> logger,
		IEmbeddingModelDownloadService downloadService)
	{
		_logger = logger;
		_downloadService = downloadService;
	}

	/// <summary>
	/// Ensures both Tier 1 and Tier 2 embedding models exist in the app data directory.
	/// If not present, downloads them from Azure Blob Storage.
	/// </summary>
	/// <param name="onProgress">Optional callback for progress updates.</param>
	public async Task<bool> EnsureModelsAsync(Action<DownloadProgress>? onProgress = null)
	{
		try
		{
			var progress = onProgress != null
				? new Progress<DownloadProgress>(onProgress)
				: null;

			// Download Tier 1 model (all-MiniLM-L6-v2)
			var tier1Path = GetTier1ModelPath();
			_logger.LogInformation("Checking for Tier 1 embedding model at {ModelPath}", tier1Path);
			
			var tier1Success = await _downloadService.EnsureModelExistsAsync(
				tier1Path,
				Tier1ModelDownloadUrl,
				progress);

			if (!tier1Success)
			{
				_logger.LogError("Failed to ensure Tier 1 embedding model exists");
				return false;
			}

			// Download Tier 2 model (nomic-embed-text-v1.5)
			var tier2Path = GetTier2ModelPath();
			_logger.LogInformation("Checking for Tier 2 embedding model at {ModelPath}", tier2Path);
			
			var tier2Success = await _downloadService.EnsureModelExistsAsync(
				tier2Path,
				Tier2ModelDownloadUrl,
				progress);

			if (!tier2Success)
			{
				_logger.LogError("Failed to ensure Tier 2 embedding model exists");
				return false;
			}

			_logger.LogInformation("Both embedding models are ready");
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error bootstrapping embedding models");
			return false;
		}
	}

	public static string GetTier1ModelPath()
	{
		var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		return Path.Combine(baseDir, "Daiv3", "models", "embeddings", "all-MiniLM-L6-v2", "model.onnx");
	}

	public static string GetTier2ModelPath()
	{
		var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		return Path.Combine(baseDir, "Daiv3", "models", "embeddings", "nomic-embed-text-v1.5", "model.onnx");
	}
}

