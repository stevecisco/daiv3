using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.Logging;

namespace Daiv3.FoundryLocal.IntegrationTests;

/// <summary>
/// Integration tests for the optional REST web server functionality.
/// Tests web server start/stop operations.
/// </summary>
[Collection("FoundryLocalManager collection")]
public sealed class WebServerTests : IAsyncLifetime, IDisposable
{
    private readonly FoundryLocalManagerFixture _fixture;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<WebServerTests> _logger;
    private FoundryLocalManager? _manager;

    public WebServerTests(FoundryLocalManagerFixture fixture)
    {
        _fixture = fixture;
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information);
            builder.AddConsole();
        });

        _logger = _loggerFactory.CreateLogger<WebServerTests>();
    }

    public async Task InitializeAsync()
    {
        _manager = _fixture.Manager;
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        // Ensure web server is stopped after tests
        try
        {
            // await _manager!.StopWebServerAsync();
            // Note: Method name may vary in actual SDK - check API reference
        }
        catch
        {
            // Ignore errors during cleanup
        }

        Dispose();
        await Task.CompletedTask;
    }

    public void Dispose()
    {
        _loggerFactory.Dispose();
    }

    [Fact(Skip = "SDK API for web server not verified - check FoundryLocalManager/-Catalog API documentation")]
    public async Task StartWebServerAsync_ShouldStartSuccessfully()
    {
        // Note: The Foundry Local SDK documentation mentions web server functionality,
        // but the actual API methods (StartWebServerAsync, StopWebServerAsync) may not be
        // publicly exposed on FoundryLocalManager or Catalog objects.
        // See API_DISCOVERY.md for method discovery guidance.
        
        // Act
        _logger.LogInformation("Starting web server...");
        // Possible API candidates (uncomment if discovered):
        // await _manager!.StartWebServerAsync();
        // OR
        // var catalog = await _manager!.GetCatalogAsync();
        // await catalog.StartWebServerAsync();

        // Assert
        _logger.LogInformation("Web server started successfully");
        Assert.NotNull(_manager);
        await Task.CompletedTask;
    }

    [Fact(Skip = "SDK API for web server not verified - check FoundryLocalManager/Catalog API documentation")]
    public async Task StopWebServerAsync_AfterStart_ShouldStopSuccessfully()
    {
        // Note: The Foundry Local SDK documentation mentions web server functionality,
        // but the actual API methods may not be publicly exposed.
        // See API_DISCOVERY.md for method discovery guidance.
        
        // Arrange
        // await _manager!.StartWebServerAsync();
        _logger.LogInformation("Web server started");

        // Act
        _logger.LogInformation("Stopping web server...");
        // await _manager.StopWebServerAsync();

        // Assert
        _logger.LogInformation("Web server stopped successfully");
        Assert.NotNull(_manager);
        await Task.CompletedTask;
    }

    [Fact(Skip = "SDK API for web server not verified - requires confirmed API discovery")]
    public async Task StartWebServerAsync_WithCustomUrl_ShouldUseCustomUrl()
    {
        // Note: This test demonstrates the pattern for custom URL configuration
        // but requires verified API methods for web server management.
        // See API_DISCOVERY.md for method discovery guidance.
        
        // Arrange
        var customConfig = new Configuration
        {
            AppName = "CustomUrlTest",
            LogLevel = Microsoft.AI.Foundry.Local.LogLevel.Information,
            Web = new Configuration.WebService
            {
                Urls = "http://127.0.0.1:55600"
            }
        };

        await FoundryLocalManager.CreateAsync(customConfig, _logger!);
        var customManager = FoundryLocalManager.Instance;

        // Act
        // Possible API pattern (uncomment if discovered):
        // await customManager.StartWebServerAsync(); // May have different method name

        // Assert
        _logger.LogInformation($"Web server configuration set with URL: {customConfig.Web.Urls}");
        Assert.Equal("http://127.0.0.1:55600", customConfig.Web.Urls);

        // Cleanup (if StartWebServerAsync is implemented)
        // await customManager.StopWebServerAsync();
    }
}
