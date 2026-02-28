using Daiv3.Orchestration.Interfaces;
using Daiv3.Orchestration.Messaging;
using Daiv3.Persistence;
using Daiv3.Persistence.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daiv3.Orchestration;

/// <summary>
/// Manages agent lifecycle and execution.
/// Persists agent definitions in user-editable JSON format to SQLite.
/// </summary>
public class AgentManager : IAgentManager
{
    private readonly ILogger<AgentManager> _logger;
    private readonly AgentRepository _agentRepository;
    private readonly IMessageBroker _messageBroker;
    private readonly OrchestrationOptions _options;
    private readonly ConcurrentDictionary<Guid, Agent> _agents = new();
    private readonly ConcurrentDictionary<string, Guid> _taskTypeAgentIds = new(StringComparer.OrdinalIgnoreCase);

    public AgentManager(
        ILogger<AgentManager> logger,
        AgentRepository agentRepository,
        IMessageBroker messageBroker,
        IOptions<OrchestrationOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
        _messageBroker = messageBroker ?? throw new ArgumentNullException(nameof(messageBroker));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async Task<Agent> CreateAgentAsync(AgentDefinition definition, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Purpose);

        // Check if agent with same name already exists
        var existing = await _agentRepository.GetByNameAsync(definition.Name, ct).ConfigureAwait(false);
        if (existing != null)
        {
            _logger.LogWarning("Agent with name '{Name}' already exists", definition.Name);
            throw new InvalidOperationException($"Agent with name '{definition.Name}' already exists");
        }

        var agentId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var agent = new Agent
        {
            Id = agentId,
            Name = definition.Name,
            Purpose = definition.Purpose,
            EnabledSkills = new List<string>(definition.EnabledSkills),
            CreatedAt = DateTimeOffset.UtcNow,
            Config = new Dictionary<string, string>(definition.Config)
        };

        if (!_agents.TryAdd(agent.Id, agent))
        {
            _logger.LogError("Failed to add agent {AgentId} to in-memory cache", agent.Id);
            throw new InvalidOperationException($"Failed to create agent {agent.Id}");
        }

        // Persist to database
        try
        {
            var dbAgent = new Persistence.Entities.Agent
            {
                AgentId = agentId.ToString(),
                Name = definition.Name,
                Purpose = definition.Purpose,
                EnabledSkillsJson = SerializeSkills(definition.EnabledSkills),
                ConfigJson = SerializeConfig(definition.Config),
                Status = "active",
                CreatedAt = now,
                UpdatedAt = now
            };

            await _agentRepository.AddAsync(dbAgent, ct).ConfigureAwait(false);
            _logger.LogInformation(
                "Created and persisted agent {AgentId} '{Name}' with {SkillCount} skills",
                agent.Id, agent.Name, agent.EnabledSkills.Count);
        }
        catch (Exception ex)
        {
            // Remove from memory if persistence fails
            _agents.TryRemove(agent.Id, out _);
            _logger.LogError(ex, "Failed to persist agent {AgentId}", agent.Id);
            throw;
        }

        return agent;
    }

    /// <inheritdoc />
    public Task<Agent?> GetAgentAsync(Guid agentId, CancellationToken ct = default)
    {
        if (_agents.TryGetValue(agentId, out var cachedAgent))
        {
            _logger.LogDebug("Retrieved agent {AgentId} '{Name}' from in-memory cache", cachedAgent.Id, cachedAgent.Name);
            return Task.FromResult<Agent?>(cachedAgent);
        }

        return GetAgentFromRepositoryAsync(agentId, ct);
    }

    /// <inheritdoc />
    public async Task<Agent> GetOrCreateAgentForTaskTypeAsync(
        string taskType,
        DynamicAgentCreationOptions? options = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskType);

        if (!_options.EnableDynamicAgentCreation)
        {
            throw new InvalidOperationException("Dynamic agent creation is disabled in orchestration options");
        }

        var normalizedTaskType = NormalizeTaskType(taskType);
        var generatedName = !string.IsNullOrWhiteSpace(options?.AgentName)
            ? options!.AgentName!.Trim()
            : BuildDynamicAgentName(normalizedTaskType);

