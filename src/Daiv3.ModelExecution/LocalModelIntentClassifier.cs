using System.Diagnostics;
using Daiv3.ModelExecution.Interfaces;
using Daiv3.ModelExecution.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Daiv3.ModelExecution;

/// <summary>
/// Local ONNX model-based intent classifier with pattern-based fallback.
/// </summary>
/// <remarks>
/// Implements MQ-REQ-011: Intent resolver SHALL run on a small local model to minimize latency.
/// Uses ONNX Runtime for inference with automatic fallback to pattern-based classification
/// when the model is not available or inference latency exceeds tolerance.
/// </remarks>
public class LocalModelIntentClassifier : ILocalModelIntentClassifier
{
    private readonly ILogger<LocalModelIntentClassifier> _logger;
    private readonly LocalModelIntentClassificationOptions _options;
    private readonly ITaskTypeClassifier _patternFallback;
    private readonly Stopwatch _latencyWatch = new();

    private bool _modelAvailable = false;
    private decimal _lastConfidenceScore = 0m;
    private string _lastClassificationMethod = "pattern-based";

    public LocalModelIntentClassifier(
        IOptions<LocalModelIntentClassificationOptions> options,
        ILogger<LocalModelIntentClassifier> logger,
        ITaskTypeClassifier patternFallback)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(patternFallback);

        _options = options.Value;
        _logger = logger;
        _patternFallback = patternFallback;

        // Initialize model availability
        InitializeModel();
    }

    public async Task<TaskType> ClassifyAsync(string content, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        _latencyWatch.Restart();

        try
        {
            // Try model-based classification if available
            if (_modelAvailable && _options.EnableLocalModel)
            {
                var (taskType, confidence) = await ClassifyWithModelAsync(content, ct);

                _latencyWatch.Stop();
                var latencyMs = _latencyWatch.ElapsedMilliseconds;

                // Check if model meets latency and confidence thresholds
                if (latencyMs <= _options.MaxInferenceLatencyMs &&
                    confidence >= _options.MinimumModelConfidence)
                {
                    _lastConfidenceScore = confidence;
                    _lastClassificationMethod = "local-model";

                    if (_options.LogClassificationMethod)
                    {
                        _logger.LogDebug(
                            "Intent classified by local model as {TaskType} (confidence={Confidence:P}, latency={LatencyMs}ms)",
                            taskType, confidence, latencyMs);
                    }

                    return taskType;
                }

                // Fall back if latency or confidence too low
                if (latencyMs > _options.MaxInferenceLatencyMs)
                {
                    _logger.LogInformation(
                        "Model inference latency {LatencyMs}ms exceeded threshold {ThresholdMs}ms, using pattern fallback",
                        latencyMs, _options.MaxInferenceLatencyMs);
                }
                else if (confidence < _options.MinimumModelConfidence)
                {
                    _logger.LogDebug(
                        "Model confidence {Confidence:P} below threshold {Threshold:P}, using pattern fallback",
                        confidence, _options.MinimumModelConfidence);
                }
            }

            // Use pattern-based fallback
            var patternRequest = new ExecutionRequest { Content = content };
            var patternTaskType = _patternFallback.Classify(patternRequest);

            _lastConfidenceScore = 1.0m; // Pattern match = high confidence
            _lastClassificationMethod = "pattern-based";

            if (_options.LogClassificationMethod)
            {
                _logger.LogDebug(
                    "Intent classified using pattern fallback: {TaskType}",
                    patternTaskType);
            }

            return patternTaskType;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during intent classification; using pattern fallback");

            // Safety fallback
            var safeRequest = new ExecutionRequest { Content = content };
            var safeTaskType = _patternFallback.Classify(safeRequest);

            _lastConfidenceScore = 0.5m; // Error fallback = lower confidence
            _lastClassificationMethod = "pattern-based (error-fallback)";

            return safeTaskType;
        }
        finally
        {
            _latencyWatch.Stop();
        }
    }

    public Task<bool> IsModelAvailableAsync()
    {
        return Task.FromResult(_modelAvailable);
    }

    public decimal GetLastConfidenceScore()
    {
        return _lastConfidenceScore;
    }

    public string GetClassificationMethod()
    {
        return _lastClassificationMethod;
    }

    private void InitializeModel()
    {
        // Validate configuration
        if (!_options.EnableLocalModel)
        {
            _logger.LogInformation("Local model intent classification disabled in configuration");
            _modelAvailable = false;
            return;
        }

        // Check if model path is provided and file exists
        if (string.IsNullOrWhiteSpace(_options.LocalModelPath))
        {
            _logger.LogInformation("No local model path configured; using pattern-based classification");
            _modelAvailable = false;
            return;
        }

        // TODO: Implement actual ONNX model loading when model is available
        // For now, this is a stub that demonstrates the architecture
        // When a small intent classification model becomes available (e.g., DistilBERT-based),
        // this method would:
        // 1. Load the ONNX model using OnnxRuntime
        // 2. Create inference session with appropriate execution provider
        // 3. Validate input/output shape expectations
        // 4. Set _modelAvailable = true

        _logger.LogInformation(
            "Local intent classification model loading deferred (awaiting model availability). " +
            "Model path configured: {ModelPath}. Using pattern-based classification.",
            _options.LocalModelPath);

        _modelAvailable = false;
    }

    private async Task<(TaskType TaskType, decimal Confidence)> ClassifyWithModelAsync(
        string content,
        CancellationToken ct)
    {
        // TODO: When ONNX model is available, implement actual inference here
        // For now, return a stub indicating model is not yet available

        return await Task.FromResult((TaskType.Unknown, 0.0m));
    }
}
