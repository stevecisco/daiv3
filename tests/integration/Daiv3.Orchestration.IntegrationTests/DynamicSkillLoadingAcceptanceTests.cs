using Daiv3.Orchestration;
using Daiv3.Orchestration.Configuration;
using Daiv3.Orchestration.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Daiv3.Orchestration.IntegrationTests;

/// <summary>
/// Acceptance tests for dynamic skill loading without recompilation.
/// Verifies AST-ACC-001: A new skill can be added without recompiling the core app.
/// </summary>
public class DynamicSkillLoadingAcceptanceTests
{
	private readonly IServiceProvider _serviceProvider;
	private readonly ISkillRegistry _skillRegistry;
	private readonly ILogger<DynamicSkillLoadingAcceptanceTests> _logger;
	private readonly string _testDataDirectory;

	public DynamicSkillLoadingAcceptanceTests()
	{
		var services = new ServiceCollection();
		services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
		services.TryAddScoped<ISkillRegistry, SkillRegistry>();
		
		_serviceProvider = services.BuildServiceProvider();
		_skillRegistry = _serviceProvider.GetRequiredService<ISkillRegistry>();
		_logger = _serviceProvider.GetRequiredService<ILogger<DynamicSkillLoadingAcceptanceTests>>();

		// Locate test data directory
		var projectDirectory = FindProjectDirectory();
		_testDataDirectory = Path.Combine(projectDirectory, "TestData", "SkillConfigs");
		
		_logger.LogInformation("Test data directory: {TestDataDirectory}", _testDataDirectory);
	}

	/// <summary>
	/// Acceptance Test: Single skill can be dynamically loaded from JSON without recompilation.
	/// </summary>
	[Fact]
	public async Task AcceptanceTest_LoadSingleSkillFromJson_WithoutRecompilation()
	{
		// Arrange
		var skillJsonPath = Path.Combine(_testDataDirectory, "DynamicTestSkill.json");
		Assert.True(File.Exists(skillJsonPath), $"Test skill JSON not found at {skillJsonPath}");

		_logger.LogInformation("Starting acceptance test: Load single skill from JSON without recompilation");
		_logger.LogInformation("Skill config file: {SkillJsonPath}", skillJsonPath);

		// Load skill configuration from JSON file (no recompilation)
		var logger = _serviceProvider.GetRequiredService<ILogger<SkillConfigFileLoader>>();
		var loader = new SkillConfigFileLoader(logger);

		// Act - Load configuration from file
		var jsonContent = await File.ReadAllTextAsync(skillJsonPath);
		_logger.LogInformation("Loaded skill configuration from file");

		var config = loader.LoadSkillConfigFromJson(jsonContent);
		Assert.NotNull(config);
		_logger.LogInformation("Parsed skill configuration: {SkillName}", config.Name);

		// Validate configuration
		var validationResult = loader.ValidateConfiguration(config);
		Assert.True(validationResult.IsValid, string.Join(", ", validationResult.Errors));
		_logger.LogInformation("Configuration validation passed");

		// Convert to metadata
		var metadata = loader.ToSkillMetadata(config);
		Assert.NotNull(metadata);
		_logger.LogInformation("Converted configuration to skill metadata: {SkillName}", metadata.Name);

		// Create a test skill instance from metadata (simulating runtime creation)
		var testSkill = new ConfigurableTestSkill(metadata);

		// Register the dynamically loaded skill
		_skillRegistry.RegisterSkill(testSkill, SkillSource.UserDefined);
		_logger.LogInformation("Registered skill with source: {Source}", SkillSource.UserDefined);

		// Assert - Verify skill is registered and accessible without recompilation
		var allSkills = _skillRegistry.ListSkills();
		Assert.NotEmpty(allSkills);

		var loadedSkill = allSkills.FirstOrDefault(s => s.Name == "DynamicTestSkill");
		Assert.NotNull(loadedSkill);
		_logger.LogInformation("Skill found in registry: {SkillName}", loadedSkill.Name);

		Assert.Equal(SkillSource.UserDefined, loadedSkill.Source);
		_logger.LogInformation("Skill source verified: {Source}", loadedSkill.Source);

		Assert.Equal("A test skill that demonstrates dynamic loading without recompilation", loadedSkill.Description);
		_logger.LogInformation("Skill description verified");

		// Verify source can be queried
		var queriedSource = _skillRegistry.GetSkillSource("DynamicTestSkill");
		Assert.Equal(SkillSource.UserDefined, queriedSource);
		_logger.LogInformation("Skill source query successful");

		_logger.LogInformation("✓ Acceptance criteria met: Skill loaded and registered without recompilation");
	}