        if (_taskTypeAgentIds.TryGetValue(normalizedTaskType, out var cachedAgentId) &&
            _agents.TryGetValue(cachedAgentId, out var cachedAgent))
        {
            _logger.LogDebug(
                "Resolved existing dynamic agent {AgentId} for task type '{TaskType}' from in-memory mapping",
                cachedAgent.Id,
                normalizedTaskType);

            return cachedAgent;
        }

        // Fallback to repository lookup (allows reuse across scopes/processes)
        var existingDbAgent = await _agentRepository.GetByNameAsync(generatedName, ct).ConfigureAwait(false);
        if (existingDbAgent != null)
        {
            var hydratedAgent = ToRuntimeAgent(existingDbAgent);
            _agents[hydratedAgent.Id] = hydratedAgent;
            _taskTypeAgentIds[normalizedTaskType] = hydratedAgent.Id;

            _logger.LogInformation(
                "Reused existing dynamic agent {AgentId} '{AgentName}' for task type '{TaskType}'",
                hydratedAgent.Id,
                hydratedAgent.Name,
                normalizedTaskType);

            return hydratedAgent;
        }

        var dynamicPurpose = !string.IsNullOrWhiteSpace(options?.Purpose)
            ? options!.Purpose!.Trim()
            : (_options.DynamicAgentPurposeTemplate ?? "Auto-generated agent for task type '{taskType}'.")
                .Replace("{taskType}", taskType.Trim(), StringComparison.OrdinalIgnoreCase);

        var resolvedSkills = ResolveDynamicSkills(normalizedTaskType, options?.EnabledSkills);

