using System.Text.Json;
using Daiv3.Orchestration.Interfaces;
using Microsoft.Extensions.Logging;

namespace Daiv3.Orchestration.Configuration;

/// <summary>
/// Loads and parses skill configuration from JSON or YAML files.
/// Converts configuration files into skill metadata for skill registration.
/// </summary>
public class SkillConfigFileLoader
{
    private readonly ILogger<SkillConfigFileLoader> _logger;

    public SkillConfigFileLoader(ILogger<SkillConfigFileLoader> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Loads a single skill configuration from a JSON or YAML file.
    /// </summary>
    /// <param name="filePath">Path to the configuration file (.json or .yaml/.yml)</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The parsed skill configuration.</returns>
    /// <exception cref="FileNotFoundException">File does not exist.</exception>
    /// <exception cref="InvalidOperationException">File format is not supported or content is invalid.</exception>
    public async Task<SkillConfigurationFile> LoadSkillConfigAsync(
        string filePath,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Configuration file not found: {filePath}");
        }

        var fileExtension = Path.GetExtension(filePath).ToLowerInvariant();

        _logger.LogInformation("Loading skill configuration from {FilePath}", filePath);

        var content = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);
        var config = fileExtension switch
        {
            ".json" => ParseJsonConfiguration(content),
            ".yaml" or ".yml" => ParseYamlConfiguration(content),
            _ => throw new InvalidOperationException(
                $"Unsupported configuration file format: {fileExtension}. Supported formats: .json, .yaml, .yml")
        };

