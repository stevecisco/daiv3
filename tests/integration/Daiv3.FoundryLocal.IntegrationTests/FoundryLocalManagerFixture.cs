using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.Logging;

namespace Daiv3.FoundryLocal.IntegrationTests;

public sealed class FoundryLocalManagerFixture : IAsyncLifetime, IDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<FoundryLocalManagerFixture> _logger;

    public FoundryLocalManagerFixture()
    {
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information);
            builder.AddConsole();
        });

        _logger = _loggerFactory.CreateLogger<FoundryLocalManagerFixture>();
    }

    public FoundryLocalManager Manager { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var config = new Configuration
        {
            AppName = "FoundryLocalIntegrationTests",
            LogLevel = Microsoft.AI.Foundry.Local.LogLevel.Information
        };

        await FoundryLocalManager.CreateAsync(config, _logger);
        Manager = FoundryLocalManager.Instance;
    }

    public Task DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _loggerFactory.Dispose();
    }
}
