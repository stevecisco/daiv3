using Daiv3.Orchestration.Interfaces;

namespace Daiv3.Orchestration.Configuration;

/// <summary>
/// Runtime skill implementation created from declarative configuration metadata.
/// </summary>
public sealed class ConfiguredSkill : ISkill
{
    private readonly IReadOnlyDictionary<string, string> _config;

    public string Name { get; }

    public string Description { get; }

    public SkillCategory Category { get; }

    public List<ParameterMetadata> Inputs { get; }

    public OutputSchema OutputSchema { get; }

    public List<string> Permissions { get; }

    public ConfiguredSkill(SkillMetadata metadata, IReadOnlyDictionary<string, string>? config = null)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        Name = metadata.Name;
        Description = metadata.Description;
        Category = metadata.Category;
        Inputs = new List<ParameterMetadata>(metadata.Inputs);
        OutputSchema = metadata.Outputs;
        Permissions = new List<string>(metadata.Permissions);
        _config = config ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public Task<object> ExecuteAsync(Dictionary<string, object> parameters, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ct.ThrowIfCancellationRequested();

        if (_config.TryGetValue("response_template", out var template) && !string.IsNullOrWhiteSpace(template))
        {
            return Task.FromResult<object>(ApplyTemplate(template, parameters));
        }

        if (_config.TryGetValue("static_output", out var staticOutput) && !string.IsNullOrWhiteSpace(staticOutput))
        {
            return Task.FromResult<object>(staticOutput);
        }

        // Default behavior keeps skills functional without custom runtime code.
        return Task.FromResult<object>(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["skill"] = Name,
            ["status"] = "ok",
            ["receivedParameters"] = new Dictionary<string, object>(parameters, StringComparer.OrdinalIgnoreCase)
        });
    }

    private static string ApplyTemplate(string template, IReadOnlyDictionary<string, object> parameters)
    {
        var result = template;

        foreach (var kvp in parameters)
        {
            result = result.Replace(
                $"{{{{{kvp.Key}}}}}",
                kvp.Value?.ToString() ?? string.Empty,
                StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }
}
