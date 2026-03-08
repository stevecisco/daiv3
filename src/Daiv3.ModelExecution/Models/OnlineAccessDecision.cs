namespace Daiv3.ModelExecution.Models;

/// <summary>
/// Result of online access policy evaluation.
/// </summary>
/// <remarks>
/// Implements ES-REQ-002: Online access policy decision structure.
/// </remarks>
public class OnlineAccessDecision
{
    /// <summary>
    /// Whether online access is allowed.
    /// </summary>
    public bool IsAllowed { get; init; }

    /// <summary>
    /// Whether user confirmation is required before proceeding.
    /// </summary>
    public bool RequiresConfirmation { get; init; }

    /// <summary>
    /// Reason for the decision (for logging and user feedback).
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// The online access mode that led to this decision.
    /// </summary>
    public string AccessMode { get; init; } = string.Empty;

    /// <summary>
    /// Creates a decision where online access is denied.
    /// </summary>
    public static OnlineAccessDecision Denied(string reason, string accessMode) =>
        new()
        {
            IsAllowed = false,
            RequiresConfirmation = false,
            Reason = reason,
            AccessMode = accessMode
        };

    /// <summary>
    /// Creates a decision where online access is allowed but requires confirmation.
    /// </summary>
    public static OnlineAccessDecision AllowedWithConfirmation(string reason, string accessMode) =>
        new()
        {
            IsAllowed = true,
            RequiresConfirmation = true,
            Reason = reason,
            AccessMode = accessMode
        };

    /// <summary>
    /// Creates a decision where online access is allowed without confirmation.
    /// </summary>
    public static OnlineAccessDecision AllowedWithoutConfirmation(string reason, string accessMode) =>
        new()
        {
            IsAllowed = true,
            RequiresConfirmation = false,
            Reason = reason,
            AccessMode = accessMode
        };
}
