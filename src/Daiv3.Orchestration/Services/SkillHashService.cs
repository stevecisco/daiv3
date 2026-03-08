using System.Security.Cryptography;
using System.Text;
using Daiv3.Persistence.Entities;
using Microsoft.Extensions.Logging;

namespace Daiv3.Orchestration.Services;

/// <summary>
/// Default implementation of ISkillHashService using SHA256.
/// Implements ES-ACC-002 Phase 1: Cryptographic hash validation for executable skills.
/// </summary>
public class SkillHashService : ISkillHashService
{
    private readonly ILogger<SkillHashService> _logger;

    public SkillHashService(ILogger<SkillHashService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> ComputeHashAsync(string filePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath, nameof(filePath));

        if (!File.Exists(filePath))
        {
            _logger.LogError("Cannot compute hash: file not found at {FilePath}", filePath);
            throw new FileNotFoundException($"Skill file not found: {filePath}", filePath);
        }

        try
        {
            _logger.LogDebug("Computing SHA256 hash for file: {FilePath}", filePath);

            await using var fileStream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true);

            var hashBytes = await SHA256.HashDataAsync(fileStream, ct).ConfigureAwait(false);
            var hashHex = Convert.ToHexString(hashBytes);

            _logger.LogDebug("Computed hash for {FilePath}: {Hash}", filePath, hashHex);
            return hashHex;
        }
        catch (IOException ioEx)
        {
            _logger.LogError(ioEx, "Failed to read skill file for hashing: {FilePath}", filePath);
            throw;
        }
        catch (UnauthorizedAccessException authEx)
        {
            _logger.LogError(authEx, "Access denied reading skill file: {FilePath}", filePath);
            throw new IOException($"Access denied to skill file: {filePath}", authEx);
        }
    }

    public async Task<bool> ValidateHashAsync(ExecutableSkill skill, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(skill, nameof(skill));

        if (string.IsNullOrWhiteSpace(skill.FilePath))
        {
            _logger.LogWarning("Cannot validate hash: skill {SkillId} has no FilePath", skill.SkillId);
            return false;
        }

        if (string.IsNullOrWhiteSpace(skill.FileHash))
        {
            _logger.LogWarning("Cannot validate hash: skill {SkillId} has no stored FileHash", skill.SkillId);
            return false;
        }

        if (!File.Exists(skill.FilePath))
        {
            _logger.LogWarning("Cannot validate hash: file not found for skill {SkillId} at {FilePath}",
                skill.SkillId, skill.FilePath);
            return false;
        }

        try
        {
            var currentHash = await ComputeHashAsync(skill.FilePath, ct).ConfigureAwait(false);
            var isValid = string.Equals(currentHash, skill.FileHash, StringComparison.OrdinalIgnoreCase);

            if (isValid)
            {
                _logger.LogDebug("Hash validation passed for skill {SkillId} ({SkillName})",
                    skill.SkillId, skill.Name);
            }
            else
            {
                _logger.LogWarning(
                    "Hash validation FAILED for skill {SkillId} ({SkillName}): expected {ExpectedHash}, got {ActualHash}",
                    skill.SkillId, skill.Name, skill.FileHash, currentHash);
            }

            return isValid;
        }
        catch (Exception ex) when (ex is FileNotFoundException or IOException)
        {
            _logger.LogError(ex, "Error validating hash for skill {SkillId}: {Message}",
                skill.SkillId, ex.Message);
            return false;
        }
    }

    public async Task UpdateHashAsync(ExecutableSkill skill, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(skill, nameof(skill));
        ArgumentException.ThrowIfNullOrWhiteSpace(skill.FilePath, nameof(skill.FilePath));

        var newHash = await ComputeHashAsync(skill.FilePath, ct).ConfigureAwait(false);
        var oldHash = skill.FileHash;

        skill.FileHash = newHash;
        skill.LastModifiedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        _logger.LogInformation("Updated hash for skill {SkillId} ({SkillName}): {OldHash} -> {NewHash}",
            skill.SkillId, skill.Name, oldHash, newHash);
    }
}
