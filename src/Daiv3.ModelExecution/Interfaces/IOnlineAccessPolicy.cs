using Daiv3.ModelExecution.Models;

namespace Daiv3.ModelExecution.Interfaces;

/// <summary>
/// Enforces configured online access policies (ES-REQ-002).
/// </summary>
/// <remarks>
/// Implements ES-REQ-002: "The system SHALL provide a configurable online fallback path
/// that requires explicit user configuration or per-call confirmation."
/// 
/// Integrates the `online_access_mode` application setting with the model execution pipeline
/// to control when and how online providers can be accessed.
/// </remarks>
public interface IOnlineAccessPolicy
{
    /// <summary>
    /// Checks if online access is allowed for the given request based on configured policy.
    /// </summary>
    /// <param name="request">The execution request</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Policy decision indicating if online access is allowed</returns>
    Task<OnlineAccessDecision> IsOnlineAccessAllowedAsync(
        ExecutionRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the current online access mode from application settings.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Online access mode (never/ask/auto_within_budget/per_task)</returns>
    Task<string> GetOnlineAccessModeAsync(CancellationToken ct = default);

    /// <summary>
    /// Checks if online providers are enabled in application settings.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if at least one online provider is enabled</returns>
    Task<bool> AreOnlineProvidersEnabledAsync(CancellationToken ct = default);
}
