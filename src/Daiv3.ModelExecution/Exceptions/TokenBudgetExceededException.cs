namespace Daiv3.ModelExecution.Exceptions;

/// <summary>
/// Exception thrown when token budget is exceeded.
/// </summary>
public class TokenBudgetExceededException : InvalidOperationException
{
    public string ProviderName { get; }
    public int TokensRequested { get; }
    public int TokensRemaining { get; }

    public TokenBudgetExceededException(
        string providerName,
        int tokensRequested,
        int tokensRemaining)
        : base($"Token budget exceeded for provider '{providerName}'. " +
               $"Requested: {tokensRequested}, Remaining: {tokensRemaining}")
    {
        ProviderName = providerName;
        TokensRequested = tokensRequested;
        TokensRemaining = tokensRemaining;
    }
}
