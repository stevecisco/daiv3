using Daiv3.Orchestration.Services;
using Daiv3.Persistence.Entities;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

#pragma warning disable IDISP025 // Test class dispose pattern is sufficient for unit-test fixture cleanup.

namespace Daiv3.Orchestration.Tests;

/// <summary>
/// Unit tests for SkillHashService.
/// Validates hash computation, tamper detection, and hash update behavior.
/// </summary>
public class SkillHashServiceTests : IDisposable
{
    private readonly Mock<ILogger<SkillHashService>> _loggerMock;
    private readonly SkillHashService _service;
    private readonly List<string> _tempFiles = new();

    public SkillHashServiceTests()
    {
        _loggerMock = new Mock<ILogger<SkillHashService>>();
        _service = new SkillHashService(_loggerMock.Object);
    }

    public void Dispose()
    {
        foreach (var tempFile in _tempFiles.Where(File.Exists))
        {
            try
            {
                File.Delete(tempFile);
            }
            catch
            {
                // Best-effort cleanup in tests.
            }
        }
    }

    [Fact]
    public async Task ComputeHashAsync_SameContent_ProducesSameHash()
    {
        var path = CreateTempFile("Console.WriteLine(\"hello\");");

        var hash1 = await _service.ComputeHashAsync(path);
        var hash2 = await _service.ComputeHashAsync(path);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public async Task ComputeHashAsync_ModifiedContent_ProducesDifferentHash()
    {
        var path = CreateTempFile("var x = 1;");

        var hash1 = await _service.ComputeHashAsync(path);
        await File.WriteAllTextAsync(path, "var x = 2;");
        var hash2 = await _service.ComputeHashAsync(path);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public async Task ComputeHashAsync_EmptyFile_ProducesKnownSha256()
    {
        var path = CreateTempFile(string.Empty);

        var hash = await _service.ComputeHashAsync(path);

        Assert.Equal("E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855", hash);
    }

    [Fact]
    public async Task ComputeHashAsync_MissingFile_ThrowsFileNotFoundException()
    {
        var path = Path.Combine(Path.GetTempPath(), $"missing_{Guid.NewGuid():N}.cs");

        await Assert.ThrowsAsync<FileNotFoundException>(() => _service.ComputeHashAsync(path));
    }

    [Fact]
    public async Task ComputeHashAsync_NullOrWhitespacePath_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.ComputeHashAsync(string.Empty));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.ComputeHashAsync("   "));
    }

    [Fact]
    public async Task ValidateHashAsync_MatchingHash_ReturnsTrue()
    {
        var path = CreateTempFile("return 42;");
        var hash = await _service.ComputeHashAsync(path);
        var skill = CreateSkill(path, hash);

        var result = await _service.ValidateHashAsync(skill);

        Assert.True(result);
    }

    [Fact]
    public async Task ValidateHashAsync_MismatchedHash_ReturnsFalse()
    {
        var path = CreateTempFile("return 42;");
        var skill = CreateSkill(path, "BADHASH");

        var result = await _service.ValidateHashAsync(skill);

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateHashAsync_MissingPathOrHash_ReturnsFalse()
    {
        var path = CreateTempFile("x");
        var noPath = CreateSkill(string.Empty, "ABC");
        var noHash = CreateSkill(path, string.Empty);

        var noPathResult = await _service.ValidateHashAsync(noPath);
        var noHashResult = await _service.ValidateHashAsync(noHash);

        Assert.False(noPathResult);
        Assert.False(noHashResult);
    }

    [Fact]
    public async Task ValidateHashAsync_MissingFile_ReturnsFalse()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"missing_{Guid.NewGuid():N}.cs");
        var skill = CreateSkill(missingPath, "ABC");

        var result = await _service.ValidateHashAsync(skill);

        Assert.False(result);
    }

    [Fact]
    public async Task UpdateHashAsync_RecomputesHashAndUpdatesTimestamp()
    {
        var path = CreateTempFile("Console.WriteLine(\"v1\");");
        var oldModified = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 10;
        var skill = CreateSkill(path, "OLD_HASH");
        skill.LastModifiedAt = oldModified;

        await _service.UpdateHashAsync(skill);

        Assert.NotEqual("OLD_HASH", skill.FileHash);
        Assert.True(skill.LastModifiedAt >= oldModified);
    }

    [Fact]
    public async Task UpdateHashAsync_NullSkill_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.UpdateHashAsync(null!));
    }

    private string CreateTempFile(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"skillhash_{Guid.NewGuid():N}.cs");
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }

    private static ExecutableSkill CreateSkill(string filePath, string hash)
    {
        return new ExecutableSkill
        {
            SkillId = Guid.NewGuid().ToString(),
            Name = "TestSkill",
            FilePath = filePath,
            FileHash = hash,
            MetadataPath = "test.md",
            ApprovalStatus = "PendingApproval",
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            LastModifiedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
    }
}
