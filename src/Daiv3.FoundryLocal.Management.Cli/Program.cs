using Daiv3.FoundryLocal.Management;
using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.Logging;

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information)
        .AddSimpleConsole(options =>
        {
            options.SingleLine = true;
            options.TimestampFormat = "HH:mm:ss ";
        });
});

var logger = loggerFactory.CreateLogger("Daiv3.FoundryLocal.Management.Cli");

await using var service = new FoundryLocalManagementService(logger);
await service.InitializeAsync(new FoundryLocalOptions
{
    AppName = "foundry-local-management-cli",
    LogLevel = Microsoft.AI.Foundry.Local.LogLevel.Information,
    EnsureExecutionProviders = true
});

Console.WriteLine("Foundry Local Model Manager");
Console.WriteLine("Type a command number or keyword. Type 'exit' to quit.");

// Store last model listing for number-based selection
var modelIndex = new Dictionary<int, (string Alias, int? Version, DeviceType? Device)>();

while (true)
{
    ShowMenu();
    Console.Write("Command: ");
    var input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input))
    {
        continue;
    }

    var command = input.Trim().ToLowerInvariant();
    if (command is "0" or "exit" or "quit" or "q")
    {
        break;
    }

    try
    {
        switch (command)
        {
            case "1":
            case "list":
            case "available":
                await ListAvailableAsync(service, modelIndex);
                break;
            case "2":
            case "download":
                await DownloadAsync(service, modelIndex);
                break;
            case "3":
            case "cached":
                await ListCachedAsync(service);
                break;
            case "4":
            case "delete":
            case "remove":
                await DeleteCachedAsync(service);
                break;
            default:
                Console.WriteLine("Unknown command.");
                break;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }
}

static void ShowMenu()
{
    Console.WriteLine();
    Console.WriteLine("1 - List available models");
    Console.WriteLine("2 - Download model");
    Console.WriteLine("3 - List cached models");
    Console.WriteLine("4 - Delete cached model");
    Console.WriteLine("0 - Exit");
}

static async Task ListAvailableAsync(FoundryLocalManagementService service, Dictionary<int, (string Alias, int? Version, DeviceType? Device)> modelIndex)
{
    var models = await service.ListAvailableModelsAsync();
    if (models.Count == 0)
    {
        Console.WriteLine("No models found in the catalog.");
        return;
    }

    modelIndex.Clear();
    Console.WriteLine("\nAvailable Models (use model # or alias when downloading):");
    Console.WriteLine();

    int index = 1;

    foreach (var model in models)
    {
        // First entry: auto-select (alias only, no version/device filter)
        var firstVariant = model.Variants.FirstOrDefault();
        if (firstVariant != null)
        {
            var size = firstVariant.FileSizeMb.HasValue ? $"{firstVariant.FileSizeMb.Value} MB" : "n/a";
            var cachedMark = firstVariant.Cached ? " [CACHED]" : "";
            Console.WriteLine($"{index,3}. {model.Alias,-30} [auto] → {firstVariant.DeviceType} v{firstVariant.Version}, {size}{cachedMark}");
            modelIndex[index] = (model.Alias, null, null);
            index++;
        }

        // Individual variants
        if (model.Variants.Count > 1)
        {
            foreach (var variant in model.Variants.OrderBy(v => v.DeviceType).ThenBy(v => v.Version))
            {
                var size = variant.FileSizeMb.HasValue ? $"{variant.FileSizeMb.Value} MB" : "n/a";
                var cachedMark = variant.Cached ? " [CACHED]" : "";
                Console.WriteLine($"{index,3}.   └─ [{variant.DeviceType}] → v{variant.Version}, {size}{cachedMark}");
                modelIndex[index] = (model.Alias, variant.Version, variant.DeviceType);
                index++;
            }
        }
    }

    Console.WriteLine();
    Console.WriteLine("Note: [auto] lets Foundry pick the best variant (prioritizes NPU > GPU > CPU)");
}

static async Task DownloadAsync(FoundryLocalManagementService service, Dictionary<int, (string Alias, int? Version, DeviceType? Device)> modelIndex)
{
    Console.Write("Model # or alias/id: ");
    var input = Console.ReadLine()?.Trim();
    if (string.IsNullOrWhiteSpace(input))
    {
        Console.WriteLine("No input provided.");
        return;
    }

    string aliasOrId;
    int? version = null;
    DeviceType? device = null;

    // Check if input is a number from the model index
    if (int.TryParse(input, out var modelNumber) && modelIndex.TryGetValue(modelNumber, out var modelEntry))
    {
        aliasOrId = modelEntry.Alias;
        version = modelEntry.Version;
        device = modelEntry.Device;
        Console.WriteLine($"Selected: {aliasOrId}" + 
            (version.HasValue ? $" v{version}" : "") + 
            (device.HasValue ? $" [{device}]" : " [auto]"));
    }
    else
    {
        // Use as alias/id and prompt for version/device
        aliasOrId = input;
        version = PromptOptionalInt("Version (optional)");
        device = PromptOptionalDevice("Device (CPU/GPU/NPU/Any, optional)");
    }

    var progress = new Progress<float>(p =>
    {
        Console.Write($"\rDownloading: {p:0.0}%");
        if (p >= 100f)
        {
            Console.WriteLine();
        }
    });

    var result = await service.DownloadModelAsync(aliasOrId, version, device, progress);
    Console.WriteLine($"Downloaded {result.Id}");
}

static async Task ListCachedAsync(FoundryLocalManagementService service)
{
    var cached = await service.ListCachedModelsAsync();
    if (cached.Count == 0)
    {
        Console.WriteLine("No cached models found.");
        return;
    }

    foreach (var variant in cached)
    {
        var size = variant.FileSizeMb.HasValue ? $"{variant.FileSizeMb.Value} MB" : "n/a";
        Console.WriteLine($"{variant.Id} | {variant.DeviceType} | {size}");
    }
}

static async Task DeleteCachedAsync(FoundryLocalManagementService service)
{
    var aliasOrId = Prompt("Model alias or id");
    var version = PromptOptionalInt("Version (optional)");
    var device = PromptOptionalDevice("Device (CPU/GPU/NPU/Any, optional)");

    var deleted = await service.DeleteCachedModelsAsync(aliasOrId, version, device);
    Console.WriteLine($"Deleted {deleted} cached model(s).");
}

static string Prompt(string label)
{
    while (true)
    {
        Console.Write($"{label}: ");
        var input = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(input))
        {
            return input.Trim();
        }
    }
}

static int? PromptOptionalInt(string label)
{
    Console.Write($"{label}: ");
    var input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input))
    {
        return null;
    }

    if (int.TryParse(input.Trim(), out var value))
    {
        return value;
    }

    Console.WriteLine("Invalid number. Ignoring.");
    return null;
}

static DeviceType? PromptOptionalDevice(string label)
{
    Console.Write($"{label}: ");
    var input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input))
    {
        return null;
    }

    var value = input.Trim().ToLowerInvariant();
    return value switch
    {
        "cpu" => DeviceType.CPU,
        "gpu" => DeviceType.GPU,
        "npu" => DeviceType.NPU,
        "any" => null,
        _ => null
    };
}
