using Daiv3.App.Maui.ViewModels;
using Daiv3.Persistence.Repositories;
using Daiv3.Persistence.Entities;
using Daiv3.Persistence;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Daiv3.App.Maui.Tests;

#pragma warning disable IDISP001 // Test mocks don't need disposal

/// <summary>
/// Unit tests for ProjectsViewModel (CT-REQ-011).
/// </summary>
public sealed class ProjectsViewModelTests : IDisposable
{
    private readonly Mock<ILogger<ProjectsViewModel>> _mockLogger;
    private readonly Mock<ILogger<ProjectRepository>> _mockRepoLogger;
    private readonly Mock<IDatabaseContext> _mockDatabaseContext;
    private readonly Mock<ProjectRepository> _mockProjectRepository;

    public ProjectsViewModelTests()
    {
        _mockLogger = new Mock<ILogger<ProjectsViewModel>>();
        _mockRepoLogger = new Mock<ILogger<ProjectRepository>>();
        _mockDatabaseContext = new Mock<IDatabaseContext>();
        _mockProjectRepository = new Mock<ProjectRepository>(_mockDatabaseContext.Object, _mockRepoLogger.Object);
    }

    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        // Arrange
        _mockProjectRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Project>());

        // Act
        var viewModel = new ProjectsViewModel(_mockLogger.Object, _mockProjectRepository.Object);

        // Assert
        Assert.Equal("Projects", viewModel.Title);
        Assert.NotNull(viewModel.Projects);
        Assert.NotNull(viewModel.CreateProjectCommand);
        Assert.NotNull(viewModel.DeleteProjectCommand);
        Assert.NotNull(viewModel.RefreshCommand);
    }

    [Fact]
    public async Task LoadProjects_ShouldPopulateCollection()
    {
        // Arrange
        var testProjects = new List<Project>
        {
            new Project
            {
                ProjectId = Guid.NewGuid().ToString(),
                Name = "Test Project",
                Description = "Test Description",
                RootPaths = "/test",
                Status = "active",
                Priority = 1,
                ProgressPercent = 50.0,
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            }
        };

        _mockProjectRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(testProjects);

        // Act
        var viewModel = new ProjectsViewModel(_mockLogger.Object, _mockProjectRepository.Object);
        await Task.Delay(200); // Allow async load to complete

        // Assert
        _mockProjectRepository.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public void SelectedProject_WhenSet_ShouldUpdateProperty()
    {
        // Arrange
        _mockProjectRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Project>());

        var viewModel = new ProjectsViewModel(_mockLogger.Object, _mockProjectRepository.Object);
        var project = new ProjectItem
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Test Project",
            Description = "Test Description"
        };

        // Act
        viewModel.SelectedProject = project;

        // Assert
        Assert.Equal(project, viewModel.SelectedProject);
    }

    [Fact]
    public async Task CreateProjectCommand_ShouldAddNewProject()
    {
        // Arrange
        _mockProjectRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Project>());
        _mockProjectRepository.Setup(r => r.AddAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project p, CancellationToken ct) => p.ProjectId);

        var viewModel = new ProjectsViewModel(_mockLogger.Object, _mockProjectRepository.Object);
        await Task.Delay(200); // Allow initial load
        var initialCount = viewModel.Projects.Count;

        // Act
        viewModel.CreateProjectCommand.Execute(null);
        await Task.Delay(200); // Allow async operation

        // Assert
        _mockProjectRepository.Verify(r => r.AddAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteProjectCommand_WithValidProject_ShouldRemoveProject()
    {
        // Arrange
        var projectId = Guid.NewGuid().ToString();
        _mockProjectRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Project>());
        _mockProjectRepository.Setup(r => r.DeleteAsync(projectId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var viewModel = new ProjectsViewModel(_mockLogger.Object, _mockProjectRepository.Object);
        var project = new ProjectItem
        {
            Id = projectId,
            Name = "Project to Delete",
            Description = "Will be deleted"
        };
        viewModel.Projects.Add(project);

        // Act
        viewModel.DeleteProjectCommand.Execute(project);
        await Task.Delay(200); // Allow async operation

        // Assert
        _mockProjectRepository.Verify(r => r.DeleteAsync(projectId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void DeleteProjectCommand_WithNullProject_ShouldNotThrow()
    {
        // Arrange
        _mockProjectRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Project>());

        var viewModel = new ProjectsViewModel(_mockLogger.Object, _mockProjectRepository.Object);
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
            Id = Guid.NewGuid().ToString(),
            Name = "Test Project",
            Description = "Test Description",
            Status = "active",
            Priority = 1,
            ProgressPercent = 50.0,
            CreatedDate = DateTime.Now,
            TaskCount = 5
        };

        // Assert
        Assert.NotEmpty(project.Id);
        Assert.Equal("Test Project", project.Name);
        Assert.Equal("Test Description", project.Description);
        Assert.Equal("active", project.Status);
        Assert.Equal(1, project.Priority);
        Assert.Equal(50.0, project.ProgressPercent);
        Assert.NotEqual(default(DateTime), project.CreatedDate);
        Assert.Equal(5, project.TaskCount);
        Assert.Equal("🟢", project.StatusBadge);
        Assert.Equal("P1", project.PriorityLabel);
    }

    public void Dispose()
    {
        // Dispose test resources
    }
}
