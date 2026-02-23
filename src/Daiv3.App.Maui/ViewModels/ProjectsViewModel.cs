using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Extensions.Logging;

namespace Daiv3.App.Maui.ViewModels;

/// <summary>
/// ViewModel for the Project Manager interface.
/// Manages projects, tasks, and their hierarchical relationships.
/// </summary>
public class ProjectsViewModel : BaseViewModel
{
    private readonly ILogger<ProjectsViewModel> _logger;
    private ProjectItem? _selectedProject;

    public ProjectsViewModel(ILogger<ProjectsViewModel> logger)
    {
        _logger = logger;
        Title = "Projects";
        Projects = new ObservableCollection<ProjectItem>();
        CreateProjectCommand = new Command(OnCreateProject);
        DeleteProjectCommand = new Command<ProjectItem>(OnDeleteProject);

        _logger.LogInformation("ProjectsViewModel initialized");
        LoadProjects();
    }

    /// <summary>
    /// Collection of all projects.
    /// </summary>
    public ObservableCollection<ProjectItem> Projects { get; }

    /// <summary>
    /// Gets or sets the currently selected project.
    /// </summary>
    public ProjectItem? SelectedProject
    {
        get => _selectedProject;
        set => SetProperty(ref _selectedProject, value);
    }

    public ICommand CreateProjectCommand { get; }
    public ICommand DeleteProjectCommand { get; }

    private void LoadProjects()
    {
        IsBusy = true;

        // TODO: Integrate with persistence layer to load projects
        Task.Run(async () =>
        {
            await Task.Delay(300); // Simulate loading

            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Add sample project for demonstration
                Projects.Add(new ProjectItem
                {
                    Id = Guid.NewGuid(),
                    Name = "Sample Project",
                    Description = "Example project (Persistence integration pending)",
                    CreatedDate = DateTime.Now,
                    TaskCount = 0
                });

                IsBusy = false;
                _logger.LogInformation("Projects loaded: {Count}", Projects.Count);
            });
        });
    }

    private async void OnCreateProject()
    {
        // TODO: Show dialog to get project details
        var newProject = new ProjectItem
        {
            Id = Guid.NewGuid(),
            Name = $"New Project {Projects.Count + 1}",
            Description = "Project description",
            CreatedDate = DateTime.Now,
            TaskCount = 0
        };

        Projects.Add(newProject);
        _logger.LogInformation("Project created: {ProjectName}", newProject.Name);

        // TODO: Persist to database
    }

    private void OnDeleteProject(ProjectItem? project)
    {
        if (project == null)
            return;

        Projects.Remove(project);
        _logger.LogInformation("Project deleted: {ProjectName}", project.Name);

        // TODO: Delete from database
    }
}

/// <summary>
/// Represents a project item with associated tasks.
/// </summary>
public class ProjectItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public int TaskCount { get; set; }
}
