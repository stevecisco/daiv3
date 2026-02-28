using System.Text.Json;

namespace Daiv3.Persistence;

/// <summary>
/// Project-level configuration stored in projects.config_json.
/// </summary>
public sealed class ProjectConfiguration
{
    public string? Instructions { get; set; }
    public ProjectModelPreferences ModelPreferences { get; set; } = new();

    public static ProjectConfiguration Parse(string? configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson))
        {
            return new ProjectConfiguration();
        }

        try
        {
            var config = JsonSerializer.Deserialize<ProjectConfiguration>(configJson, JsonSerializerOptions);
            return config ?? new ProjectConfiguration();
        }
        catch (JsonException)
        {
            return new ProjectConfiguration();
        }
    }

    public string? ToJsonOrNull()
    {
        var normalizedInstructions = NormalizeValue(Instructions);
        var normalizedModelPreferences = ModelPreferences.Normalize();

        if (normalizedInstructions is null && normalizedModelPreferences is null)
        {
            return null;
        }

        var payload = new ProjectConfiguration
        {
            Instructions = normalizedInstructions,
            ModelPreferences = normalizedModelPreferences ?? new ProjectModelPreferences()
        };

        return JsonSerializer.Serialize(payload, JsonSerializerOptions);
    }

    private static string? NormalizeValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}

public sealed class ProjectModelPreferences
{
    public string? PreferredModelId { get; set; }
    public string? FallbackModelId { get; set; }

    public ProjectModelPreferences? Normalize()
    {
        var preferredModelId = NormalizeValue(PreferredModelId);
        var fallbackModelId = NormalizeValue(FallbackModelId);

        if (preferredModelId is null && fallbackModelId is null)
        {
            return null;
        }

        return new ProjectModelPreferences
        {
            PreferredModelId = preferredModelId,
            FallbackModelId = fallbackModelId
        };
    }

    private static string? NormalizeValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
