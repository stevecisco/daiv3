namespace Daiv3.ModelExecution.Models;

/// <summary>
/// Execution request priority level.
/// </summary>
public enum ExecutionPriority
{
    /// <summary>Immediate: Preempt current model, switch if needed</summary>
    Immediate = 0,

    /// <summary>Normal: Batch with current model, then execute</summary>
    Normal = 1,

    /// <summary>Background: Batch and drain before model switch</summary>
    Background = 2
}
