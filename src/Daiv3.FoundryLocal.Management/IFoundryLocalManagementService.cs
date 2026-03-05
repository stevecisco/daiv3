namespace Daiv3.FoundryLocal.Management;

/// <summary>
/// Abstraction for querying Foundry Local management data.
/// </summary>
public interface IFoundryLocalManagementService
{
    /// <summary>
    /// Gets the configured Foundry model cache directory.
    /// </summary>
    Task<string?> GetModelsDirectoryAsync(CancellationToken ct = default);
}