	/// <summary>
	/// Acceptance Test: Multiple skills can be loaded from directory without recompilation.
	/// </summary>
	[Fact]
	public async Task AcceptanceTest_LoadMultipleSkillsFromDirectory_WithoutRecompilation()
	{
		// Arrange
		Assert.True(Directory.Exists(_testDataDirectory), $"Test data directory not found at {_testDataDirectory}");

		var logger = _serviceProvider.GetRequiredService<ILogger<SkillConfigFileLoader>>();
		var loader = new SkillConfigFileLoader(logger);

		_logger.LogInformation("Starting acceptance test: Load multiple skills from directory");
		_logger.LogInformation("Skills directory: {SkillsDirectory}", _testDataDirectory);

		// Act - Load all skill configurations from directory
		var jsonFiles = Directory.GetFiles(_testDataDirectory, "*.json");
		Assert.NotEmpty(jsonFiles);
		_logger.LogInformation("Found {SkillCount} skill configuration files", jsonFiles.Length);

		var loadedSkills = new List<SkillMetadata>();

		foreach (var jsonFile in jsonFiles)
		{
			var jsonContent = await File.ReadAllTextAsync(jsonFile);
			var config = loader.LoadSkillConfigFromJson(jsonContent);

			if (config != null)
			{
				var validationResult = loader.ValidateConfiguration(config);
				if (validationResult.IsValid)
				{
					var metadata = loader.ToSkillMetadata(config);
					var testSkill = new ConfigurableTestSkill(metadata);
					_skillRegistry.RegisterSkill(testSkill, SkillSource.UserDefined);
					loadedSkills.Add(metadata);

					_logger.LogInformation("Loaded and registered skill: {SkillName}", config.Name);
				}
				else
				{
					_logger.LogWarning("Validation failed for skill at {FilePath}: {Errors}",
						jsonFile, string.Join(", ", validationResult.Errors));
				}
			}
		}

		// Assert - All skills registered without recompilation
		Assert.NotEmpty(loadedSkills);
		_logger.LogInformation("Loaded {SkillCount} skills total", loadedSkills.Count);

		var registrySkills = _skillRegistry.ListSkills();
		foreach (var loadedSkill in loadedSkills)
		{
			var foundSkill = registrySkills.FirstOrDefault(s => s.Name == loadedSkill.Name);
			Assert.NotNull(foundSkill);
			Assert.Equal(SkillSource.UserDefined, foundSkill.Source);

			_logger.LogInformation("✓ Verified skill in registry: {SkillName}", foundSkill.Name);
		}

		_logger.LogInformation("✓ Acceptance criteria met: All skills loaded and registered without recompilation");
	}

	/// <summary>
	/// Acceptance Test: Skill configuration changes take effect without recompilation.
	/// </summary>
	[Fact]
	public async Task AcceptanceTest_SkillConfigurationChanges_TakeEffectWithoutRecompilation()
	{
		// Arrange
		var skillJsonPath = Path.Combine(_testDataDirectory, "DynamicTestSkill.json");
		Assert.True(File.Exists(skillJsonPath));

		var logger = _serviceProvider.GetRequiredService<ILogger<SkillConfigFileLoader>>();
		var loader = new SkillConfigFileLoader(logger);

		_logger.LogInformation("Starting acceptance test: Configuration changes without recompilation");

		// Act - Load initial configuration
		var originalContent = await File.ReadAllTextAsync(skillJsonPath);
		var originalConfig = loader.LoadSkillConfigFromJson(originalContent);
		var originalMetadata = loader.ToSkillMetadata(originalConfig);
		var originalSkill = new ConfigurableTestSkill(originalMetadata);
		_skillRegistry.RegisterSkill(originalSkill, SkillSource.UserDefined);

		var initialSkills = _skillRegistry.ListSkills();
		var initialSkill = initialSkills.First(s => s.Name == "DynamicTestSkill");

		_logger.LogInformation("Initial skill description: {Description}", initialSkill.Description);
		Assert.Equal("A test skill that demonstrates dynamic loading without recompilation", initialSkill.Description);

		// Simulate configuration change in memory (no recompilation)
		var modifiedConfig = loader.LoadSkillConfigFromJson(originalContent);
		modifiedConfig.Description = "Updated skill description without recompilation";

		// Create new skill with updated metadata
		var updatedMetadata = loader.ToSkillMetadata(modifiedConfig);
		var updatedSkill = new ConfigurableTestSkill(updatedMetadata);

		// Remove old and register new version (in real scenario, config file would be updated)
		var skillsAfterUpdate = _skillRegistry.ListSkills().Where(s => s.Name != "DynamicTestSkill").ToList();

		// Register updated skill
		_skillRegistry.RegisterSkill(updatedSkill, SkillSource.UserDefined);

		// Assert - Changes are reflected without recompilation
		var allSkills = _skillRegistry.ListSkills();
		var updatedRegisteredSkill = allSkills.First(s => s.Name == "DynamicTestSkill");

		_logger.LogInformation("Updated skill description: {Description}", updatedRegisteredSkill.Description);
		Assert.Equal("Updated skill description without recompilation", updatedRegisteredSkill.Description);

		_logger.LogInformation("✓ Acceptance criteria met: Configuration changes reflected without recompilation");
	}

