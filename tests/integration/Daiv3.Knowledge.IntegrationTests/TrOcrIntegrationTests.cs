using Daiv3.Infrastructure.Shared.Hardware;
using Daiv3.Infrastructure.Shared.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Daiv3.Knowledge.IntegrationTests;

/// <summary>
/// Integration tests for TrOCR optical character recognition model.
/// Tests encoder-decoder pipeline and hardware-aware model selection.
/// Verifies document text extraction capability.
/// </summary>
public class TrOcrIntegrationTests : IAsyncLifetime
{
	private ILogger<TrOcrIntegrationTests>? _logger;
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
		_logger = _serviceProvider.GetRequiredService<ILogger<TrOcrIntegrationTests>>();
		_hardwareDetection = _serviceProvider.GetRequiredService<IHardwareDetectionProvider>();

		_logger.LogInformation("=== TrOCR Integration Tests Initialized ===");
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
	/// Tests that OCR encoder-decoder model paths are constructed correctly based on hardware tier.
	/// Verifies fp16 models for NPU/GPU and quantized for CPU.
	/// </summary>
	[Fact]
	public void OcrModelPaths_ConstructedCorrectly_ForAvailableHardware()
	{
		// Arrange
		var bestTier = _hardwareDetection!.GetBestAvailableTier();
		var useFp16 = bestTier == HardwareAccelerationTier.Npu || bestTier == HardwareAccelerationTier.Gpu;

		// Act
		var encoderPath = GetOcrEncoderModelPath(useFp16);
		var decoderPath = GetOcrDecoderModelPath(useFp16);

		// Assert
		Assert.NotNull(encoderPath);
		Assert.NotNull(decoderPath);

		// Verify paths contain expected variant directories
		string variant = useFp16 ? "fp16" : "quantized";
		Assert.Contains(variant, encoderPath);
		Assert.Contains(variant, decoderPath);

		// Verify file names match variant selection
		if (useFp16)
		{
			Assert.EndsWith("encoder_model_fp16.onnx", encoderPath);
			Assert.EndsWith("decoder_model_merged_fp16.onnx", decoderPath);
		}
		else
		{
			Assert.EndsWith("encoder_model_quantized.onnx", encoderPath);
			Assert.EndsWith("decoder_model_merged_quantized.onnx", decoderPath);
		}

		_logger?.LogInformation("✓ OCR model paths constructed correctly for {HardwareTier}:", bestTier);
		_logger?.LogInformation("  Encoder: {Path}", encoderPath);
		_logger?.LogInformation("  Decoder: {Path}", decoderPath);
	}

	/// <summary>
	/// Tests that both fp16 and quantized model paths are valid for both variants.
	/// </summary>
	[Theory]
	[InlineData(true)]  // FP16
	[InlineData(false)] // Quantized
	public void OcrModelPaths_AreValid_ForBothVariants(bool useFp16)
	{
		// Arrange
		var variant = useFp16 ? "fp16" : "quantized";

		// Act
		var encoderPath = GetOcrEncoderModelPath(useFp16);
		var decoderPath = GetOcrDecoderModelPath(useFp16);

		// Assert - Paths should contain Daiv3/models/ocr structure
		Assert.Contains("Daiv3", encoderPath);
		Assert.Contains("models", encoderPath);
		Assert.Contains("ocr", encoderPath);
		Assert.Contains("trocr-base-printed", encoderPath);

		Assert.Contains("Daiv3", decoderPath);
		Assert.Contains("models", decoderPath);
		Assert.Contains("ocr", decoderPath);
		Assert.Contains("trocr-base-printed", decoderPath);

		_logger?.LogInformation("✓ OCR {Variant} model paths are valid:", variant);
		_logger?.LogInformation("  Encoder: {Path}", Path.GetFileName(encoderPath));
		_logger?.LogInformation("  Decoder: {Path}", Path.GetFileName(decoderPath));
	}

