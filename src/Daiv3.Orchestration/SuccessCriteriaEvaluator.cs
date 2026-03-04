using Daiv3.Orchestration.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace Daiv3.Orchestration;

/// <summary>
/// Evaluates agent output against success criteria.
/// Currently uses pattern-based and keyword-based evaluation.
/// Future: Integrate with LLM-based evaluation.
/// </summary>
public class SuccessCriteriaEvaluator : ISuccessCriteriaEvaluator
{
    private readonly ILogger<SuccessCriteriaEvaluator> _logger;

    public SuccessCriteriaEvaluator(ILogger<SuccessCriteriaEvaluator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task<SuccessEvaluationResult> EvaluateAsync(
        string? successCriteria,
        string output,
        SuccessCriteriaContext context,
        CancellationToken ct = default)
    {
        // Null or empty criteria always pass
        if (string.IsNullOrWhiteSpace(successCriteria))
        {
            _logger.LogDebug("Success criteria is empty, evaluation passes by default");
            return Task.FromResult(new SuccessEvaluationResult
            {
                MeetsCriteria = true,
                ConfidenceScore = 1.0m,
                EvaluationMessage = "No success criteria specified - output accepted",
                EvaluationMethod = "Default"
            });
        }

        if (string.IsNullOrWhiteSpace(output))
        {
            _logger.LogDebug("Output is empty, evaluation fails");
            return Task.FromResult(new SuccessEvaluationResult
            {
                MeetsCriteria = false,
                ConfidenceScore = 1.0m,
                EvaluationMessage = "Output is empty",
                EvaluationMethod = "EmptyCheck"
            });
        }

        // Validate criteria first
        var validation = Validate(successCriteria);
        if (!validation.IsValid)
        {
            _logger.LogWarning("Success criteria failed validation: {Errors}", string.Join(", ", validation.Errors));
            return Task.FromResult(new SuccessEvaluationResult
            {
                MeetsCriteria = false,
                ConfidenceScore = 0.5m,
                EvaluationMessage = $"Invalid criteria: {string.Join(", ", validation.Errors)}",
                EvaluationMethod = "ValidationFailed"
            });
        }

        // Evaluate using pattern-based approach
        var result = EvaluatePatternBased(successCriteria, output, context);

        _logger.LogDebug(
            "Success criteria evaluation completed. Iteration: {Iteration}, MeetsCriteria: {MeetsCriteria}, Confidence: {Confidence}, Method: {Method}",
            context.IterationNumber,
            result.MeetsCriteria,
            result.ConfidenceScore,
            result.EvaluationMethod);

        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public SuccessCriteriaValidationResult Validate(string? successCriteria)
    {
        var result = new SuccessCriteriaValidationResult { IsValid = true };

        if (string.IsNullOrWhiteSpace(successCriteria))
        {
            return result; // Empty criteria is valid (means no constraint)
        }

        var criteria = successCriteria.Trim();

        // Check for minimum reasonable length
        if (criteria.Length < 5)
        {
            result.Warnings.Add("Criteria is very short, may be too vague for meaningful evaluation");
        }

        // Check for obvious syntax patterns
        if (criteria.Contains("&&") && !IsValidLogicalExpression(criteria, "&&"))
        {
            result.Errors.Add("Invalid AND (&&) syntax in criteria");
            result.IsValid = false;
        }

        if (criteria.Contains("||") && !IsValidLogicalExpression(criteria, "||"))
        {
            result.Errors.Add("Invalid OR (||) syntax in criteria");
            result.IsValid = false;
        }

        // Check for unmatched parentheses
        if (!AreParenthesesBalanced(criteria))
        {
            result.Errors.Add("Unmatched parentheses in criteria");
            result.IsValid = false;
        }

        return result;
    }

    /// <summary>
    /// Evaluates criteria using pattern-based approach (keyword matching, presence checks, etc).
    /// </summary>
    private SuccessEvaluationResult EvaluatePatternBased(
        string criteria,
        string output,
        SuccessCriteriaContext context)
    {
        var lowerCriteria = criteria.ToLowerInvariant();
        var lowerOutput = output.ToLowerInvariant();

        // Check for negation patterns (NOT/shouldn't/must not)
        if (lowerCriteria.Contains("not ") || lowerCriteria.Contains("shouldn't") || lowerCriteria.Contains("must not"))
        {
            return EvaluateNegationCriteria(criteria, output, lowerCriteria, lowerOutput, context);
        }

        // Check for keyword/phrase presence (before other checks to avoid false positives)
        if (lowerCriteria.Contains("contain") || lowerCriteria.Contains("include"))
        {
            return EvaluateKeywordPresenceCriteria(criteria, lowerOutput, context);
        }

        // Check for format/structure criteria
        if (lowerCriteria.Contains("format") || lowerCriteria.Contains("structure") ||
            lowerCriteria.Contains("json") || lowerCriteria.Contains("xml") ||
            lowerCriteria.Contains("list") || lowerCriteria.Contains("table"))
        {
            return EvaluateFormatCriteria(criteria, output, context);
        }

        // Check for validation/valid patterns
        if (lowerCriteria.Contains("valid") || lowerCriteria.Contains("pass") ||
            lowerCriteria.Contains("compil") || lowerCriteria.Contains("error-free"))
        {
            return EvaluateValidationCriteria(output, context);
        }

        // Check for length/size constraints using regex (matches numeric patterns like "50 characters" or "10 words")
        if (Regex.IsMatch(criteria, @"\d+\s*(character|word|line)", RegexOptions.IgnoreCase))
        {
            return EvaluateLengthCriteria(criteria, output, context);
        }

        // Default: Check if all keywords from criteria appear in output
        return EvaluateKeywordMatchCriteria(criteria, lowerOutput, context);
    }

    private SuccessEvaluationResult EvaluateNegationCriteria(
        string criteria, string output, string lowerCriteria, string lowerOutput, SuccessCriteriaContext context)
    {
        var extractedKeywords = ExtractKeywords(criteria);
        var hasNegatedKeywords = extractedKeywords.Any(kw => lowerOutput.Contains(kw.ToLowerInvariant()));

        var meets = !hasNegatedKeywords;
        return new SuccessEvaluationResult
        {
            MeetsCriteria = meets,
            ConfidenceScore = 0.85m,
            EvaluationMessage = meets
                ? $"Output correctly avoids forbidden words: {string.Join(", ", extractedKeywords)}"
                : $"Output contains forbidden words that should be avoided",
            EvaluationMethod = "NegationPattern",
            SuggestedCorrection = meets ? null : $"Remove or rephrase to avoid: {string.Join(", ", extractedKeywords)}"
        };
    }

    private SuccessEvaluationResult EvaluateKeywordPresenceCriteria(
        string criteria, string lowerOutput, SuccessCriteriaContext context)
    {
        var keywords = ExtractKeywordsForPresenceCriteria(criteria);
        var presentKeywords = keywords.Where(kw => lowerOutput.Contains(kw.ToLowerInvariant())).ToList();
        var missingKeywords = keywords.Except(presentKeywords, StringComparer.OrdinalIgnoreCase).ToList();

        var meets = missingKeywords.Count == 0;
        var confidence = keywords.Count > 0 ? decimal.Divide(presentKeywords.Count, keywords.Count) : 1.0m;

        return new SuccessEvaluationResult
        {
            MeetsCriteria = meets,
            ConfidenceScore = confidence,
            EvaluationMessage = meets
                ? $"All required keywords present: {string.Join(", ", keywords)}"
                : $"Missing keywords: {string.Join(", ", missingKeywords)}",
            EvaluationMethod = "KeywordPresence",
            SuggestedCorrection = missingKeywords.Count > 0 ? $"Add content covering: {string.Join(", ", missingKeywords)}" : null
        };
    }

    private SuccessEvaluationResult EvaluateFormatCriteria(
        string criteria, string output, SuccessCriteriaContext context)
    {
        var lowerCriteria = criteria.ToLowerInvariant();

        // Check for JSON format
        if (lowerCriteria.Contains("json"))
        {
            var isValidJson = TryParseAsJson(output);
            return new SuccessEvaluationResult
            {
                MeetsCriteria = isValidJson,
                ConfidenceScore = 0.95m,
                EvaluationMessage = isValidJson ? "Output is valid JSON" : "Output is not valid JSON",
                EvaluationMethod = "JsonFormat",
                SuggestedCorrection = isValidJson ? null : "Ensure output is properly formatted JSON"
            };
        }

        // Check for list/lines format
        if (lowerCriteria.Contains("list") || lowerCriteria.Contains("line"))
        {
            var lineCount = output.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries).Length;
            var meets = lineCount > 0;
            return new SuccessEvaluationResult
            {
                MeetsCriteria = meets,
                ConfidenceScore = 0.8m,
                EvaluationMessage = $"Output contains {lineCount} line(s)",
                EvaluationMethod = "ListFormat",
                SuggestedCorrection = meets ? null : "Output should be formatted as a list"
            };
        }

        // Default format check - output should be non-empty and have some structure
        var meets_default = !string.IsNullOrWhiteSpace(output);
        return new SuccessEvaluationResult
        {
            MeetsCriteria = meets_default,
            ConfidenceScore = 0.7m,
            EvaluationMessage = meets_default ? "Output has structure" : "Output is empty or unstructured",
            EvaluationMethod = "FormatBasic",
            SuggestedCorrection = meets_default ? null : "Ensure output is properly structured"
        };
    }

    private SuccessEvaluationResult EvaluateValidationCriteria(
        string output, SuccessCriteriaContext context)
    {
        // Simple validation: non-empty output without obvious error indicators
        var lowerOutput = output.ToLowerInvariant();
        var hasErrorIndicators = lowerOutput.Contains("error") || lowerOutput.Contains("failed") ||
                                 lowerOutput.Contains("exception") || lowerOutput.Contains("invalid");

        var meets = !hasErrorIndicators && !string.IsNullOrWhiteSpace(output);
        return new SuccessEvaluationResult
        {
            MeetsCriteria = meets,
            ConfidenceScore = 0.75m,
            EvaluationMessage = meets ? "Output appears valid (no error indicators)" : "Output contains error indicators",
            EvaluationMethod = "ValidationCheck",
            SuggestedCorrection = meets ? null : "Fix any errors or exceptions and retry"
        };
    }

    private SuccessEvaluationResult EvaluateLengthCriteria(
        string criteria, string output, SuccessCriteriaContext context)
    {
        // Extract length constraints from criteria
        var numberMatch = Regex.Match(criteria, @"(\d+)\s*(character|word|line)", RegexOptions.IgnoreCase);
        if (!numberMatch.Success)
        {
            // No specific length found, just check non-empty
            var meets_default = !string.IsNullOrWhiteSpace(output);
            return new SuccessEvaluationResult
            {
                MeetsCriteria = meets_default,
                ConfidenceScore = 0.6m,
                EvaluationMessage = $"Output length: {output.Length} characters",
                EvaluationMethod = "LengthCheck"
            };
        }

        var targetLength = int.Parse(numberMatch.Groups[1].Value);
        var type = numberMatch.Groups[2].Value.ToLowerInvariant();

        int actualLength = type switch
        {
            "word" => output.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length,
            "line" => output.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries).Length,
            _ => output.Length
        };

        var meets = actualLength >= targetLength;
        return new SuccessEvaluationResult
        {
            MeetsCriteria = meets,
            ConfidenceScore = 0.9m,
            EvaluationMessage = $"Output {type}: {actualLength} (required: {targetLength})",
            EvaluationMethod = "LengthValidation",
            SuggestedCorrection = meets ? null : $"Expand output to at least {targetLength} {type}(s)"
        };
    }

