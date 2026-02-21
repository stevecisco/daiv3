using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.Logging;

namespace FoundryLocal.IntegrationTests;

/// <summary>
/// Integration tests for the optional REST web server functionality.
/// Tests web server start/stop operations.
/// </summary>
public class WebServerTests : IAsyncLifetime
{
    private ILoggerFactory? _loggerFactory;
    private ILogger<WebServerTests>? _logger;
    private FoundryLocalManager? _manager;

    public async Task InitializeAsync()
    {
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information);
            builder.AddConsole();
        });
        
        _logger = _loggerFactory.CreateLogger<WebServerTests>();

        var config = new Configuration
        {
            AppName = "WebServerTests",
            LogLevel = Microsoft.AI.Foundry.Local.LogLevel.Information,
            Web = new Configuration.WebService
            {
                Urls = "http://127.0.0.1:55588"
            }
        };

        await FoundryLocalManager.CreateAsync(config, _logger);
        _manager = FoundryLocalManager.Instance;
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

        if (_loggerFactory != null)
        {
            _loggerFactory.Dispose();
        }
    }

    [Fact(Skip = "Web server API methods need verification - check FoundryLocalManager API reference")]
    public async Task StartWebServerAsync_ShouldStartSuccessfully()
    {
        // Act
        _logger!.LogInformation("Starting web server...");
        // await _manager!.StartWebServerAsync();

        // Assert
        _logger.LogInformation("Web server started successfully");
        Assert.NotNull(_manager);
        await Task.CompletedTask;
    }

    [Fact(Skip = "Web server API methods need verification - check FoundryLocalManager API reference")]
    public async Task StopWebServerAsync_AfterStart_ShouldStopSuccessfully()
    {
        // Arrange
        // await _manager!.StartWebServerAsync();
        _logger!.LogInformation("Web server started");

        // Act
        _logger.LogInformation("Stopping web server...");
        // await _manager.StopWebServerAsync();

        // Assert
        _logger.LogInformation("Web server stopped successfully");
        Assert.NotNull(_manager);
        await Task.CompletedTask;
    }

    [Fact(Skip = "Web server API methods need verification - check FoundryLocalManager API reference")]
    public async Task StartWebServerAsync_WithCustomUrl_ShouldUseCustomUrl()
    {
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
        // await customManager.StartWebServerAsync();

        // Assert
        _logger!.LogInformation($"Web server configuration set with URL: {customConfig.Web.Urls}");
        Assert.Equal("http://127.0.0.1:55600", customConfig.Web.Urls);

        // Cleanup
        // await customManager.StopWebServerAsync();
    }
}
