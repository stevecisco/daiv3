using System.Text.RegularExpressions;
using Daiv3.ModelExecution.Interfaces;
using Daiv3.ModelExecution.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Daiv3.ModelExecution;

/// <summary>
/// Pattern-based task type classifier for execution requests.
/// </summary>
/// <remarks>
/// Uses keyword and phrase matching to classify requests. Can be enhanced
/// with ML-based classification in future versions (MQ-REQ-011).
/// </remarks>
public class TaskTypeClassifier : ITaskTypeClassifier
{
    private readonly TaskTypeClassifierOptions _options;
    private readonly ILogger<TaskTypeClassifier> _logger;
    private readonly Dictionary<TaskType, string[]> _patterns;

    public TaskTypeClassifier(
        IOptions<TaskTypeClassifierOptions> options,
        ILogger<TaskTypeClassifier> logger)
    {
        _options = options.Value;
        _logger = logger;
        _patterns = InitializePatterns();
    }

    public TaskType Classify(ExecutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        // If request already has an explicit task type, respect it (if enabled)
        if (_options.UseExplicitTaskType && !string.IsNullOrWhiteSpace(request.TaskType))
        {
            if (Enum.TryParse<TaskType>(request.TaskType, ignoreCase: true, out var explicitType))
            {
                _logger.LogDebug(
                    "Using explicit task type {TaskType} for request {RequestId}",
                    explicitType, request.Id);
                return explicitType;
            }
        }

        // Classify based on content and context
        return Classify(request.Content);
    }

    public TaskType Classify(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var scores = new Dictionary<TaskType, int>();

        // Initialize scores for all task types (except Unknown)
        foreach (var taskType in Enum.GetValues<TaskType>())
        {
            if (taskType != TaskType.Unknown)
            {
                scores[taskType] = 0;
            }
        }

        // Score each task type based on pattern matches
        var comparison = _options.CaseInsensitiveMatching
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        foreach (var (taskType, patterns) in _patterns)
        {
            foreach (var pattern in patterns)
            {
                if (content.Contains(pattern, comparison))
                {
                    scores[taskType]++;
                }
            }
        }

        // Find task type with highest score
        var bestMatch = scores.MaxBy(kvp => kvp.Value);
        
        if (bestMatch.Value == 0)
        {
            _logger.LogDebug("No patterns matched for content, returning Unknown");
            return TaskType.Unknown;
        }

        // Check confidence threshold (simple approach: at least 1 match = 100% confidence for now)
        // In future versions, this can be enhanced with weighted patterns and ML confidence scores
        var confidence = bestMatch.Value > 0 ? 1.0 : 0.0;

        if (confidence < _options.MinimumConfidence)
        {
            _logger.LogDebug(
                "Confidence {Confidence:F2} below threshold {Threshold:F2}, returning Unknown",
                confidence, _options.MinimumConfidence);
            return TaskType.Unknown;
        }

        _logger.LogDebug(
            "Classified request as {TaskType} with {Score} pattern matches",
            bestMatch.Key, bestMatch.Value);

        return bestMatch.Key;
    }

    private Dictionary<TaskType, string[]> InitializePatterns()
    {
        var patterns = new Dictionary<TaskType, string[]>
        {
            [TaskType.Chat] = new[]
            {
                "chat", "talk", "conversation", "discuss", "tell me about",
                "what do you think", "can you help", "i need help"
            },
            [TaskType.Search] = new[]
            {
                "search", "find", "look for", "locate", "where is",
                "show me", "what is the", "where can i find"
            },
            [TaskType.Summarize] = new[]
            {
                "summarize", "summary", "brief", "overview", "tldr",
                "in short", "key points", "main ideas", "condense"
            },
            [TaskType.Code] = new[]
            {
                "code", "function", "class", "method", "implement",
                "program", "script", "debug", "refactor", "algorithm",
                "variable", "syntax", "compile", "import", "def ", "public ", "private "
            },
            [TaskType.QuestionAnswer] = new[]
            {
                "what", "why", "how", "when", "where", "who",
                "explain", "describe", "?", "answer", "question"
            },
            [TaskType.Rewrite] = new[]
            {
                "rewrite", "rephrase", "paraphrase", "improve", "edit",
                "revise", "change", "modify", "reword", "better version"
            },
            [TaskType.Translation] = new[]
            {
                "translate", "translation", "convert to", "in spanish",
                "in french", "in german", "in chinese", "in japanese", "language"
            },
            [TaskType.Analysis] = new[]
            {
                "analyze", "analysis", "evaluate", "assess", "examine",
                "inspect", "review", "compare", "contrast", "pros and cons"
            },
            [TaskType.Generation] = new[]
            {
                "generate", "create", "make", "build", "produce",
                "write", "compose", "draft", "design", "develop"
            },
            [TaskType.Extraction] = new[]
            {
                "extract", "extraction", "get", "pull out", "identify",
                "list", "enumerate", "parse", "retrieve", "obtain"
            }
        };

        // Merge with custom patterns from configuration
        foreach (var customPattern in _options.CustomPatterns)
        {
            if (Enum.TryParse<TaskType>(customPattern.Key, ignoreCase: true, out var taskType))
            {
                if (patterns.ContainsKey(taskType))
                {
                    var combined = patterns[taskType].Concat(customPattern.Value).ToArray();
                    patterns[taskType] = combined;
                }
                else
                {
                    patterns[taskType] = customPattern.Value;
                }
            }
        }

        return patterns;
    }
}
