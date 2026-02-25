using Microsoft.Extensions.Logging;

namespace Daiv3.App.Maui.Services;

/// <summary>
/// Bootstraps the embedding model by copying it from bundled resources to app data directory on first launch.
/// </summary>
public class EmbeddingModelBootstrapService
{
	private readonly ILogger<EmbeddingModelBootstrapService> _logger;

	public EmbeddingModelBootstrapService(ILogger<EmbeddingModelBootstrapService> logger)
	{
		_logger = logger;
	}

	/// <summary>
	/// Ensures the embedding model exists in the app data directory.
	/// If not present, copies it from the bundled resources.
	/// </summary>
	public async Task EnsureModelAsync()
	{
		try
		{
			var modelPath = GetModelDestinationPath();
			var modelDirectory = Path.GetDirectoryName(modelPath);

			// Create directory if it doesn't exist
			if (!Directory.Exists(modelDirectory))
			{
				Directory.CreateDirectory(modelDirectory!);
				_logger.LogInformation("Created model directory: {ModelDirectory}", modelDirectory);
			}

			// Check if model already exists
			if (File.Exists(modelPath))
			{
				_logger.LogInformation("Embedding model already exists at {ModelPath}", modelPath);
				return;
			}

			// Copy from bundled resources
			await CopyModelFromResourcesAsync(modelPath);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error bootstrapping embedding model");
			throw;
		}
	}

	private async Task CopyModelFromResourcesAsync(string destinationPath)
	{
		try
		{
			// Access the model from bundled resources
			// MauiAsset LogicalName strips "Resources\Raw" prefix, so path is just "models/model.onnx"
			var resourcePath = "models/model.onnx";
			
			_logger.LogInformation("Attempting to open bundled resource: {ResourcePath}", resourcePath);
			
			using var sourceStream = await FileSystem.OpenAppPackageFileAsync(resourcePath);
			using var destinationStream = File.Create(destinationPath);
			
			_logger.LogInformation("Copying model from bundle to {DestinationPath}...", destinationPath);
			await sourceStream.CopyToAsync(destinationStream);
			
			var fileInfo = new FileInfo(destinationPath);
			_logger.LogInformation("Successfully copied embedding model to {ModelPath} ({SizeBytes} bytes)", 
				destinationPath, fileInfo.Length);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to copy embedding model from resources to {DestinationPath}", destinationPath);
			throw;
		}
	}

	private static string GetModelDestinationPath()
	{
		var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		return Path.Combine(baseDir, "Daiv3", "models", "embeddings", "model.onnx");
	}
}
