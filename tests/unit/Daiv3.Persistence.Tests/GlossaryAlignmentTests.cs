using System.Text.RegularExpressions;
using Xunit;

namespace Daiv3.Persistence.Tests;

/// <summary>
/// Validates UI labels and documentation align with canonical glossary terms (GLO-REQ-002).
/// </summary>
public class GlossaryAlignmentTests
{
    private static readonly (string NonCanonical, string Canonical, string Context)[] ProhibitedTermVariants = {
        ("FoundryLocal", "Foundry Local", "UI labels and prose documentation"),
        ("foundry-local", "Foundry Local", "UI labels and prose documentation"),
        ("FOUNDRY_LOCAL", "Foundry Local", "UI labels and prose documentation"),
        ("OnnxRuntime", "ONNX Runtime", "UI labels and prose documentation"),
        ("ONNX-Runtime", "ONNX Runtime", "UI labels and prose documentation"),
        ("onnx-runtime", "ONNX Runtime", "UI labels and prose documentation"),
        ("LearningMemory", "Learning Memory", "UI feature names (individual records use lowercase 'learning')"),
        ("learning-memory", "Learning Memory", "UI feature names"),
        ("tier1", "Tier 1", "UI labels and documentation"),
        ("tier2", "Tier 2", "UI labels and documentation"),
        ("Tier-1", "Tier 1", "UI labels and documentation"),
        ("Tier-2", "Tier 2", "UI labels and documentation"),
    };

    /// <summary>
    /// Validates that XAML UI labels use canonical glossary terms, not common incorrect variants.
    /// Excludes namespace declarations, class names, and binding paths (code identifiers are exempt).
    /// </summary>
    [Fact]
    public void XamlUiLabels_UseCanonicalGlossaryTerms()
    {
        var repoRoot = FindRepoRoot();
        var xamlFiles = Directory.GetFiles(
            Path.Combine(repoRoot, "src", "Daiv3.App.Maui"),
            "*.xaml",
            SearchOption.AllDirectories
        );

        var violations = new List<string>();

        foreach (var xamlFile in xamlFiles)
        {
            var content = File.ReadAllText(xamlFile);
            var fileName = Path.GetRelativePath(repoRoot, xamlFile);

            // Skip namespace declarations and clr-namespace patterns (code identifiers)
            var lines = content.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                
                // Skip lines that are namespace declarations or binding paths
                if (line.Contains("xmlns:") || 
                    line.Contains("clr-namespace:") || 
                    line.Contains("x:Class=") ||
                    line.Contains("x:DataType=") ||
                    line.Contains("Binding ") ||
                    line.Contains("{Binding"))
                {
                    continue;
                }

                // Check for non-canonical term variants in UI-visible content
                foreach (var (nonCanonical, canonical, context) in ProhibitedTermVariants)
                {
                    // Use word boundary matching to avoid false positives in compound identifiers
                    var pattern = $@"\b{Regex.Escape(nonCanonical)}\b";
                    if (Regex.IsMatch(line, pattern, RegexOptions.IgnoreCase))
                    {
                        violations.Add($"{fileName}:{i + 1} - Found '{nonCanonical}' (should be '{canonical}' in {context})");
                    }
                }
            }
        }

