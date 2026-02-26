using Microsoft.Extensions.Logging;
using Daiv3.Infrastructure.Shared.Hardware;

namespace Daiv3.Knowledge.Embedding;

/// <summary>
/// Bootstraps embedding, OCR, and multimodal image models by downloading them from Azure Blob Storage on first launch.
/// Automatically selects appropriate model variants based on hardware capabilities.
/// </summary>
public class EmbeddingModelBootstrapService
{
	private readonly ILogger<EmbeddingModelBootstrapService> _logger;
	private readonly IEmbeddingModelDownloadService _downloadService;
	private readonly IHardwareDetectionProvider _hardwareDetection;
	
	// Tier 1: all-MiniLM-L6-v2 (384 dimensions) - Topic/Summary level
	private const string Tier1ModelDownloadUrl = "https://stdaiv3.blob.core.windows.net/models/embedding/onnx/all-MiniLM-L6-v2/model.onnx";
	
	// Tier 2: nomic-embed-text-v1.5 (768 dimensions) - Chunk level
	private const string Tier2ModelDownloadUrl = "https://stdaiv3.blob.core.windows.net/models/embedding/onnx/nomic-embed-text-v1.5/model.onnx";

	// OCR: TrOCR base printed - encoder models
	private const string OcrEncoderFp16Url = "https://stdaiv3.blob.core.windows.net/models/embedding/onnx/ocr-npu-xenova-trocr-base-printed/encoder_model_fp16.onnx";
	private const string OcrEncoderQuantizedUrl = "https://stdaiv3.blob.core.windows.net/models/embedding/onnx/ocr-npu-xenova-trocr-base-printed/encoder_model_quantized.onnx";
	
	// OCR: TrOCR base printed - decoder models
	private const string OcrDecoderFp16Url = "https://stdaiv3.blob.core.windows.net/models/embedding/onnx/ocr-npu-xenova-trocr-base-printed/decoder_model_merged_fp16.onnx";
	private const string OcrDecoderQuantizedUrl = "https://stdaiv3.blob.core.windows.net/models/embedding/onnx/ocr-npu-xenova-trocr-base-printed/decoder_model_merged_quantized.onnx";

	// Multimodal: CLIP Vision Transformer - text encoder models
	private const string MultimodalTextModelUrl = "https://stdaiv3.blob.core.windows.net/models/embedding/onnx/multim-npu-xenova-clip-vit-base-patch32/model.onnx";
	private const string MultimodalTextModelUint8Url = "https://stdaiv3.blob.core.windows.net/models/embedding/onnx/multim-npu-xenova-clip-vit-base-patch32/model_uint8.onnx";
	
	// Multimodal: CLIP Vision Transformer - vision encoder models
	private const string MultimodalVisionModelUrl = "https://stdaiv3.blob.core.windows.net/models/embedding/onnx/multim-npu-xenova-clip-vit-base-patch32/vision_model.onnx";
	private const string MultimodalVisionModelInt8Url = "https://stdaiv3.blob.core.windows.net/models/embedding/onnx/multim-npu-xenova-clip-vit-base-patch32/vision_model_int8.onnx";

	public EmbeddingModelBootstrapService(
		ILogger<EmbeddingModelBootstrapService> logger,
		IEmbeddingModelDownloadService downloadService,
		IHardwareDetectionProvider hardwareDetection)
	{
		_logger = logger;
		_downloadService = downloadService;
		_hardwareDetection = hardwareDetection;
	}

	/// <summary>
	/// Ensures Tier 1, Tier 2 embedding models, OCR models, and multimodal image models exist in the app data directory.
	/// If not present, downloads them from Azure Blob Storage.
	/// Model variants are selected based on hardware capabilities (full precision for NPU/GPU, quantized for CPU).
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

			// Download OCR models (hardware-aware selection)
			var ocrSuccess = await EnsureOcrModelsAsync(progress);
			if (!ocrSuccess)
			{
				_logger.LogError("Failed to ensure OCR models exist");
				return false;
			}

			// Download multimodal image models (hardware-aware selection)
			var multimodalSuccess = await EnsureMultimodalModelsAsync(progress);
			if (!multimodalSuccess)
			{
				_logger.LogError("Failed to ensure multimodal image models exist");
				return false;
			}

