using System.CommandLine;

namespace Daiv3.App.Cli;

/// <summary>
/// Command-line interface for Daiv3.
/// </summary>
public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Daiv3 CLI - Distributed AI System");

        // TODO: Add commands

        return await rootCommand.InvokeAsync(args);
    }
}
