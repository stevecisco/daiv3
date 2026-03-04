using System.Collections.Concurrent;

namespace Daiv3.Orchestration;

/// <summary>
/// Provides control over a running agent execution, allowing pause, resume, and stop operations.
/// </summary>
public sealed class AgentExecutionControl : IDisposable
{
    private readonly ManualResetEventSlim _pauseEvent = new(initialState: true);
    private readonly CancellationTokenSource _stopTokenSource = new();
    private bool _isPaused;
    private bool _isStopped;
    private bool _disposed;
    private DateTimeOffset? _pausedAt;
    private TimeSpan _totalPausedDuration = TimeSpan.Zero;

    /// <summary>
    /// Gets the unique identifier for this execution.
    /// </summary>
    public Guid ExecutionId { get; }

    /// <summary>
    /// Gets the agent ID for this execution.
    /// </summary>
    public Guid AgentId { get; }

    /// <summary>
    /// Gets whether the execution is currently paused.
    /// </summary>
    public bool IsPaused
    {
        get
        {
            lock (_pauseEvent)
            {
                return _isPaused;
            }
        }
    }

    /// <summary>
    /// Gets whether the execution has been stopped.
    /// </summary>
    public bool IsStopped
    {
        get
        {
            lock (_pauseEvent)
            {
                return _isStopped;
            }
        }
    }

    /// <summary>
    /// Gets the total duration the execution has been paused.
    /// </summary>
    public TimeSpan TotalPausedDuration
    {
        get
        {
            lock (_pauseEvent)
            {
                return _totalPausedDuration;
            }
        }
    }

    /// <summary>
    /// Gets the cancellation token that is triggered when Stop() is called.
    /// </summary>
    internal CancellationToken StopToken => _stopTokenSource.Token;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentExecutionControl"/> class.
    /// </summary>
    /// <param name="executionId">The execution identifier.</param>
    /// <param name="agentId">The agent identifier.</param>
    public AgentExecutionControl(Guid executionId, Guid agentId)
    {
        ExecutionId = executionId;
        AgentId = agentId;
    }

    /// <summary>
    /// Pauses the execution. The execution will block until Resume() is called.
    /// </summary>
    public void Pause()
    {
        lock (_pauseEvent)
        {
            if (_isStopped)
            {
                throw new InvalidOperationException("Cannot pause a stopped execution.");
            }

            if (_isPaused)
            {
                return; // Already paused
            }

            _isPaused = true;
            _pausedAt = DateTimeOffset.UtcNow;
            _pauseEvent.Reset(); // Block execution
        }
    }

    /// <summary>
    /// Resumes the execution if it is paused.
    /// </summary>
    public void Resume()
    {
        lock (_pauseEvent)
        {
            if (_isStopped)
            {
                throw new InvalidOperationException("Cannot resume a stopped execution.");
            }

            if (!_isPaused)
            {
                return; // Not paused
            }

            _isPaused = false;

            if (_pausedAt.HasValue)
            {
                var pauseDuration = DateTimeOffset.UtcNow - _pausedAt.Value;
                _totalPausedDuration += pauseDuration;
                _pausedAt = null;
            }

            _pauseEvent.Set(); // Unblock execution
        }
    }

    /// <summary>
    /// Stops the execution. This triggers the stop token and unblocks any paused state.
    /// </summary>
    public void Stop()
    {
        lock (_pauseEvent)
        {
            if (_isStopped)
            {
                return; // Already stopped
            }

            _isStopped = true;

            // If paused, update pause duration
            if (_isPaused && _pausedAt.HasValue)
            {
                var pauseDuration = DateTimeOffset.UtcNow - _pausedAt.Value;
                _totalPausedDuration += pauseDuration;
                _pausedAt = null;
            }

            // Unblock if paused
            _pauseEvent.Set();

            // Trigger cancellation
            _stopTokenSource.Cancel();
        }
    }

    /// <summary>
    /// Waits until the execution is not paused. This is called internally by the execution loop.
    /// </summary>
    internal void WaitIfPaused()
    {
        _pauseEvent.Wait(StopToken);
    }

    /// <summary>
    /// Disposes the resources used by this execution control.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _pauseEvent?.Dispose();
        _stopTokenSource?.Dispose();
        _disposed = true;
    }
}

/// <summary>
/// Manages active agent executions and their control objects.
/// </summary>
internal class AgentExecutionRegistry
{
    private readonly ConcurrentDictionary<Guid, AgentExecutionControl> _activeExecutions = new();

    /// <summary>
    /// Registers a new execution.
    /// </summary>
    public void RegisterExecution(AgentExecutionControl control)
    {
        _activeExecutions.TryAdd(control.ExecutionId, control);
    }

    /// <summary>
    /// Gets the execution control for a given execution ID.
    /// </summary>
    public AgentExecutionControl? GetExecution(Guid executionId)
    {
        _activeExecutions.TryGetValue(executionId, out var control);
        return control;
    }

    /// <summary>
    /// Unregisters an execution when it completes.
    /// </summary>
    public void UnregisterExecution(Guid executionId)
    {
        if (_activeExecutions.TryRemove(executionId, out var control))
        {
            control.Dispose();
        }
    }

    /// <summary>
    /// Gets all active executions.
    /// </summary>
    public IReadOnlyCollection<AgentExecutionControl> GetActiveExecutions()
    {
        return _activeExecutions.Values.ToList();
    }
}
