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
            ".md" => ParseMarkdownConfiguration(content, filePath),
            _ => throw new InvalidOperationException(
                $"Unsupported configuration file format: {fileExtension}. Supported formats: .json, .yaml, .yml, .md")
        };

        config.SourcePath = filePath;

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

        // If it's a file, load it as a batch file (.json with "skills") or a single skill file.
        if (File.Exists(sourcePathOrFile))
        {
            _logger.LogInformation("Loading skill batch from file: {FilePath}", sourcePathOrFile);

            var extension = Path.GetExtension(sourcePathOrFile).ToLowerInvariant();
            if (extension == ".json")
            {
                var content = await File.ReadAllTextAsync(sourcePathOrFile, ct).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(content);

                if (doc.RootElement.TryGetProperty("skills", out _))
                {
                    return await LoadBatchFileAsync(sourcePathOrFile, ct).ConfigureAwait(false);
                }
            }

            var single = await LoadSkillConfigAsync(sourcePathOrFile, ct).ConfigureAwait(false);
            return new SkillConfigurationBatch
            {
                BatchName = Path.GetFileName(sourcePathOrFile),
                Description = $"Single skill loaded from {sourcePathOrFile}",
                Skills = ComposeProgressiveSkillHierarchy(new List<SkillConfigurationFile> { single })
            };
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

        // Validate hierarchy metadata
        if (!TryParseHierarchyLevel(config.ScopeLevel, out _))
        {
            result.AddWarning(
                $"Unknown scope level '{config.ScopeLevel}'. Using 'Global'. Valid values are: Global, Project, SubProject, Task");
        }

        if (!string.IsNullOrWhiteSpace(config.OverrideMode) &&
            !config.OverrideMode.Equals("Merge", StringComparison.OrdinalIgnoreCase) &&
            !config.OverrideMode.Equals("Replace", StringComparison.OrdinalIgnoreCase))
        {
            result.AddWarning(
                $"Unknown override mode '{config.OverrideMode}'. Using 'Merge'. Valid values are: Merge, Replace");
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

        // Preserve rich markdown metadata in config for runtime/catalog consumers.
        metadata.Permissions = MergeDistinct(metadata.Permissions, config.Capabilities.Select(c => $"Capability:{c}"));
        metadata.Permissions = MergeDistinct(metadata.Permissions, config.Restrictions.Select(r => $"Restriction:{r}"));

        if (!string.IsNullOrWhiteSpace(config.ScopeLevel))
        {
            config.Config["scope_level"] = config.ScopeLevel;
        }

        if (!string.IsNullOrWhiteSpace(config.Domain))
        {
            config.Config["domain"] = config.Domain;
        }

        if (!string.IsNullOrWhiteSpace(config.Language))
        {
            config.Config["language"] = config.Language;
        }

        if (!string.IsNullOrWhiteSpace(config.ProjectId))
        {
            config.Config["project_id"] = config.ProjectId;
        }

        if (!string.IsNullOrWhiteSpace(config.SubProjectId))
        {
            config.Config["sub_project_id"] = config.SubProjectId;
        }

        if (!string.IsNullOrWhiteSpace(config.TaskId))
        {
            config.Config["task_id"] = config.TaskId;
        }

        if (!string.IsNullOrWhiteSpace(config.ExtendsSkill))
        {
            config.Config["extends_skill"] = config.ExtendsSkill;
        }

        if (!string.IsNullOrWhiteSpace(config.OverrideMode))
        {
            config.Config["override_mode"] = config.OverrideMode;
        }

        if (!string.IsNullOrWhiteSpace(config.Instructions))
        {
            config.Config["instructions"] = config.Instructions;
        }

        if (config.Capabilities.Count > 0)
        {
            config.Config["capabilities"] = string.Join(",", config.Capabilities);
        }

        if (config.Restrictions.Count > 0)
        {
            config.Config["restrictions"] = string.Join(",", config.Restrictions);
        }

        if (config.Keywords.Count > 0)
        {
            config.Config["keywords"] = string.Join(",", config.Keywords);
        }

        if (!string.IsNullOrWhiteSpace(config.SourcePath))
        {
            config.Config["source_path"] = config.SourcePath;
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
                using var inputsEnumerator = inputsElement.EnumerateArray();
                while (inputsEnumerator.MoveNext())
                {
                    var paramElement = inputsEnumerator.Current;
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
                using var permissionsEnumerator = permissionsElement.EnumerateArray();
                while (permissionsEnumerator.MoveNext())
                {
                    var permElement = permissionsEnumerator.Current;
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
                using var configEnumerator = configElement.EnumerateObject();
                while (configEnumerator.MoveNext())
                {
                    var prop = configEnumerator.Current;
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

        var searchPattern = new[] { "*.json", "*.yaml", "*.yml", "*.md" };
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

        batch.Skills = ComposeProgressiveSkillHierarchy(batch.Skills);

        _logger.LogInformation("Loaded {Count} effective skill(s) from {DirectoryPath}", batch.Skills.Count, directoryPath);
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
                using var skillsEnumerator = skillsElement.EnumerateArray();
                while (skillsEnumerator.MoveNext())
                {
                    var skillElement = skillsEnumerator.Current;
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
                using var metadataEnumerator = metadataElement.EnumerateObject();
                while (metadataEnumerator.MoveNext())
                {
                    var prop = metadataEnumerator.Current;
                    batch.Metadata[prop.Name] = prop.Value.GetString() ?? string.Empty;
                }
            }

            batch.Skills = ComposeProgressiveSkillHierarchy(batch.Skills);
            return batch;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to parse batch JSON configuration: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Builds a searchable multi-view catalog from loaded skills.
    /// </summary>
    public SkillCatalog BuildSkillCatalog(IEnumerable<SkillConfigurationFile> skills)
    {
        ArgumentNullException.ThrowIfNull(skills);

        var catalog = new SkillCatalog();
        foreach (var skill in skills)
        {
            catalog.Entries.Add(new SkillCatalogEntry
            {
                Name = skill.Name,
                Description = skill.Description,
                ScopeLevel = NormalizeScopeLevel(skill.ScopeLevel),
                Domain = skill.Domain,
                Language = skill.Language,
                ProjectId = skill.ProjectId,
                SubProjectId = skill.SubProjectId,
                TaskId = skill.TaskId,
                ExtendsSkill = skill.ExtendsSkill,
                SourcePath = skill.SourcePath,
                Capabilities = new List<string>(skill.Capabilities),
                Restrictions = new List<string>(skill.Restrictions),
                Keywords = new List<string>(skill.Keywords)
            });
        }

        return catalog;
    }

    /// <summary>
    /// Loads a skill catalog directly from file or directory source.
    /// </summary>
    public async Task<SkillCatalog> LoadSkillCatalogAsync(
        string sourcePathOrFile,
        bool recursive = false,
        CancellationToken ct = default)
    {
        var batch = await LoadSkillBatchAsync(sourcePathOrFile, recursive, ct).ConfigureAwait(false);
        return BuildSkillCatalog(batch.Skills);
    }

    private static SkillConfigurationFile ParseMarkdownConfiguration(string content, string filePath)
    {
        var lines = content.Replace("\r\n", "\n").Split('\n');
        var nonEmpty = lines.Where(static l => !string.IsNullOrWhiteSpace(l)).ToList();

        if (nonEmpty.Count < 2)
        {
            throw new InvalidOperationException("Markdown skill format requires first line = name and second line = description.");
        }

        var config = new SkillConfigurationFile
        {
            Name = StripLeadingMarkdownPrefix(nonEmpty[0]),
            Description = StripLeadingMarkdownPrefix(nonEmpty[1]),
            SourcePath = filePath,
            Output = new SkillOutputSchemaConfiguration { Type = "object", Description = "Structured skill output" }
        };

        var currentSection = string.Empty;
        var instructionLines = new List<string>();

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                if (currentSection.Equals("instructions", StringComparison.OrdinalIgnoreCase))
                {
                    instructionLines.Add(string.Empty);
                }

                continue;
            }

            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                currentSection = line[3..].Trim();
                continue;
            }

            if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                continue;
            }

            if (currentSection.Equals("instructions", StringComparison.OrdinalIgnoreCase))
            {
                instructionLines.Add(rawLine.TrimEnd());
                continue;
            }

            if (currentSection.Equals("capabilities", StringComparison.OrdinalIgnoreCase))
            {
                AddIfBulletOrDelimited(config.Capabilities, line);
                continue;
            }

            if (currentSection.Equals("restrictions", StringComparison.OrdinalIgnoreCase))
            {
                AddIfBulletOrDelimited(config.Restrictions, line);
                continue;
            }

            if (currentSection.Equals("keywords", StringComparison.OrdinalIgnoreCase) ||
                currentSection.Equals("tags", StringComparison.OrdinalIgnoreCase))
            {
                AddIfBulletOrDelimited(config.Keywords, line);
                continue;
            }

            if (currentSection.Equals("inputs", StringComparison.OrdinalIgnoreCase))
            {
                TryParseInputLine(config, line);
                continue;
            }

            if (currentSection.Equals("output", StringComparison.OrdinalIgnoreCase))
            {
                ParseOutputLine(config, line);
                continue;
            }

            if (line.Contains(':'))
            {
                ParseMetadataLine(config, line);
            }
        }

        var normalizedInstructions = string.Join("\n", instructionLines).Trim();
        if (!string.IsNullOrWhiteSpace(normalizedInstructions))
        {
            config.Instructions = normalizedInstructions;
        }

        if (config.Output == null)
        {
            config.Output = new SkillOutputSchemaConfiguration { Type = "object", Description = "Structured skill output" };
        }

        return config;
    }

    private static List<SkillConfigurationFile> ComposeProgressiveSkillHierarchy(List<SkillConfigurationFile> configs)
    {
        if (configs.Count <= 1)
        {
            return configs;
        }

        var byName = configs
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var effective = new List<SkillConfigurationFile>();

        foreach (var (_, group) in byName)
        {
            var ordered = group
                .OrderBy(c => GetScopeRank(c.ScopeLevel))
                .ThenBy(c => c.SourcePath, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var current = CloneSkill(ordered[0]);

            for (var i = 1; i < ordered.Count; i++)
            {
                var next = ordered[i];
                if (next.OverrideMode.Equals("Replace", StringComparison.OrdinalIgnoreCase))
                {
                    current = CloneSkill(next);
                    continue;
                }

                current = MergeSkill(current, next);
            }

            effective.Add(current);
        }

        return effective
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static SkillConfigurationFile MergeSkill(SkillConfigurationFile parent, SkillConfigurationFile child)
    {
        var merged = CloneSkill(parent);

        merged.Description = string.IsNullOrWhiteSpace(child.Description) ? merged.Description : child.Description;
        merged.Category = string.IsNullOrWhiteSpace(child.Category) ? merged.Category : child.Category;
        merged.Source = string.IsNullOrWhiteSpace(child.Source) ? merged.Source : child.Source;
        merged.Domain = string.IsNullOrWhiteSpace(child.Domain) ? merged.Domain : child.Domain;
        merged.Language = string.IsNullOrWhiteSpace(child.Language) ? merged.Language : child.Language;
        merged.ScopeLevel = NormalizeScopeLevel(string.IsNullOrWhiteSpace(child.ScopeLevel) ? merged.ScopeLevel : child.ScopeLevel);
        merged.ProjectId = string.IsNullOrWhiteSpace(child.ProjectId) ? merged.ProjectId : child.ProjectId;
        merged.SubProjectId = string.IsNullOrWhiteSpace(child.SubProjectId) ? merged.SubProjectId : child.SubProjectId;
        merged.TaskId = string.IsNullOrWhiteSpace(child.TaskId) ? merged.TaskId : child.TaskId;
        merged.ExtendsSkill = string.IsNullOrWhiteSpace(child.ExtendsSkill) ? merged.ExtendsSkill : child.ExtendsSkill;
        merged.OverrideMode = string.IsNullOrWhiteSpace(child.OverrideMode) ? merged.OverrideMode : child.OverrideMode;
        merged.Instructions = string.IsNullOrWhiteSpace(child.Instructions) ? merged.Instructions : child.Instructions;
        merged.SourcePath = string.IsNullOrWhiteSpace(child.SourcePath) ? merged.SourcePath : child.SourcePath;

        merged.Permissions = MergeDistinct(merged.Permissions, child.Permissions);
        merged.Capabilities = MergeDistinct(merged.Capabilities, child.Capabilities);
        merged.Restrictions = MergeDistinct(merged.Restrictions, child.Restrictions);
        merged.Keywords = MergeDistinct(merged.Keywords, child.Keywords);

        var inputByName = merged.Inputs.ToDictionary(i => i.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var input in child.Inputs)
        {
            inputByName[input.Name] = new SkillParameterConfiguration
            {
                Name = input.Name,
                Type = input.Type,
                Required = input.Required,
                Description = input.Description
            };
        }

        merged.Inputs = inputByName.Values.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToList();
        merged.Output = child.Output ?? merged.Output;

        foreach (var kvp in child.Config)
        {
            merged.Config[kvp.Key] = kvp.Value;
        }

        return merged;
    }

    private static SkillConfigurationFile CloneSkill(SkillConfigurationFile source)
    {
        return new SkillConfigurationFile
        {
            Name = source.Name,
            Description = source.Description,
            Category = source.Category,
            Source = source.Source,
            Inputs = source.Inputs
                .Select(i => new SkillParameterConfiguration
                {
                    Name = i.Name,
                    Type = i.Type,
                    Required = i.Required,
                    Description = i.Description
                })
                .ToList(),
            Output = source.Output == null
                ? null
                : new SkillOutputSchemaConfiguration
                {
                    Type = source.Output.Type,
                    Description = source.Output.Description,
                    Schema = source.Output.Schema
                },
            Permissions = new List<string>(source.Permissions),
            Config = new Dictionary<string, string>(source.Config, StringComparer.OrdinalIgnoreCase),
            Domain = source.Domain,
            Language = source.Language,
            ScopeLevel = NormalizeScopeLevel(source.ScopeLevel),
            ProjectId = source.ProjectId,
            SubProjectId = source.SubProjectId,
            TaskId = source.TaskId,
            ExtendsSkill = source.ExtendsSkill,
            OverrideMode = source.OverrideMode,
            Capabilities = new List<string>(source.Capabilities),
            Restrictions = new List<string>(source.Restrictions),
            Keywords = new List<string>(source.Keywords),
            Instructions = source.Instructions,
            SourcePath = source.SourcePath
        };
    }

    private static void ParseMetadataLine(SkillConfigurationFile config, string line)
    {
        var separatorIndex = line.IndexOf(':');
        if (separatorIndex <= 0)
        {
            return;
        }

        var key = line[..separatorIndex].Trim();
        var value = line[(separatorIndex + 1)..].Trim();

        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        switch (key.ToLowerInvariant())
        {
            case "category":
                config.Category = value;
                break;
            case "source":
                config.Source = value;
                break;
            case "domain":
                config.Domain = value;
                break;
            case "language":
                config.Language = value;
                break;
            case "scope":
            case "scopelevel":
                config.ScopeLevel = NormalizeScopeLevel(value);
                break;
            case "project":
            case "projectid":
                config.ProjectId = value;
                break;
            case "subproject":
            case "subprojectid":
                config.SubProjectId = value;
                break;
            case "task":
            case "taskid":
                config.TaskId = value;
                break;
            case "extends":
            case "extendsskill":
                config.ExtendsSkill = value;
                break;
            case "override":
            case "overridemode":
                config.OverrideMode = value;
                break;
            case "permissions":
                config.Permissions = MergeDistinct(config.Permissions, SplitList(value));
                break;
            case "capabilities":
                config.Capabilities = MergeDistinct(config.Capabilities, SplitList(value));
                break;
            case "restrictions":
                config.Restrictions = MergeDistinct(config.Restrictions, SplitList(value));
                break;
            case "keywords":
            case "tags":
                config.Keywords = MergeDistinct(config.Keywords, SplitList(value));
                break;
            case "output_type":
                config.Output ??= new SkillOutputSchemaConfiguration { Type = value };
                config.Output.Type = value;
                break;
            case "output_description":
                config.Output ??= new SkillOutputSchemaConfiguration { Type = "object" };
                config.Output.Description = value;
                break;
            default:
                config.Config[key] = value;
                break;
        }
    }

    private static void TryParseInputLine(SkillConfigurationFile config, string line)
    {
        var normalized = line.StartsWith("-", StringComparison.Ordinal) ? line[1..].Trim() : line;
        var parts = normalized.Split('|', StringSplitOptions.TrimEntries);

        if (parts.Length < 2)
        {
            return;
        }

        var required = true;
        if (parts.Length >= 3 && bool.TryParse(parts[2], out var parsedRequired))
        {
            required = parsedRequired;
        }

        config.Inputs.Add(new SkillParameterConfiguration
        {
            Name = parts[0],
            Type = parts[1],
            Required = required,
            Description = parts.Length >= 4 ? parts[3] : null
        });
    }

    private static void ParseOutputLine(SkillConfigurationFile config, string line)
    {
        config.Output ??= new SkillOutputSchemaConfiguration { Type = "object" };

        var normalized = line.StartsWith("-", StringComparison.Ordinal) ? line[1..].Trim() : line;
        if (!normalized.Contains(':'))
        {
            return;
        }

        var separatorIndex = normalized.IndexOf(':');
        var key = normalized[..separatorIndex].Trim();
        var value = normalized[(separatorIndex + 1)..].Trim();

        if (key.Equals("type", StringComparison.OrdinalIgnoreCase))
        {
            config.Output.Type = value;
        }
        else if (key.Equals("description", StringComparison.OrdinalIgnoreCase))
        {
            config.Output.Description = value;
        }
        else if (key.Equals("schema", StringComparison.OrdinalIgnoreCase))
        {
            config.Output.Schema = value;
        }
    }

    private static void AddIfBulletOrDelimited(List<string> target, string line)
    {
        if (line.StartsWith("-", StringComparison.Ordinal))
        {
            var value = line[1..].Trim();
            if (!string.IsNullOrWhiteSpace(value))
            {
                target.Add(value);
            }

            return;
        }

        target.AddRange(SplitList(line));
    }

    private static List<string> SplitList(string value)
    {
        return value
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> MergeDistinct(IEnumerable<string> left, IEnumerable<string> right)
    {
        return left
            .Concat(right)
            .Where(static v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string StripLeadingMarkdownPrefix(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith("#", StringComparison.Ordinal))
        {
            trimmed = trimmed.TrimStart('#').Trim();
        }

        if (trimmed.StartsWith("-", StringComparison.Ordinal))
        {
            trimmed = trimmed[1..].Trim();
        }

        return trimmed;
    }

    private static string NormalizeScopeLevel(string? scopeLevel)
    {
        if (TryParseHierarchyLevel(scopeLevel, out var level))
        {
            return level.ToString();
        }

        return SkillHierarchyLevel.Global.ToString();
    }

    private static int GetScopeRank(string? scopeLevel)
    {
        return TryParseHierarchyLevel(scopeLevel, out var level) ? (int)level : 0;
    }

    private static bool TryParseHierarchyLevel(string? scopeLevel, out SkillHierarchyLevel level)
    {
        if (string.IsNullOrWhiteSpace(scopeLevel))
        {
            level = SkillHierarchyLevel.Global;
            return true;
        }

        if (Enum.TryParse<SkillHierarchyLevel>(scopeLevel, ignoreCase: true, out level))
        {
            return true;
        }

        level = SkillHierarchyLevel.Global;
        return false;
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