	/// <summary>
	/// Acceptance Test: Dynamically loaded skill inputs and outputs are accessible.
	/// </summary>
	[Fact]
	public async Task AcceptanceTest_DynamicSkillMetadata_IsAccessible()
	{
		// Arrange
		var skillJsonPath = Path.Combine(_testDataDirectory, "JsonParsingSkill.json");
		Assert.True(File.Exists(skillJsonPath));

		var logger = _serviceProvider.GetRequiredService<ILogger<SkillConfigFileLoader>>();
		var loader = new SkillConfigFileLoader(logger);

		_logger.LogInformation("Starting acceptance test: Dynamic skill metadata accessibility");

		// Act - Load skill with multiple parameters
		var jsonContent = await File.ReadAllTextAsync(skillJsonPath);
		var config = loader.LoadSkillConfigFromJson(jsonContent);
		var metadata = loader.ToSkillMetadata(config);
		var testSkill = new ConfigurableTestSkill(metadata);
		_skillRegistry.RegisterSkill(testSkill, SkillSource.UserDefined);

		var loadedSkill = _skillRegistry.ListSkills().First(s => s.Name == "JsonParsingSkill");

		// Assert - Metadata is fully accessible
		Assert.NotNull(loadedSkill);
		Assert.Equal("JsonParsingSkill", loadedSkill.Name);
		Assert.Equal(SkillCategory.Document, loadedSkill.Category);

		// Verify inputs
		Assert.NotEmpty(loadedSkill.Inputs);
		_logger.LogInformation("Skill has {InputCount} inputs", loadedSkill.Inputs.Count);

		var jsonStringInput = loadedSkill.Inputs.FirstOrDefault(i => i.Name == "jsonString");
		Assert.NotNull(jsonStringInput);
		Assert.True(jsonStringInput.Required);
		_logger.LogInformation("Required input 'jsonString' found");

		var pathInput = loadedSkill.Inputs.FirstOrDefault(i => i.Name == "path");
		Assert.NotNull(pathInput);
		Assert.False(pathInput.Required);
		_logger.LogInformation("Optional input 'path' found");

		// Verify output
		Assert.NotNull(loadedSkill.Outputs);
		Assert.Equal("string", loadedSkill.Outputs.Type);
		_logger.LogInformation("Output type verified: {OutputType}", loadedSkill.Outputs.Type);

		// Verify permissions
		Assert.NotEmpty(loadedSkill.Permissions);
		Assert.Contains("Document.Read", loadedSkill.Permissions);
		_logger.LogInformation("Permissions verified");

		_logger.LogInformation("✓ Acceptance criteria met: All skill metadata accessible");
	}

	#region Helpers

	private string FindProjectDirectory()
	{
		// Navigate up from the assembly location to find the test project directory
		var assemblyLocation = typeof(DynamicSkillLoadingAcceptanceTests).Assembly.Location;
		var directory = Path.GetDirectoryName(assemblyLocation);

		while (directory != null && !File.Exists(Path.Combine(directory, "Daiv3.Orchestration.IntegrationTests.csproj")))
		{
			directory = Path.GetDirectoryName(directory);
		}

		if (directory == null)
		{
			throw new InvalidOperationException("Could not find project directory");
		}

		return directory;
	}

	/// <summary>
	/// Test implementation of ISkill that can be created from configuration metadata.
	/// </summary>
	private class ConfigurableTestSkill : ISkill
	{
		private readonly SkillMetadata _metadata;

		public string Name => _metadata.Name;
		public string Description => _metadata.Description;
		public SkillCategory Category => _metadata.Category;
		public List<ParameterMetadata> Inputs => _metadata.Inputs;
		public OutputSchema OutputSchema => _metadata.Outputs;
		public List<string> Permissions => _metadata.Permissions;

		public ConfigurableTestSkill(SkillMetadata metadata)
		{
			_metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
		}

		public Task<object> ExecuteAsync(Dictionary<string, object> parameters, CancellationToken ct = default)
		{
			// Test implementation - just echoes input or returns success
			if (parameters.Count > 0)
			{
				var firstValue = parameters.Values.FirstOrDefault();
				return Task.FromResult<object>(firstValue ?? "Test execution completed");
			}

			return Task.FromResult<object>("Test execution completed");
		}
	}

	#endregion
}
