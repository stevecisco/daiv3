using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Daiv3.Persistence.Repositories;
using Daiv3.Persistence.Entities;

namespace Daiv3.App.Maui.ViewModels;

/// <summary>
/// ViewModel for the Project Master Dashboard (CT-REQ-011).
/// Manages projects with hierarchical relationships and multiple pivot views.
/// </summary>
public class ProjectsViewModel : BaseViewModel
{
    private readonly ILogger<ProjectsViewModel> _logger;
    private readonly ProjectRepository _projectRepository;
    private ProjectItem? _selectedProject;
    private string _currentView = "Tree";

    public ProjectsViewModel(ILogger<ProjectsViewModel> logger, ProjectRepository projectRepository)
    {
        _logger = logger;
        _projectRepository = projectRepository;
        Title = "Projects";
        Projects = new ObservableCollection<ProjectItem>();
        CreateProjectCommand = new Command(OnCreateProject);
        DeleteProjectCommand = new Command<ProjectItem>(OnDeleteProject);
        RefreshCommand = new Command(async () => await LoadProjectsAsync());

        _logger.LogInformation("ProjectsViewModel initialized");
        _ = LoadProjectsAsync();
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

    /// <summary>
    /// Current dashboard view (Tree, Priority, Status, Assignment, Timeline, Metrics).
    /// </summary>
    public string CurrentView
    {
        get => _currentView;
        set
        {
            if (SetProperty(ref _currentView, value))
            {
                _ = LoadProjectsAsync();
            }
        }
    }

    public ICommand CreateProjectCommand { get; }
    public ICommand DeleteProjectCommand { get; }
    public ICommand RefreshCommand { get; }

    private async Task LoadProjectsAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;

        try
        {
            var projects = await _projectRepository.GetAllAsync();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Projects.Clear();
                foreach (var project in projects)
                {
                    Projects.Add(MapToProjectItem(project));
                }

                _logger.LogInformation("Projects loaded: {Count}", Projects.Count);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load projects");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static ProjectItem MapToProjectItem(Project project)
    {
        return new ProjectItem
        {
            Id = project.ProjectId,
            Name = project.Name,
            Description = project.Description ?? string.Empty,
            Status = project.Status,
            Priority = project.Priority,
            ProgressPercent = project.ProgressPercent,
            Deadline = project.Deadline.HasValue
                ? DateTimeOffset.FromUnixTimeSeconds(project.Deadline.Value).DateTime
                : null,
            AssignedAgent = project.AssignedAgent,
            EstimatedCost = project.EstimatedCost,
            ActualCost = project.ActualCost,
            CreatedDate = DateTimeOffset.FromUnixTimeSeconds(project.CreatedAt).DateTime,
            UpdatedDate = DateTimeOffset.FromUnixTimeSeconds(project.UpdatedAt).DateTime,
            ParentProjectId = project.ParentProjectId,
            TaskCount = 0 // TODO: Load from TaskRepository
        };
    }

    private async void OnCreateProject()
    {
        // TODO: Show dialog to get project details
        var newProject = new Project
        {
            ProjectId = Guid.NewGuid().ToString(),
            Name = $"New Project {Projects.Count + 1}",
            Description = "Project description",
            RootPaths = Daiv3.Persistence.ProjectRootPaths.Serialize([Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)]),
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Status = "active",
            Priority = 2,
            ProgressPercent = 0.0
        };

        try
        {
            await _projectRepository.AddAsync(newProject);
            _logger.LogInformation("Project created: {ProjectName}", newProject.Name);
            await LoadProjectsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create project");
        }
    }

    private async void OnDeleteProject(ProjectItem? project)
    {
        if (project == null)
            return;

        try
        {
            await _projectRepository.DeleteAsync(project.Id);
            _logger.LogInformation("Project deleted: {ProjectName}", project.Name);
            await LoadProjectsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete project");
        }
    }
}

/// <summary>
/// Represents a project item with associated tasks (CT-REQ-011).
/// </summary>
public class ProjectItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
    public int Priority { get; set; } = 2;
    public double ProgressPercent { get; set; } = 0.0;
    public DateTime? Deadline { get; set; }
    public string? AssignedAgent { get; set; }
    public double? EstimatedCost { get; set; }
    public double? ActualCost { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime UpdatedDate { get; set; }
    public string? ParentProjectId { get; set; }
    public int TaskCount { get; set; }

    public string StatusBadge => Status.ToLowerInvariant() switch
    {
        "active" => "🟢",
        "pending" => "🔵",
        "blocked" => "🟡",
        "completed" => "⚫",
        "archived" => "📦",
        _ => "⚪"
    };

    public string PriorityLabel => $"P{Priority}";
    public string ProgressLabel => $"{ProgressPercent:F0}%";
    public string DeadlineLabel => Deadline.HasValue ? Deadline.Value.ToString("MMM dd, yyyy") : "No deadline";
    public string AssignedAgentLabel => AssignedAgent ?? "Unassigned";
}
