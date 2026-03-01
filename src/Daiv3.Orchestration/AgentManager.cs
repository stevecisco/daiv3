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
    private readonly ISuccessCriteriaEvaluator _successCriteriaEvaluator;
    private readonly IToolRegistry _toolRegistry;
    private readonly IToolInvoker _toolInvoker;
    private readonly OrchestrationOptions _options;
    private readonly ConcurrentDictionary<Guid, Agent> _agents = new();
    private readonly ConcurrentDictionary<string, Guid> _taskTypeAgentIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly AgentExecutionRegistry _executionRegistry = new();

    public AgentManager(
        ILogger<AgentManager> logger,
        AgentRepository agentRepository,
        IMessageBroker messageBroker,
        ISuccessCriteriaEvaluator successCriteriaEvaluator,
        IToolRegistry toolRegistry,
        IToolInvoker toolInvoker,
        IOptions<OrchestrationOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
        _messageBroker = messageBroker ?? throw new ArgumentNullException(nameof(messageBroker));
        _successCriteriaEvaluator = successCriteriaEvaluator ?? throw new ArgumentNullException(nameof(successCriteriaEvaluator));
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _toolInvoker = toolInvoker ?? throw new ArgumentNullException(nameof(toolInvoker));
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
    public (AgentExecutionControl Control, Task<AgentExecutionResult> ExecutionTask) StartExecutionWithControl(
        AgentExecutionRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TaskGoal);

        var control = new AgentExecutionControl(Guid.NewGuid(), request.AgentId);
        _executionRegistry.RegisterExecution(control);

        _logger.LogInformation(
            "Starting execution with control for Agent {AgentId}. ExecutionId: {ExecutionId}",
            request.AgentId, control.ExecutionId);

        // Start execution as a background task
        var executionTask = Task.Run(async () =>
        {
            try
            {
                return await ExecuteTaskInternalAsync(request, control, ct);
            }
            finally
            {
                _executionRegistry.UnregisterExecution(control.ExecutionId);
            }
        }, ct);

        return (control, executionTask);
    }

    /// <inheritdoc />
    public AgentExecutionControl? GetExecutionControl(Guid executionId)
    {
        return _executionRegistry.GetExecution(executionId);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<AgentExecutionControl> GetActiveExecutions()
    {
        return _executionRegistry.GetActiveExecutions();
    }

    /// <inheritdoc />
    public async Task<AgentExecutionResult> ExecuteTaskAsync(
        AgentExecutionRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TaskGoal);

        // Execute without control object (backward compatibility)
        return await ExecuteTaskInternalAsync(request, null, ct);
    }

    /// <summary>
    /// Internal method that performs the actual execution with optional pause/resume control.
    /// </summary>
    private async Task<AgentExecutionResult> ExecuteTaskInternalAsync(
        AgentExecutionRequest request,
        AgentExecutionControl? control,
        CancellationToken ct = default)
    {

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

        // Use control's ExecutionId if provided
        if (control != null)
        {
            var executionIdField = typeof(AgentExecutionResult).GetProperty(nameof(AgentExecutionResult.ExecutionId));
            if (executionIdField != null)
            {
                executionIdField.SetValue(result, control.ExecutionId);
            }
        }

        _logger.LogInformation(
            "Starting agent execution for Agent {AgentId} '{AgentName}'. ExecutionId: {ExecutionId}, Goal: {Goal}, MaxIterations: {MaxIterations}, Timeout: {Timeout}s, TokenBudget: {Budget}",
            agent.Id, agent.Name, result.ExecutionId, request.TaskGoal, options.MaxIterations, options.TimeoutSeconds, options.TokenBudget);

        var stopwatch = Stopwatch.StartNew();
        var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(options.TimeoutSeconds));
        
        // Link cancellation tokens: user token, timeout token, and control stop token if available
        var linkedCts = control != null
            ? CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token, control.StopToken)
            : CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        try
        {
            // Execute iteration loop
            string? lastFailureContext = null;
            
            for (int iteration = 1; iteration <= options.MaxIterations; iteration++)
            {
                // Check for pause before each iteration
                if (control != null)
                {
                    control.WaitIfPaused();
                }

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
                    lastFailureContext,
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

                // Evaluate success criteria
                var evaluationContext = new SuccessCriteriaContext
                {
                    TaskGoal = request.TaskGoal,
                    IterationNumber = iteration,
                    PreviousStepOutputs = result.Steps.Select(s => s.Output).ToList(),
                    FailureContext = lastFailureContext
                };

                var criteriaEvaluation = await _successCriteriaEvaluator.EvaluateAsync(
                    request.SuccessCriteria,
                    step.Output,
                    evaluationContext,
                    linkedCts.Token);

                // If criteria are met, task is successful
                if (criteriaEvaluation.MeetsCriteria)
                {
                    result.Success = true;
                    result.TerminationReason = "Success";
                    result.Output = step.Output;
                    
                    _logger.LogInformation(
                        "Agent {AgentId} completed task successfully after {Iterations} iterations. SuccessCriteria met with {Confidence:P} confidence",
                        agent.Id, iteration, criteriaEvaluation.ConfidenceScore);
                    break;
                }

                // If criteria not met, check if we should attempt self-correction
                if (!criteriaEvaluation.MeetsCriteria && options.EnableSelfCorrection && iteration < options.MaxIterations)
                {
                    lastFailureContext = $"Iteration {iteration} failed criteria evaluation: {criteriaEvaluation.EvaluationMessage ?? "Unknown failure"}";
                    
                    if (!string.IsNullOrEmpty(criteriaEvaluation.SuggestedCorrection))
                    {
                        lastFailureContext += $"\nSuggested correction: {criteriaEvaluation.SuggestedCorrection}";
                    }

                    _logger.LogDebug(
                        "Agent {AgentId} criteria evaluation failed (confidence: {Confidence:P}). Attempting self-correction on iteration {NextIteration}. Reason: {Reason}",
                        agent.Id,
                        criteriaEvaluation.ConfidenceScore,
                        iteration + 1,
                        criteriaEvaluation.EvaluationMessage);
                    
                    // Continue to next iteration with failure context
                    continue;
                }

                // If criteria not met but self-correction disabled or at max iterations
                if (!criteriaEvaluation.MeetsCriteria)
                {
                    result.Success = false;
                    result.TerminationReason = iteration == options.MaxIterations ? "MaxIterations" : "SuccessCriteriaNotMet";
                    result.ErrorMessage = criteriaEvaluation.EvaluationMessage ?? "Output did not meet success criteria";
                    result.Output = step.Output;

                    _logger.LogWarning(
                        "Agent {AgentId} failed to meet criteria (self-correction: {SelfCorrectionEnabled}, max iterations: {MaxIterations})",
                        agent.Id,
                        options.EnableSelfCorrection,
                        options.MaxIterations);
                    break;
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
            
            // Capture paused duration if control was used
            if (control != null)
            {
                result.PausedDuration = control.TotalPausedDuration;
            }
            
            timeoutCts.Dispose();
            linkedCts.Dispose();

            _logger.LogInformation(
                "Agent {AgentId} execution completed. ExecutionId: {ExecutionId}, Success: {Success}, Reason: {Reason}, Iterations: {Iterations}, Tokens: {Tokens}, Duration: {Duration}ms, PausedDuration: {PausedDuration}ms",
                agent.Id, result.ExecutionId, result.Success, result.TerminationReason,
                result.IterationsExecuted, result.TokensConsumed, stopwatch.ElapsedMilliseconds, result.PausedDuration.TotalMilliseconds);

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
        string? failureContext,
        CancellationToken ct)
    {
        var stepStartTime = DateTimeOffset.UtcNow;

        // Determine step type based on iteration number
        var stepType = iteration switch
        {
            1 => "Planning",
            var i when i < 5 => "ToolExecution",
            _ => "Completion"
        };

        var stepDescription = $"Iteration {iteration}: {stepType} step for goal: {taskGoal}";
        if (!string.IsNullOrEmpty(failureContext))
        {
            stepDescription += $"\nSelf-correcting based on: {failureContext}";
        }

        var tokensConsumed = 0;
        var output = string.Empty;
        var success = true;
        string? errorMessage = null;

        // Execute iteration based on step type
        try
        {
            switch (stepType)
            {
                case "Planning":
                    // Planning step: analyze task and determine tools needed
                    var availableTools = await _toolRegistry.GetAvailableToolsAsync(ct);
                    tokensConsumed = Math.Min(availableTools.Count * 10, 100);
                    output = $"Analyzed task and identified {availableTools.Count} available tools";
                    break;

                case "ToolExecution":
                    // Tool execution step: invoke appropriate tools
                    (tokensConsumed, output) = await ExecuteToolsForTaskAsync(taskGoal, context, ct);
                    break;

                case "Completion":
                    // Completion step: finalize results
                    tokensConsumed = 50;
                    output = $"Task completion step for: {taskGoal}";
                    break;
            }
        }
        catch (Exception ex)
        {
            success = false;
            errorMessage = ex.Message;
            tokensConsumed = 50;
            output = $"Step failed: {ex.Message}";
            _logger.LogError(ex, "Agent {AgentId} iteration {Iteration} ({StepType}) failed", agent.Id, iteration, stepType);
        }

        var step = new AgentExecutionStep
        {
            StepNumber = iteration,
            StepType = stepType,
            Description = stepDescription,
            Output = output,
            TokensConsumed = tokensConsumed,
            StartedAt = stepStartTime,
            CompletedAt = DateTimeOffset.UtcNow,
            Success = success
        };

        _logger.LogInformation(
            "Agent {AgentId} completed iteration {Iteration} ({StepType}): Success={Success}, Tokens={Tokens}, Output={Output}",
            agent.Id, iteration, stepType, success, tokensConsumed, output);

        return step;
    }

    /// <summary>
    /// Executes tool invocations for the given task goal.
    /// </summary>
    /// <remarks>
    /// This method demonstrates agent tool invocation with intelligent routing.
    /// In a production system, a language model would determine which tools to invoke
    /// based on the task goal and available tools.
    /// </remarks>
    private async Task<(int TokensUsed, string Output)> ExecuteToolsForTaskAsync(
        string taskGoal,
        Dictionary<string, string> context,
        CancellationToken ct)
    {
        var tools = await _toolRegistry.GetAvailableToolsAsync(ct);
        var totalTokens = 0;
        var outputs = new List<string>();

        // Select and invoke appropriate tools based on task goal
        // For demonstration, we attempt to invoke the first available tool
        if (tools.Count > 0)
        {
            var selectedTool = tools.First();
            
            _logger.LogInformation("Agent attempting to invoke tool '{ToolId}' ({Backend} backend) for task: {Goal}",
                selectedTool.ToolId, selectedTool.Backend, taskGoal);

            try
            {
                // Build parameters from task goal and context
                var parameters = new Dictionary<string, object> { ["goal"] = taskGoal };
                foreach (var kvp in context)
                {
                    parameters[kvp.Key] = kvp.Value;
                }

                // Invoke the tool with intelligent routing
                var result = await _toolInvoker.InvokeToolAsync(
                    selectedTool.ToolId,
                    parameters,
                    new ToolInvocationPreferences { PreferLowOverhead = true },
                    ct);

                totalTokens += result.ContextTokenCost;

                if (result.Success)
                {
                    outputs.Add($"Tool '{selectedTool.ToolId}' ({result.BackendUsed}): {result.Result}");
                    _logger.LogInformation("Tool '{ToolId}' invocation succeeded via {Backend} backend ({Duration}ms, {Tokens} tokens)",
                        selectedTool.ToolId, result.BackendUsed, result.DurationMs, result.ContextTokenCost);
                }
                else
                {
                    outputs.Add($"Tool '{selectedTool.ToolId}' failed: {result.ErrorMessage}");
                    _logger.LogWarning("Tool '{ToolId}' invocation failed: {ErrorCode} - {ErrorMessage}",
                        selectedTool.ToolId, result.ErrorCode, result.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                outputs.Add($"Tool invocation error: {ex.Message}");
                _logger.LogError(ex, "Unexpected error invoking tool '{ToolId}'", selectedTool.ToolId);
            }
        }
        else
        {
            outputs.Add("No tools available for task execution");
            _logger.LogInformation("No tools available for task execution");
        }

        var output = outputs.Count > 0 ? string.Join("; ", outputs) : "No tools executed";
        return (totalTokens, output);
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
