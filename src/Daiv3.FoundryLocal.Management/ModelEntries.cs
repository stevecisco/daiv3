namespace Daiv3.FoundryLocal.Management;

using Microsoft.AI.Foundry.Local;

public sealed record ModelVariantEntry(
    string Id,
    string Alias,
    int Version,
    DeviceType DeviceType,
    string ExecutionProvider,
    bool Cached,
    int? FileSizeMb);

public sealed record ModelCatalogEntry(
    string Alias,
    string? DisplayName,
    IReadOnlyList<ModelVariantEntry> Variants);