    private SuccessEvaluationResult EvaluateKeywordMatchCriteria(
        string criteria, string lowerOutput, SuccessCriteriaContext context)
    {
        var keywords = ExtractKeywords(criteria);
        var matchedKeywords = keywords.Where(kw => lowerOutput.Contains(kw.ToLowerInvariant())).ToList();
        var matchPercentage = keywords.Count > 0 ? (decimal)matchedKeywords.Count / keywords.Count : 1.0m;

        // Criteria are met if at least 70% of keywords are present, or high confidence match
        var meets = matchPercentage >= 0.7m;
        var confidence = matchPercentage;

        return new SuccessEvaluationResult
        {
            MeetsCriteria = meets,
            ConfidenceScore = Math.Min(confidence, 1.0m),
            EvaluationMessage = $"Matched {matchedKeywords.Count}/{keywords.Count} keywords ({(int)(matchPercentage * 100)}%)",
            EvaluationMethod = "KeywordMatch",
            SuggestedCorrection = meets ? null : $"Include more details about: {string.Join(", ", keywords)}"
        };
    }

    private List<string> ExtractKeywords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new List<string>();

        // Remove common stop words and extract meaningful keywords
        var stopWords = new[] { "the", "a", "an", "and", "or", "but", "in", "on", "at", "to", "for", "of", "is", "are" };

