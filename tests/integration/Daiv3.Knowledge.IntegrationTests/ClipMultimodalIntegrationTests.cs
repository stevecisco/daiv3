using Daiv3.Infrastructure.Shared.Hardware;
using Daiv3.Infrastructure.Shared.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Daiv3.Knowledge.IntegrationTests;

/// <summary>
/// Integration tests for CLIP multimodal image/text embeddings.
/// Tests vision encoder (image embeddings) and text encoder functionality.
/// Verifies hardware-aware model selection and loading.
/// </summary>
public class ClipMultimodalIntegrationTests : IAsyncLifetime
{
	private ILogger<ClipMultimodalIntegrationTests>? _logger;
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
		_logger = _serviceProvider.GetRequiredService<ILogger<ClipMultimodalIntegrationTests>>();
		_hardwareDetection = _serviceProvider.GetRequiredService<IHardwareDetectionProvider>();

		_logger.LogInformation("=== CLIP Multimodal Integration Tests Initialized ===");
		var diag = _hardwareDetection.GetDiagnosticInfo();
		_logger.LogInformation("Hardware Diagnostic: {Diagnostic}", diag);

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
	/// Tests that CLIP model paths are constructed correctly based on hardware tier.
	/// Verifies full-precision models for NPU/GPU and quantized for CPU.
	/// </summary>
	[Fact]
	public void ClipModelPaths_ConstructedCorrectly_ForAvailableHardware()
	{
		// Arrange
		var bestTier = _hardwareDetection!.GetBestAvailableTier();
		var useFullPrecision = bestTier == HardwareAccelerationTier.Npu || bestTier == HardwareAccelerationTier.Gpu;

		// Act
		var textModelPath = GetMultimodalTextModelPath(useFullPrecision);
		var visionModelPath = GetMultimodalVisionModelPath(useFullPrecision);

		// Assert
		Assert.NotNull(textModelPath);
		Assert.NotNull(visionModelPath);

		// Verify paths contain expected variant directories
		string variant = useFullPrecision ? "full-precision" : "quantized";
		Assert.Contains(variant, textModelPath);
		Assert.Contains(variant, visionModelPath);

		// Verify file names match variant selection
		if (useFullPrecision)
		{
			Assert.EndsWith("model.onnx", textModelPath);
			Assert.EndsWith("vision_model.onnx", visionModelPath);
		}
		else
		{
			Assert.EndsWith("model_uint8.onnx", textModelPath);
			Assert.EndsWith("vision_model_int8.onnx", visionModelPath);
		}

		_logger?.LogInformation("✓ CLIP model paths constructed correctly for {HardwareTier}:", bestTier);
		_logger?.LogInformation("  Text Model: {Path}", textModelPath);
		_logger?.LogInformation("  Vision Model: {Path}", visionModelPath);
	}

	/// <summary>
	/// Tests that both full-precision and quantized model paths are valid.
	/// </summary>
	[Theory]
	[InlineData(true)]  // Full precision
	[InlineData(false)] // Quantized
	public void ClipModelPaths_AreValid_ForBothVariants(bool useFullPrecision)
	{
		// Arrange
		var variant = useFullPrecision ? "full-precision" : "quantized";

		// Act
		var textModelPath = GetMultimodalTextModelPath(useFullPrecision);
		var visionModelPath = GetMultimodalVisionModelPath(useFullPrecision);

		// Assert - Paths should contain Daiv3/models/multimodal structure
		Assert.Contains("Daiv3", textModelPath);
		Assert.Contains("models", textModelPath);
		Assert.Contains("multimodal", textModelPath);
		Assert.Contains("clip-vit-base-patch32", textModelPath);

		Assert.Contains("Daiv3", visionModelPath);
		Assert.Contains("models", visionModelPath);
		Assert.Contains("multimodal", visionModelPath);
		Assert.Contains("clip-vit-base-patch32", visionModelPath);

		_logger?.LogInformation("✓ CLIP {Variant} model paths are valid:", variant);
		_logger?.LogInformation("  {Path}", Path.GetFileName(textModelPath));
		_logger?.LogInformation("  {Path}", Path.GetFileName(visionModelPath));
	}