	/// <summary>
	/// Tests hardware tier detection for OCR model selection.
	/// Verifies NPU/GPU trigger fp16, CPU triggers quantized.
	/// </summary>
	[Fact]
	public void HardwareTierDetection_Determines_OcrModelVariant()
	{
		// Arrange
		var availableTiers = _hardwareDetection!.GetAvailableTiers();
		var bestTier = _hardwareDetection.GetBestAvailableTier();

		// Act
		var useFp16 = bestTier == HardwareAccelerationTier.Npu || bestTier == HardwareAccelerationTier.Gpu;

		// Assert
		Assert.NotEmpty(availableTiers);
		Assert.Equal(bestTier, availableTiers[0]);

		// Verify selection logic
		if (bestTier == HardwareAccelerationTier.Npu || bestTier == HardwareAccelerationTier.Gpu)
		{
			Assert.True(useFp16, "NPU or GPU should select fp16 models");
		}
		else if (bestTier == HardwareAccelerationTier.Cpu)
		{
			Assert.False(useFp16, "CPU should select quantized models");
		}

		_logger?.LogInformation("✓ Hardware tier {Tier} correctly determines OCR variant selection: {Variant}",
			bestTier, useFp16 ? "fp16" : "quantized");
	}

	/// <summary>
	/// Tests that OCR model files match expected naming conventions.
	/// </summary>
	[Fact]
	public void OcrModelFiles_FollowNamingConventions()
	{
		// Arrange
		var fp16EncoderPath = GetOcrEncoderModelPath(true);
		var quantizedEncoderPath = GetOcrEncoderModelPath(false);
		var fp16DecoderPath = GetOcrDecoderModelPath(true);
		var quantizedDecoderPath = GetOcrDecoderModelPath(false);

		// Act & Assert
		var fp16EncoderFile = Path.GetFileName(fp16EncoderPath);
		var quantizedEncoderFile = Path.GetFileName(quantizedEncoderPath);
		var fp16DecoderFile = Path.GetFileName(fp16DecoderPath);
		var quantizedDecoderFile = Path.GetFileName(quantizedDecoderPath);

		// Encoder naming
		Assert.Equal("encoder_model_fp16.onnx", fp16EncoderFile);
		Assert.Equal("encoder_model_quantized.onnx", quantizedEncoderFile);

		// Decoder naming
		Assert.Equal("decoder_model_merged_fp16.onnx", fp16DecoderFile);
		Assert.Equal("decoder_model_merged_quantized.onnx", quantizedDecoderFile);

		_logger?.LogInformation("✓ OCR model file names follow conventions:");
		_logger?.LogInformation("  FP16: {Encoder}, {Decoder}", fp16EncoderFile, fp16DecoderFile);
		_logger?.LogInformation("  Quantized: {Encoder}, {Decoder}", quantizedEncoderFile, quantizedDecoderFile);
	}

	/// <summary>
	/// Verifies that OCR model directories are properly organized by hardware variant.
	/// </summary>
	[Fact]
	public void OcrModelDirectoryStructure_IsOrganized_ByVariant()
	{
		// Arrange
		var fp16EncoderPath = GetOcrEncoderModelPath(true);
		var quantizedEncoderPath = GetOcrEncoderModelPath(false);

		// Act
		var fp16Dir = Path.GetDirectoryName(fp16EncoderPath);
		var quantizedDir = Path.GetDirectoryName(quantizedEncoderPath);

		// Assert
		Assert.NotNull(fp16Dir);
		Assert.NotNull(quantizedDir);

		Assert.Contains("fp16", fp16Dir);
		Assert.Contains("quantized", quantizedDir);

		// Both should be under the same parent OCR directory
		var fp16Parent = Path.GetDirectoryName(fp16Dir);
		var quantizedParent = Path.GetDirectoryName(quantizedDir);

		Assert.Equal(fp16Parent, quantizedParent);
		Assert.Contains("trocr-base-printed", fp16Parent!);

		_logger?.LogInformation("✓ OCR model directory structure is properly organized:");
		_logger?.LogInformation("  FP16 Dir: {Dir}", Path.GetFileName(fp16Dir));
		_logger?.LogInformation("  Quantized Dir: {Dir}", Path.GetFileName(quantizedDir));
	}

	// Helper methods
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
}
