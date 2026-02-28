using Daiv3.Orchestration.Configuration;
using Daiv3.Orchestration.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Daiv3.UnitTests.Orchestration;

/// <summary>
/// Unit tests for AgentConfigFileLoader.
/// Tests JSON/YAML parsing, validation, and conversion to AgentDefinition.
/// </summary>
public class AgentConfigFileLoaderTests : IDisposable
{
    private readonly Mock<ILogger<AgentConfigFileLoader>> _mockLogger;
    private readonly Mock<ISkillRegistry> _mockSkillRegistry;
    private readonly AgentConfigFileLoader _loader;
    private readonly string _testDirectory;

    public AgentConfigFileLoaderTests()
    {
        _mockLogger = new Mock<ILogger<AgentConfigFileLoader>>();
        _mockSkillRegistry = new Mock<ISkillRegistry>();
        _loader = new AgentConfigFileLoader(_mockLogger.Object, _mockSkillRegistry.Object);
        _testDirectory = Path.Combine(Path.GetTempPath(), $"daiv3-agent-config-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    #region JSON Parsing Tests

    [Fact]
    public async Task LoadAgentConfigAsync_WithValidJsonFile_ReturnsAgentConfiguration()
    {
        // Arrange
        var jsonContent = @"{
            ""name"": ""TestAgent"",
            ""purpose"": ""Test agent for unit testing"",
            ""enabledSkills"": [""skill-search"", ""skill-analyze""],
            ""config"": {
                ""model_preference"": ""phi-4"",
                ""max_iterations"": ""15"",
                ""output_format"": ""json""
            }
        }";

        var filePath = Path.Combine(_testDirectory, "test-agent.json");
        await File.WriteAllTextAsync(filePath, jsonContent);

        // Act
        var config = await _loader.LoadAgentConfigAsync(filePath);

        // Assert
        Assert.NotNull(config);
        Assert.Equal("TestAgent", config.Name);
        Assert.Equal("Test agent for unit testing", config.Purpose);
        Assert.Equal(2, config.EnabledSkills.Count);
        Assert.Contains("skill-search", config.EnabledSkills);
        Assert.Contains("skill-analyze", config.EnabledSkills);
        Assert.Equal(3, config.Config.Count);
        Assert.Equal("phi-4", config.Config["model_preference"]);
        Assert.Equal("15", config.Config["max_iterations"]);
        Assert.Equal("json", config.Config["output_format"]);
    }

    [Fact]
    public async Task LoadAgentConfigAsync_WithMinimalJsonFile_ReturnsConfigWithDefaults()
    {
        // Arrange
        var jsonContent = @"{
            ""name"": ""MinimalAgent"",
            ""purpose"": ""A minimal agent""
        }";

        var filePath = Path.Combine(_testDirectory, "minimal-agent.json");
        await File.WriteAllTextAsync(filePath, jsonContent);

        // Act
        var config = await _loader.LoadAgentConfigAsync(filePath);

        // Assert
        Assert.NotNull(config);
        Assert.Equal("MinimalAgent", config.Name);
        Assert.Equal("A minimal agent", config.Purpose);
        Assert.Empty(config.EnabledSkills);
        Assert.Empty(config.Config);
    }

    [Fact]
    public async Task LoadAgentConfigAsync_WithEmptySkillsList_HandlesGracefully()
    {
        // Arrange
        var jsonContent = @"{
            ""name"": ""AgentWithEmptySkills"",
            ""purpose"": ""Test agent"",
            ""enabledSkills"": []
        }";

        var filePath = Path.Combine(_testDirectory, "empty-skills.json");
        await File.WriteAllTextAsync(filePath, jsonContent);

        // Act
        var config = await _loader.LoadAgentConfigAsync(filePath);

