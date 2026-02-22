namespace Daiv3.FoundryLocal.Management;

using Microsoft.AI.Foundry.Local;

public sealed class FoundryLocalOptions
{
    public string AppName { get; set; } = "foundry-local-management";
    public LogLevel LogLevel { get; set; } = LogLevel.Information;
    public string? AppDataDir { get; set; }
    public string? ModelCacheDir { get; set; }
    public bool EnsureExecutionProviders { get; set; } = true;
}
