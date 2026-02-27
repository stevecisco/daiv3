using Daiv3.ModelExecution.Models;

namespace Daiv3.ModelExecution.Interfaces;

/// <summary>
/// Manages model lifecycle with enforcement of the single-model-at-a-time constraint.
/// </summary>
/// <remarks>
/// This is a Foundry Local SDK limitation (not a design choice). Only one SLM/LLM can be 
/// loaded in memory at a time. This manager enforces this constraint and provides clear
/// error handling and observability.
/// 
/// Constraint Enforcement:
/// - Only one model can be loaded at a time
/// - Attempting to load a different model without unloading first throws InvalidOperationException
/// - Unloading a model that is not currently loaded is a no-op with warning log
/// 
/// Model Switching Cost:
/// - Loading a new model has significant latency (several seconds)
/// - The queue system minimizes switches by batching requests for the current model
/// - P0 (Immediate) requests can trigger a switch
/// </remarks>
public interface IModelLifecycleManager
{
    /// <summary>
    /// Loads a model into memory, unloading the previous model if necessary.
    /// </summary>
    /// <remarks>
    /// - If no model is currently loaded, loads the specified model
    /// - If a different model is loaded, throws InvalidOperationException
    /// - If the same model is already loaded, returns immediately (idempotent)
    /// - Model loading has significant latency
    /// </remarks>
    /// <param name="modelId">Model identifier to load</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Task that completes when model is loaded</returns>
    /// <exception cref="ArgumentException">If modelId is null or empty</exception>
    /// <exception cref="InvalidOperationException">If a different model is already loaded</exception>
    Task LoadModelAsync(string modelId, CancellationToken ct = default);

    /// <summary>
    /// Switches from the currently loaded model to a different model.
    /// </summary>
    /// <remarks>
    /// - Unloads the current model and loads the new one atomically
    /// - If no model is currently loaded, just loads the new model
    /// - If the same model is already loaded, returns immediately (idempotent)
    /// - Model switching has significant time cost (series of seconds)
    /// </remarks>
    /// <param name="newModelId">Target model identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Task that completes when model switch is complete</returns>
    /// <exception cref="ArgumentException">If newModelId is null or empty</exception>
    Task SwitchModelAsync(string newModelId, CancellationToken ct = default);

    /// <summary>
    /// Unloads the currently loaded model from memory.
    /// </summary>
    /// <remarks>
    /// - If a model is loaded, unloads it and frees memory
    /// - If no model is loaded, this is a no-op
    /// - After unloading, GetLoadedModelAsync returns null
    /// </remarks>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Task that completes when model is unloaded</returns>
    Task UnloadModelAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the currently loaded model identifier.
    /// </summary>
    /// <returns>Model ID or null if no model is loaded</returns>
    Task<string?> GetLoadedModelAsync();

    /// <summary>
    /// Checks if a specific model is currently loaded.
    /// </summary>
    /// <param name="modelId">Model identifier to check</param>
    /// <returns>True if the model is currently loaded, false otherwise</returns>
    Task<bool> IsModelLoadedAsync(string modelId);

    /// <summary>
    /// Gets the timestamp of the last model switch (load or unload).
    /// </summary>
    /// <returns>DateTimeOffset of last switch, or null if no model has been loaded</returns>
    Task<DateTimeOffset?> GetLastModelSwitchAsync();

    /// <summary>
    /// Gets metrics about model lifecycle operations.
    /// </summary>
    /// <returns>Model lifecycle metrics</returns>
    Task<ModelLifecycleMetrics> GetMetricsAsync();
}

/// <summary>
/// Metrics for model lifecycle operations.
/// </summary>
public class ModelLifecycleMetrics
{
    /// <summary>
    /// Total number of model loads attempted.
    /// </summary>
    public int TotalLoads { get; set; }

    /// <summary>
    /// Total number of successful model loads.
    /// </summary>
    public int SuccessfulLoads { get; set; }

    /// <summary>
    /// Total number of failed model loads.
    /// </summary>
    public int FailedLoads { get; set; }

    /// <summary>
    /// Total number of constraint violations (attempted to load different model while another is loaded).
    /// </summary>
    public int ConstraintViolations { get; set; }

    /// <summary>
    /// Current model ID or null if none loaded.
    /// </summary>
    public string? CurrentModelId { get; set; }

    /// <summary>
    /// Timestamp of last model switch.
    /// </summary>
    public DateTimeOffset? LastModelSwitch { get; set; }

    /// <summary>
    /// Average time to load a model (in milliseconds).
    /// </summary>
    public double AverageLoadTimeMs { get; set; }
}
