using Daiv3.Orchestration;
using Daiv3.Orchestration.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Daiv3.UnitTests.Orchestration;

/// <summary>
/// Unit tests for AgentManager.
/// </summary>
public class AgentManagerTests
{
    private readonly Mock<ILogger<AgentManager>> _mockLogger;
    private readonly AgentManager _manager;

    public AgentManagerTests()
    {
        _mockLogger = new Mock<ILogger<AgentManager>>();
        _manager = new AgentManager(_mockLogger.Object);
    }

    [Fact]
    public async Task CreateAgentAsync_WithValidDefinition_CreatesAgent()
    {
        // Arrange
        var definition = new AgentDefinition
        {
            Name = "TestAgent",
            Purpose = "Test purposes",
            EnabledSkills = new List<string> { "skill1", "skill2" },
            Config = new Dictionary<string, string> { ["key"] = "value" }
        };

        // Act
        var agent = await _manager.CreateAgentAsync(definition);

        // Assert
        Assert.NotNull(agent);
        Assert.NotEqual(Guid.Empty, agent.Id);
        Assert.Equal("TestAgent", agent.Name);
        Assert.Equal("Test purposes", agent.Purpose);
        Assert.Equal(2, agent.EnabledSkills.Count);
        Assert.Contains("skill1", agent.EnabledSkills);
        Assert.Contains("skill2", agent.EnabledSkills);
        Assert.True(agent.CreatedAt <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task CreateAgentAsync_WithNullDefinition_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _manager.CreateAgentAsync(null!));
    }

    [Fact]
    public async Task CreateAgentAsync_WithEmptyName_ThrowsArgumentException()
    {
        // Arrange
        var definition = new AgentDefinition
        {
            Name = "",
            Purpose = "Test purposes"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _manager.CreateAgentAsync(definition));
    }

    [Fact]
    public async Task CreateAgentAsync_WithEmptyPurpose_ThrowsArgumentException()
    {
        // Arrange
        var definition = new AgentDefinition
        {
            Name = "TestAgent",
            Purpose = ""
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _manager.CreateAgentAsync(definition));
    }

    [Fact]
    public async Task GetAgentAsync_WithExistingAgent_ReturnsAgent()
    {
        // Arrange
        var definition = new AgentDefinition
        {
            Name = "TestAgent",
            Purpose = "Test purposes"
        };
        var createdAgent = await _manager.CreateAgentAsync(definition);

        // Act
        var retrievedAgent = await _manager.GetAgentAsync(createdAgent.Id);

        // Assert
        Assert.NotNull(retrievedAgent);
        Assert.Equal(createdAgent.Id, retrievedAgent.Id);
        Assert.Equal(createdAgent.Name, retrievedAgent.Name);
    }

    [Fact]
    public async Task GetAgentAsync_WithNonExistentAgent_ReturnsNull()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var agent = await _manager.GetAgentAsync(nonExistentId);

        // Assert
        Assert.Null(agent);
    }

    [Fact]
    public async Task ListAgentsAsync_WithNoAgents_ReturnsEmptyList()
    {
        // Act
        var agents = await _manager.ListAgentsAsync();

        // Assert
        Assert.NotNull(agents);
        Assert.Empty(agents);
    }

    [Fact]
    public async Task ListAgentsAsync_WithMultipleAgents_ReturnsAllAgents()
    {
        // Arrange
        await _manager.CreateAgentAsync(new AgentDefinition
        {
            Name = "Agent1",
            Purpose = "Purpose1"
        });
        await _manager.CreateAgentAsync(new AgentDefinition
        {
            Name = "Agent2",
            Purpose = "Purpose2"
        });

        // Act
        var agents = await _manager.ListAgentsAsync();

        // Assert
        Assert.Equal(2, agents.Count);
        Assert.Contains(agents, a => a.Name == "Agent1");
        Assert.Contains(agents, a => a.Name == "Agent2");
    }

    [Fact]
    public async Task DeleteAgentAsync_WithExistingAgent_RemovesAgent()
    {
        // Arrange
        var definition = new AgentDefinition
        {
            Name = "TestAgent",
            Purpose = "Test purposes"
        };
        var agent = await _manager.CreateAgentAsync(definition);

        // Act
        await _manager.DeleteAgentAsync(agent.Id);

        // Assert
        var retrievedAgent = await _manager.GetAgentAsync(agent.Id);
        Assert.Null(retrievedAgent);
    }

    [Fact]
    public async Task DeleteAgentAsync_WithNonExistentAgent_DoesNotThrow()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act & Assert - should not throw
        await _manager.DeleteAgentAsync(nonExistentId);
    }

    [Fact]
    public async Task CreateAgentAsync_PreservesConfigValues()
    {
        // Arrange
        var config = new Dictionary<string, string>
        {
            ["setting1"] = "value1",
            ["setting2"] = "value2"
        };
        var definition = new AgentDefinition
        {
            Name = "ConfigAgent",
            Purpose = "Test config preservation",
            Config = config
        };

        // Act
        var agent = await _manager.CreateAgentAsync(definition);

        // Assert
        Assert.Equal(2, agent.Config.Count);
        Assert.Equal("value1", agent.Config["setting1"]);
        Assert.Equal("value2", agent.Config["setting2"]);
    }
}
