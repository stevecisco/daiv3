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
