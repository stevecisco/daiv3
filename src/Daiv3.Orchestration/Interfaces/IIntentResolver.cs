namespace Daiv3.Orchestration.Interfaces;

/// <summary>
/// Resolves user intent from natural language input.
/// </summary>
public interface IIntentResolver
{
    /// <summary>
    /// Resolves the intent from user input.
    /// </summary>
    /// <param name="userInput">The user's input text.</param>
    /// <param name="context">Additional context for intent resolution.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The resolved intent with entities and confidence.</returns>
    Task<Intent> ResolveAsync(string userInput, Dictionary<string, string> context, CancellationToken ct = default);
}

/// <summary>
/// Represents a resolved user intent.
/// </summary>
public class Intent
{
    /// <summary>
    /// The intent type (e.g., "chat", "search", "create", "analyze").
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// Extracted entities from the user input.
    /// </summary>
    public Dictionary<string, string> Entities { get; set; } = new();

    /// <summary>
    /// Confidence score for the intent classification (0.0 to 1.0).
    /// </summary>
    public decimal Confidence { get; set; }
}