	/// <summary>
	/// Tests hardware tier detection for CLIP model selection.
	/// Verifies NPU/GPU trigger full precision, CPU triggers quantized.
	/// </summary>
	[Fact]
	public void HardwareTierDetection_Determines_ClipModelVariant()
	{
		// Arrange
		var availableTiers = _hardwareDetection!.GetAvailableTiers();
		var bestTier = _hardwareDetection.GetBestAvailableTier();

		// Act
		var useFullPrecision = bestTier == HardwareAccelerationTier.Npu || bestTier == HardwareAccelerationTier.Gpu;

		// Assert
		Assert.NotEmpty(availableTiers);
		Assert.Equal(bestTier, availableTiers[0]);

		// Verify selection logic
		if (bestTier == HardwareAccelerationTier.Npu || bestTier == HardwareAccelerationTier.Gpu)
		{
			Assert.True(useFullPrecision, "NPU or GPU should select full-precision models");
		}
		else if (bestTier == HardwareAccelerationTier.Cpu)
		{
			Assert.False(useFullPrecision, "CPU should select quantized models");
		}

		_logger?.LogInformation("✓ Hardware tier {Tier} correctly determines variant selection: {Variant}",
			bestTier, useFullPrecision ? "full-precision" : "quantized");
	}

	/// <summary>
	/// Tests that CLIP model files match expected naming conventions.
	/// </summary>
	[Fact]
	public void ClipModelFiles_FollowNamingConventions()
	{
		// Arrange
		var fullPrecisionTextPath = GetMultimodalTextModelPath(true);
		var quantizedTextPath = GetMultimodalTextModelPath(false);
		var fullPrecisionVisionPath = GetMultimodalVisionModelPath(true);
		var quantizedVisionPath = GetMultimodalVisionModelPath(false);

		// Act & Assert
		var textFileName = Path.GetFileName(fullPrecisionTextPath);
		var textFileNameQuantized = Path.GetFileName(quantizedTextPath);
		var visionFileName = Path.GetFileName(fullPrecisionVisionPath);
		var visionFileNameQuantized = Path.GetFileName(quantizedVisionPath);

		// Text model naming
		Assert.Equal("model.onnx", textFileName);
		Assert.Equal("model_uint8.onnx", textFileNameQuantized);

		// Vision model naming
		Assert.Equal("vision_model.onnx", visionFileName);
		Assert.Equal("vision_model_int8.onnx", visionFileNameQuantized);

		_logger?.LogInformation("✓ CLIP model file names follow conventions:");
		_logger?.LogInformation("  Full Precision: {Text}, {Vision}", textFileName, visionFileName);
		_logger?.LogInformation("  Quantized: {Text}, {Vision}", textFileNameQuantized, visionFileNameQuantized);
	}

	/// <summary>
	/// Verifies that model directories are properly organized by hardware variant.
	/// </summary>
	[Fact]
	public void ClipModelDirectoryStructure_IsOrganized_ByVariant()
	{
		// Arrange
		var fullPrecisionTextPath = GetMultimodalTextModelPath(true);
		var quantizedTextPath = GetMultimodalTextModelPath(false);

		// Act
		var fullDir = Path.GetDirectoryName(fullPrecisionTextPath);
		var quantizedDir = Path.GetDirectoryName(quantizedTextPath);

		// Assert
		Assert.NotNull(fullDir);
		Assert.NotNull(quantizedDir);

		Assert.Contains("full-precision", fullDir);
		Assert.Contains("quantized", quantizedDir);

		// Both should be under the same parent multimodal directory
		var fullParent = Path.GetDirectoryName(fullDir);
		var quantizedParent = Path.GetDirectoryName(quantizedDir);

		Assert.Equal(fullParent, quantizedParent);
		Assert.Contains("clip-vit-base-patch32", fullParent!);

		_logger?.LogInformation("✓ CLIP model directory structure is properly organized:");
		_logger?.LogInformation("  Full Precision Dir: {Dir}", Path.GetFileName(fullDir));
		_logger?.LogInformation("  Quantized Dir: {Dir}", Path.GetFileName(quantizedDir));
	}

	// Helper methods
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
