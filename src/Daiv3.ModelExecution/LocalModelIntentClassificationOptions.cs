namespace Daiv3.ModelExecution;

/// <summary>
/// Configuration options for local model-based intent classification.
/// </summary>
public class LocalModelIntentClassificationOptions
{
    /// <summary>
    /// Enable local model-based intent classification (when available).
    /// </summary>
    /// <remarks>
    /// If false or model unavailable, falls back to pattern-based classification.
    /// Default: true (use model when available).
    /// </remarks>
    public bool EnableLocalModel { get; set; } = true;

    /// <summary>
    /// Path to the ONNX model file for intent classification.
    /// </summary>
    /// <remarks>
    /// If null/empty or file not found, pattern-based fallback is used.
    /// Supports relative paths (resolved from app base directory) and environment variables.
    /// Default: null (no model).
    /// </remarks>
    public string? LocalModelPath { get; set; }

    /// <summary>
    /// Execution provider for the ONNX model ("CPU", "CUDA", "DirectML", "CoreML", etc.).
    /// </summary>
    /// <remarks>
    /// DirectML recommended for Windows 11 Copilot+ devices; falls back to CPU if unavailable.
    /// Default: "DirectML" for Windows, "CPU" for others.
    /// </remarks>
    public string ExecutionProvider { get; set; } = OperatingSystem.IsWindows() ? "DirectML" : "CPU";

    /// <summary>
    /// Minimum confidence threshold for local model predictions (0.0 to 1.0).
    /// </summary>
    /// <remarks>
    /// If model confidence &lt; threshold, falls back to pattern-based classification.
    /// Default: 0.7 (accepts predictions with 70%+ confidence).
    /// </remarks>
    public decimal MinimumModelConfidence { get; set; } = 0.7m;

    /// <summary>
    /// Maximum latency tolerance in milliseconds for model inference.
    /// </summary>
    /// <remarks>
    /// If model inference exceeds this time, subsequent requests use pattern fallback.
    /// This prevents slow model I/O from blocking request handling.
    /// Default: 100ms.
    /// </remarks>
    public int MaxInferenceLatencyMs { get; set; } = 100;

    /// <summary>
    /// Fallback pattern-based configuration for when model is unavailable.
    /// </summary>
    /// <remarks>
    /// Always available as a fast, deterministic fallback.
    /// </remarks>
    public TaskTypeClassifierOptions PatternFallbackOptions { get; set; } = new();

    /// <summary>
    /// Enable logging of classification method selection and performance.
    /// </summary>
    /// <remarks>
    /// Useful for debugging and monitoring fallback behavior.
    /// Default: true.
    /// </remarks>
    public bool LogClassificationMethod { get; set; } = true;
}
