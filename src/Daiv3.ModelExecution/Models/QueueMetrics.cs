namespace Daiv3.ModelExecution.Models;

/// <summary>
/// Observable metrics for queue behavior and execution outcomes.
/// </summary>
public class QueueMetrics
{
    /// <summary>Total number of requests enqueued.</summary>
    public long TotalEnqueued { get; set; }

    /// <summary>Total number of requests selected for execution.</summary>
    public long TotalDequeued { get; set; }

    /// <summary>Total number of successfully completed requests.</summary>
    public long TotalCompleted { get; set; }

    /// <summary>Total number of failed requests.</summary>
    public long TotalFailed { get; set; }

    /// <summary>Total number of requests preempted and requeued.</summary>
    public long TotalPreempted { get; set; }

    /// <summary>Total number of requests routed to local execution.</summary>
    public long TotalLocalExecutions { get; set; }

    /// <summary>Total number of requests routed to online providers.</summary>
    public long TotalOnlineExecutions { get; set; }

    /// <summary>Current number of requests in active execution.</summary>
    public long InFlightExecutions { get; set; }

    /// <summary>Average wait time in queue before execution starts.</summary>
    public double AverageQueueWaitMs { get; set; }

    /// <summary>Average execution duration from dispatch to completion/failure.</summary>
    public double AverageExecutionDurationMs { get; set; }

    /// <summary>Most recent dequeue timestamp.</summary>
    public DateTimeOffset? LastDequeuedAt { get; set; }

    /// <summary>Current point-in-time queue depth by priority.</summary>
    public QueueStatus QueueStatus { get; set; } = new();
}
