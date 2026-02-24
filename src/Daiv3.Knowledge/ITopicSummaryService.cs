namespace Daiv3.Knowledge;

/// <summary>
/// Generates topic summaries for documents.
/// Supports pluggable implementations: extractive (current), SLM-based (future).
/// </summary>
public interface ITopicSummaryService
{
    /// <summary>
    /// Generates a 2-3 sentence topic summary from document text.
    /// </summary>
    /// <param name="documentText">The full document text to summarize.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A 2-3 sentence summary of the document.</returns>
    /// <exception cref="ArgumentException">Thrown when text is null or empty.</exception>
    Task<string> GenerateSummaryAsync(string documentText, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets implementation name for diagnostics/logging.
    /// </summary>
    string ImplementationName { get; }
}