        var words = Regex.Matches(text, @"\b[a-z]+\b", RegexOptions.IgnoreCase)
            .Cast<Match>()
            .Select(m => m.Value)
            .Where(w => !stopWords.Contains(w.ToLowerInvariant()) && w.Length > 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return words;
    }

    private List<string> ExtractKeywordsForPresenceCriteria(string criteria)
    {
        if (string.IsNullOrWhiteSpace(criteria))
            return new List<string>();

        var keywords = new List<string>();

        // Try to extract quoted phrases first (e.g., "Output must contain the word 'success'" -> "success")
        // Pattern matches text within single or double quotes
        var quotedMatches = Regex.Matches(criteria, "(['\"])([^'\"]*?)\\1");
        if (quotedMatches.Count > 0)
        {
            foreach (Match match in quotedMatches)
            {
                if (match.Groups.Count > 2)
                {
                    var quoted = match.Groups[2].Value.Trim();
                    if (!string.IsNullOrWhiteSpace(quoted) && quoted.Length > 0)
                    {
                        keywords.Add(quoted);
                    }
                }
            }
        }

        // If no quoted phrases, extract meaningful words that typically come after "contain" or "include"
        if (keywords.Count == 0)
        {
            // Look for patterns like "must contain X" or "must include Y"
            var containMatch = Regex.Match(criteria, @"(?:contain|include)(?:s)?\s+([^,\.!?]+)", RegexOptions.IgnoreCase);
            if (containMatch.Success)
            {
                var phrase = containMatch.Groups[1].Value.Trim();
                // Extract meaningful words (length > 3 for more meaningful keywords)
                var words = Regex.Matches(phrase, @"\b[a-z]+\b", RegexOptions.IgnoreCase)
                    .Cast<Match>()
                    .Select(m => m.Value)
                    .Where(w => w.Length > 2 && !new[] { "the", "and", "or", "but", "for", "the" }.Contains(w.ToLowerInvariant()))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                keywords.AddRange(words);
            }
        }

        // Fall back to general keyword extraction if nothing found
        if (keywords.Count == 0)
        {
            keywords = ExtractKeywords(criteria);
        }

        return keywords;
    }

    private bool TryParseAsJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        try
        {
            var trimmed = text.Trim();
            if (!((trimmed.StartsWith("{") && trimmed.EndsWith("}")) ||
                  (trimmed.StartsWith("[") && trimmed.EndsWith("]"))))
            {
                return false;
            }

            // Very basic JSON validation - just check structure
            System.Text.Json.JsonDocument.Parse(text);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool IsValidLogicalExpression(string criteria, string op)
    {
        var parts = criteria.Split(new[] { op }, StringSplitOptions.None);
        return parts.Length >= 2 && parts.All(p => !string.IsNullOrWhiteSpace(p));
    }

    private bool AreParenthesesBalanced(string text)
    {
        var openCount = text.Count(c => c == '(');
        var closeCount = text.Count(c => c == ')');
        return openCount == closeCount;
    }
}
