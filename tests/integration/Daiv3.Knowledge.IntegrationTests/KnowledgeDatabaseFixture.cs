using Daiv3.Infrastructure.Shared.Logging;
using Daiv3.Knowledge.Embedding;
using Daiv3.Persistence;
using Daiv3.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Daiv3.Knowledge.IntegrationTests;

/// <summary>
/// Collection definition for Knowledge integration tests that need database access.
/// </summary>
[CollectionDefinition("Knowledge Database Collection")]
public class KnowledgeDatabaseCollection : ICollectionFixture<KnowledgeDatabaseFixture>
{
  // This class has no code, and is never created. Its purpose is simply
  // to define the collection that tests can use with [Collection("Knowledge Database Collection")]
}

/// <summary>
/// Fixture for setting up and tearing down test databases for Knowledge layer tests.
/// </summary>
public class KnowledgeDatabaseFixture : IAsyncLifetime
{
    private readonly string _testDbPath;
    private DatabaseContext? _context;
    public IServiceProvider ServiceProvider { get; private set; } = null!;

    public KnowledgeDatabaseFixture()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"daiv3_knowledge_test_{Guid.NewGuid():N}.db");
    }

    public async Task InitializeAsync()
    {
        // Create database context
        var loggerFactory = LoggerFactory.Create(builder => 
        {
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
            builder.AddFileLogging(Path.Combine(Path.GetTempPath(), "daiv3_tests"), "knowledge_test", LogLevel.Debug);
        });
        var options = Options.Create(new PersistenceOptions { DatabasePath = _testDbPath });
        _context = new DatabaseContext(loggerFactory.CreateLogger<DatabaseContext>(), options);

        // Initialize database
        await _context.InitializeAsync();

        // Set up DI container
        var services = new ServiceCollection();
        services.AddSingleton<IDatabaseContext>(_context);
        services.AddSingleton(_context);
        services.AddSingleton(loggerFactory.CreateLogger<VectorStoreService>());
        services.AddSingleton(loggerFactory.CreateLogger<TwoTierIndexService>());
        services.AddSingleton(loggerFactory);
        services.AddSingleton<IVectorSimilarityService, CpuVectorSimilarityService>();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

        // Register repositories
        services.AddScoped<TopicIndexRepository>();
        services.AddScoped<ChunkIndexRepository>();
        services.AddScoped<DocumentRepository>();

        // Register Knowledge services
        services.AddScoped<IVectorStoreService, VectorStoreService>();
        services.AddScoped<ITwoTierIndexService, TwoTierIndexService>();

        // Register embedding services for real model testing
        var modelPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Daiv3", "models", "embeddings", "all-MiniLM-L6-v2", "model.onnx");
        
        services.AddEmbeddingServices(opts =>
        {
            opts.ModelPath = modelPath;
        });

        ServiceProvider = services.BuildServiceProvider();
    }

    public async Task DisposeAsync()
    {
        if (_context != null)
        {
            await _context.DisposeAsync();
            _context = null;
        }

        // Clear connection pools
        SqliteConnection.ClearAllPools();

        // Wait for finalizers
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        await Task.Delay(200);

        // Clean up test database
        if (File.Exists(_testDbPath))
        {
            var retries = 10;
            while (retries > 0)
            {
                try
                {
                    File.Delete(_testDbPath);
                    break;
                }
                catch (IOException) when (retries > 1)
                {
                    retries--;
                    await Task.Delay(100);
                }
            }
        }
    }
}
