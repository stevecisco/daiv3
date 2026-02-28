namespace Daiv3.ModelExecution.Models;

/// <summary>
/// Context information for priority assignment decisions.
/// </summary>
public class PriorityContext
{
    /// <summary>Whether this is a user-facing request (as opposed to background work).</summary>
    public bool IsUserFacing { get; set; } = true;

    /// <summary>Whether this request is interactive (requires immediate response).</summary>
    public bool IsInteractive { get; set; } = true;

    /// <summary>Session ID if part of ongoing conversation or workflow.</summary>
    public string? SessionId { get; set; }

    /// <summary>Project ID if associated with a project.</summary>
    public string? ProjectId { get; set; }

    /// <summary>User ID for user-specific priority rules.</summary>
    public string? UserId { get; set; }

    /// <summary>Priority override (null = use rules-based assignment).</summary>
    public ExecutionPriority? PriorityOverride { get; set; }

    /// <summary>Whether this is a retry of a previously failed request.</summary>
    public bool IsRetry { get; set; } = false;

    /// <summary>Estimated processing time in milliseconds (if known).</summary>
    public int? EstimatedProcessingTimeMs { get; set; }

    /// <summary>Additional context key-value pairs.</summary>
    public Dictionary<string, string> AdditionalContext { get; set; } = new();
}
