using Daiv3.ModelExecution.Interfaces;
using Daiv3.ModelExecution.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Daiv3.ModelExecution;

/// <summary>
/// Assigns queue priority based on task type and context.
/// </summary>
public class PriorityAssigner : IPriorityAssigner
{
    private readonly PriorityAssignerOptions _options;
    private readonly ILogger<PriorityAssigner> _logger;
    private readonly Dictionary<TaskType, ExecutionPriority> _taskTypePriorityMappings;

    public PriorityAssigner(
        IOptions<PriorityAssignerOptions> options,
        ILogger<PriorityAssigner> logger)
    {
        _options = options.Value;
        _logger = logger;

        // Initialize default mappings if none configured
        if (_options.TaskTypePriorityMappings.Count == 0)
        {
            _options.TaskTypePriorityMappings = PriorityAssignerOptions.GetDefaultMappings();
        }

        // Build runtime task type mappings
        _taskTypePriorityMappings = new Dictionary<TaskType, ExecutionPriority>();
        foreach (var mapping in _options.TaskTypePriorityMappings)
        {
            if (Enum.TryParse<TaskType>(mapping.Key, ignoreCase: true, out var taskType) &&
                Enum.TryParse<ExecutionPriority>(mapping.Value, ignoreCase: true, out var priority))
            {
                _taskTypePriorityMappings[taskType] = priority;
            }
        }
    }

    public ExecutionPriority AssignPriority(TaskType taskType, PriorityContext? context = null)
    {
        context ??= new PriorityContext();

        // 1. Check for explicit priority override
        if (context.PriorityOverride.HasValue)
        {
            _logger.LogDebug(
                "Using explicit priority override {Priority} for task type {TaskType}",
                context.PriorityOverride.Value, taskType);
            return context.PriorityOverride.Value;
        }

        // 2. Get base priority from task type mapping
        var basePriority = GetDefaultPriority(taskType);

        // 3. Apply context-based adjustments

        // User-facing requests can be elevated to Immediate
        if (context.IsUserFacing && _options.UserFacingAlwaysImmediate)
        {
            _logger.LogDebug(
                "Elevating to Immediate priority for user-facing request (task type: {TaskType})",
                taskType);
            return ExecutionPriority.Immediate;
        }

        // Interactive requests should be at least Normal priority
        if (context.IsInteractive && _options.ElevateInteractivePriority)
        {
            if (basePriority > ExecutionPriority.Normal)
            {
                _logger.LogDebug(
                    "Elevating from {BasePriority} to Normal for interactive request (task type: {TaskType})",
                    basePriority, taskType);
                basePriority = ExecutionPriority.Normal;
            }
        }

        // Retries get elevated priority to avoid repeated failures
        if (context.IsRetry && _options.ElevateRetryPriority)
        {
            var elevatedPriority = basePriority switch
            {
                ExecutionPriority.Background => ExecutionPriority.Normal,
                ExecutionPriority.Normal => ExecutionPriority.Immediate,
                _ => basePriority
            };

            if (elevatedPriority != basePriority)
            {
                _logger.LogDebug(
                    "Elevating retry request from {BasePriority} to {ElevatedPriority} (task type: {TaskType})",
                    basePriority, elevatedPriority, taskType);
                return elevatedPriority;
            }
        }

        _logger.LogDebug(
            "Assigned priority {Priority} for task type {TaskType} " +
            "(IsUserFacing: {IsUserFacing}, IsInteractive: {IsInteractive}, IsRetry: {IsRetry})",
            basePriority, taskType, context.IsUserFacing, context.IsInteractive, context.IsRetry);

        return basePriority;
    }

    public ExecutionPriority GetDefaultPriority(TaskType taskType)
    {
        if (_taskTypePriorityMappings.TryGetValue(taskType, out var priority))
        {
            return priority;
        }

        return _options.DefaultPriority;
    }
}
