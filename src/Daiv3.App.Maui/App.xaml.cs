using Microsoft.Maui;
using Microsoft.Maui.Controls;

namespace Daiv3.App.Maui;

/// <summary>
/// Main application class for Daiv3 MAUI app.
/// </summary>
public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }
}
