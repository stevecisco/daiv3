namespace Daiv3.ModelExecution;

/// <summary>
/// Defines the mode for user confirmation of online provider requests.
/// </summary>
public enum ConfirmationMode
{
    /// <summary>
    /// Always require user confirmation for online requests.
    /// </summary>
    Always,

    /// <summary>
    /// Require confirmation only when estimated tokens exceed the configured threshold.
    /// </summary>
    AboveThreshold,

    /// <summary>
    /// Automatically approve requests within budget limits; require confirmation when exceeding budget.
    /// </summary>
    AutoWithinBudget,

    /// <summary>
    /// Never require confirmation (auto-approve all requests).
    /// </summary>
    Never
}
