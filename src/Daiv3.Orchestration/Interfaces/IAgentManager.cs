namespace Daiv3.Orchestration.Interfaces;

/// <summary>
/// Manages agent lifecycle and execution.
/// </summary>
public interface IAgentManager
{
    /// <summary>
    /// Creates a new agent from a definition.
    /// </summary>
    /// <param name="definition">The agent definition.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created agent.</returns>
    Task<Agent> CreateAgentAsync(AgentDefinition definition, CancellationToken ct = default);
    
    /// <summary>
    /// Retrieves an agent by ID.
    /// </summary>
    /// <param name="agentId">The agent ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The agent, or null if not found.</returns>
    Task<Agent?> GetAgentAsync(Guid agentId, CancellationToken ct = default);
    
    /// <summary>
    /// Lists all agents, optionally filtered by project.
    /// </summary>
    /// <param name="projectId">Optional project ID filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of agents.</returns>
    Task<List<Agent>> ListAgentsAsync(Guid? projectId = null, CancellationToken ct = default);
    
    /// <summary>
    /// Deletes an agent.
    /// </summary>
    /// <param name="agentId">The agent ID to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteAgentAsync(Guid agentId, CancellationToken ct = default);
}

/// <summary>
/// Definition for creating a new agent.
/// </summary>
public class AgentDefinition
{
    /// <summary>
    /// Agent name.
    /// </summary>
    public required string Name { get; set; }
    
    /// <summary>
    /// Agent purpose/description.
    /// </summary>
    public required string Purpose { get; set; }
    
    /// <summary>
    /// List of skill names enabled for this agent.
    /// </summary>
    public List<string> EnabledSkills { get; set; } = new();
    
    /// <summary>
    /// Agent-specific configuration.
    /// </summary>
    public Dictionary<string, string> Config { get; set; } = new();
}

/// <summary>
/// Represents an agent instance.
/// </summary>
public class Agent
{
    /// <summary>
    /// Agent unique identifier.
    /// </summary>
    public Guid Id { get; set; }
    
    /// <summary>
    /// Agent name.
    /// </summary>
    public required string Name { get; set; }
    
    /// <summary>
    /// Agent purpose/description.
    /// </summary>
    public required string Purpose { get; set; }
    
    /// <summary>
    /// List of enabled skill names.
    /// </summary>
    public List<string> EnabledSkills { get; set; } = new();
    
    /// <summary>
    /// When the agent was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
    
    /// <summary>
    /// Agent-specific configuration.
    /// </summary>
    public Dictionary<string, string> Config { get; set; } = new();
}