        Assert.Empty(violations);
    }

    /// <summary>
    /// Validates that user-facing documentation uses canonical glossary terms.
    /// Checks README files and high-level requirement documents (Specs/*.md).
    /// </summary>
    [Fact]
    public void Documentation_UsesCanonicalGlossaryTerms()
    {
        var repoRoot = FindRepoRoot();
        var docsToCheck = new List<string>();

        // Check all README files
        if (File.Exists(Path.Combine(repoRoot, "README.md")))
            docsToCheck.Add(Path.Combine(repoRoot, "README.md"));

        // Check specification documents (high-level design docs viewed by users)
        var specsDir = Path.Combine(repoRoot, "Docs", "Requirements", "Specs");
        if (Directory.Exists(specsDir))
        {
            docsToCheck.AddRange(Directory.GetFiles(specsDir, "*.md"));
        }

        var violations = new List<string>();
        var inCodeBlock = false;

        foreach (var docFile in docsToCheck)
        {
            var content = File.ReadAllText(docFile);
            var fileName = Path.GetRelativePath(repoRoot, docFile);
            var lines = content.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var trimmed = line.TrimStart();

                // Track code block boundaries
                if (trimmed.StartsWith("```"))
                {
                    inCodeBlock = !inCodeBlock;
                    continue;
                }

                // Skip code blocks, inline code, and lines with dotted identifiers (package/namespace names)
                if (inCodeBlock || 
                    line.Contains("`") ||
                    Regex.IsMatch(line, @"\b\w+\.\w+\.(?:OnnxRuntime|FoundryLocal)") || // e.g., Microsoft.ML.OnnxRuntime.DirectML
                    Regex.IsMatch(line, @"\bDaiv3\.FoundryLocal\.") || // e.g., Daiv3.FoundryLocal.Management
                    line.Contains("\"tier1\"") || line.Contains("\"tier2\"") || // JSON keys
                    line.Contains("'tier1'") || line.Contains("'tier2'")) // JSON keys with single quotes
                {
                    continue;
                }

                foreach (var (nonCanonical, canonical, context) in ProhibitedTermVariants)
                {
                    var pattern = $@"\b{Regex.Escape(nonCanonical)}\b";
                    if (Regex.IsMatch(line, pattern, RegexOptions.IgnoreCase))
                    {
                        violations.Add($"{fileName}:{i + 1} - Found '{nonCanonical}' (should be '{canonical}')");
                    }
                }
            }

            inCodeBlock = false; // Reset for next file
        }


        Assert.Empty(violations);
    }

    /// <summary>
    /// Validates that short-form "Foundry" in UI labels is properly qualified as "Foundry Local"
    /// when referring to the Microsoft Foundry Local runtime (not generic foundry concepts).
    /// </summary>
    [Fact]
    public void XamlLabels_QualifyFoundryAsFoundryLocal()
    {
        var repoRoot = FindRepoRoot();
        var xamlFiles = Directory.GetFiles(
            Path.Combine(repoRoot, "src", "Daiv3.App.Maui"),
            "*.xaml",
            SearchOption.AllDirectories
        );

        var violations = new List<string>();

        foreach (var xamlFile in xamlFiles)
        {
            var content = File.ReadAllText(xamlFile);
            var fileName = Path.GetRelativePath(repoRoot, xamlFile);
            var lines = content.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                // Skip namespace declarations and code references
                if (line.Contains("xmlns:") || 
                    line.Contains("clr-namespace:") ||
                    line.Contains("x:Class=") ||
                    line.Contains("Binding "))
                {
                    continue;
                }

                // Check for standalone "Foundry" in Text attributes (UI labels)
                // This catches patterns like: Text="Foundry Models Directory"
                if (line.Contains("Text=\"") && Regex.IsMatch(line, @"Text=""[^""]*\bFoundry\b[^""]*"""))
                {
                    // If line contains "Foundry" but not "Foundry Local", flag it
                    if (!line.Contains("Foundry Local"))
                    {
                        violations.Add($"{fileName}:{i + 1} - UI label contains 'Foundry' without 'Local' qualifier (use 'Foundry Local')");
                    }
                }
            }
        }

        Assert.Empty(violations);
    }

    private static string FindRepoRoot()
    {
        var current = Directory.GetCurrentDirectory();
        while (current != null && !File.Exists(Path.Combine(current, "Daiv3.FoundryLocal.slnx")))
        {
            current = Directory.GetParent(current)?.FullName;
        }
        return current ?? throw new InvalidOperationException("Could not find repository root with Daiv3.FoundryLocal.slnx");
    }
}
