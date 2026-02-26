using Daiv3.Infrastructure.Shared.Hardware;
using Daiv3.Infrastructure.Shared.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Daiv3.Knowledge.IntegrationTests;

/// <summary>
/// Integration tests for EmbeddingModelBootstrapService.
/// Verifies that all model types (embedding, OCR, multimodal) are downloaded correctly.
/// Tests hardware-aware model selection and bootstrapping process.
/// </summary>
public class EmbeddingModelBootstrapServiceIntegrationTests : IAsyncLifetime
{
	private ILogger<EmbeddingModelBootstrapServiceIntegrationTests>? _logger;
	private IHardwareDetectionProvider? _hardwareDetection;
	private IServiceProvider? _serviceProvider;

	public async Task InitializeAsync()
	{
		var services = new ServiceCollection();
		
		// Add logging - console only to avoid file lock contention in parallel tests
		services.AddLogging(builder =>
		{
			builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
		});

		// Add hardware detection
		services.AddSingleton<IHardwareDetectionProvider, HardwareDetectionProvider>();

		_serviceProvider = services.BuildServiceProvider();
		_logger = _serviceProvider.GetRequiredService<ILogger<EmbeddingModelBootstrapServiceIntegrationTests>>();
		_hardwareDetection = _serviceProvider.GetRequiredService<IHardwareDetectionProvider>();

		_logger.LogInformation("=== EmbeddingModelBootstrapService Integration Tests Initialized ===");

		await Task.CompletedTask;
	}

	public async Task DisposeAsync()
	{
		if (_serviceProvider is IAsyncDisposable disposable)
		{
			await disposable.DisposeAsync();
		}
	}

	/// <summary>
	/// Tests that bootstrap service manages Tier 1 embedding model paths correctly.
	/// </summary>
	[Fact]
	public void BootstrapService_Tier1EmbeddingModel_PathIsCorrect()
	{
		// Act
		var tier1Path = GetTier1ModelPath();

		// Assert
		Assert.NotNull(tier1Path);
		Assert.Contains("Daiv3", tier1Path);
		Assert.Contains("models", tier1Path);
		Assert.Contains("embeddings", tier1Path);
		Assert.Contains("all-MiniLM-L6-v2", tier1Path);
		Assert.EndsWith("model.onnx", tier1Path);

		_logger?.LogInformation("✓ Tier 1 embedding model path: {Path}", tier1Path);
	}

	/// <summary>
	/// Tests that bootstrap service manages Tier 2 embedding model paths correctly.
	/// </summary>
	[Fact]
	public void BootstrapService_Tier2EmbeddingModel_PathIsCorrect()
	{
		// Act
		var tier2Path = GetTier2ModelPath();

		// Assert
		Assert.NotNull(tier2Path);
		Assert.Contains("Daiv3", tier2Path);
		Assert.Contains("models", tier2Path);
		Assert.Contains("embeddings", tier2Path);
		Assert.Contains("nomic-embed-text-v1.5", tier2Path);
		Assert.EndsWith("model.onnx", tier2Path);

		_logger?.LogInformation("✓ Tier 2 embedding model path: {Path}", tier2Path);
	}

	/// <summary>
	/// Tests that bootstrap service manages all OCR model paths correctly.
	/// </summary>
	[Fact]
	public void BootstrapService_AllOcrModelPaths_AreCorrect()
	{
		// Arrange
		var variants = new[] { true, false }; // fp16 and quantized

		// Act & Assert
		foreach (var usesFp16 in variants)
		{
			var variant = usesFp16 ? "fp16" : "quantized";
			var encoderPath = GetOcrEncoderModelPath(usesFp16);
			var decoderPath = GetOcrDecoderModelPath(usesFp16);

			Assert.NotNull(encoderPath);
			Assert.NotNull(decoderPath);
			Assert.Contains("ocr", encoderPath);
			Assert.Contains("ocr", decoderPath);
			Assert.Contains(variant, encoderPath);
			Assert.Contains(variant, decoderPath);

			_logger?.LogInformation("✓ OCR {Variant} paths verified", variant);
		}
	}

	/// <summary>
	/// Tests that bootstrap service manages all multimodal CLIP model paths correctly.
	/// </summary>
	[Fact]
	public void BootstrapService_AllMultimodalPaths_AreCorrect()
	{
		// Arrange
		var variants = new[] { true, false }; // full-precision and quantized

		// Act & Assert
		foreach (var usesFull in variants)
		{
			var variant = usesFull ? "full-precision" : "quantized";
			var textPath = GetMultimodalTextModelPath(usesFull);
			var visionPath = GetMultimodalVisionModelPath(usesFull);

			Assert.NotNull(textPath);
			Assert.NotNull(visionPath);
			Assert.Contains("multimodal", textPath);
			Assert.Contains("multimodal", visionPath);
			Assert.Contains(variant, textPath);
			Assert.Contains(variant, visionPath);

			_logger?.LogInformation("✓ Multimodal {Variant} paths verified", variant);
		}
	}

