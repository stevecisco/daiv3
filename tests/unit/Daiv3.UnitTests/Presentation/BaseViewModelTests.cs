using Daiv3.App.Maui.ViewModels;
using Xunit;

namespace Daiv3.UnitTests.Presentation;

/// <summary>
/// Unit tests for BaseViewModel.
/// </summary>
public class BaseViewModelTests
{
    private class TestViewModel : BaseViewModel
    {
        private string _testProperty = string.Empty;

        public string TestProperty
        {
            get => _testProperty;
            set => SetProperty(ref _testProperty, value);
        }
    }

    [Fact]
    public void IsBusy_WhenSet_ShouldRaisePropertyChanged()
    {
        // Arrange
        var viewModel = new TestViewModel();
        var propertyChangedRaised = false;
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(BaseViewModel.IsBusy))
                propertyChangedRaised = true;
        };

        // Act
        viewModel.IsBusy = true;

        // Assert
        Assert.True(viewModel.IsBusy);
        Assert.True(propertyChangedRaised);
    }

    [Fact]
    public void Title_WhenSet_ShouldRaisePropertyChanged()
    {
        // Arrange
        var viewModel = new TestViewModel();
        var propertyChangedRaised = false;
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(BaseViewModel.Title))
                propertyChangedRaised = true;
        };

        // Act
        viewModel.Title = "Test Title";

        // Assert
        Assert.Equal("Test Title", viewModel.Title);
        Assert.True(propertyChangedRaised);
    }

    [Fact]
    public void SetProperty_WithSameValue_ShouldNotRaisePropertyChanged()
    {
        // Arrange
        var viewModel = new TestViewModel();
        viewModel.TestProperty = "Initial";
        var propertyChangedCount = 0;
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(TestViewModel.TestProperty))
                propertyChangedCount++;
        };

        // Act
        viewModel.TestProperty = "Initial";

        // Assert
        Assert.Equal(0, propertyChangedCount);
    }

    [Fact]
    public void SetProperty_WithDifferentValue_ShouldRaisePropertyChanged()
    {
        // Arrange
        var viewModel = new TestViewModel();
        viewModel.TestProperty = "Initial";
        var propertyChangedRaised = false;
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(TestViewModel.TestProperty))
                propertyChangedRaised = true;
        };

        // Act
        viewModel.TestProperty = "Changed";

        // Assert
        Assert.Equal("Changed", viewModel.TestProperty);
        Assert.True(propertyChangedRaised);
    }
}
