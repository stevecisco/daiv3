using Daiv3.Orchestration.Configuration;
using Daiv3.Orchestration.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Daiv3.Orchestration.Tests;

public sealed class ModuleAutoLoadHostedServiceTests : IDisposable
{
    private readonly string _rootDirectory;
    private readonly string _skillsDirectory;
    private readonly string _agentsDirectory;
    private readonly List<IDisposable> _disposables = new();

    public ModuleAutoLoadHostedServiceTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), $"daiv3-module-autoload-{Guid.NewGuid()}");
        _skillsDirectory = Path.Combine(_rootDirectory, "skills");
        _agentsDirectory = Path.Combine(_rootDirectory, "agents");

        Directory.CreateDirectory(_skillsDirectory);
        Directory.CreateDirectory(_agentsDirectory);
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }

        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task StartAsync_AutoDiscoveryDisabled_DoesNotCreateAgents()
    {
        // Arrange
        var mockAgentManager = new Mock<IAgentManager>(MockBehavior.Strict);
        var service = CreateService(mockAgentManager, options =>
        {
            options.EnableModuleAutoDiscovery = false;
            options.SkillConfigAutoLoadPath = _skillsDirectory;
            options.AgentConfigAutoLoadPath = _agentsDirectory;
            options.ModuleAutoLoadRecursive = false;
        });

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        mockAgentManager.Verify(
            m => m.CreateAgentAsync(It.IsAny<AgentDefinition>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task StartAsync_WithValidConfigFiles_LoadsSkillAndCreatesAgent()
    {
        // Arrange
        var skillJson = """
{
  "name": "GreetingSkill",
  "description": "Generates greeting text",
  "category": "Communication",
  "source": "UserDefined",
  "inputs": [
    { "name": "name", "type": "string", "required": true }
  ],
  "output": { "type": "string", "description": "Greeting response" },
  "permissions": [],
  "config": {
    "response_template": "Hello {{name}}"
  }
}
""";

        var agentJson = """
{
  "name": "GreetingAgent",
  "purpose": "Handles greeting tasks",
  "enabledSkills": ["GreetingSkill"],
  "config": {
    "max_iterations": "3"
  }
}
""";

        await File.WriteAllTextAsync(Path.Combine(_skillsDirectory, "greeting-skill.json"), skillJson);
        await File.WriteAllTextAsync(Path.Combine(_agentsDirectory, "greeting-agent.json"), agentJson);

        var mockAgentManager = new Mock<IAgentManager>(MockBehavior.Strict);
        mockAgentManager
            .Setup(m => m.CreateAgentAsync(It.IsAny<AgentDefinition>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentDefinition def, CancellationToken _) => new Agent
            {
                Id = Guid.NewGuid(),
                Name = def.Name,
                Purpose = def.Purpose,
                EnabledSkills = new List<string>(def.EnabledSkills),
                Config = new Dictionary<string, string>(def.Config),
                CreatedAt = DateTimeOffset.UtcNow
            });

        var service = CreateService(mockAgentManager, options =>
        {
            options.EnableModuleAutoDiscovery = true;
            options.SkillConfigAutoLoadPath = _skillsDirectory;
            options.AgentConfigAutoLoadPath = _agentsDirectory;
            options.ModuleAutoLoadRecursive = false;
        }, out var registry);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        var skills = registry.ListSkills();
        var loadedSkill = Assert.Single(skills, s => s.Name == "GreetingSkill");
        Assert.Equal(SkillSource.UserDefined, loadedSkill.Source);

        mockAgentManager.Verify(
            m => m.CreateAgentAsync(
                It.Is<AgentDefinition>(d =>
                    d.Name == "GreetingAgent" &&
                    d.EnabledSkills.Count == 1 &&
                    d.EnabledSkills[0] == "GreetingSkill"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private ModuleAutoLoadHostedService CreateService(
        Mock<IAgentManager> mockAgentManager,
        Action<OrchestrationOptions> configureOptions,
        out ISkillRegistry skillRegistry)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.Configure(configureOptions);
        services.AddSingleton<ISkillRegistry, SkillRegistry>();
        services.AddScoped<SkillConfigFileLoader>();
        services.AddScoped<AgentConfigFileLoader>();
        services.AddScoped(_ => mockAgentManager.Object);

        var provider = services.BuildServiceProvider();
        _disposables.Add(provider);
        skillRegistry = provider.GetRequiredService<ISkillRegistry>();

        return new ModuleAutoLoadHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<ILogger<ModuleAutoLoadHostedService>>(),
            provider.GetRequiredService<IOptions<OrchestrationOptions>>());
    }

    private ModuleAutoLoadHostedService CreateService(
        Mock<IAgentManager> mockAgentManager,
        Action<OrchestrationOptions> configureOptions)
    {
        return CreateService(mockAgentManager, configureOptions, out _);
    }
}