			_logger.LogInformation("All models ready: embedding (Tier 1/2), OCR, and multimodal");
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error bootstrapping models");
			return false;
		}
	}

	/// <summary>
	/// Ensures OCR models exist, selecting appropriate variants based on hardware capabilities.
	/// Downloads fp16 models for NPU/GPU, quantized models for CPU-only devices.
	/// </summary>
	private async Task<bool> EnsureOcrModelsAsync(IProgress<DownloadProgress>? progress)
	{
		try
		{
			// Detect hardware and determine which OCR model variant to download
			var bestTier = _hardwareDetection.GetBestAvailableTier();
			var useFp16 = bestTier == HardwareAccelerationTier.Npu || bestTier == HardwareAccelerationTier.Gpu;
			
			var encoderUrl = useFp16 ? OcrEncoderFp16Url : OcrEncoderQuantizedUrl;
			var decoderUrl = useFp16 ? OcrDecoderFp16Url : OcrDecoderQuantizedUrl;
			var variant = useFp16 ? "fp16" : "quantized";

			_logger.LogInformation("Hardware tier: {Tier}, downloading OCR models variant: {Variant}", 
				bestTier, variant);

			// Download encoder model
			var encoderPath = GetOcrEncoderModelPath(useFp16);
			_logger.LogInformation("Checking for OCR encoder model at {ModelPath}", encoderPath);
			
			var encoderSuccess = await _downloadService.EnsureModelExistsAsync(
				encoderPath,
				encoderUrl,
				progress);

			if (!encoderSuccess)
			{
				_logger.LogError("Failed to ensure OCR encoder model exists");
				return false;
			}

			// Download decoder model
			var decoderPath = GetOcrDecoderModelPath(useFp16);
			_logger.LogInformation("Checking for OCR decoder model at {ModelPath}", decoderPath);
			
			var decoderSuccess = await _downloadService.EnsureModelExistsAsync(
				decoderPath,
				decoderUrl,
				progress);

			if (!decoderSuccess)
			{
				_logger.LogError("Failed to ensure OCR decoder model exists");
				return false;
			}

			_logger.LogInformation("OCR models ({Variant}) are ready", variant);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error bootstrapping OCR models");
			return false;
		}
	}

	/// <summary>
	/// Ensures multimodal CLIP image models exist, selecting appropriate variants based on hardware capabilities.
	/// Downloads full precision models for NPU/GPU, uint8/int8 quantized models for CPU-only devices.
	/// </summary>
	private async Task<bool> EnsureMultimodalModelsAsync(IProgress<DownloadProgress>? progress)
	{
		try
		{
			// Detect hardware and determine which multimodal model variant to download
			var bestTier = _hardwareDetection.GetBestAvailableTier();
			var useFullPrecision = bestTier == HardwareAccelerationTier.Npu || bestTier == HardwareAccelerationTier.Gpu;
			
			var textModelUrl = useFullPrecision ? MultimodalTextModelUrl : MultimodalTextModelUint8Url;
			var visionModelUrl = useFullPrecision ? MultimodalVisionModelUrl : MultimodalVisionModelInt8Url;
			var variant = useFullPrecision ? "full-precision" : "quantized";

			_logger.LogInformation("Hardware tier: {Tier}, downloading multimodal CLIP models variant: {Variant}", 
				bestTier, variant);

			// Download text model
			var textModelPath = GetMultimodalTextModelPath(useFullPrecision);
			_logger.LogInformation("Checking for multimodal text model at {ModelPath}", textModelPath);
			
			var textSuccess = await _downloadService.EnsureModelExistsAsync(
				textModelPath,
				textModelUrl,
				progress);

			if (!textSuccess)
			{
				_logger.LogError("Failed to ensure multimodal text model exists");
				return false;
			}

			// Download vision model
			var visionModelPath = GetMultimodalVisionModelPath(useFullPrecision);
			_logger.LogInformation("Checking for multimodal vision model at {ModelPath}", visionModelPath);
			
			var visionSuccess = await _downloadService.EnsureModelExistsAsync(
				visionModelPath,
				visionModelUrl,
				progress);

			if (!visionSuccess)
			{
				_logger.LogError("Failed to ensure multimodal vision model exists");
				return false;
			}

			_logger.LogInformation("Multimodal CLIP models ({Variant}) are ready", variant);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error bootstrapping multimodal CLIP models");
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

	public static string GetOcrEncoderModelPath(bool useFp16)
	{
		var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		var variant = useFp16 ? "fp16" : "quantized";
		var fileName = useFp16 ? "encoder_model_fp16.onnx" : "encoder_model_quantized.onnx";
		return Path.Combine(baseDir, "Daiv3", "models", "embeddings", "ocr", "trocr-base-printed", variant, fileName);
	}

	public static string GetOcrDecoderModelPath(bool useFp16)
	{
		var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		var variant = useFp16 ? "fp16" : "quantized";
		var fileName = useFp16 ? "decoder_model_merged_fp16.onnx" : "decoder_model_merged_quantized.onnx";
		return Path.Combine(baseDir, "Daiv3", "models", "embeddings", "ocr", "trocr-base-printed", variant, fileName);
	}

	public static string GetMultimodalTextModelPath(bool useFullPrecision)
	{
		var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		var variant = useFullPrecision ? "full-precision" : "quantized";
		var fileName = useFullPrecision ? "model.onnx" : "model_uint8.onnx";
		return Path.Combine(baseDir, "Daiv3", "models", "embeddings", "multimodal", "clip-vit-base-patch32", variant, fileName);
	}

	public static string GetMultimodalVisionModelPath(bool useFullPrecision)
	{
		var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		var variant = useFullPrecision ? "full-precision" : "quantized";
		var fileName = useFullPrecision ? "vision_model.onnx" : "vision_model_int8.onnx";
		return Path.Combine(baseDir, "Daiv3", "models", "embeddings", "multimodal", "clip-vit-base-patch32", variant, fileName);
	}
}
