using Daiv3.Knowledge;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Daiv3.App.Maui.ViewModels;

/// <summary>
/// ViewModel for the Indexing Status page.
/// Implements CT-REQ-005: Dashboard SHALL display indexing progress, file browser, per-file status.
/// </summary>
public sealed class IndexingViewModel : BaseViewModel, IAsyncDisposable
{
    private readonly ILogger<IndexingViewModel> _logger;
    private readonly IIndexingStatusService _indexingStatusService;
    private readonly HashSet<string> _expandedDirectories = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _viewLifetimeCts;

    // Overall statistics
    private int _totalIndexed;
    private int _totalNotIndexed;
    private int _totalDiscovered;
    private int _totalErrors;
    private int _totalInProgress;
    private int _totalWarnings;
    private long _totalStorageBytes;
    private bool _isWatcherActive;
    private string _lastScanTime = "Never";
    private string _lastScanDuration = "N/A";

    // Orchestration statistics
    private int _filesProcessed;
    private int _filesDeleted;
    private int _processingErrors;

    // File list and filtering
    private FileIndexInfo? _selectedFile;
    private ObservableCollection<FileIndexInfo> _files = [];
    private ObservableCollection<FileIndexInfo> _filteredFiles = [];
    private ObservableCollection<string> _availableFormats = [];
    private ObservableCollection<DirectoryBrowserGroup> _directoryGroups = [];
    private string _searchText = string.Empty;
    private FileIndexingStatus? _statusFilter;
    private string? _formatFilter;

