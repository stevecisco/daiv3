using Daiv3.Persistence;
using Xunit;

namespace Daiv3.Persistence.Tests;

public class ProjectRootPathsTests
{
    [Fact]
    public void Serialize_WithEmptyPaths_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => ProjectRootPaths.Serialize(Array.Empty<string>()));
    }

    [Fact]
    public void Serialize_AndParse_WithMultiplePaths_ReturnsNormalizedDistinctPaths()
    {
        var tempRootA = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var tempRootB = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        var serialized = ProjectRootPaths.Serialize([tempRootA, tempRootB, tempRootA]);
        var parsed = ProjectRootPaths.Parse(serialized);

        Assert.Equal(2, parsed.Count);
        Assert.Contains(Path.GetFullPath(tempRootA), parsed);
        Assert.Contains(Path.GetFullPath(tempRootB), parsed);
    }

    [Fact]
    public void Parse_WithLegacySinglePath_ReturnsSingleNormalizedPath()
    {
        var legacyPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        var parsed = ProjectRootPaths.Parse(legacyPath);

        Assert.Single(parsed);
        Assert.Equal(Path.GetFullPath(legacyPath), parsed[0]);
    }

    [Fact]
    public void Parse_WithLegacyDelimitedPaths_ReturnsDistinctNormalizedPaths()
    {
        var pathA = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var pathB = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var serialized = $"{pathA};{pathB};{pathA}";

        var parsed = ProjectRootPaths.Parse(serialized);

        Assert.Equal(2, parsed.Count);
        Assert.Contains(Path.GetFullPath(pathA), parsed);
        Assert.Contains(Path.GetFullPath(pathB), parsed);
    }
}