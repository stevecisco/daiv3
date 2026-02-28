using Daiv3.Orchestration.Interfaces;
using Daiv3.Persistence;
using Daiv3.Persistence.Repositories;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Daiv3.Orchestration;

/// <summary>
/// Manages agent lifecycle and execution.
/// Persists agent definitions in user-editable JSON format to SQLite.
/// </summary>
public class AgentManager : IAgentManager
{
    private readonly ILogger<AgentManager> _logger;
    private readonly AgentRepository _agentRepository;
    private readonly ConcurrentDictionary<Guid, Agent> _agents = new();

    public AgentManager(ILogger<AgentManager> logger, AgentRepository agentRepository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
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
}
