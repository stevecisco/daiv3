using Daiv3.Persistence;
using Daiv3.Persistence.Entities;
using Daiv3.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace Daiv3.Persistence.IntegrationTests;

/// <summary>
/// Integration tests for settings versioning with database migrations.
/// Tests actual database operations with schema migrations (CT-DATA-001).
/// </summary>
[Collection("Database")]
public sealed class SettingsMigrationIntegrationTests : IAsyncLifetime, IDisposable
{
    private string _testDatabasePath = null!;
    private IDatabaseContext _databaseContext = null!;
    private SettingsRepository _repository = null!;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IServiceProvider _serviceProvider;
    private bool _disposed;

    public SettingsMigrationIntegrationTests()
    {
        _loggerFactory = LoggerFactory.Create(b => b.AddConsole());

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole());
        services.AddPersistence();
        _serviceProvider = services.BuildServiceProvider();
    }

    public async Task InitializeAsync()
    {
        // Create a temporary database file for testing
        _testDatabasePath = Path.Combine(Path.GetTempPath(), $"test_settings_{Guid.NewGuid()}.db");

        // Setup persistence options
        var options = new PersistenceOptions
        {
            DatabasePath = _testDatabasePath,
            EnableWAL = true,
            BusyTimeout = 5000
        };

        // Create database context and initialize
        var logger = _loggerFactory.CreateLogger<DatabaseContext>();
        _databaseContext = new DatabaseContext(logger, Microsoft.Extensions.Options.Options.Create(options));

        await _databaseContext.InitializeAsync();

        // Create repository
        var repositoryLogger = _loggerFactory.CreateLogger<SettingsRepository>();
        _repository = new SettingsRepository(_databaseContext, repositoryLogger);
    }

    public async Task DisposeAsync()
    {
        if (_disposed)
            return;

        // Dispose all disposable resources FIRST
        (_repository as IDisposable)?.Dispose();
        (_databaseContext as IDisposable)?.Dispose();
        (_serviceProvider as IDisposable)?.Dispose();
        _loggerFactory?.Dispose();

        _disposed = true;

        // Wait for DB locks to be released
        await Task.Delay(100);

        // Clean up database files AFTER disposing resources
        if (File.Exists(_testDatabasePath))
        {
            try
            {
                File.Delete(_testDatabasePath);
            }
            catch (IOException)
            {
                // Ignore if file is still locked
            }
        }

        // Also clean up WAL files if they exist
        var walPath = _testDatabasePath + "-wal";
        if (File.Exists(walPath))
        {
            try
            {
                File.Delete(walPath);
            }
            catch (IOException)
            {
                // Ignore if file is still locked
            }
        }
    }

    [Fact]
    public async Task DatabaseMigration_CreatesSettingsTables()
    {
        // Arrange - database already migrated during InitializeAsync

        // Act - verify tables exist by inserting data
        var setting = new AppSetting
        {
            SettingId = Guid.NewGuid().ToString(),
            SettingKey = "test_key",
            SettingValue = "test_value",
            ValueType = "string",
            Category = "testing",
            SchemaVersion = 1,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedBy = "test"
        };

        var result = await _repository.AddAsync(setting);

        // Assert
        Assert.NotEmpty(result);
        Assert.Equal(setting.SettingId, result);

        // Verify retrieval
        var retrieved = await _repository.GetByKeyAsync(setting.SettingKey);
        Assert.NotNull(retrieved);
        Assert.Equal(setting.SettingKey, retrieved.SettingKey);
    }

    [Fact]
    public async Task SettingsUpserAndHistory_TracksChanges()
    {
        // Arrange
        var settingKey = "version_test";
        var setting = new AppSetting
        {
            SettingId = Guid.NewGuid().ToString(),
            SettingKey = settingKey,
            SettingValue = "v1",
            ValueType = "string",
            Category = "testing",
            SchemaVersion = 1,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedBy = "test"
        };

        // Act - Create initial setting
        var settingId = await _repository.UpsertAsync(setting, "initial");

        // Act - Update setting
        setting.SettingId = settingId;
        setting.SettingValue = "v2";
        setting.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await _repository.UpsertAsync(setting, "update_v2");

        // Assert - Check history was recorded
        var history = await _repository.GetHistoryByKeyAsync(settingKey);
        Assert.NotEmpty(history);
        
        // Verify at least one history entry exists
        var changes = history.Where(h => h.SettingKey == settingKey).ToList();
        Assert.NotEmpty(changes);

        // Verify the change was recorded
        Assert.Contains(changes, c => c.NewValue == "v2");
    }

    [Fact]
    public async Task SettingsCategorization_FiltersCorrectly()
    {
        // Arrange
        var pathsSetting = new AppSetting
        {
            SettingId = Guid.NewGuid().ToString(),
            SettingKey = "data_path",
            SettingValue = "/data",
            ValueType = "string",
            Category = "paths",
            SchemaVersion = 1,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        var modelSetting = new AppSetting
        {
            SettingId = Guid.NewGuid().ToString(),
            SettingKey = "model_id",
            SettingValue = "llama-2",
            ValueType = "string",
            Category = "models",
            SchemaVersion = 1,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        // Act
        await _repository.AddAsync(pathsSetting);
        await _repository.AddAsync(modelSetting);

        // Assert
        var pathsSettings = await _repository.GetByCategoryAsync("paths");
        var modelSettings = await _repository.GetByCategoryAsync("models");

        Assert.Single(pathsSettings);
        Assert.Single(modelSettings);
        Assert.Equal("data_path", pathsSettings[0].SettingKey);
        Assert.Equal("model_id", modelSettings[0].SettingKey);
    }

    [Fact]
    public async Task SensitiveSettings_AreFlaggedCorrectly()
    {
        // Arrange
        var apiKeySetting = new AppSetting
        {
            SettingId = Guid.NewGuid().ToString(),
            SettingKey = "api_key",
            SettingValue = "secret_key_12345",
            ValueType = "string",
            Category = "providers",
            SchemaVersion = 1,
            IsSensitive = true,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        // Act
        var settingId = await _repository.AddAsync(apiKeySetting);
        var retrieved = await _repository.GetByIdAsync(settingId);

        // Assert
        Assert.NotNull(retrieved);
        Assert.True(retrieved.IsSensitive);
        Assert.Equal(apiKeySetting.SettingValue, retrieved.SettingValue);
    }

    [Fact]
    public async Task SchemaVersion_TracksAcrossUpdates()
    {
        // Arrange
        var setting = new AppSetting
        {
            SettingId = Guid.NewGuid().ToString(),
            SettingKey = "schema_test",
            SettingValue = "v1_value",
            ValueType = "string",
            Category = "testing",
            SchemaVersion = 1,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        // Act
        var settingId = await _repository.AddAsync(setting);

        // Retrieve and verify version 1
        var retrieved1 = await _repository.GetByIdAsync(settingId);
        Assert.NotNull(retrieved1);
        Assert.Equal(1, retrieved1.SchemaVersion);

        // Update to schema version 2
        retrieved1.SettingValue = "v2_value";
        retrieved1.SchemaVersion = 2;
        retrieved1.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await _repository.UpdateAsync(retrieved1);

        // Verify version 2
        var retrieved2 = await _repository.GetByIdAsync(settingId);
        Assert.NotNull(retrieved2);
        Assert.Equal(2, retrieved2.SchemaVersion);
        Assert.Equal("v2_value", retrieved2.SettingValue);
    }

    [Fact]
    public async Task MultipleSettings_CanBeStoredRetrieved()
    {
        // Arrange
        var settings = new List<AppSetting>();
        for (int i = 0; i < 5; i++)
        {
            settings.Add(new AppSetting
            {
                SettingId = Guid.NewGuid().ToString(),
                SettingKey = $"setting_{i}",
                SettingValue = $"value_{i}",
                ValueType = "string",
                Category = "testing",
                SchemaVersion = 1,
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
        }

        // Act
        foreach (var setting in settings)
        {
            await _repository.AddAsync(setting);
        }

        var allSettings = await _repository.GetAllAsync();

        // Assert
        Assert.NotEmpty(allSettings);
        Assert.True(allSettings.Count >= 5);
    }

    public void Dispose()
    {
        // xUnit calls DisposeAsync for IAsyncLifetime first
        // This is a safety fallback in case someone uses 'using' directly
        if (!_disposed)
        {
            (_repository as IDisposable)?.Dispose();
            (_databaseContext as IDisposable)?.Dispose();
            (_serviceProvider as IDisposable)?.Dispose();
            _loggerFactory?.Dispose();
            _disposed = true;
        }
    }
}
