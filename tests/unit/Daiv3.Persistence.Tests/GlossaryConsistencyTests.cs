using Xunit;

namespace Daiv3.Persistence.Tests;

/// <summary>
/// Validates canonical glossary coverage and term standardization rules.
/// Implements GLO-REQ-001.
/// </summary>
public class GlossaryConsistencyTests
{
    private static readonly string[] RequiredCanonicalTerms =
    {
        "Chunk",
        "Embedding",
        "Foundry Local",
        "MCP",
        "NPU",
        "ONNX Runtime",
        "Learning Memory",
        "RAG",
        "SLM",
        "Tier 1 / Tier 2",
        "TensorPrimitives",
    };

    [Fact]
    public void CanonicalGlossary_DefinesAllRequiredTerms()
    {
        var repoRoot = FindRepoRoot();
        var glossaryPath = Path.Combine(repoRoot, "Docs", "Requirements", "Glossary.md");

        Assert.True(File.Exists(glossaryPath), $"Expected glossary file was not found: {glossaryPath}");

        var glossaryText = File.ReadAllText(glossaryPath);

        foreach (var term in RequiredCanonicalTerms)
        {
            Assert.Contains($"| {term} |", glossaryText, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void CanonicalGlossary_UsesPreferredSpellings()
    {
        var repoRoot = FindRepoRoot();
        var glossaryPath = Path.Combine(repoRoot, "Docs", "Requirements", "Glossary.md");
        var glossaryText = File.ReadAllText(glossaryPath);

        Assert.DoesNotContain("| FoundryLocal |", glossaryText, StringComparison.Ordinal);
        Assert.DoesNotContain("| OnnxRuntime |", glossaryText, StringComparison.Ordinal);
    }

    [Fact]
    public void DesignDocument_GlossarySectionContainsCanonicalTerms()
    {
        var repoRoot = FindRepoRoot();
        var designDocPath = Path.Combine(repoRoot, "Docs", "Requirements", "Daiv3_Design_Document.md");

        Assert.True(File.Exists(designDocPath), $"Expected design document was not found: {designDocPath}");

        var designDocText = File.ReadAllText(designDocPath);

        foreach (var term in RequiredCanonicalTerms)
        {
            Assert.Contains(term, designDocText, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Validates that the canonical glossary includes version metadata and change history.
    /// Implements GLO-REQ-003.
    /// </summary>
    [Fact]
    public void CanonicalGlossary_IncludesVersionMetadata()
    {
        var repoRoot = FindRepoRoot();
        var glossaryPath = Path.Combine(repoRoot, "Docs", "Requirements", "Glossary.md");
        var glossaryText = File.ReadAllText(glossaryPath);

        // Validate version metadata section exists
        Assert.Contains("**Version:**", glossaryText);
        Assert.Contains("**Last Updated:**", glossaryText);
        Assert.Contains("**Status:**", glossaryText);

        // Validate version history table exists
        Assert.Contains("## Version History", glossaryText);
        Assert.Contains("| Version | Date | Changes | Author/Context |", glossaryText);

        // Validate version format (e.g., "1.0", "1.1", "2.0")
        var versionMatch = System.Text.RegularExpressions.Regex.Match(
            glossaryText,
            @"\*\*Version:\*\*\s+(\d+\.\d+)",
            System.Text.RegularExpressions.RegexOptions.Multiline
        );
        Assert.True(versionMatch.Success, "Version metadata must include a semantic version number (e.g., 1.0)");

        // Validate date format (e.g., "2026-03-08")
        var dateMatch = System.Text.RegularExpressions.Regex.Match(
            glossaryText,
            @"\*\*Last Updated:\*\*\s+(\d{4}-\d{2}-\d{2})",
            System.Text.RegularExpressions.RegexOptions.Multiline
        );
        Assert.True(dateMatch.Success, "Last Updated metadata must include a date in YYYY-MM-DD format");

        // Validate at least one entry in version history table (beyond the header)
        var historyLines = glossaryText.Split('\n')
            .SkipWhile(line => !line.Contains("## Version History"))
            .Skip(1) // Skip the section header
            .TakeWhile(line => !line.StartsWith("##")) // Take until next section
            .Where(line => line.TrimStart().StartsWith("|") && !line.Contains("Version | Date")) // Table rows, excluding header
            .ToList();

        Assert.NotEmpty(historyLines);
        Assert.True(historyLines.Count >= 1, "Version History table must contain at least one version entry");
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var solutionPath = Path.Combine(current.FullName, "Daiv3.FoundryLocal.slnx");
            if (File.Exists(solutionPath))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root from test execution directory.");
    }
}