        var config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["task_type"] = normalizedTaskType,
            ["creation_mode"] = "dynamic"
        };

        if (options?.Config != null)
        {
            foreach (var item in options.Config)
            {
                if (string.IsNullOrWhiteSpace(item.Key) || string.IsNullOrWhiteSpace(item.Value))
                {
                    continue;
                }

                config[item.Key.Trim()] = item.Value.Trim();
            }
        }

        try
        {
            var createdAgent = await CreateAgentAsync(new AgentDefinition
            {
                Name = generatedName,
                Purpose = dynamicPurpose,
                EnabledSkills = resolvedSkills,
                Config = config
            }, ct).ConfigureAwait(false);

            _taskTypeAgentIds[normalizedTaskType] = createdAgent.Id;

            _logger.LogInformation(
                "Created dynamic agent {AgentId} '{AgentName}' for task type '{TaskType}'",
                createdAgent.Id,
                createdAgent.Name,
                normalizedTaskType);

            return createdAgent;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
        {
            // Handles race condition where another request created the same dynamic agent name.
            var concurrentAgent = await _agentRepository.GetByNameAsync(generatedName, ct).ConfigureAwait(false);
            if (concurrentAgent == null)
            {
                throw;
            }

            var hydratedAgent = ToRuntimeAgent(concurrentAgent);
            _agents[hydratedAgent.Id] = hydratedAgent;
            _taskTypeAgentIds[normalizedTaskType] = hydratedAgent.Id;
            return hydratedAgent;
        }
    }

    private async Task<Agent?> GetAgentFromRepositoryAsync(Guid agentId, CancellationToken ct)
    {
        var dbAgent = await _agentRepository.GetByIdAsync(agentId.ToString(), ct).ConfigureAwait(false);
        if (dbAgent == null || string.Equals(dbAgent.Status, "deleted", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("Agent {AgentId} not found", agentId);
            return null;
        }

        var hydrated = ToRuntimeAgent(dbAgent);
        _agents[hydrated.Id] = hydrated;

        if (hydrated.Config.TryGetValue("task_type", out var taskTypeFromConfig) &&
            !string.IsNullOrWhiteSpace(taskTypeFromConfig))
        {
            _taskTypeAgentIds[NormalizeTaskType(taskTypeFromConfig)] = hydrated.Id;
        }

        _logger.LogDebug("Retrieved agent {AgentId} '{Name}' from repository", hydrated.Id, hydrated.Name);
        return hydrated;
    }
        
    public Task<List<Agent>> ListAgentsAsync(Guid? projectId = null, CancellationToken ct = default)
    {
        var agents = _agents.Values.ToList();

        // TODO: Filter by projectId when agent-project association is implemented
        if (projectId.HasValue)
        {
            _logger.LogDebug("Project filtering not yet implemented, returning all agents");
        }

        _logger.LogInformation("Listed {Count} agent(s)", agents.Count);
        return Task.FromResult(agents);
    }

    /// <inheritdoc />
    public async Task DeleteAgentAsync(Guid agentId, CancellationToken ct = default)
    {
        if (_agents.TryRemove(agentId, out var agent))
        {
            _logger.LogInformation(
                "Deleted agent {AgentId} '{Name}'",
                agentId, agent.Name);

            // Mark as deleted in database (soft delete)
            try
            {
                var dbAgent = new Persistence.Entities.Agent
                {
                    AgentId = agentId.ToString(),
                    Name = agent.Name,
                    Purpose = agent.Purpose,
                    Status = "deleted",
                    UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };
                await _agentRepository.DeleteAsync(agentId.ToString(), ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete agent {AgentId} from database", agentId);
                throw;
            }
        }
        else
        {
            _logger.LogWarning("Attempted to delete non-existent agent {AgentId}", agentId);
        }
    }

    /// <inheritdoc />
    public async Task<AgentExecutionResult> ExecuteTaskAsync(
        AgentExecutionRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TaskGoal);

        // Retrieve agent
        var agent = await GetAgentAsync(request.AgentId, ct);
        if (agent == null)
        {
            throw new InvalidOperationException($"Agent {request.AgentId} not found");
        }

        // Resolve execution options (use provided or defaults)
        var options = ResolveExecutionOptions(request.Options);

        var result = new AgentExecutionResult
        {
            AgentId = request.AgentId,
            StartedAt = DateTimeOffset.UtcNow
        };

        _logger.LogInformation(
            "Starting agent execution for Agent {AgentId} '{AgentName}'. Goal: {Goal}, MaxIterations: {MaxIterations}, Timeout: {Timeout}s, TokenBudget: {Budget}",
            agent.Id, agent.Name, request.TaskGoal, options.MaxIterations, options.TimeoutSeconds, options.TokenBudget);

        var stopwatch = Stopwatch.StartNew();
        var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(options.TimeoutSeconds));
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        try
        {
            // Execute iteration loop
            for (int iteration = 1; iteration <= options.MaxIterations; iteration++)
            {
                linkedCts.Token.ThrowIfCancellationRequested();

                _logger.LogDebug("Agent {AgentId} starting iteration {Iteration}/{MaxIterations}",
                    agent.Id, iteration, options.MaxIterations);

                // Execute one iteration step
                var step = await ExecuteIterationAsync(
                    agent,
                    request.TaskGoal,
                    request.Context,
                    request.SuccessCriteria,
                    iteration,
                    result.Steps,
                    linkedCts.Token);

                result.Steps.Add(step);
                result.TokensConsumed += step.TokensConsumed;
                result.IterationsExecuted = iteration;

                // Check token budget
                if (result.TokensConsumed >= options.TokenBudget)
                {
                    _logger.LogWarning(
                        "Agent {AgentId} exceeded token budget ({Consumed}/{Budget})",
                        agent.Id, result.TokensConsumed, options.TokenBudget);

                    result.Success = false;
                    result.TerminationReason = "TokenBudgetExceeded";
                    result.ErrorMessage = $"Token budget exceeded: {result.TokensConsumed}/{options.TokenBudget}";
                    break;
                }

                // Check if task completed successfully
                if (step.Success && step.StepType == "Completion")
                {
                    result.Success = true;
                    result.TerminationReason = "Success";
                    result.Output = step.Output;
                    
                    _logger.LogInformation(
                        "Agent {AgentId} completed task successfully after {Iterations} iterations",
                        agent.Id, iteration);
                    break;
                }

                // Handle failure with optional self-correction
                if (!step.Success && options.EnableSelfCorrection && iteration < options.MaxIterations)
                {
                    _logger.LogDebug(
                        "Agent {AgentId} step failed, attempting self-correction on next iteration",
                        agent.Id);
                    // Self-correction context is passed via result.Steps history to next iteration
                }

                // Check if max iterations reached
                if (iteration == options.MaxIterations)
                {
                    result.Success = false;
                    result.TerminationReason = "MaxIterations";
                    result.ErrorMessage = $"Maximum iterations ({options.MaxIterations}) reached without completion";
                    result.Output = step.Output; // Partial output
                    
                    _logger.LogWarning(
                        "Agent {AgentId} reached max iterations ({MaxIterations}) without completing task",
                        agent.Id, options.MaxIterations);
                }
            }
        }
        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
        {
            result.Success = false;
            result.TerminationReason = "Timeout";
            result.ErrorMessage = $"Execution timeout ({options.TimeoutSeconds}s) exceeded";
            
            _logger.LogWarning(
                "Agent {AgentId} execution timed out after {Elapsed}ms",
                agent.Id, stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            result.Success = false;
            result.TerminationReason = "Cancelled";
            result.ErrorMessage = "Execution cancelled by user";
            
            _logger.LogInformation(
                "Agent {AgentId} execution cancelled by user",
                agent.Id);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.TerminationReason = "Error";
            result.ErrorMessage = ex.Message;
            
            _logger.LogError(ex,
                "Agent {AgentId} execution failed with error",
                agent.Id);
        }
        finally
        {
            stopwatch.Stop();
            result.CompletedAt = DateTimeOffset.UtcNow;
            timeoutCts.Dispose();
            linkedCts.Dispose();

            _logger.LogInformation(
                "Agent {AgentId} execution completed. ExecutionId: {ExecutionId}, Success: {Success}, Reason: {Reason}, Iterations: {Iterations}, Tokens: {Tokens}, Duration: {Duration}ms",
                agent.Id, result.ExecutionId, result.Success, result.TerminationReason,
                result.IterationsExecuted, result.TokensConsumed, stopwatch.ElapsedMilliseconds);

            // Publish execution completion message
            try
            {
                var executionMessage = new AgentMessage<AgentExecutionResult>(
                    $"agent-execution/{agent.Id}",
                    agent.Id.ToString(),
                    result,
                    new MessageMetadata
                    {
                        Tags = new Dictionary<string, string>
                        {
                            { "agent-name", agent.Name },
                            { "status", result.Success ? "completed" : "failed" },
                            { "termination-reason", result.TerminationReason },
                            { "iterations", result.IterationsExecuted.ToString() }
                        }
                    });
                
                // Don't await this, fire and forget with logging
                _ = _messageBroker.PublishAsync(executionMessage, CancellationToken.None).ContinueWith(
                    t =>
                    {
                        if (t.IsFaulted)
                            _logger.LogWarning(t.Exception, "Failed to publish agent execution message for agent {AgentId}", agent.Id);
                    },
                    TaskScheduler.Default);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create agent execution message for agent {AgentId}", agent.Id);
            }
        }

        return result;
    }

    /// <summary>
    /// Executes a single iteration step for the agent.
    /// </summary>
    /// <remarks>
    /// This is a placeholder implementation. In the future, this will:
    /// - Call language models for reasoning and planning
    /// - Invoke skills based on agent's enabled skills
    /// - Execute tools via IToolInvoker
    /// - Evaluate success criteria
    /// - Learn from previous step failures (self-correction)
    /// </remarks>
    private async Task<AgentExecutionStep> ExecuteIterationAsync(
        Agent agent,
        string taskGoal,
        Dictionary<string, string> context,
        string? successCriteria,
        int iteration,
        List<AgentExecutionStep> previousSteps,
        CancellationToken ct)
    {
        var stepStartTime = DateTimeOffset.UtcNow;

        // TODO: Replace with actual model inference and skill execution
        // For now, simulate a multi-step task completion

        var stepType = iteration switch
        {
            1 => "Planning",
            var i when i < 5 => "Execution",
            _ => "Completion"
        };

        // Simulate processing delay
        await Task.Delay(50, ct);

        var step = new AgentExecutionStep
        {
            StepNumber = iteration,
            StepType = stepType,
            Description = $"Iteration {iteration}: {stepType} step for goal: {taskGoal}",
            Output = $"Step {iteration} output (placeholder)",
            TokensConsumed = 100, // Placeholder token count
            StartedAt = stepStartTime,
            CompletedAt = DateTimeOffset.UtcNow,
            Success = true
        };

        _logger.LogDebug(
            "Agent {AgentId} completed step {StepNumber} ({StepType}): {Description}",
            agent.Id, step.StepNumber, step.StepType, step.Description);

        return step;
    }

    /// <summary>
    /// Resolves execution options by merging provided options with configured defaults.
    /// </summary>
    private AgentExecutionOptions ResolveExecutionOptions(AgentExecutionOptions? provided)
    {
        if (provided == null)
        {
            return new AgentExecutionOptions
            {
                MaxIterations = _options.DefaultAgentMaxIterations,
                TimeoutSeconds = _options.DefaultAgentTimeoutSeconds,
                TokenBudget = _options.DefaultAgentTokenBudget,
                EnableSelfCorrection = _options.DefaultAgentEnableSelfCorrection
            };
        }

        // Use provided values, fallback to defaults if not specified
        return new AgentExecutionOptions
        {
            MaxIterations = provided.MaxIterations > 0 ? provided.MaxIterations : _options.DefaultAgentMaxIterations,
            TimeoutSeconds = provided.TimeoutSeconds > 0 ? provided.TimeoutSeconds : _options.DefaultAgentTimeoutSeconds,
            TokenBudget = provided.TokenBudget > 0 ? provided.TokenBudget : _options.DefaultAgentTokenBudget,
            EnableSelfCorrection = provided.EnableSelfCorrection
        };
    }

    /// <summary>
    /// Serializes a list of skill names to JSON for storage.
    /// </summary>
    private static string? SerializeSkills(List<string> skills)
    {
        if (skills == null || skills.Count == 0)
            return null;

        return JsonSerializer.Serialize(skills);
    }

    /// <summary>
    /// Deserializes a JSON-serialized skill list.
    /// </summary>
    private static List<string> DeserializeSkills(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return new List<string>();

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch (JsonException)
        {
            return new List<string>();
        }
    }

    /// <summary>
    /// Serializes configuration dictionary to JSON for storage.
    /// </summary>
    private static string? SerializeConfig(Dictionary<string, string> config)
    {
        if (config == null || config.Count == 0)
            return null;

        return JsonSerializer.Serialize(config);
    }

    /// <summary>
    /// Deserializes a JSON-serialized configuration dictionary.
    /// </summary>
    private static Dictionary<string, string> DeserializeConfig(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return new Dictionary<string, string>();

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>();
        }
    }

    private static string NormalizeTaskType(string taskType)
    {
        var normalized = Regex.Replace(taskType.Trim().ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrWhiteSpace(normalized) ? "unknown" : normalized;
    }

    private string BuildDynamicAgentName(string normalizedTaskType)
    {
        var prefix = string.IsNullOrWhiteSpace(_options.DynamicAgentNamePrefix)
            ? "task"
            : _options.DynamicAgentNamePrefix.Trim();

        return $"{prefix}-{normalizedTaskType}-agent";
    }

    private List<string> ResolveDynamicSkills(string normalizedTaskType, List<string>? explicitSkills)
    {
        if (explicitSkills is { Count: > 0 })
        {
            return explicitSkills
                .Where(skill => !string.IsNullOrWhiteSpace(skill))
                .Select(skill => skill.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var mergedSkills = new List<string>(_options.DynamicAgentDefaultSkills);

        if (_options.DynamicAgentSkillsByTaskType.TryGetValue(normalizedTaskType, out var taskSpecificSkills))
        {
            mergedSkills.AddRange(taskSpecificSkills);
        }

        return mergedSkills
            .Where(skill => !string.IsNullOrWhiteSpace(skill))
            .Select(skill => skill.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static Agent ToRuntimeAgent(Persistence.Entities.Agent dbAgent)
    {
        return new Agent
        {
            Id = Guid.Parse(dbAgent.AgentId),
            Name = dbAgent.Name,
            Purpose = dbAgent.Purpose,
            EnabledSkills = DeserializeSkills(dbAgent.EnabledSkillsJson),
            CreatedAt = DateTimeOffset.FromUnixTimeSeconds(dbAgent.CreatedAt),
            Config = DeserializeConfig(dbAgent.ConfigJson)
        };
    }
}
