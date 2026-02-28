namespace Daiv3.ModelExecution.Models;

/// <summary>
/// Details about a request requiring user confirmation.
/// </summary>
public class ConfirmationDetails
{
    /// <summary>
    /// The provider that will be used for execution.
    /// </summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>
    /// Estimated input tokens for the request.
    /// </summary>
    public int EstimatedInputTokens { get; set; }

    /// <summary>
    /// Estimated output tokens for the request (if known).
    /// </summary>
    public int EstimatedOutputTokens { get; set; }

    /// <summary>
    /// Total estimated tokens (input + output).
    /// </summary>
    public int TotalEstimatedTokens => EstimatedInputTokens + EstimatedOutputTokens;

    /// <summary>
    /// Current daily token usage for this provider.
    /// </summary>
    public int CurrentDailyInputTokens { get; set; }

    /// <summary>
    /// Daily token limit for this provider.
    /// </summary>
    public int DailyInputLimit { get; set; }

    /// <summary>
    /// Remaining daily budget (tokens).
    /// </summary>
    public int RemainingDailyBudget => Math.Max(0, DailyInputLimit - CurrentDailyInputTokens);

    /// <summary>
    /// Whether this request would exceed the daily budget.
    /// </summary>
    public bool ExceedsBudget => EstimatedInputTokens > RemainingDailyBudget;

    /// <summary>
    /// Reason why confirmation is required.
    /// </summary>
    public string ConfirmationReason { get; set; } = string.Empty;
}
