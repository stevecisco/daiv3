using Daiv3.Orchestration.Interfaces;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Daiv3.Orchestration;

/// <summary>
/// Manages agent lifecycle and execution.
/// </summary>
public class AgentManager : IAgentManager
{
    private readonly ILogger<AgentManager> _logger;
    private readonly ConcurrentDictionary<Guid, Agent> _agents = new();

    public AgentManager(ILogger<AgentManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task<Agent> CreateAgentAsync(AgentDefinition definition, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Purpose);

        var agent = new Agent
        {
            Id = Guid.NewGuid(),
            Name = definition.Name,
            Purpose = definition.Purpose,
            EnabledSkills = new List<string>(definition.EnabledSkills),
            CreatedAt = DateTimeOffset.UtcNow,
            Config = new Dictionary<string, string>(definition.Config)
        };

        if (!_agents.TryAdd(agent.Id, agent))
        {
            _logger.LogError("Failed to add agent {AgentId}", agent.Id);
            throw new InvalidOperationException($"Failed to create agent {agent.Id}");
        }

        _logger.LogInformation(
            "Created agent {AgentId} '{Name}' with {SkillCount} skills",
            agent.Id, agent.Name, agent.EnabledSkills.Count);

        // TODO: Persist agent to database
        return Task.FromResult(agent);
    }

    /// <inheritdoc />
    public Task<Agent?> GetAgentAsync(Guid agentId, CancellationToken ct = default)
    {
        _agents.TryGetValue(agentId, out var agent);
        
        if (agent != null)
        {
            _logger.LogDebug("Retrieved agent {AgentId} '{Name}'", agent.Id, agent.Name);
        }
        else
        {
            _logger.LogDebug("Agent {AgentId} not found", agentId);
        }

        return Task.FromResult(agent);
    }

    /// <inheritdoc />
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
    public Task DeleteAgentAsync(Guid agentId, CancellationToken ct = default)
    {
        if (_agents.TryRemove(agentId, out var agent))
        {
            _logger.LogInformation(
                "Deleted agent {AgentId} '{Name}'",
                agentId, agent.Name);
        }
        else
        {
            _logger.LogWarning("Attempted to delete non-existent agent {AgentId}", agentId);
        }

        // TODO: Remove agent from database
        return Task.CompletedTask;
    }
}
