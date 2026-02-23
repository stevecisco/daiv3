namespace Daiv3.ModelExecution.Models;

/// <summary>
/// Snapshot of queue state.
/// </summary>
public class QueueStatus
{
    public int ImmediateCount { get; set; }  // P0
    public int NormalCount { get; set; }     // P1
    public int BackgroundCount { get; set; } //  P2
    public string? CurrentModelId { get; set; }
    public DateTimeOffset LastModelSwitch { get; set; }
}
