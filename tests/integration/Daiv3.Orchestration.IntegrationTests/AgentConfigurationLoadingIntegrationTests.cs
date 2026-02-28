using Daiv3.Orchestration;
using Daiv3.Orchestration.Configuration;
using Daiv3.Orchestration.Interfaces;
using Daiv3.Persistence;
using Daiv3.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Daiv3.IntegrationTests.Orchestration;

/// <summary>
/// Integration tests for agent configuration loading end-to-end.
/// Tests the flow from loading configuration files to creating agents in the database.
/// </summary>
public class AgentConfigurationLoadingIntegrationTests : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly AgentConfigFileLoader _loader;
    private readonly IAgentManager _agentManager;
    private readonly string _testDirectory;
    private readonly string _dbPath;

    public AgentConfigurationLoadingIntegrationTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"daiv3-agent-integration-test-{Guid.NewGuid()}");
        _dbPath = Path.Combine(Path.GetTempPath(), $"daiv3-test-{Guid.NewGuid()}.db");
        Directory.CreateDirectory(_testDirectory);

        // Setup service provider with all required services
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPersistence(options => { options.DatabasePath = _dbPath; });
        services.AddOrchestrationServices();
        services.AddScoped<AgentConfigFileLoader>();

        _serviceProvider = services.BuildServiceProvider();

        // Initialize database
        var scope = _serviceProvider.CreateScope();
        scope.ServiceProvider.InitializeDatabaseAsync().GetAwaiter().GetResult();
        
        _loader = _serviceProvider.CreateScope().ServiceProvider.GetRequiredService<AgentConfigFileLoader>();
        _agentManager = _serviceProvider.CreateScope().ServiceProvider.GetRequiredService<IAgentManager>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
        (_serviceProvider as IDisposable)?.Dispose();
    }

    [Fact]
    public async Task LoadAndCreateAgent_FromJsonFile_CreatesAgentInDatabase()
    {
        // Arrange
        var jsonContent = @"{
            ""name"": ""TestAgent"",
            ""purpose"": ""Test agent for integration testing"",
            ""enabledSkills"": [],
            ""config"": {
                ""model_preference"": ""phi-4""
            }
        }";

        var filePath = Path.Combine(_testDirectory, "test-agent.json");
        await File.WriteAllTextAsync(filePath, jsonContent);

        // Act - Load configuration
        var config = await _loader.LoadAgentConfigAsync(filePath);
        var definition = _loader.ToAgentDefinition(config);
        var agent = await _agentManager.CreateAgentAsync(definition);

        // Assert
        Assert.NotNull(agent);
        Assert.Equal("TestAgent", agent.Name);
        Assert.Equal("Test agent for integration testing", agent.Purpose);
        Assert.NotEqual(Guid.Empty, agent.Id);

        // Verify agent was persisted to database
        var retrievedAgent = await _agentManager.GetAgentAsync(agent.Id);
        Assert.NotNull(retrievedAgent);
        Assert.Equal(agent.Id, retrievedAgent.Id);
        Assert.Equal("TestAgent", retrievedAgent.Name);
    }

    [Fact]
    public async Task LoadAndCreateMultipleAgents_FromBatchFile_CreatesAllAgents()
    {
        // Arrange
        var batchContent = @"{
            ""batchName"": ""TestBatch"",
            ""agents"": [
                {
                    ""name"": ""AnalysisAgent"",
                    ""purpose"": ""Analyzes documents"",
                    ""enabledSkills"": []
                },
                {
                    ""name"": ""SummaryAgent"",
                    ""purpose"": ""Generates summaries"",
                    ""enabledSkills"": []
                }
            ]
        }";

        var filePath = Path.Combine(_testDirectory, "batch.json");
        await File.WriteAllTextAsync(filePath, batchContent);

        // Act - Load batch
        var batch = await _loader.LoadAgentBatchAsync(filePath);
        var createdAgents = new List<Agent>();
        
        foreach (var config in batch.Agents)
        {
            var definition = _loader.ToAgentDefinition(config);
            var agent = await _agentManager.CreateAgentAsync(definition);
            createdAgents.Add(agent);
        }

        // Assert
        Assert.Equal(2, createdAgents.Count);
        Assert.Equal("AnalysisAgent", createdAgents[0].Name);
        Assert.Equal("SummaryAgent", createdAgents[1].Name);

        // Verify both agents exist in database
        var allAgents = await _agentManager.ListAgentsAsync();
        Assert.Equal(2, allAgents.Count);
    }

    [Fact]
    public async Task LoadAndCreateAgents_FromDirectory_CreatesAllConfigFiles()
    {
        // Arrange - Create multiple config files
        var agent1Content = @"{""name"": ""Agent1"", ""purpose"": ""First agent"", ""enabledSkills"": []}";
        var agent2Content = @"{""name"": ""Agent2"", ""purpose"": ""Second agent"", ""enabledSkills"": []}";

        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "agent1.json"), agent1Content);
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "agent2.json"), agent2Content);

        // Act - Load from directory
        var batch = await _loader.LoadAgentBatchAsync(_testDirectory);
        var createdAgents = new List<Agent>();
        
        foreach (var config in batch.Agents)
        {
            var definition = _loader.ToAgentDefinition(config);
            var agent = await _agentManager.CreateAgentAsync(definition);
            createdAgents.Add(agent);
        }

        // Assert
        Assert.Equal(2, createdAgents.Count);
        var names = createdAgents.Select(a => a.Name).OrderBy(n => n).ToList();
        Assert.Equal(["Agent1", "Agent2"], names);
    }

    [Fact]
    public async Task LoadAndCreateAgent_WithSkills_AssociatesSkillsWithAgent()
    {
        // Arrange
        var jsonContent = @"{
            ""name"": ""SkillfulAgent"",
            ""purpose"": ""Agent with skills"",
            ""enabledSkills"": [""skill1"", ""skill2""]
        }";

        var filePath = Path.Combine(_testDirectory, "skillful-agent.json");
        await File.WriteAllTextAsync(filePath, jsonContent);

        // Act
        var config = await _loader.LoadAgentConfigAsync(filePath);
        var definition = _loader.ToAgentDefinition(config);
        var agent = await _agentManager.CreateAgentAsync(definition);

        // Assert
        Assert.Equal(2, agent.EnabledSkills.Count);
        Assert.Contains("skill1", agent.EnabledSkills);
        Assert.Contains("skill2", agent.EnabledSkills);

        // Verify database persisted the skills
        var retrievedAgent = await _agentManager.GetAgentAsync(agent.Id);
        Assert.NotNull(retrievedAgent);
        Assert.Equal(2, retrievedAgent.EnabledSkills.Count);
    }

    [Fact]
    public async Task LoadAndCreateAgent_WithConfig_PersistsCustomConfiguration()
    {
        // Arrange
        var jsonContent = @"{
            ""name"": ""ConfiguredAgent"",
            ""purpose"": ""Agent with config"",
            ""enabledSkills"": [],
            ""config"": {
                ""model_preference"": ""phi-4"",
                ""temperature"": ""0.7"",
                ""max_tokens"": ""1000""
            }
        }";

        var filePath = Path.Combine(_testDirectory, "configured-agent.json");
        await File.WriteAllTextAsync(filePath, jsonContent);

        // Act
        var config = await _loader.LoadAgentConfigAsync(filePath);
        var definition = _loader.ToAgentDefinition(config);
        var agent = await _agentManager.CreateAgentAsync(definition);

        // Assert
        Assert.Equal(3, agent.Config.Count);
        Assert.Equal("phi-4", agent.Config["model_preference"]);
        Assert.Equal("0.7", agent.Config["temperature"]);
        Assert.Equal("1000", agent.Config["max_tokens"]);

        // Verify database persisted the config
        var retrievedAgent = await _agentManager.GetAgentAsync(agent.Id);
        Assert.NotNull(retrievedAgent);
        Assert.Equal(3, retrievedAgent.Config.Count);
        Assert.Equal("phi-4", retrievedAgent.Config["model_preference"]);
    }

    [Fact]
    public async Task ValidationAndCreation_WithInvalidConfig_FailsValidation()
    {
        // Arrange
        var jsonContent = @"{
            ""name"": """",
            ""purpose"": ""Missing name""
        }";

        var filePath = Path.Combine(_testDirectory, "invalid.json");
        await File.WriteAllTextAsync(filePath, jsonContent);

        // Act
        var config = await _loader.LoadAgentConfigAsync(filePath);
        var validationResult = _loader.ValidateConfiguration(config);

        // Assert - Validation should fail
        Assert.False(validationResult.IsValid);
        Assert.NotEmpty(validationResult.Errors);
    }

    [Fact]
    public async Task LoadFromDirectory_WithMixedValidityFiles_CreatesOnlyValid()
    {
        // Arrange - Create valid and invalid config files
        var validContent = @"{""name"": ""ValidAgent"", ""purpose"": ""Valid"", ""enabledSkills"": []}";
        var invalidContent = @"{""name"": """", ""purpose"": ""Missing name""}"; // Invalid

        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "valid.json"), validContent);
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "invalid.json"), invalidContent);

        // Act - Load from directory
        var batch = await _loader.LoadAgentBatchAsync(_testDirectory);

        // Validate each config
        var validConfigs = batch.Agents
            .Where(c => _loader.ValidateConfiguration(c).IsValid)
            .ToList();

        var createdAgents = new List<Agent>();
        foreach (var config in validConfigs)
        {
            var definition = _loader.ToAgentDefinition(config);
            var agent = await _agentManager.CreateAgentAsync(definition);
            createdAgents.Add(agent);
        }

        // Assert - Only valid agent should be created
        Assert.Single(createdAgents);
        Assert.Equal("ValidAgent", createdAgents[0].Name);
    }
}
