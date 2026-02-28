using System.Text.Json;

namespace Daiv3.Persistence;

public static class ProjectRootPaths
{
    public static string Serialize(IEnumerable<string> rootPaths)
    {
        var normalizedPaths = Normalize(rootPaths);
        if (normalizedPaths.Count == 0)
        {
            throw new ArgumentException("At least one project root path is required.", nameof(rootPaths));
        }

        return JsonSerializer.Serialize(normalizedPaths);
    }

    public static IReadOnlyList<string> Parse(string? serializedRootPaths)
    {
        if (string.IsNullOrWhiteSpace(serializedRootPaths))
        {
            return Array.Empty<string>();
        }

        var trimmed = serializedRootPaths.Trim();
        if (trimmed.StartsWith("[", StringComparison.Ordinal))
        {
            try
            {
                var jsonPaths = JsonSerializer.Deserialize<List<string>>(trimmed);
                return jsonPaths is null ? Array.Empty<string>() : Normalize(jsonPaths);
            }
            catch (JsonException)
            {
            }
        }

        var legacySplit = trimmed.Split([';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (legacySplit.Length > 1)
        {
            return Normalize(legacySplit);
        }

        return Normalize([trimmed]);
    }

    public static IReadOnlyList<string> Normalize(IEnumerable<string> rootPaths)
    {
        ArgumentNullException.ThrowIfNull(rootPaths);

        return rootPaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.GetFullPath(path.Trim()))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}