using Daiv3.Persistence.Entities;

namespace Daiv3.Orchestration.Services;

/// <summary>
/// Service for computing and validating cryptographic hashes of executable skill files.
/// Implements ES-ACC-002 Phase 1: Hash-based tamper detection for executable skills.
/// </summary>
public interface ISkillHashService
{
    /// <summary>
    /// Computes SHA256 hash of the skill file content.
    /// </summary>
    /// <param name="filePath">Absolute path to the skill file (.cs).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Hex-encoded SHA256 hash string (64 characters).</returns>
    /// <exception cref="FileNotFoundException">Thrown if the file does not exist.</exception>
    /// <exception cref="IOException">Thrown if the file cannot be read (e.g., locked, permissions).</exception>
    Task<string> ComputeHashAsync(string filePath, CancellationToken ct = default);

    /// <summary>
    /// Validates that the current file hash matches the stored hash in the ExecutableSkill entity.
    /// Returns true if hashes match, false otherwise.
    /// </summary>
    /// <param name="skill">The ExecutableSkill entity with stored FileHash.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if current file hash matches stored hash; false if mismatch or file not found.</returns>
    Task<bool> ValidateHashAsync(ExecutableSkill skill, CancellationToken ct = default);

    /// <summary>
    /// Recomputes the hash for a skill file and updates the ExecutableSkill entity (does not persist).
    /// Caller is responsible for persisting the updated entity via repository.
    /// </summary>
    /// <param name="skill">The ExecutableSkill entity to update.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="FileNotFoundException">Thrown if the file does not exist.</exception>
    /// <exception cref="IOException">Thrown if the file cannot be read.</exception>
    Task UpdateHashAsync(ExecutableSkill skill, CancellationToken ct = default);
}