        // Assert
        Assert.NotNull(config);
        Assert.Empty(config.EnabledSkills);
    }

    [Fact]
    public async Task LoadAgentConfigAsync_WithNumericConfigValues_ConvertsToStrings()
    {
        // Arrange
        var jsonContent = @"{
            ""name"": ""NumericAgent"",
            ""purpose"": ""Test agent"",
            ""config"": {
                ""timeout_seconds"": 600,
                ""token_budget"": 20000,
                ""enabled"": true,
                ""disabled"": false
            }
        }";

        var filePath = Path.Combine(_testDirectory, "numeric-config.json");
        await File.WriteAllTextAsync(filePath, jsonContent);

        // Act
        var config = await _loader.LoadAgentConfigAsync(filePath);

        // Assert
        Assert.NotNull(config);
        Assert.Equal("600", config.Config["timeout_seconds"]);
        Assert.Equal("20000", config.Config["token_budget"]);
        Assert.Equal("true", config.Config["enabled"]);
        Assert.Equal("false", config.Config["disabled"]);
    }

    [Fact]
    public async Task LoadAgentConfigAsync_WithFileNotFound_ThrowsFileNotFoundException()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testDirectory, "nonexistent.json");

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() => _loader.LoadAgentConfigAsync(nonExistentPath));
    }

    [Fact]
    public async Task LoadAgentConfigAsync_WithInvalidJsonSyntax_ThrowsInvalidOperationException()
    {
        // Arrange
        var jsonContent = @"{ invalid json }";
        var filePath = Path.Combine(_testDirectory, "invalid.json");
        await File.WriteAllTextAsync(filePath, jsonContent);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _loader.LoadAgentConfigAsync(filePath));
    }

    [Fact]
    public async Task LoadAgentConfigAsync_WithMissingNameField_ThrowsInvalidOperationException()
    {
        // Arrange
        var jsonContent = @"{
            ""purpose"": ""Missing name field""
        }";

        var filePath = Path.Combine(_testDirectory, "missing-name.json");
        await File.WriteAllTextAsync(filePath, jsonContent);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _loader.LoadAgentConfigAsync(filePath));
    }

    [Fact]
    public async Task LoadAgentConfigAsync_WithMissingPurposeField_ThrowsInvalidOperationException()
    {
        // Arrange
        var jsonContent = @"{
            ""name"": ""NoMeta""
        }";

        var filePath = Path.Combine(_testDirectory, "missing-purpose.json");
        await File.WriteAllTextAsync(filePath, jsonContent);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _loader.LoadAgentConfigAsync(filePath));
    }

    [Fact]
    public async Task LoadAgentConfigAsync_WithUnsupportedFileFormat_ThrowsInvalidOperationException()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "agent.txt");
        await File.WriteAllTextAsync(filePath, "dummy content");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _loader.LoadAgentConfigAsync(filePath));
    }

    #endregion

    #region Batch Loading Tests

    [Fact]
    public async Task LoadAgentBatchAsync_WithValidJsonBatchFile_ReturnsBatchConfiguration()
    {
        // Arrange
        var batchContent = @"{
            ""batchName"": ""TestBatch"",
            ""description"": ""Test batch of agents"",
            ""metadata"": {
                ""version"": ""1.0"",
                ""author"": ""test""
            },
            ""agents"": [
                {
                    ""name"": ""Agent1"",
                    ""purpose"": ""First agent""
                },
                {
                    ""name"": ""Agent2"",
                    ""purpose"": ""Second agent"",
                    ""enabledSkills"": [""skill1""]
                }
            ]
        }";

        var filePath = Path.Combine(_testDirectory, "batch.json");
        await File.WriteAllTextAsync(filePath, batchContent);

        // Act
        var batch = await _loader.LoadAgentBatchAsync(filePath);

        // Assert
        Assert.NotNull(batch);
        Assert.Equal("TestBatch", batch.BatchName);
        Assert.Equal("Test batch of agents", batch.Description);
        Assert.Equal(2, batch.Agents.Count);
        Assert.Equal("Agent1", batch.Agents[0].Name);
        Assert.Equal("Agent2", batch.Agents[1].Name);
        Assert.Single(batch.Agents[1].EnabledSkills);
    }

    [Fact]
    public async Task LoadAgentBatchAsync_FromDirectory_LoadsAllConfigFiles()
    {
        // Arrange
        var agent1Content = @"{""name"": ""Agent1"", ""purpose"": ""Test agent 1""}";
        var agent2Content = @"{""name"": ""Agent2"", ""purpose"": ""Test agent 2""}";

        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "agent1.json"), agent1Content);
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "agent2.json"), agent2Content);

        // Act
        var batch = await _loader.LoadAgentBatchAsync(_testDirectory, recursive: false);

        // Assert
        Assert.NotNull(batch);
        Assert.Equal(2, batch.Agents.Count);
        var names = batch.Agents.Select(a => a.Name).OrderBy(n => n).ToList();
        Assert.Equal(["Agent1", "Agent2"], names);
    }

    [Fact]
    public async Task LoadAgentBatchAsync_FromDirectoryRecursive_LoadsNestedConfigFiles()
    {
        // Arrange
        var subDir = Path.Combine(_testDirectory, "subdir");
        Directory.CreateDirectory(subDir);

        var agent1Content = @"{""name"": ""Agent1"", ""purpose"": ""Test agent 1""}";
        var agent2Content = @"{""name"": ""Agent2"", ""purpose"": ""Test agent 2""}";

        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "agent1.json"), agent1Content);
        await File.WriteAllTextAsync(Path.Combine(subDir, "agent2.json"), agent2Content);

        // Act
        var batch = await _loader.LoadAgentBatchAsync(_testDirectory, recursive: true);

        // Assert
        Assert.NotNull(batch);
        Assert.Equal(2, batch.Agents.Count);
    }

    [Fact]
    public async Task LoadAgentBatchAsync_FromNonExistentPath_ThrowsFileNotFoundException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _loader.LoadAgentBatchAsync("/nonexistent/path"));
    }

    #endregion

    #region Validation Tests

    [Fact]
    public void ValidateConfiguration_WithValidConfig_ReturnsValidResult()
    {
        // Arrange
        _mockSkillRegistry.Setup(s => s.ListSkills())
            .Returns(new List<SkillMetadata>
            {
                new()
                {
                    Name = "skill-search",
                    Description = "Search skill",
                    Outputs = new OutputSchema { Type = "string" }
                }
            });

        var config = new AgentConfigurationFile
        {
            Name = "TestAgent",
            Purpose = "Test purpose",
            EnabledSkills = new List<string> { "skill-search" }
        };

        // Act
        var result = _loader.ValidateConfiguration(config);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void ValidateConfiguration_WithEmptyName_ReturnsError()
    {
        // Arrange
        var config = new AgentConfigurationFile
        {
            Name = string.Empty,
            Purpose = "Test purpose"
        };

        // Act
        var result = _loader.ValidateConfiguration(config);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
        Assert.Contains("name is required", result.Errors[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateConfiguration_WithEmptyPurpose_ReturnsError()
    {
        // Arrange
        var config = new AgentConfigurationFile
        {
            Name = "TestAgent",
            Purpose = string.Empty
        };

        // Act
        var result = _loader.ValidateConfiguration(config);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void ValidateConfiguration_WithUnregisteredSkill_ReturnsWarning()
    {
        // Arrange
        _mockSkillRegistry.Setup(s => s.ListSkills())
            .Returns(new List<SkillMetadata>
            {
                new()
                {
                    Name = "skill-search",
                    Description = "Search skill",
                    Outputs = new OutputSchema { Type = "string" }
                }
            });

        var config = new AgentConfigurationFile
        {
            Name = "TestAgent",
            Purpose = "Test purpose",
            EnabledSkills = new List<string> { "skill-nonexistent" }
        };

        // Act
        var result = _loader.ValidateConfiguration(config);

        // Assert
        Assert.True(result.IsValid); // Not an error, just a warning
        Assert.NotEmpty(result.Warnings);
        Assert.Contains("not registered", result.Warnings[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateConfiguration_WithInvalidIterationLimit_ReturnsWarning()
    {
        // Arrange
        _mockSkillRegistry.Setup(s => s.ListSkills())
            .Returns(new List<SkillMetadata>());

        var config = new AgentConfigurationFile
        {
            Name = "TestAgent",
            Purpose = "Test purpose",
            Config = new Dictionary<string, string> { { "max_iterations", "invalid" } }
        };

        // Act
        var result = _loader.ValidateConfiguration(config);

        // Assert
        Assert.True(result.IsValid);
        Assert.NotEmpty(result.Warnings);
        Assert.Contains("max_iterations", result.Warnings[0]);
    }

    [Fact]
    public void ValidateConfiguration_WithInvalidTokenBudget_ReturnsWarning()
    {
        // Arrange
        _mockSkillRegistry.Setup(s => s.ListSkills())
            .Returns(new List<SkillMetadata>());

        var config = new AgentConfigurationFile
        {
            Name = "TestAgent",
            Purpose = "Test purpose",
            Config = new Dictionary<string, string> { { "token_budget", "-5000" } }
        };

        // Act
        var result = _loader.ValidateConfiguration(config);

        // Assert
        Assert.True(result.IsValid);
        Assert.NotEmpty(result.Warnings);
    }

    #endregion

    #region Conversion Tests

    [Fact]
    public void ToAgentDefinition_WithValidConfig_ReturnsAgentDefinition()
    {
        // Arrange
        var config = new AgentConfigurationFile
        {
            Name = "TestAgent",
            Purpose = "Test purpose",
            EnabledSkills = new List<string> { "skill1", "skill2" },
            Config = new Dictionary<string, string>
            {
                { "key1", "value1" },
                { "key2", "value2" }
            }
        };

        // Act
        var definition = _loader.ToAgentDefinition(config);

        // Assert
        Assert.NotNull(definition);
        Assert.Equal("TestAgent", definition.Name);
        Assert.Equal("Test purpose", definition.Purpose);
        Assert.Equal(2, definition.EnabledSkills.Count);
        Assert.Contains("skill1", definition.EnabledSkills);
        Assert.Contains("skill2", definition.EnabledSkills);
        Assert.Equal(2, definition.Config.Count);
        Assert.Equal("value1", definition.Config["key1"]);
        Assert.Equal("value2", definition.Config["key2"]);
    }

    [Fact]
    public void ToAgentDefinition_CreatesIndependentLists_NotReference()
    {
        // Arrange
        var skillsList = new List<string> { "skill1" };
        var configDict = new Dictionary<string, string> { { "key1", "value1" } };

        var config = new AgentConfigurationFile
        {
            Name = "TestAgent",
            Purpose = "Test purpose",
            EnabledSkills = skillsList,
            Config = configDict
        };

        // Act
        var definition = _loader.ToAgentDefinition(config);

        // Modify originals
        skillsList.Add("skill2");
        configDict["key2"] = "value2";

        // Assert - definition should not be affected
        Assert.Single(definition.EnabledSkills);
        Assert.Single(definition.Config);
    }

    #endregion

    #region File Format Tests

    [Theory]
    [InlineData(".json")]
    [InlineData(".JSON")]
    [InlineData(".Json")]
    public async Task LoadAgentConfigAsync_WithCaseInsensitiveJsonExtension_ParsesCorrctly(string extension)
    {
        // Arrange
        var jsonContent = @"{""name"": ""TestAgent"", ""purpose"": ""Test""}";
        var filePath = Path.Combine(_testDirectory, $"test{extension}");
        await File.WriteAllTextAsync(filePath, jsonContent);

        // Act
        var config = await _loader.LoadAgentConfigAsync(filePath);

        // Assert
        Assert.NotNull(config);
        Assert.Equal("TestAgent", config.Name);
    }

    [Fact]
    public async Task LoadAgentConfigAsync_WithYamlExtension_ThrowsInvalidOperationException()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "test.yaml");
        await File.WriteAllTextAsync(filePath, "dummy content");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _loader.LoadAgentConfigAsync(filePath));
        Assert.Contains("YamlDotNet", ex.Message);
    }

    #endregion
}
