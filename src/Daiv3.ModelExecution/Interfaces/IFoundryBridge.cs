using Daiv3.ModelExecution.Models;

namespace Daiv3.ModelExecution.Interfaces;

/// <summary>
/// Interface to Microsoft Foundry Local for local SLM execution.
/// </summary>
/// <remarks>
/// - Only one model can be loaded at a time
/// - Model switching has meaningful time cost
/// - Used for chat, summarization, analysis, code generation
/// </remarks>
public interface IFoundryBridge
{
    /// <summary>
    /// Executes a completion request using Foundry Local.
    /// </summary>
    /// <remarks>
    /// - If model not loaded, loads it first (may take several seconds)
    /// - Returns generated text response
    /// - Automatically tracks token usage
    /// </remarks>
    /// <param name="request">Execution request</param>
    /// <param name="modelId">Model to use (if different from current)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Execution result with content and token usage</returns>
    /// <exception cref="InvalidOperationException">If Foundry Local unavailable</exception>
    /// <exception cref="ArgumentException">If model not found</exception>
    Task<ExecutionResult> ExecuteAsync(
        ExecutionRequest request,
        string modelId,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the currently loaded model ID.
    /// </summary>
    /// <returns>Model ID or null if none loaded</returns>
    Task<string?> GetLoadedModelAsync();

    /// <summary>
    /// Lists models available in Foundry Local.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Available model IDs</returns>
    Task<List<string>> ListAvailableModelsAsync(CancellationToken ct = default);
}
