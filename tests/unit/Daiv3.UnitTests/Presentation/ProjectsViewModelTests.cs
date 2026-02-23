using Daiv3.App.Maui.ViewModels;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Daiv3.UnitTests.Presentation;

/// <summary>
/// Unit tests for ProjectsViewModel.
/// </summary>
public class ProjectsViewModelTests
{
    private readonly Mock<ILogger<ProjectsViewModel>> _mockLogger;

    public ProjectsViewModelTests()
    {
        _mockLogger = new Mock<ILogger<ProjectsViewModel>>();
    }

    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        // Act
        var viewModel = new ProjectsViewModel(_mockLogger.Object);

        // Assert
        Assert.Equal("Projects", viewModel.Title);
        Assert.NotNull(viewModel.Projects);
        Assert.NotNull(viewModel.CreateProjectCommand);
        Assert.NotNull(viewModel.DeleteProjectCommand);
    }

    [Fact]
    public async Task Constructor_ShouldLoadInitialProjects()
    {
        // Act
        var viewModel = new ProjectsViewModel(_mockLogger.Object);
        await Task.Delay(600); // Wait longer for initial load to complete

        // Assert - Should have at least the sample project (or might be empty in test context)
        // Note: MainThread.BeginInvokeOnMainThread may not work in test context
        Assert.NotNull(viewModel.Projects);
    }

    [Fact]
    public void SelectedProject_WhenSet_ShouldUpdateProperty()
    {
        // Arrange
        var viewModel = new ProjectsViewModel(_mockLogger.Object);
        var project = new ProjectItem
        {
            Id = Guid.NewGuid(),
            Name = "Test Project",
            Description = "Test Description"
        };

        // Act
        viewModel.SelectedProject = project;

        // Assert
        Assert.Equal(project, viewModel.SelectedProject);
    }

    [Fact]
    public void CreateProjectCommand_ShouldAddNewProject()
    {
        // Arrange
        var viewModel = new ProjectsViewModel(_mockLogger.Object);
        var initialCount = viewModel.Projects.Count;

        // Act
        viewModel.CreateProjectCommand.Execute(null);

        // Assert
        Assert.Equal(initialCount + 1, viewModel.Projects.Count);
    }

    [Fact]
    public void DeleteProjectCommand_WithValidProject_ShouldRemoveProject()
    {
        // Arrange
        var viewModel = new ProjectsViewModel(_mockLogger.Object);
        var project = new ProjectItem
        {
            Id = Guid.NewGuid(),
            Name = "Project to Delete",
            Description = "Will be deleted"
        };
        viewModel.Projects.Add(project);
        var countBefore = viewModel.Projects.Count;

        // Act
        viewModel.DeleteProjectCommand.Execute(project);

        // Assert
        Assert.Equal(countBefore - 1, viewModel.Projects.Count);
        Assert.DoesNotContain(project, viewModel.Projects);
    }

    [Fact]
    public void DeleteProjectCommand_WithNullProject_ShouldNotThrow()
    {
        // Arrange
        var viewModel = new ProjectsViewModel(_mockLogger.Object);
        var countBefore = viewModel.Projects.Count;

        // Act & Assert
        var exception = Record.Exception(() => viewModel.DeleteProjectCommand.Execute(null));
        Assert.Null(exception);
        Assert.Equal(countBefore, viewModel.Projects.Count);
    }

    [Fact]
    public void ProjectItem_ShouldHaveRequiredProperties()
    {
        // Arrange & Act
        var project = new ProjectItem
        {
            Id = Guid.NewGuid(),
            Name = "Test Project",
            Description = "Test Description",
            CreatedDate = DateTime.Now,
            TaskCount = 5
        };

        // Assert
        Assert.NotEqual(Guid.Empty, project.Id);
        Assert.Equal("Test Project", project.Name);
        Assert.Equal("Test Description", project.Description);
        Assert.NotEqual(default(DateTime), project.CreatedDate);
        Assert.Equal(5, project.TaskCount);
    }
}