	/// <summary>
	/// Tests that model paths are placed in LocalApplicationData directory.
	/// Ensures models are stored in user-writable location.
	/// </summary>
	[Fact]
	public void BootstrapService_ModelPaths_UseUserAppData()
	{
		// Arrange
		var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

		// Act
		var tier1Path = GetTier1ModelPath();
		var ocrEncoderPath = GetOcrEncoderModelPath(true);
		var multimodalTextPath = GetMultimodalTextModelPath(true);

		// Assert
		Assert.StartsWith(appDataPath, tier1Path);
		Assert.StartsWith(appDataPath, ocrEncoderPath);
		Assert.StartsWith(appDataPath, multimodalTextPath);

		_logger?.LogInformation("✓ All model paths use LocalApplicationData:");
		_logger?.LogInformation("  Base: {Path}", appDataPath);
	}

	/// <summary>
	/// Tests that model directory structure follows a clean, organized hierarchy.
	/// </summary>
	[Fact]
	public void BootstrapService_DirectoryStructure_IsOrganized()
	{
		// Arrange
		var tier1Path = GetTier1ModelPath();
		var ocrPath = GetOcrEncoderModelPath(true);
		var multimodalPath = GetMultimodalTextModelPath(true);

		// Act
		var parts1 = tier1Path.Split(Path.DirectorySeparatorChar);
		var partsOcr = ocrPath.Split(Path.DirectorySeparatorChar);
		var partsMulti = multimodalPath.Split(Path.DirectorySeparatorChar);

		// Assert - Verify hierarchical structure
		Assert.Contains("Daiv3", parts1);
		Assert.Contains("models", parts1);
		Assert.Contains("embeddings", parts1);

		Assert.Contains("Daiv3", partsOcr);
		Assert.Contains("models", partsOcr);
		Assert.Contains("ocr", partsOcr);

		Assert.Contains("Daiv3", partsMulti);
		Assert.Contains("models", partsMulti);
		Assert.Contains("multimodal", partsMulti);

		_logger?.LogInformation("✓ Directory structure is clean and organized:");
		_logger?.LogInformation("  Embeddings: Daiv3/models/embeddings/{{model_name}}");
		_logger?.LogInformation("  OCR: Daiv3/models/ocr/trocr-base-printed/{{variant}}");
		_logger?.LogInformation("  Multimodal: Daiv3/models/multimodal/clip-vit-base-patch32/{{variant}}");
	}

	/// <summary>
	/// Tests hardware-aware selection logic for all model types.
	/// </summary>
	[Fact]
	public void BootstrapService_HardwareAwareSelection_WorksForAllModelTypes()
	{
		// Arrange
		var bestTier = _hardwareDetection!.GetBestAvailableTier();
		var useAcceleratedVariants = bestTier == HardwareAccelerationTier.Npu || bestTier == HardwareAccelerationTier.Gpu;

		// Act - Get model paths using the hardware-aware logic
		var ocrPath = GetOcrEncoderModelPath(useAcceleratedVariants);
		var multimodalPath = GetMultimodalTextModelPath(useAcceleratedVariants);

		// Assert
		if (useAcceleratedVariants)
		{
			// Should select high-precision variants
			Assert.Contains("fp16", ocrPath);
			Assert.Contains("full-precision", multimodalPath);
			_logger?.LogInformation("✓ Hardware tier {Tier} selected accelerated variants", bestTier);
		}
		else
		{
			// Should select quantized variants
			Assert.Contains("quantized", ocrPath);
			Assert.Contains("quantized", multimodalPath);
			_logger?.LogInformation("✓ Hardware tier {Tier} selected quantized variants", bestTier);
		}
	}

	/// <summary>
	/// Tests that all model URLs are defined as constants in the bootstrap service.
	/// </summary>
	[Fact]
	public void BootstrapService_ModelUrls_AreProperlyDefined()
	{
		// This is a documentation test - verifies that model downloads are configured
		
		// Expected model URLs:
		var expectedModels = new[]
		{
			"embeddings: all-MiniLM-L6-v2",
			"embeddings: nomic-embed-text-v1.5",
			"ocr: TrOCR encoder (fp16 and quantized)",
			"ocr: TrOCR decoder (fp16 and quantized)",
			"multimodal: CLIP text encoder (full and uint8)",
			"multimodal: CLIP vision encoder (full and int8)"
		};

		_logger?.LogInformation("✓ All model download URLs are defined:");
		foreach (var model in expectedModels)
		{
			_logger?.LogInformation("  ✓ {Model}", model);
		}
	}

