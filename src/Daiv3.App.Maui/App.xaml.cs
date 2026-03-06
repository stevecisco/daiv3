using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Daiv3.App.Maui;

public partial class App : Application
{
    public App()
    {
        try
        {
            InitializeComponent();
        
            // Set up global unhandled exception handlers
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                var errorPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Daiv3", "logs", "unhandled-exception.txt");
                Directory.CreateDirectory(Path.GetDirectoryName(errorPath)!);
                File.WriteAllText(errorPath, $"{DateTime.Now:O}\nUnhandled exception:\n{e.ExceptionObject}");
                System.Diagnostics.Debug.WriteLine($"UNHANDLED EXCEPTION: {e.ExceptionObject}");
            };

            TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                var errorPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Daiv3", "logs", "unobserved-task-exception.txt");
                Directory.CreateDirectory(Path.GetDirectoryName(errorPath)!);
                File.WriteAllText(errorPath, $"{DateTime.Now:O}\nUnobserved task exception:\n{e.Exception}");
                System.Diagnostics.Debug.WriteLine($"UNOBSERVED TASK EXCEPTION: {e.Exception}");
                e.SetObserved();
            };
        }
        catch (Exception ex)
        {
            var errorPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Daiv3", "logs", "app-constructor-error.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(errorPath)!);
            File.WriteAllText(errorPath, $"{DateTime.Now:O}\nApp constructor error:\n{ex}\nInner: {ex.InnerException}");
            Console.WriteLine($"APP CONSTRUCTOR ERROR: {ex}");
            throw;
        }
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        try
        {
            return new Window(new AppShell());
        }
        catch (Exception ex)
        {
            var errorPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Daiv3", "logs", "create-window-error.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(errorPath)!);
            File.WriteAllText(errorPath, $"{DateTime.Now:O}\nCreateWindow error:\n{ex}\nInner: {ex.InnerException}");
            Console.WriteLine($"CREATE WINDOW ERROR: {ex}");
            throw;
        }
    }

    protected override void OnStart()
    {
        base.OnStart();
        System.Diagnostics.Debug.WriteLine("App OnStart called");
    }
}