        _logger.LogInformation("Loaded skill configuration: {SkillName}", config.Name);
        return config;
    }

    /// <summary>
    /// Loads multiple skill configurations from a batch file or directory.
    /// </summary>
    /// <param name="sourcePathOrFile">Path to configuration file or directory containing .json/.yaml files</param>
    /// <param name="recursive">If sourcePathOrFile is a directory, whether to search recursively</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Batch of skill configurations.</returns>
    public async Task<SkillConfigurationBatch> LoadSkillBatchAsync(
        string sourcePathOrFile,
        bool recursive = false,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePathOrFile);

        // If it's a file, load it as a batch file
        if (File.Exists(sourcePathOrFile))
        {
            _logger.LogInformation("Loading skill batch from file: {FilePath}", sourcePathOrFile);
            return await LoadBatchFileAsync(sourcePathOrFile, ct).ConfigureAwait(false);
        }

        // If it's a directory, scan for config files
        if (Directory.Exists(sourcePathOrFile))
        {
            _logger.LogInformation(
                "Loading skill configurations from directory: {DirectoryPath} (recursive={Recursive})",
                sourcePathOrFile,
                recursive);
            return await LoadFromDirectoryAsync(sourcePathOrFile, recursive, ct).ConfigureAwait(false);
        }

        throw new FileNotFoundException($"Path not found: {sourcePathOrFile}");
    }

    /// <summary>
    /// Parses JSON content into a skill configuration object.
    /// Useful for programmatic configuration loading and testing.
    /// </summary>
    /// <param name="jsonContent">JSON content as a string.</param>
    /// <returns>The parsed skill configuration.</returns>
    /// <exception cref="InvalidOperationException">Content is not valid JSON or is malformed.</exception>
    public SkillConfigurationFile LoadSkillConfigFromJson(string jsonContent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jsonContent);

        _logger.LogInformation("Parsing skill configuration from JSON content");
        var config = ParseJsonConfiguration(jsonContent);
        _logger.LogInformation("Parsed skill configuration: {SkillName}", config.Name);
        return config;
    }

    /// <summary>
    /// Validates a skill configuration against the system's current state.
    /// </summary>
    /// <param name="config">The configuration to validate.</param>
    /// <returns>Validation result with errors and warnings.</returns>
    public SkillConfigurationValidationResult ValidateConfiguration(SkillConfigurationFile config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var result = new SkillConfigurationValidationResult();

        // Validate required fields
        if (string.IsNullOrWhiteSpace(config.Name))
        {
            result.AddError("Skill name is required and cannot be empty");
        }
        else if (config.Name.Length > 100)
        {
            result.AddError("Skill name must be 100 characters or less");
        }

        if (string.IsNullOrWhiteSpace(config.Description))
        {
            result.AddError("Skill description is required and cannot be empty");
        }

        // Validate output schema
        if (config.Output == null)
        {
            result.AddError("Output schema is required");
        }
        else if (string.IsNullOrWhiteSpace(config.Output.Type))
        {
            result.AddError("Output schema type is required");
        }

        // Validate category
        if (!IsValidCategory(config.Category))
        {
            result.AddWarning(
                $"Unknown skill category '{config.Category}'. Using 'Other'. Valid categories are: ReasoningAndAnalysis, Code, Document, DataAndVisualization, WebAndResearch, ProjectManagement, Communication, Other, Unspecified");
        }

        // Validate source
        if (!IsValidSource(config.Source))
        {
            result.AddWarning(
                $"Unknown skill source '{config.Source}'. Using 'UserDefined'. Valid sources are: BuiltIn, UserDefined, Imported");
        }

        // Validate input parameters
        foreach (var param in config.Inputs)
        {
            if (string.IsNullOrWhiteSpace(param.Name))
            {
                result.AddError("Parameter name is required");
            }

            if (string.IsNullOrWhiteSpace(param.Type))
            {
                result.AddError($"Parameter '{param.Name}' type is required");
            }
        }

        return result;
    }

    /// <summary>
    /// Converts a skill configuration file to SkillMetadata.
    /// </summary>
    /// <param name="config">The configuration file.</param>
    /// <returns>Skill metadata.</returns>
    public SkillMetadata ToSkillMetadata(SkillConfigurationFile config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var category = ParseSkillCategory(config.Category);
        var source = ParseSkillSource(config.Source);

        var metadata = new SkillMetadata
        {
            Name = config.Name,
            Description = config.Description,
            Category = category,
            Source = source,
            Outputs = new OutputSchema
            {
                Type = config.Output?.Type ?? "object",
                Description = config.Output?.Description,
                Schema = config.Output?.Schema
            },
            Permissions = new List<string>(config.Permissions)
        };

        // Convert input parameters
        foreach (var param in config.Inputs)
        {
            metadata.Inputs.Add(new ParameterMetadata
            {
                Name = param.Name,
                Type = param.Type,
                Required = param.Required,
                Description = param.Description
            });
        }

        return metadata;
    }

    /// <summary>
    /// Parses JSON configuration content.
    /// </summary>
    private static SkillConfigurationFile ParseJsonConfiguration(string content)
    {
        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            // Check if this is a batch file (has "skills" array) or single skill
            if (root.TryGetProperty("skills", out _))
            {
                throw new InvalidOperationException(
                    "Batch format detected. Use LoadSkillBatchAsync for batch files containing multiple skills.");
            }

            var config = new SkillConfigurationFile
            {
                Name = GetRequiredString(root, "name"),
                Description = GetRequiredString(root, "description")
            };

            if (root.TryGetProperty("category", out var categoryElement))
            {
                config.Category = categoryElement.GetString() ?? "Other";
            }

            if (root.TryGetProperty("source", out var sourceElement))
            {
                config.Source = sourceElement.GetString() ?? "UserDefined";
            }

            // Parse inputs
            if (root.TryGetProperty("inputs", out var inputsElement))
            {
                foreach (var paramElement in inputsElement.EnumerateArray())
                {
                    var param = new SkillParameterConfiguration
                    {
                        Name = GetRequiredString(paramElement, "name"),
                        Type = GetRequiredString(paramElement, "type")
                    };

                    if (paramElement.TryGetProperty("required", out var requiredElement))
                    {
                        param.Required = requiredElement.GetBoolean();
                    }

                    if (paramElement.TryGetProperty("description", out var descElement))
                    {
                        param.Description = descElement.GetString();
                    }

                    config.Inputs.Add(param);
                }
            }

            // Parse output
            if (root.TryGetProperty("output", out var outputElement))
            {
                config.Output = new SkillOutputSchemaConfiguration
                {
                    Type = GetRequiredString(outputElement, "type")
                };

                if (outputElement.TryGetProperty("description", out var descElement))
                {
                    config.Output.Description = descElement.GetString();
                }

                if (outputElement.TryGetProperty("schema", out var schemaElement))
                {
                    config.Output.Schema = schemaElement.GetRawText();
                }
            }

            // Parse permissions
            if (root.TryGetProperty("permissions", out var permissionsElement))
            {
                foreach (var permElement in permissionsElement.EnumerateArray())
                {
                    var perm = permElement.GetString();
                    if (!string.IsNullOrWhiteSpace(perm))
                    {
                        config.Permissions.Add(perm);
                    }
                }
            }

            // Parse custom config
            if (root.TryGetProperty("config", out var configElement))
            {
                foreach (var prop in configElement.EnumerateObject())
                {
                    config.Config[prop.Name] = prop.Value.GetString() ?? string.Empty;
                }
            }

            return config;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to parse JSON configuration: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Parses YAML configuration content.
    /// For now, this is a basic implementation that logs a warning and returns a default config.
    /// Full YAML support would require an external library like YamlDotNet.
    /// </summary>
    private static SkillConfigurationFile ParseYamlConfiguration(string content)
    {
        // TODO: Implement full YAML parsing using YamlDotNet or similar
        // For v0.1, we'll support JSON format primarily
        throw new NotImplementedException(
            "YAML parsing requires YamlDotNet dependency (not yet approved). " +
            "Please use JSON format for skill configurations.");
    }

    /// <summary>
    /// Loads a batch file containing multiple skills.
    /// </summary>
    private async Task<SkillConfigurationBatch> LoadBatchFileAsync(
        string filePath,
        CancellationToken ct)
    {
        var fileExtension = Path.GetExtension(filePath).ToLowerInvariant();
        var content = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);

        if (fileExtension == ".json")
        {
            return ParseJsonBatch(content);
        }

        throw new InvalidOperationException($"Batch files must be in JSON format");
    }

    /// <summary>
    /// Loads skill configurations from a directory.
    /// </summary>
    private async Task<SkillConfigurationBatch> LoadFromDirectoryAsync(
        string directoryPath,
        bool recursive,
        CancellationToken ct)
    {
        var batch = new SkillConfigurationBatch
        {
            BatchName = Path.GetFileName(directoryPath),
            Description = $"Skills loaded from {directoryPath}"
        };

        var searchPattern = new[] { "*.json", "*.yaml", "*.yml" };
        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        foreach (var pattern in searchPattern)
        {
            var files = Directory.GetFiles(directoryPath, pattern, searchOption);

            foreach (var file in files)
            {
                try
                {
                    var config = await LoadSkillConfigAsync(file, ct).ConfigureAwait(false);
                    batch.Skills.Add(config);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Failed to load skill configuration from {FilePath}",
                        file);
                }
            }
        }

        _logger.LogInformation("Loaded {Count} skill(s) from {DirectoryPath}", batch.Skills.Count, directoryPath);
        return batch;
    }

    /// <summary>
    /// Parses a batch JSON file.
    /// </summary>
    private static SkillConfigurationBatch ParseJsonBatch(string content)
    {
        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            var batch = new SkillConfigurationBatch();

            // Parse batch metadata
            if (root.TryGetProperty("batchName", out var batchNameElement))
            {
                batch.BatchName = batchNameElement.GetString();
            }

            if (root.TryGetProperty("description", out var descElement))
            {
                batch.Description = descElement.GetString();
            }

            // Parse skills
            if (root.TryGetProperty("skills", out var skillsElement))
            {
                foreach (var skillElement in skillsElement.EnumerateArray())
                {
                    var config = new SkillConfigurationFile
                    {
                        Name = GetRequiredString(skillElement, "name"),
                        Description = GetRequiredString(skillElement, "description")
                    };

                    if (skillElement.TryGetProperty("category", out var categoryElement))
                    {
                        config.Category = categoryElement.GetString() ?? "Other";
                    }

                    if (skillElement.TryGetProperty("source", out var sourceElement))
                    {
                        config.Source = sourceElement.GetString() ?? "UserDefined";
                    }

                    // Parse inputs and output similarly to single skill
                    // (reuse logic from ParseJsonConfiguration)
                    batch.Skills.Add(config);
                }
            }

            // Parse metadata
            if (root.TryGetProperty("metadata", out var metadataElement))
            {
                foreach (var prop in metadataElement.EnumerateObject())
                {
                    batch.Metadata[prop.Name] = prop.Value.GetString() ?? string.Empty;
                }
            }

            return batch;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to parse batch JSON configuration: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gets a required string property from a JSON element.
    /// </summary>
    private static string GetRequiredString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            throw new InvalidOperationException($"Required property '{propertyName}' not found");
        }

        var value = property.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Property '{propertyName}' cannot be empty");
        }

        return value;
    }

    /// <summary>
    /// Validates if a category string is valid.
    /// </summary>
    private static bool IsValidCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return false;

        return category switch
        {
            "ReasoningAndAnalysis" or "Code" or "Document" or "DataAndVisualization" or
            "WebAndResearch" or "ProjectManagement" or "Communication" or "Other" or "Unspecified" => true,
            _ => false
        };
    }

    /// <summary>
    /// Validates if a source string is valid.
    /// </summary>
    private static bool IsValidSource(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return false;

        return source switch
        {
            "BuiltIn" or "UserDefined" or "Imported" => true,
            _ => false
        };
    }

    /// <summary>
    /// Parses a skill category string to enum.
    /// </summary>
    private static SkillCategory ParseSkillCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return SkillCategory.Other;

        return category switch
        {
            "ReasoningAndAnalysis" => SkillCategory.ReasoningAndAnalysis,
            "Code" => SkillCategory.Code,
            "Document" => SkillCategory.Document,
            "DataAndVisualization" => SkillCategory.DataAndVisualization,
            "WebAndResearch" => SkillCategory.WebAndResearch,
            "ProjectManagement" => SkillCategory.ProjectManagement,
            "Communication" => SkillCategory.Communication,
            "Unspecified" => SkillCategory.Unspecified,
            _ => SkillCategory.Other
        };
    }

    /// <summary>
    /// Parses a skill source string to enum.
    /// </summary>
    private static SkillSource ParseSkillSource(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return SkillSource.UserDefined;

        return source switch
        {
            "BuiltIn" => SkillSource.BuiltIn,
            "Imported" => SkillSource.Imported,
            _ => SkillSource.UserDefined
        };
    }
}
