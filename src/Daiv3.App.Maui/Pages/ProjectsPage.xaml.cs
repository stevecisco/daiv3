using Daiv3.App.Maui.ViewModels;

namespace Daiv3.App.Maui.Pages;

public partial class ProjectsPage : ContentPage
{
    public ProjectsPage(ProjectsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