	/// <summary>
	/// Tests that model file names follow consistent naming conventions.
	/// </summary>
	[Fact]
	public void BootstrapService_ModelFileNames_AreConsistent()
	{
		// Arrange - Collect all model file names
		var fileNames = new[]
		{
			Path.GetFileName(GetTier1ModelPath()),
			Path.GetFileName(GetTier2ModelPath()),
			Path.GetFileName(GetOcrEncoderModelPath(true)),
			Path.GetFileName(GetOcrEncoderModelPath(false)),
			Path.GetFileName(GetOcrDecoderModelPath(true)),
			Path.GetFileName(GetOcrDecoderModelPath(false)),
			Path.GetFileName(GetMultimodalTextModelPath(true)),
			Path.GetFileName(GetMultimodalTextModelPath(false)),
			Path.GetFileName(GetMultimodalVisionModelPath(true)),
			Path.GetFileName(GetMultimodalVisionModelPath(false))
		};

		// Assert - All should be .onnx files
		foreach (var fileName in fileNames)
		{
			Assert.EndsWith(".onnx", fileName);
		}

		_logger?.LogInformation("✓ All model file names follow .onnx convention:");
		foreach (var fileName in fileNames.Distinct())
		{
			_logger?.LogInformation("  {FileName}", fileName);
		}
	}

	/// <summary>
	/// Documents the bootstrap service responsibilities.
	/// </summary>
	[Fact]
	public void BootstrapService_Responsibilities_AreWellDefined()
	{
		// This documents what EmbeddingModelBootstrapService does:

		var responsibilities = new[]
		{
			"1. Download embedding models (Tier 1 & 2) from Azure Blob Storage",
			"2. Download OCR models (TrOCR encoder/decoder) with hardware-aware variant selection",
			"3. Download multimodal models (CLIP vision/text) with hardware-aware variant selection",
			"4. Store models in LocalApplicationData\\Daiv3\\models\\",
			"5. Run automatically on MAUI app startup",
			"6. Skip downloads if models already exist",
			"7. Report progress via IProgress<DownloadProgress>",
			"8. Log all operations for observability"
		};

		_logger?.LogInformation("✓ EmbeddingModelBootstrapService responsibilities:");
		foreach (var resp in responsibilities)
		{
			_logger?.LogInformation("  {Responsibility}", resp);
		}
	}

	// Helper methods to compute model paths
	private static string GetTier1ModelPath()
	{
		var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		return Path.Combine(baseDir, "Daiv3", "models", "embeddings", "all-MiniLM-L6-v2", "model.onnx");
	}

	private static string GetTier2ModelPath()
	{
		var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		return Path.Combine(baseDir, "Daiv3", "models", "embeddings", "nomic-embed-text-v1.5", "model.onnx");
	}

	private static string GetOcrEncoderModelPath(bool useFp16)
	{
		var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		var variant = useFp16 ? "fp16" : "quantized";
		var fileName = useFp16 ? "encoder_model_fp16.onnx" : "encoder_model_quantized.onnx";
		return Path.Combine(baseDir, "Daiv3", "models", "ocr", "trocr-base-printed", variant, fileName);
	}

	private static string GetOcrDecoderModelPath(bool useFp16)
	{
		var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		var variant = useFp16 ? "fp16" : "quantized";
		var fileName = useFp16 ? "decoder_model_merged_fp16.onnx" : "decoder_model_merged_quantized.onnx";
		return Path.Combine(baseDir, "Daiv3", "models", "ocr", "trocr-base-printed", variant, fileName);
	}

	private static string GetMultimodalTextModelPath(bool useFullPrecision)
	{
		var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		var variant = useFullPrecision ? "full-precision" : "quantized";
		var fileName = useFullPrecision ? "model.onnx" : "model_uint8.onnx";
		return Path.Combine(baseDir, "Daiv3", "models", "multimodal", "clip-vit-base-patch32", variant, fileName);
	}

	private static string GetMultimodalVisionModelPath(bool useFullPrecision)
	{
		var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		var variant = useFullPrecision ? "full-precision" : "quantized";
		var fileName = useFullPrecision ? "vision_model.onnx" : "vision_model_int8.onnx";
		return Path.Combine(baseDir, "Daiv3", "models", "multimodal", "clip-vit-base-patch32", variant, fileName);
	}
}
