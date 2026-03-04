using Daiv3.Orchestration.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Daiv3.Orchestration;

/// <summary>
/// Resolves user intent from natural language input using pattern matching and classification.
/// </summary>
public class IntentResolver : IIntentResolver
{
    private readonly ILogger<IntentResolver> _logger;
    private readonly OrchestrationOptions _options;

    // Intent patterns for simple rule-based classification
    private static readonly Dictionary<string, List<string>> IntentPatterns = new()
    {
        ["search"] = new() { "search", "find", "look for", "locate", "query" },
        ["chat"] = new() { "chat", "talk", "ask", "tell me", "explain", "what is", "how do", "help" },
        ["create"] = new() { "create", "make", "generate", "build", "new" },
        ["analyze"] = new() { "analyze", "review", "examine", "check", "inspect", "evaluate" },
        ["summarize"] = new() { "summarize", "summary", "tldr", "brief", "overview" },
        ["code"] = new() { "code", "implement", "write code", "program", "develop" },
        ["debug"] = new() { "debug", "fix", "solve", "error", "issue", "problem" },
        ["test"] = new() { "test", "verify", "validate", "check" }
    };

    public IntentResolver(
        ILogger<IntentResolver> logger,
        IOptions<OrchestrationOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public Task<Intent> ResolveAsync(
        string userInput,
        Dictionary<string, string> context,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userInput);
        ArgumentNullException.ThrowIfNull(context);

        _logger.LogInformation("Resolving intent for: {Input}", userInput);

        var normalizedInput = userInput.ToLowerInvariant();

        // Simple pattern-based matching
        // TODO: Replace with ML-based intent classification using specified model
        var (intentType, confidence) = ClassifyIntent(normalizedInput);

        // Extract basic entities (simple keyword extraction)
        var entities = ExtractEntities(userInput, context);

        var intent = new Intent
        {
            Type = intentType,
            Entities = entities,
            Confidence = confidence
        };

        _logger.LogInformation(
            "Resolved intent: {Type} (confidence: {Confidence:P2})",
            intent.Type, intent.Confidence);

        return Task.FromResult(intent);
    }

    private (string Type, decimal Confidence) ClassifyIntent(string normalizedInput)
    {
        var scores = new Dictionary<string, int>();

        // Score each intent based on pattern matches
        foreach (var (intentType, patterns) in IntentPatterns)
        {
            var matchCount = patterns.Count(pattern => normalizedInput.Contains(pattern));
            if (matchCount > 0)
            {
                scores[intentType] = matchCount;
            }
        }

        // If no patterns match, default to "chat"
        if (scores.Count == 0)
        {
            _logger.LogInformation("No clear intent pattern matched, defaulting to 'chat'");
            return ("chat", 0.7m);
        }

        // Return intent with highest score
        var bestIntent = scores.OrderByDescending(kvp => kvp.Value).First();

        // Calculate confidence based on score strength
        // Simple heuristic: higher match count = higher confidence
        var totalMatches = scores.Values.Sum();
        var confidence = Math.Min(0.95m, 0.6m + (0.35m * bestIntent.Value / totalMatches));

        _logger.LogDebug(
            "Intent classification scores: {Scores}",
            string.Join(", ", scores.Select(kvp => $"{kvp.Key}={kvp.Value}")));

        return (bestIntent.Key, confidence);
    }

    private Dictionary<string, string> ExtractEntities(string userInput, Dictionary<string, string> context)
    {
        var entities = new Dictionary<string, string>(context);

        // Simple entity extraction
        // TODO: Implement proper NER (Named Entity Recognition)

        // Extract file extensions
        var extensionMatch = System.Text.RegularExpressions.Regex.Match(userInput, @"\.(cs|txt|md|json|xml|yaml|py|java)\b");
        if (extensionMatch.Success)
        {
            entities["file_type"] = extensionMatch.Value;
        }

        // Extract quoted text as potential entity values
        var quotedMatches = System.Text.RegularExpressions.Regex.Matches(userInput, @"""([^""]+)""");
        if (quotedMatches.Count > 0)
        {
            for (int i = 0; i < quotedMatches.Count; i++)
            {
                entities[$"quoted_{i}"] = quotedMatches[i].Groups[1].Value;
            }
        }

        return entities;
    }
}