    public IndexingViewModel(
        ILogger<IndexingViewModel> logger,
        IIndexingStatusService indexingStatusService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _indexingStatusService = indexingStatusService ?? throw new ArgumentNullException(nameof(indexingStatusService));
        Title = "Indexing Status";

        RefreshCommand = new Command(async () => await RefreshAsync().ConfigureAwait(false));
        ClearFiltersCommand = new Command(async () => await ClearFiltersAsync().ConfigureAwait(false));
        SetStatusFilterCommand = new Command<FileIndexingStatus>(async status =>
        {
            StatusFilter = status;
            await ApplyFiltersAsync().ConfigureAwait(false);
        });
        ToggleDirectoryCommand = new Command<string>(async directoryPath =>
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                return;
            }

            if (_expandedDirectories.Contains(directoryPath))
            {
                _expandedDirectories.Remove(directoryPath);
            }
            else
            {
                _expandedDirectories.Add(directoryPath);
            }

            await RebuildDirectoryGroupsAsync().ConfigureAwait(false);
        });
        SelectFileCommand = new Command<FileIndexInfo>(file => SelectedFile = file);

        _logger.LogInformation("IndexingViewModel initialized");
    }

    #region Commands

    public ICommand RefreshCommand { get; }

    public ICommand ClearFiltersCommand { get; }

    public ICommand SetStatusFilterCommand { get; }

    public ICommand ToggleDirectoryCommand { get; }

    public ICommand SelectFileCommand { get; }

    #endregion

    #region Properties

    public int TotalIndexed
    {
        get => _totalIndexed;
        set
        {
            if (SetProperty(ref _totalIndexed, value))
            {
                OnPropertyChanged(nameof(IndexingProgress));
            }
        }
    }

    public int TotalNotIndexed
    {
        get => _totalNotIndexed;
        set
        {
            if (SetProperty(ref _totalNotIndexed, value))
            {
                OnPropertyChanged(nameof(IndexingProgress));
            }
        }
    }

    public int TotalDiscovered
    {
        get => _totalDiscovered;
        set
        {
            if (SetProperty(ref _totalDiscovered, value))
            {
                OnPropertyChanged(nameof(IndexingProgress));
            }
        }
    }

    public int TotalErrors
    {
        get => _totalErrors;
        set
        {
            if (SetProperty(ref _totalErrors, value))
            {
                OnPropertyChanged(nameof(HasErrorIndicator));
                OnPropertyChanged(nameof(ScanStatus));
            }
        }
    }

    public int TotalInProgress
    {
        get => _totalInProgress;
        set
        {
            if (SetProperty(ref _totalInProgress, value))
            {
                OnPropertyChanged(nameof(ScanStatus));
            }
        }
    }

    public int TotalWarnings
    {
        get => _totalWarnings;
        set
        {
            if (SetProperty(ref _totalWarnings, value))
            {
                OnPropertyChanged(nameof(HasErrorIndicator));
                OnPropertyChanged(nameof(ScanStatus));
            }
        }
    }

    public long TotalStorageBytes
    {
        get => _totalStorageBytes;
        set => SetProperty(ref _totalStorageBytes, value);
    }

    public string TotalStorageFormatted
    {
        get
        {
            const long kb = 1024;
            const long mb = kb * 1024;
            const long gb = mb * 1024;

            if (TotalStorageBytes >= gb)
            {
                return $"{TotalStorageBytes / (double)gb:F2} GB";
            }

            if (TotalStorageBytes >= mb)
            {
                return $"{TotalStorageBytes / (double)mb:F2} MB";
            }

            if (TotalStorageBytes >= kb)
            {
                return $"{TotalStorageBytes / (double)kb:F2} KB";
            }

            return $"{TotalStorageBytes} bytes";
        }
    }

    public bool IsWatcherActive
    {
        get => _isWatcherActive;
        set
        {
            if (SetProperty(ref _isWatcherActive, value))
            {
                OnPropertyChanged(nameof(ScanStatus));
                OnPropertyChanged(nameof(WatcherStatusText));
            }
        }
    }

    public string LastScanTime
    {
        get => _lastScanTime;
        set => SetProperty(ref _lastScanTime, value);
    }

    public string LastScanDuration
    {
        get => _lastScanDuration;
        set => SetProperty(ref _lastScanDuration, value);
    }

    public int FilesProcessed
    {
        get => _filesProcessed;
        set => SetProperty(ref _filesProcessed, value);
    }

    public int FilesDeleted
    {
        get => _filesDeleted;
        set => SetProperty(ref _filesDeleted, value);
    }

    public int ProcessingErrors
    {
        get => _processingErrors;
        set => SetProperty(ref _processingErrors, value);
    }

    public ObservableCollection<FileIndexInfo> Files
    {
        get => _files;
        set => SetProperty(ref _files, value);
    }

    public ObservableCollection<FileIndexInfo> FilteredFiles
    {
        get => _filteredFiles;
        set => SetProperty(ref _filteredFiles, value);
    }

    public ObservableCollection<string> AvailableFormats
    {
        get => _availableFormats;
        set => SetProperty(ref _availableFormats, value);
    }

    public ObservableCollection<DirectoryBrowserGroup> DirectoryGroups
    {
        get => _directoryGroups;
        set => SetProperty(ref _directoryGroups, value);
    }

    public FileIndexInfo? SelectedFile
    {
        get => _selectedFile;
        set
        {
            if (SetProperty(ref _selectedFile, value))
            {
                OnPropertyChanged(nameof(SelectedFileIndexedAtDisplay));
                OnPropertyChanged(nameof(SelectedFileLastModifiedDisplay));
                OnPropertyChanged(nameof(SelectedFileEmbeddingStatus));
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                _ = ApplyFiltersAsync();
            }
        }
    }

    public FileIndexingStatus? StatusFilter
    {
        get => _statusFilter;
        set
        {
            if (SetProperty(ref _statusFilter, value))
            {
                _ = ApplyFiltersAsync();
            }
        }
    }

    public string? FormatFilter
    {
        get => _formatFilter;
        set
        {
            if (SetProperty(ref _formatFilter, value))
            {
                _ = ApplyFiltersAsync();
            }
        }
    }

    public double IndexingProgress
    {
        get
        {
            if (TotalDiscovered <= 0)
            {
                return 0;
            }

            return Math.Clamp(TotalIndexed / (double)TotalDiscovered, 0, 1);
        }
    }

    public bool HasErrorIndicator => TotalErrors > 0 || TotalWarnings > 0 || ProcessingErrors > 0;

    public string ScanStatus
    {
        get
        {
            if (TotalInProgress > 0)
            {
                return "Active";
            }

            if (HasErrorIndicator)
            {
                return "Warning";
            }

            return IsWatcherActive ? "Idle" : "Paused";
        }
    }

    public string WatcherStatusText => IsWatcherActive ? "Watcher: Active" : "Watcher: Inactive";

    public string SelectedFileIndexedAtDisplay => FormatUnixTimestamp(SelectedFile?.IndexedAt);

    public string SelectedFileLastModifiedDisplay => FormatUnixTimestamp(SelectedFile?.LastModified);

    public string SelectedFileEmbeddingStatus
    {
        get
        {
            if (SelectedFile == null)
            {
                return "N/A";
            }

            if (SelectedFile.HasTier1Embedding && SelectedFile.HasTier2Embedding)
            {
                return "Tier 1 + Tier 2";
            }

            if (SelectedFile.HasTier1Embedding)
            {
                return "Tier 1 only";
            }

            if (SelectedFile.HasTier2Embedding)
            {
                return "Tier 2 only";
            }

            return "Not embedded";
        }
    }

    #endregion

    public async Task InitializeAsync()
    {
        if (IsBusy)
        {
            return;
        }

        _logger.LogInformation("Initializing indexing view");
        IsBusy = true;

        try
        {
            _viewLifetimeCts?.Dispose();
            _viewLifetimeCts = new CancellationTokenSource();

            await LoadDataAsync(_viewLifetimeCts.Token).ConfigureAwait(false);

            _logger.LogInformation("Indexing view initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing indexing view");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadDataAsync(CancellationToken ct)
    {
        _logger.LogDebug("Loading indexing data");

        var stats = await _indexingStatusService.GetIndexingStatisticsAsync(ct).ConfigureAwait(false);
        var files = await _indexingStatusService.GetAllFilesAsync(ct).ConfigureAwait(false);

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            TotalIndexed = stats.TotalIndexed;
            TotalNotIndexed = stats.TotalNotIndexed;
            TotalDiscovered = stats.TotalDiscovered;
            TotalErrors = stats.TotalErrors;
            TotalInProgress = stats.TotalInProgress;
            TotalWarnings = stats.TotalWarnings;
            TotalStorageBytes = stats.TotalEmbeddingStorageBytes;
            IsWatcherActive = stats.IsWatcherActive;
            LastScanTime = FormatUnixTimestamp(stats.LastScanTime);
            LastScanDuration = stats.LastScanDurationMs.HasValue
                ? $"{stats.LastScanDurationMs.Value} ms"
                : "N/A";

            FilesProcessed = stats.OrchestrationStats.FilesProcessed;
            FilesDeleted = stats.OrchestrationStats.FilesDeleted;
            ProcessingErrors = stats.OrchestrationStats.ProcessingErrors;

            OnPropertyChanged(nameof(TotalStorageFormatted));
            OnPropertyChanged(nameof(IndexingProgress));
            OnPropertyChanged(nameof(HasErrorIndicator));
            OnPropertyChanged(nameof(ScanStatus));

            Files.Clear();
            foreach (var file in files)
            {
                Files.Add(file);
            }

            RebuildAvailableFormats();
            await ApplyFiltersAsync().ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    private void RebuildAvailableFormats()
    {
        var formats = Files
            .Select(file => file.Format)
            .Where(format => !string.IsNullOrWhiteSpace(format))
            .Select(format => format!.ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(format => format, StringComparer.OrdinalIgnoreCase)
            .ToList();

        AvailableFormats.Clear();
        foreach (var format in formats)
        {
            AvailableFormats.Add(format);
        }
    }

    private async Task ApplyFiltersAsync()
    {
        try
        {
            var filtered = Files.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                filtered = filtered.Where(file =>
                    file.FilePath.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    file.FileName.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
            }

            if (StatusFilter.HasValue)
            {
                filtered = filtered.Where(file => file.Status == StatusFilter.Value);
            }

            if (!string.IsNullOrWhiteSpace(FormatFilter))
            {
                filtered = filtered.Where(file =>
                    file.Format != null && file.Format.Equals(FormatFilter, StringComparison.OrdinalIgnoreCase));
            }

            var filteredList = filtered
                .OrderBy(file => file.DirectoryPath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(file => file.FileName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                FilteredFiles.Clear();
                foreach (var file in filteredList)
                {
                    FilteredFiles.Add(file);
                }

                await RebuildDirectoryGroupsAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying filters");
        }
    }

    private async Task RebuildDirectoryGroupsAsync()
    {
        var groupedFiles = FilteredFiles
            .GroupBy(file => file.DirectoryPath)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var groups = new List<DirectoryBrowserGroup>();

        foreach (var group in groupedFiles)
        {
            var directoryPath = group.Key;
            var expanded = _expandedDirectories.Contains(directoryPath);
            var displayName = string.IsNullOrWhiteSpace(directoryPath)
                ? "(Root)"
                : new DirectoryInfo(directoryPath).Name;

            var fileList = group
                .OrderBy(file => file.FileName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            groups.Add(new DirectoryBrowserGroup
            {
                DirectoryPath = directoryPath,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? directoryPath : displayName,
                FileCount = fileList.Count,
                IsExpanded = expanded,
                Files = fileList
            });

            if (!_expandedDirectories.Any() && groupedFiles.Count <= 3)
            {
                _expandedDirectories.Add(directoryPath);
            }
        }

        DirectoryGroups.Clear();
        foreach (var group in groups)
        {
            DirectoryGroups.Add(group);
        }

        await Task.CompletedTask;
    }

    public async Task RefreshAsync()
    {
        if (_viewLifetimeCts == null || _viewLifetimeCts.IsCancellationRequested)
        {
            return;
        }

        await LoadDataAsync(_viewLifetimeCts.Token).ConfigureAwait(false);
    }

    public async Task ClearFiltersAsync()
    {
        SearchText = string.Empty;
        StatusFilter = null;
        FormatFilter = null;
        await ApplyFiltersAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        _logger.LogInformation("Disposing IndexingViewModel");

        _viewLifetimeCts?.Cancel();
        _viewLifetimeCts?.Dispose();
        _viewLifetimeCts = null;

        await Task.CompletedTask;
    }

    private static string FormatUnixTimestamp(long? unixSeconds)
    {
        if (!unixSeconds.HasValue || unixSeconds.Value <= 0)
        {
            return "Never";
        }

        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(unixSeconds.Value)
                .ToLocalTime()
                .ToString("yyyy-MM-dd HH:mm:ss");
        }
        catch
        {
            return "Unknown";
        }
    }
}

public sealed class DirectoryBrowserGroup
{
    public required string DirectoryPath { get; init; }

    public required string DisplayName { get; init; }

    public int FileCount { get; init; }

    public bool IsExpanded { get; init; }

    public string ExpansionGlyph => IsExpanded ? "▼" : "▶";

    public required IReadOnlyList<FileIndexInfo> Files { get; init; }
}
