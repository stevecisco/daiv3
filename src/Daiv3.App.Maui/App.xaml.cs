using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Daiv3.App.Maui;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        
        // Set up global unhandled exception handlers
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"UNHANDLED EXCEPTION: {e.ExceptionObject}");
        };

        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"UNOBSERVED TASK EXCEPTION: {e.Exception}");
            e.SetObserved();
        };
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }

    protected override void OnStart()
    {
        base.OnStart();
        System.Diagnostics.Debug.WriteLine("App OnStart called");
    }
}
