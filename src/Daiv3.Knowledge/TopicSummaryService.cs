using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace Daiv3.Knowledge;

/// <summary>
/// Extractive topic summary service using TF-based sentence scoring.
/// Phase 1 implementation: Extracts important sentences from document text.
/// Future: Will be extended with SLM-based abstractive summarization.
/// </summary>
public sealed class TopicSummaryService : ITopicSummaryService
{
    private readonly ILogger<TopicSummaryService> _logger;
    private readonly TopicSummaryOptions _options;

    public string ImplementationName => "Extractive (TF-based)";

    public TopicSummaryService(
        ILogger<TopicSummaryService> logger,
        IOptions<TopicSummaryOptions>? options = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? new TopicSummaryOptions();
        _options.Validate();
    }

    public async Task<string> GenerateSummaryAsync(
        string documentText,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentText))
            throw new ArgumentException("Document text cannot be null or empty", nameof(documentText));

        return await Task.Run(() => GenerateSummaryInternal(documentText), cancellationToken)
            .ConfigureAwait(false);
    }

    private string GenerateSummaryInternal(string text)
    {
        // Extract sentences from text
        var sentences = ExtractSentences(text);

        if (sentences.Count == 0)
        {
            _logger.LogWarning("No sentences extracted from document text");
            return string.Empty;
        }

        if (sentences.Count <= _options.MinSentences)
        {
            // Text is already very short, return as-is (sentences already include punctuation)
            var result = string.Join(" ", sentences);
            return LimitToMaxCharacters(result);
        }

        // Calculate word frequencies (TF)
        var wordFrequencies = CalculateWordFrequencies(text);

        // Score sentences based on word frequencies
        var scoredSentences = new List<(int OriginalIndex, string Sentence, double Score)>();
        for (int i = 0; i < sentences.Count; i++)
        {
            var score = ScoreSentence(sentences[i], wordFrequencies);
            scoredSentences.Add((i, sentences[i], score));
        }

        // Select top N sentences
        var targetCount = Math.Min(_options.MaxSentences, sentences.Count);
        targetCount = Math.Max(targetCount, _options.MinSentences);

        var topSentences = scoredSentences
            .OrderByDescending(s => s.Score)
            .Take(targetCount)
            .ToList();

        // Preserve original order if configured
        if (_options.PreserveSentenceOrder)
        {
            topSentences = topSentences
                .OrderBy(s => s.OriginalIndex)
                .ToList();
        }

        var summary = string.Join(" ", topSentences.Select(s => s.Sentence));
        summary = LimitToMaxCharacters(summary);

        _logger.LogDebug(
            "Generated summary: {SentenceCount} sentences selected from {TotalSentences}, {CharCount} characters",
            topSentences.Count,
            sentences.Count,
            summary.Length);

        return summary;
    }

    /// <summary>
    /// Extracts sentences from text using regex.
    /// Handles periods, question marks, and exclamation marks as delimiters.
    /// Preserves the ending punctuation with each sentence.
    /// </summary>
    private static List<string> ExtractSentences(string text)
    {
        // Match sentence patterns: text followed by . ! or ?
        // This regex captures sentences WITH their ending punctuation
        var sentencePattern = new Regex(@"[^.!?]*[.!?]+");
        var matches = sentencePattern.Matches(text);

        var sentences = new List<string>();
        foreach (Match match in matches)
        {
            var sentence = match.Value.Trim();
            if (sentence.Length > 5) // Ignore very short fragments
            {
                sentences.Add(sentence);
            }
        }

        return sentences;
    }

    /// <summary>
    /// Calculates word frequency in the document text.
    /// Case-insensitive, filters common stop words.
    /// </summary>
    private static Dictionary<string, int> CalculateWordFrequencies(string text)
    {
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "a", "an", "the", "and", "or", "but", "in", "on", "at", "to", "for",
            "of", "with", "by", "from", "as", "is", "was", "are", "were", "be",
            "been", "being", "have", "has", "had", "do", "does", "did", "will",
            "would", "could", "should", "may", "might", "must", "can", "it", "its",
            "that", "this", "these", "those", "i", "you", "he", "she", "we", "they",
            "what", "which", "who", "when", "where", "why", "how", "not", "no", "yes"
        };

        var words = Regex.Split(text.ToLowerInvariant(), @"[^a-z0-9]+")
            .Where(w => w.Length > 2 && !stopWords.Contains(w))
            .ToList();

        var frequencies = new Dictionary<string, int>();
        foreach (var word in words)
        {
            if (frequencies.ContainsKey(word))
                frequencies[word]++;
            else
                frequencies[word] = 1;
        }

        return frequencies;
    }

    /// <summary>
    /// Scores a sentence based on word frequencies (TF).
    /// Higher frequency words contribute more to the score.
    /// </summary>
    private static double ScoreSentence(string sentence, Dictionary<string, int> wordFrequencies)
    {
        var words = Regex.Split(sentence.ToLowerInvariant(), @"[^a-z0-9]+")
            .Where(w => w.Length > 2)
            .ToList();

        if (words.Count == 0)
            return 0;

        double score = 0;
        foreach (var word in words)
        {
            if (wordFrequencies.TryGetValue(word, out var frequency))
                score += frequency;
        }

        // Normalize by sentence length to avoid bias toward longer sentences
        return score / words.Count;
    }

    /// <summary>
    /// Limits summary to maximum character length, cutting at sentence boundary if possible.
    /// </summary>
    private string LimitToMaxCharacters(string text)
    {
        if (text.Length <= _options.MaxCharacters)
            return text;

        var truncated = text[.._options.MaxCharacters];

        // Find last sentence boundary to avoid cutting mid-sentence
        var lastBoundary = truncated.LastIndexOfAny(new[] { '.', '!', '?' });
        if (lastBoundary > 0 && lastBoundary > _options.MaxCharacters - 100)
        {
            truncated = truncated[..(lastBoundary + 1)];
        }

        return truncated;
    }
}
