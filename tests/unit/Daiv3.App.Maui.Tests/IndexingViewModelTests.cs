using Daiv3.App.Maui.ViewModels;
using Daiv3.Knowledge;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Daiv3.App.Maui.Tests;

public sealed class IndexingViewModelTests
{
    private readonly Mock<ILogger<IndexingViewModel>> _mockLogger = new();
    private readonly Mock<IIndexingStatusService> _mockStatusService = new();

    [Fact]
    public void Constructor_InitializesCommandsAndTitle()
    {
        var viewModel = new IndexingViewModel(_mockLogger.Object, _mockStatusService.Object);

        Assert.Equal("Indexing Status", viewModel.Title);
        Assert.NotNull(viewModel.RefreshCommand);
        Assert.NotNull(viewModel.ClearFiltersCommand);
        Assert.NotNull(viewModel.SetStatusFilterCommand);
        Assert.NotNull(viewModel.ToggleDirectoryCommand);
        Assert.NotNull(viewModel.SelectFileCommand);
    }

    [Fact]
    public void ProgressAndStatus_ReflectAssignedCounters()
    {
        var viewModel = new IndexingViewModel(_mockLogger.Object, _mockStatusService.Object)
        {
            TotalIndexed = 4,
            TotalDiscovered = 8,
            TotalErrors = 1,
            TotalWarnings = 1,
            TotalInProgress = 0,
            IsWatcherActive = true
        };

        Assert.Equal(0.5, viewModel.IndexingProgress, 3);
        Assert.True(viewModel.HasErrorIndicator);
        Assert.Equal("Warning", viewModel.ScanStatus);
    }

    [Fact]
    public void SelectedFile_ExposesDetailDisplayFields()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var viewModel = new IndexingViewModel(_mockLogger.Object, _mockStatusService.Object);

        viewModel.SelectedFile = new FileIndexInfo
        {
            DocId = "doc-x",
            FilePath = @"C:\docs\x.txt",
            FileName = "x.txt",
            DirectoryPath = @"C:\docs",
            Status = FileIndexingStatus.Indexed,
            IndexedAt = now,
            LastModified = now,
            SizeBytes = 256,
            Format = "txt",
            ChunkCount = 2,
            EmbeddingDimension = 768,
            TopicSummary = "summary",
            IsSensitive = true,
            IsShareable = false,
            MachineLocation = "node-a",
            HasTier1Embedding = true,
            HasTier2Embedding = true
        };

        Assert.Equal("Tier 1 + Tier 2", viewModel.SelectedFileEmbeddingStatus);
        Assert.NotEqual("Never", viewModel.SelectedFileIndexedAtDisplay);
        Assert.NotEqual("Never", viewModel.SelectedFileLastModifiedDisplay);
    }
}
