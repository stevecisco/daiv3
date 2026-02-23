namespace Daiv3.ModelExecution.Models;

/// <summary>
/// Result of request execution.
/// </summary>
public class ExecutionResult
{
    /// <summary>Corresponding request ID.</summary>
    public Guid RequestId { get; set; }

    /// <summary>Model output text.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Execution status.</summary>
    public ExecutionStatus Status { get; set; }

    /// <summary>When execution completed.</summary>
    public DateTimeOffset CompletedAt { get; set; }

    /// <summary>Error message if Status == Failed.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Token usage statistics.</summary>
    public TokenUsage TokenUsage { get; set; } = new();
}
