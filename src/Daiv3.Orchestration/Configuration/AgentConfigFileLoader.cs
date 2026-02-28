using System.Text.Json;
using Daiv3.Orchestration.Interfaces;
using Microsoft.Extensions.Logging;

namespace Daiv3.Orchestration.Configuration;

/// <summary>
/// Loads and parses agent configuration from JSON or YAML files.
/// Converts configuration files into AgentDefinition objects for agent creation.
/// </summary>
public class AgentConfigFileLoader
{
    private readonly ILogger<AgentConfigFileLoader> _logger;
    private readonly ISkillRegistry _skillRegistry;

    public AgentConfigFileLoader(
        ILogger<AgentConfigFileLoader> logger,
        ISkillRegistry skillRegistry)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _skillRegistry = skillRegistry ?? throw new ArgumentNullException(nameof(skillRegistry));
    }

    /// <summary>
    /// Loads a single agent configuration from a JSON or YAML file.
    /// </summary>
    /// <param name="filePath">Path to the configuration file (.json or .yaml/.yml)</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The parsed agent configuration.</returns>
    /// <exception cref="FileNotFoundException">File does not exist.</exception>
    /// <exception cref="InvalidOperationException">File format is not supported or content is invalid.</exception>
    public async Task<AgentConfigurationFile> LoadAgentConfigAsync(
        string filePath,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Configuration file not found: {filePath}");
        }

        var fileExtension = Path.GetExtension(filePath).ToLowerInvariant();

        _logger.LogInformation("Loading agent configuration from {FilePath}", filePath);

        var content = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);
        var config = fileExtension switch
        {
            ".json" => ParseJsonConfiguration(content),
            ".yaml" or ".yml" => ParseYamlConfiguration(content),
            _ => throw new InvalidOperationException(
                $"Unsupported configuration file format: {fileExtension}. Supported formats: .json, .yaml, .yml")
        };

        _logger.LogInformation("Loaded agent configuration: {AgentName}", config.Name);
        return config;
    }

    /// <summary>
    /// Loads multiple agent configurations from a batch file or directory.
    /// </summary>
    /// <param name="sourcePathOrFile">Path to configuration file or directory containing .json/.yaml files</param>
    /// <param name="recursive">If sourcePathOrFile is a directory, whether to search recursively</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Batch of agent configurations.</returns>
    public async Task<AgentConfigurationBatch> LoadAgentBatchAsync(
        string sourcePathOrFile,
        bool recursive = false,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePathOrFile);

        // If it's a file, load it as a batch file
        if (File.Exists(sourcePathOrFile))
        {
            _logger.LogInformation("Loading agent batch from file: {FilePath}", sourcePathOrFile);
            return await LoadBatchFileAsync(sourcePathOrFile, ct).ConfigureAwait(false);
        }

        // If it's a directory, scan for config files
        if (Directory.Exists(sourcePathOrFile))
        {
            _logger.LogInformation(
                "Loading agent configurations from directory: {DirectoryPath} (recursive={Recursive})",
                sourcePathOrFile,
                recursive);
            return await LoadFromDirectoryAsync(sourcePathOrFile, recursive, ct).ConfigureAwait(false);
        }

        throw new FileNotFoundException($"Path not found: {sourcePathOrFile}");
    }

    /// <summary>
    /// Validates an agent configuration against the system's current state.
    /// </summary>
    /// <param name="config">The configuration to validate.</param>
    /// <returns>Validation result with errors and warnings.</returns>
    public AgentConfigurationValidationResult ValidateConfiguration(AgentConfigurationFile config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var result = new AgentConfigurationValidationResult();

        // Validate required fields
        if (string.IsNullOrWhiteSpace(config.Name))
        {
            result.AddError("Agent name is required and cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(config.Purpose))
        {
            result.AddError("Agent purpose is required and cannot be empty");
        }

        // Validate skill names
        if (config.EnabledSkills.Count > 0)
        {
            var registeredSkills = _skillRegistry.ListSkills();
            var registeredSkillNames = registeredSkills.Select(s => s.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var skillName in config.EnabledSkills)
            {
                if (!registeredSkillNames.Contains(skillName))
                {
                    result.AddWarning($"Skill '{skillName}' is not registered. Agent will still be created, but skill will be unavailable.");
                }
            }
        }

        // Validate config values (e.g., numeric fields)
        if (config.Config.TryGetValue("max_iterations", out var maxIterStr))
        {
            if (!int.TryParse(maxIterStr, out var maxIter) || maxIter <= 0)
            {
                result.AddWarning($"Invalid max_iterations value '{maxIterStr}'. Must be positive integer. Using default.");
            }
        }

        if (config.Config.TryGetValue("timeout_seconds", out var timeoutStr))
        {
            if (!int.TryParse(timeoutStr, out var timeout) || timeout <= 0)
            {
                result.AddWarning($"Invalid timeout_seconds value '{timeoutStr}'. Must be positive integer. Using default.");
            }
        }

        if (config.Config.TryGetValue("token_budget", out var budgetStr))
        {
            if (!int.TryParse(budgetStr, out var budget) || budget <= 0)
            {
                result.AddWarning($"Invalid token_budget value '{budgetStr}'. Must be positive integer. Using default.");
            }
        }

        return result;
    }

    /// <summary>
    /// Converts an agent configuration file to an agent definition for creation.
    /// </summary>
    /// <param name="config">The configuration file.</param>
    /// <returns>Agent definition ready for creation.</returns>
    public AgentDefinition ToAgentDefinition(AgentConfigurationFile config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return new AgentDefinition
        {
            Name = config.Name,
            Purpose = config.Purpose,
            EnabledSkills = new List<string>(config.EnabledSkills),
            Config = new Dictionary<string, string>(config.Config)
        };
    }

    /// <summary>
    /// Parses JSON configuration content.
    /// </summary>
    private static AgentConfigurationFile ParseJsonConfiguration(string content)
    {
        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            // Check if this is a batch file (has "agents" array) or single agent
            if (root.TryGetProperty("agents", out var agentsArray))
            {
                // This is a batch file, extract the first agent or use a default structure
                throw new InvalidOperationException(
                    "Batch format detected. Use LoadAgentBatchAsync for batch files containing multiple agents.");
            }

            var config = new AgentConfigurationFile
            {
                Name = GetRequiredString(root, "name"),
                Purpose = GetRequiredString(root, "purpose")
            };

            // Parse enabled skills if present
            if (root.TryGetProperty("enabledSkills", out var skillsArray))
            {
                config.EnabledSkills = skillsArray.EnumerateArray()
                    .Select(s => s.GetString() ?? string.Empty)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();
            }

            // Parse config if present
            if (root.TryGetProperty("config", out var configObj))
            {
                foreach (var prop in configObj.EnumerateObject())
                {
                    var value = prop.Value.ValueKind switch
                    {
                        JsonValueKind.String => prop.Value.GetString(),
                        JsonValueKind.Number => prop.Value.GetRawText(),
                        JsonValueKind.True => "true",
                        JsonValueKind.False => "false",
                        JsonValueKind.Null => null,
                        _ => prop.Value.GetRawText()
                    };

                    if (!string.IsNullOrEmpty(value))
                    {
                        config.Config[prop.Name] = value;
                    }
                }
            }

            return config;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Invalid JSON configuration format: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Parses YAML configuration content.
    /// Currently throws NotImplementedException. YAML support requires YamlDotNet library.
    /// </summary>
    private static AgentConfigurationFile ParseYamlConfiguration(string content)
    {
        // YAML support requires adding YamlDotNet as a dependency
        // For now, provide informative error message
        throw new InvalidOperationException(
            "YAML configuration format requires YamlDotNet library. " +
            "Please use JSON format for now, or create an Architecture Decision Document (ADD) to approve YamlDotNet dependency.");
    }

    /// <summary>
    /// Loads a batch configuration file containing multiple agents.
    /// </summary>
    private async Task<AgentConfigurationBatch> LoadBatchFileAsync(
        string filePath,
        CancellationToken ct)
    {
        var fileExtension = Path.GetExtension(filePath).ToLowerInvariant();
        var content = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);

        var batch = fileExtension switch
        {
            ".json" => ParseJsonBatch(content),
            ".yaml" or ".yml" => ParseYamlBatch(content),
            _ => throw new InvalidOperationException($"Unsupported batch file format: {fileExtension}")
        };

        _logger.LogInformation(
            "Loaded batch '{BatchName}' with {AgentCount} agents",
            batch.BatchName ?? "unnamed",
            batch.Agents.Count);

        return batch;
    }

    /// <summary>
    /// Loads all configuration files from a directory.
    /// </summary>
    private async Task<AgentConfigurationBatch> LoadFromDirectoryAsync(
        string directoryPath,
        bool recursive,
        CancellationToken ct)
    {
        var batch = new AgentConfigurationBatch
        {
            BatchName = Path.GetFileName(directoryPath),
            Description = $"Agents loaded from {directoryPath}"
        };

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var configFiles = Directory.EnumerateFiles(
            directoryPath,
            "*.*",
            searchOption)
            .Where(f => f.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
                       f.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) ||
                       f.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f) // Consistent ordering
            .ToList();

        _logger.LogInformation("Found {ConfigFileCount} configuration files in {DirectoryPath}", configFiles.Count, directoryPath);

        foreach (var configFile in configFiles)
        {
            try
            {
                var config = await LoadAgentConfigAsync(configFile, ct).ConfigureAwait(false);
                batch.Agents.Add(config);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load configuration file: {FilePath}", configFile);
                batch.Metadata[$"error_file_{configFile}"] = ex.Message;
            }
        }

        return batch;
    }

    /// <summary>
    /// Parses a JSON batch configuration file.
    /// </summary>
    private static AgentConfigurationBatch ParseJsonBatch(string content)
    {
        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            var batch = new AgentConfigurationBatch();

            // Check for batch metadata
            if (root.TryGetProperty("batchName", out var batchNameElem))
            {
                batch.BatchName = batchNameElem.GetString();
            }

            if (root.TryGetProperty("description", out var descElem))
            {
                batch.Description = descElem.GetString();
            }

            // Parse metadata if present
            if (root.TryGetProperty("metadata", out var metadataObj))
            {
                foreach (var prop in metadataObj.EnumerateObject())
                {
                    batch.Metadata[prop.Name] = prop.Value.GetString() ?? string.Empty;
                }
            }

            // Parse agents array
            if (root.TryGetProperty("agents", out var agentsArray))
            {
                foreach (var agentElem in agentsArray.EnumerateArray())
                {
                    var agent = ParseJsonAgentElement(agentElem);
                    batch.Agents.Add(agent);
                }
            }

            return batch;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Invalid JSON batch configuration format: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Parses a YAML batch configuration file.
    /// Currently throws NotImplementedException.
    /// </summary>
    private static AgentConfigurationBatch ParseYamlBatch(string content)
    {
        throw new InvalidOperationException(
            "YAML configuration format requires YamlDotNet library. Please use JSON format for now.");
    }

    /// <summary>
    /// Parses a single agent from a JSON element.
    /// </summary>
    private static AgentConfigurationFile ParseJsonAgentElement(JsonElement agentElem)
    {
        var config = new AgentConfigurationFile
        {
            Name = GetRequiredString(agentElem, "name"),
            Purpose = GetRequiredString(agentElem, "purpose")
        };

        if (agentElem.TryGetProperty("enabledSkills", out var skillsArray))
        {
            config.EnabledSkills = skillsArray.EnumerateArray()
                .Select(s => s.GetString() ?? string.Empty)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
        }

        if (agentElem.TryGetProperty("config", out var configObj))
        {
            foreach (var prop in configObj.EnumerateObject())
            {
                var value = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString(),
                    JsonValueKind.Number => prop.Value.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Null => null,
                    _ => prop.Value.GetRawText()
                };

                if (!string.IsNullOrEmpty(value))
                {
                    config.Config[prop.Name] = value;
                }
            }
        }

        return config;
    }

    /// <summary>
    /// Gets a required string property from a JSON element.
    /// </summary>
    /// <exception cref="InvalidOperationException">Property is missing or not a string.</exception>
    private static string GetRequiredString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop))
        {
            throw new InvalidOperationException($"Required property '{propertyName}' is missing from configuration");
        }

        var value = prop.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Required property '{propertyName}' cannot be empty");
        }

        return value;
    }
}
