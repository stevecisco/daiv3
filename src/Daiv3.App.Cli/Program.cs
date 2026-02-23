using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Daiv3.Persistence;

namespace Daiv3.App.Cli;

/// <summary>
/// Command-line interface for Daiv3.
/// </summary>
public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Daiv3 CLI - Distributed AI System");

        // Database commands
        var dbCommand = new Command("db", "Database management commands");
        
        var dbInitCommand = new Command("init", "Initialize the database");
        dbInitCommand.SetHandler(async () =>
        {
            var host = CreateHost();
            var exitCode = await DatabaseInitCommand(host);
            Environment.Exit(exitCode);
        });

        var dbStatusCommand = new Command("status", "Show database status");
        dbStatusCommand.SetHandler(async () =>
        {
            var host = CreateHost();
            var exitCode = await DatabaseStatusCommand(host);
            Environment.Exit(exitCode);
        });

        dbCommand.AddCommand(dbInitCommand);
        dbCommand.AddCommand(dbStatusCommand);
        rootCommand.AddCommand(dbCommand);

        return await rootCommand.InvokeAsync(args);
    }

    private static IHost CreateHost()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // Configure logging
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Information);
                });

                // Add persistence layer
                services.AddPersistence(options =>
                {
                    // Use default options (database in %LOCALAPPDATA%\Daiv3\)
                });
            })
            .Build();
    }

    private static async Task<int> DatabaseInitCommand(IHost host)
    {
        try
        {
            Console.WriteLine("Initializing Daiv3 database...");
            
            await host.Services.InitializeDatabaseAsync();
            
            var dbContext = host.Services.GetRequiredService<IDatabaseContext>();
            var version = await dbContext.GetSchemaVersionAsync();
            
            Console.WriteLine($"✓ Database initialized successfully");
            Console.WriteLine($"  Path: {dbContext.DatabasePath}");
            Console.WriteLine($"  Schema Version: {version}");
            
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to initialize database:");
            Console.WriteLine($"  Error: {ex.Message}");
            Console.WriteLine($"  Type: {ex.GetType().Name}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"  Inner: {ex.InnerException.Message}");
            }
            Console.WriteLine($"\nStack Trace:");
            Console.WriteLine(ex.StackTrace);
            return 1;
        }
    }

    private static async Task<int> DatabaseStatusCommand(IHost host)
    {
        try
        {
            var dbContext = host.Services.GetRequiredService<IDatabaseContext>();
            
            Console.WriteLine("Database Status:");
            Console.WriteLine($"  Path: {dbContext.DatabasePath}");
            
            if (!File.Exists(dbContext.DatabasePath))
            {
                Console.WriteLine($"  Status: Not initialized");
                return 0;
            }

            var fileInfo = new FileInfo(dbContext.DatabasePath);
            Console.WriteLine($"  Size: {fileInfo.Length:N0} bytes");
            Console.WriteLine($"  Last Modified: {fileInfo.LastWriteTime}");
            
            var version = await dbContext.GetSchemaVersionAsync();
            Console.WriteLine($"  Schema Version: {version}");
            
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to get database status: {ex.Message}");
            return 1;
        }
    }
}
