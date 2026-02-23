namespace Daiv3.ModelExecution.Models;

/// <summary>
/// Token usage in a request.
/// </summary>
public class TokenUsage
{
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int TotalTokens => InputTokens + OutputTokens;
}
