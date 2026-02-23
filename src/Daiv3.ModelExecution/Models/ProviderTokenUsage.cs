namespace Daiv3.ModelExecution.Models;

/// <summary>
/// Token usage summary for an online provider.
/// </summary>
public class ProviderTokenUsage
{
    public string ProviderName { get; set; } = string.Empty;
    public int DailyInputTokens { get; set; }
    public int DailyOutputTokens { get; set; }
    public int MonthlyInputTokens { get; set; }
    public int MonthlyOutputTokens { get; set; }
    public int DailyInputLimit { get; set; }
    public int DailyOutputLimit { get; set; }
    public int MonthlyInputLimit { get; set; }
    public int MonthlyOutputLimit { get; set; }
    public DateTimeOffset ResetDate { get; set; }
}
