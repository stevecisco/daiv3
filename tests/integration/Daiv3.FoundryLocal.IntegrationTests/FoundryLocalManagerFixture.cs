using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.Logging;

namespace Daiv3.FoundryLocal.IntegrationTests;

public sealed class FoundryLocalManagerFixture : IAsyncLifetime, IDisposable
{
    private static readonly SemaphoreSlim InitializationGate = new(1, 1);
    private static bool _initialized;
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
        if (_initialized)
        {
            Manager = FoundryLocalManager.Instance;
            return;
        }

        await InitializationGate.WaitAsync();
        try
        {
            if (_initialized)
            {
                Manager = FoundryLocalManager.Instance;
                return;
            }

            var config = new Configuration
            {
                AppName = "FoundryLocalIntegrationTests",
                LogLevel = Microsoft.AI.Foundry.Local.LogLevel.Information
            };

            try
            {
                await FoundryLocalManager.CreateAsync(config, _logger);
            }
            catch (FoundryLocalException ex) when (ex.Message.Contains("already been created", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("FoundryLocalManager singleton already initialized; reusing existing instance");
            }

            Manager = FoundryLocalManager.Instance;
            _initialized = true;
        }
        finally
        {
            InitializationGate.Release();
        }
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
