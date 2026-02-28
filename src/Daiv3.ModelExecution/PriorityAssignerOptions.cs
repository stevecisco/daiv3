using Daiv3.ModelExecution.Models;

namespace Daiv3.ModelExecution;

/// <summary>
/// Configuration options for priority assignment.
/// </summary>
public class PriorityAssignerOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "PriorityAssigner";

    /// <summary>
    /// Task type to default priority mappings.
    /// Key: TaskType enum name, Value: ExecutionPriority enum name.
    /// </summary>
    public Dictionary<string, string> TaskTypePriorityMappings { get; set; } = new();

    /// <summary>
    /// Whether user-facing requests should always get Immediate priority.
    /// Default: false (allows task-based priority even for user-facing work).
    /// </summary>
    public bool UserFacingAlwaysImmediate { get; set; } = false;

    /// <summary>
    /// Whether interactive requests should get elevated priority.
    /// Default: true (interactive work gets at least Normal priority).
    /// </summary>
    public bool ElevateInteractivePriority { get; set; } = true;

    /// <summary>
    /// Whether retries should get elevated priority.
    /// Default: true (retries get higher priority to avoid repeated failures).
    /// </summary>
    public bool ElevateRetryPriority { get; set; } = true;

    /// <summary>
    /// Default priority for unknown task types.
    /// </summary>
    public ExecutionPriority DefaultPriority { get; set; } = ExecutionPriority.Normal;

    /// <summary>
    /// Default priority mappings for task types.
    /// </summary>
    public static Dictionary<string, string> GetDefaultMappings()
    {
        return new Dictionary<string, string>
        {
            // Interactive tasks get Immediate priority
            [nameof(TaskType.Chat)] = nameof(ExecutionPriority.Immediate),
            [nameof(TaskType.QuestionAnswer)] = nameof(ExecutionPriority.Immediate),
            
            // User-initiated tasks get Normal priority
            [nameof(TaskType.Code)] = nameof(ExecutionPriority.Normal),
            [nameof(TaskType.Rewrite)] = nameof(ExecutionPriority.Normal),
            [nameof(TaskType.Translation)] = nameof(ExecutionPriority.Normal),
            [nameof(TaskType.Generation)] = nameof(ExecutionPriority.Normal),
            
            // Background/batch tasks get Background priority
            [nameof(TaskType.Search)] = nameof(ExecutionPriority.Background),
            [nameof(TaskType.Summarize)] = nameof(ExecutionPriority.Background),
            [nameof(TaskType.Analysis)] = nameof(ExecutionPriority.Background),
            [nameof(TaskType.Extraction)] = nameof(ExecutionPriority.Background),
            
            // Unknown defaults to Normal
            [nameof(TaskType.Unknown)] = nameof(ExecutionPriority.Normal)
        };
    }
}
