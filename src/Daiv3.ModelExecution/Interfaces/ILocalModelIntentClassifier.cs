using Daiv3.ModelExecution.Models;

namespace Daiv3.ModelExecution.Interfaces;

/// <summary>
/// Classifies task intent using a small local ONNX model for fast, accurate, offline-capable intent resolution.
/// </summary>
/// <remarks>
/// Implements MQ-REQ-011: Intent resolver SHALL run on a small local model to minimize latency.
/// Provides both model-based classification (when available) and pattern-based fallback.
/// </remarks>
public interface ILocalModelIntentClassifier
{
    /// <summary>
    /// Classifies the intent of a request using a local ML model with pattern-based fallback.
    /// </summary>
    /// <param name="content">Request content to classify</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Classified task type</returns>
    /// <remarks>
    /// - Attempts model-based classification first (if model loaded)
    /// - Falls back to pattern-based classification if model unavailable
    /// - Never blocks on model loading; fast fallback ensures low latency
    /// </remarks>
    /// <exception cref="ArgumentNullException">If content is null</exception>
    Task<TaskType> ClassifyAsync(string content, CancellationToken ct = default);

    /// <summary>
    /// Checks if the local inference model is currently available (loaded and ready).
    /// </summary>
    /// <returns>True if model is available; false if using pattern-based fallback</returns>
    Task<bool> IsModelAvailableAsync();

    /// <summary>
    /// Gets the classification confidence score (0.0 to 1.0) from the last classification.
    /// </summary>
    /// <returns>Confidence score</returns>
    /// <remarks>
    /// Returns 1.0 for pattern-based matches, lower values for uncertain model predictions.
    /// </remarks>
    decimal GetLastConfidenceScore();

    /// <summary>
    /// Gets the model type being used (e.g., "local", "pattern-fallback").
    /// </summary>
    /// <returns>Classification model type</returns>
    string GetClassificationMethod();
}
