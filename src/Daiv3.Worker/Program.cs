using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Daiv3.Worker;

/// <summary>
/// Background worker service for Daiv3.
/// </summary>
public class Program
{
    public static void Main(string[] args)
    {
        CreateHostBuilder(args).Build().Run();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                // TODO: Add worker services
            });
}
