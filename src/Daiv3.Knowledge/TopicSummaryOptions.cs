namespace Daiv3.Knowledge;

/// <summary>
/// Configuration options for topic summary generation.
/// </summary>
public class TopicSummaryOptions
{
    /// <summary>
    /// Minimum number of sentences in the output summary.
    /// Default: 2
    /// </summary>
    public int MinSentences { get; set; } = 2;

    /// <summary>
    /// Maximum number of sentences in the output summary.
    /// Default: 3
    /// </summary>
    public int MaxSentences { get; set; } = 3;

    /// <summary>
    /// Maximum character length for the summary.
    /// Default: 500. Prevents extremely long summaries.
    /// </summary>
    public int MaxCharacters { get; set; } = 500;

    /// <summary>
    /// Whether to preserve sentence order from original text.
    /// Default: true. If false, sentences are returned in score order.
    /// </summary>
    public bool PreserveSentenceOrder { get; set; } = true;

    /// <summary>
    /// Validates configuration options.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when configuration is invalid.</exception>
    public void Validate()
    {
        if (MinSentences < 1)
            throw new InvalidOperationException("MinSentences must be at least 1");
        
        if (MaxSentences < MinSentences)
            throw new InvalidOperationException("MaxSentences must be >= MinSentences");
        
        if (MaxCharacters < 50)
            throw new InvalidOperationException("MaxCharacters must be at least 50");
    }
}
