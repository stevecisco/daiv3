using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using Daiv3.Knowledge;

namespace Daiv3.App.Maui.ViewModels;

/// <summary>
/// ViewModel for the Indexing Status page.
/// Implements CT-REQ-005: Dashboard SHALL display indexing progress, file browser, per-file status.
/// Displays indexing progress, file browser with per-file status indicators, and statistics.
/// </summary>
public sealed class IndexingViewModel : BaseViewModel, IAsyncDisposable
{
    private readonly ILogger<IndexingViewModel> _logger;
    private readonly IIndexingStatusService _indexingStatusService;
    private CancellationTokenSource? _viewLifetimeCts;

    // Overall statistics
    private int _totalIndexed;
    private int _totalNotIndexed;
    private int _totalErrors;
    private int _totalInProgress;
    private int _totalWarnings;
    private long _totalStorageBytes;
    private bool _isWatcherActive;
    private string _lastScanTime = "Never";

    // Orchestration statistics
    private int _filesProcessed;
    private int _filesDeleted;
    private int _processingErrors;

    // File list and filtering
    private FileIndexInfo? _selectedFile;
    private ObservableCollection<FileIndexInfo> _files = [];
    private ObservableCollection<FileIndexInfo> _filteredFiles = [];
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
        _logger.LogInformation("IndexingViewModel initialized");
    }

    #region Properties

    /// <summary>
    /// Gets or sets the total number of indexed documents.
    /// </summary>
    public int TotalIndexed
    {
        get => _totalIndexed;
        set => SetProperty(ref _totalIndexed, value);
    }

    /// <summary>
    /// Gets or sets the total number of documents not yet indexed.
    /// </summary>
    public int TotalNotIndexed
    {
        get => _totalNotIndexed;
        set => SetProperty(ref _totalNotIndexed, value);
    }

    /// <summary>
    /// Gets or sets the total number of documents with errors.
    /// </summary>
    public int TotalErrors
    {
        get => _totalErrors;
        set => SetProperty(ref _totalErrors, value);
    }

    /// <summary>
    /// Gets or sets the total number of documents currently being processed.
    /// </summary>
    public int TotalInProgress
    {
        get => _totalInProgress;
        set => SetProperty(ref _totalInProgress, value);
    }

    /// <summary>
    /// Gets or sets the total number of documents with warnings.
    /// </summary>
    public int TotalWarnings
    {
        get => _totalWarnings;
        set => SetProperty(ref _totalWarnings, value);
    }

    /// <summary>
    /// Gets or sets the total storage used by embeddings (bytes).
    /// </summary>
    public long TotalStorageBytes
    {
        get => _totalStorageBytes;
        set => SetProperty(ref _totalStorageBytes, value);
    }

    /// <summary>
    /// Gets formatted storage size string (KB, MB, GB).
    /// </summary>
    public string TotalStorageFormatted
    {
        get
        {
            const long KB = 1024;
            const long MB = KB * 1024;
            const long GB = MB * 1024;

            if (TotalStorageBytes >= GB)
                return $"{TotalStorageBytes / (double)GB:F2} GB";
            if (TotalStorageBytes >= MB)
                return $"{TotalStorageBytes / (double)MB:F2} MB";
            if (TotalStorageBytes >= KB)
                return $"{TotalStorageBytes / (double)KB:F2} KB";
            return $"{TotalStorageBytes} bytes";
        }
    }

    /// <summary>
    /// Gets or sets whether the file system watcher is active.
    /// </summary>
    public bool IsWatcherActive
    {
        get => _isWatcherActive;
        set => SetProperty(ref _isWatcherActive, value);
    }

    /// <summary>
    /// Gets or sets the last scan time.
    /// </summary>
    public string LastScanTime
    {
        get => _lastScanTime;
        set => SetProperty(ref _lastScanTime, value);
    }

    /// <summary>
    /// Gets or sets the number of files processed (from orchestration stats).
    /// </summary>
    public int FilesProcessed
    {
        get => _filesProcessed;
        set => SetProperty(ref _filesProcessed, value);
    }

    /// <summary>
    /// Gets or sets the number of files deleted (from orchestration stats).
    /// </summary>
    public int FilesDeleted
    {
        get => _filesDeleted;
        set => SetProperty(ref _filesDeleted, value);
    }

    /// <summary>
    /// Gets or sets the number of processing errors (from orchestration stats).
    /// </summary>
    public int ProcessingErrors
    {
        get => _processingErrors;
        set => SetProperty(ref _processingErrors, value);
    }

    /// <summary>
    /// Gets the observable collection of all files.
    /// </summary>
    public ObservableCollection<FileIndexInfo> Files
    {
        get => _files;
        set => SetProperty(ref _files, value);
    }

    /// <summary>
    /// Gets the observable collection of filtered files.
    /// </summary>
    public ObservableCollection<FileIndexInfo> FilteredFiles
    {
        get => _filteredFiles;
        set => SetProperty(ref _filteredFiles, value);
    }

    /// <summary>
    /// Gets or sets the selected file for detail view.
    /// </summary>
    public FileIndexInfo? SelectedFile
    {
        get => _selectedFile;
        set => SetProperty(ref _selectedFile, value);
    }

    /// <summary>
    /// Gets or sets the search text for filtering files.
    /// </summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                _ = Task.Run(() => ApplyFiltersAsync());
            }
        }
    }

    /// <summary>
    /// Gets or sets the status filter.
    /// </summary>
    public FileIndexingStatus? StatusFilter
    {
        get => _statusFilter;
        set
        {
            if (SetProperty(ref _statusFilter, value))
            {
                _ = Task.Run(() => ApplyFiltersAsync());
            }
        }
    }

    /// <summary>
    /// Gets or sets the format filter.
    /// </summary>
    public string? FormatFilter
    {
        get => _formatFilter;
        set
        {
            if (SetProperty(ref _formatFilter, value))
            {
                _ = Task.Run(() => ApplyFiltersAsync());
            }
        }
    }

    #endregion

    /// <summary>
    /// Initializes the view and loads indexing data.
    /// Called when the view becomes visible.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (IsBusy)
            return;

        _logger.LogInformation("Initializing indexing view");
        IsBusy = true;

        try
        {
            // Create cancellation token for this view's lifetime
            _viewLifetimeCts?.Dispose();
            _viewLifetimeCts = new CancellationTokenSource();

            // Load initial data
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

    /// <summary>
    /// Loads indexing statistics and file list.
    /// </summary>
    private async Task LoadDataAsync(CancellationToken ct)
    {
        try
        {
            _logger.LogDebug("Loading indexing data");

            // Load statistics
            var stats = await _indexingStatusService.GetIndexingStatisticsAsync(ct).ConfigureAwait(false);
            
            // Update properties on UI thread
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                TotalIndexed = stats.TotalIndexed;
                TotalNotIndexed = stats.TotalNotIndexed;
                TotalErrors = stats.TotalErrors;
                TotalInProgress = stats.TotalInProgress;
                TotalWarnings = stats.TotalWarnings;
                TotalStorageBytes = stats.TotalEmbeddingStorageBytes;
                IsWatcherActive = stats.IsWatcherActive;
                
                if (stats.LastScanTime.HasValue)
                {
                    var lastScan = DateTimeOffset.FromUnixTimeSeconds(stats.LastScanTime.Value);
                    LastScanTime = lastScan.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
                }
                else
                {
                    LastScanTime = "Never";
                }

                FilesProcessed = stats.OrchestrationStats.FilesProcessed;
                FilesDeleted = stats.OrchestrationStats.FilesDeleted;
                ProcessingErrors = stats.OrchestrationStats.ProcessingErrors;

                OnPropertyChanged(nameof(TotalStorageFormatted));

                _logger.LogDebug(
                    "Statistics updated: {Indexed} indexed, {Errors} errors, {Pending} pending",
                    TotalIndexed, TotalErrors, TotalNotIndexed);
            }).ConfigureAwait(false);

            // Load all files
            var files = await _indexingStatusService.GetAllFilesAsync(ct).ConfigureAwait(false);
            
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Files.Clear();
                foreach (var file in files)
                {
                    Files.Add(file);
                }

                _logger.LogDebug("Loaded {Count} files", Files.Count);
            }).ConfigureAwait(false);

            // Apply filters
            await ApplyFiltersAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading indexing data");
            throw;
        }
    }

    /// <summary>
    /// Applies current filters to the file list.
    /// </summary>
    private async Task ApplyFiltersAsync()
    {
        try
        {
            var filtered = Files.AsEnumerable();

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                filtered = filtered.Where(f => f.FilePath.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
            }

            // Apply status filter
            if (StatusFilter.HasValue)
            {
                filtered = filtered.Where(f => f.Status == StatusFilter.Value);
            }

            // Apply format filter
            if (!string.IsNullOrWhiteSpace(FormatFilter))
            {
                filtered = filtered.Where(f => 
                    f.Format != null && 
                    f.Format.Equals(FormatFilter, StringComparison.OrdinalIgnoreCase));
            }

            var filteredList = filtered.ToList();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                FilteredFiles.Clear();
                foreach (var file in filteredList)
                {
                    FilteredFiles.Add(file);
                }

                _logger.LogDebug("Filtered to {Count} files", FilteredFiles.Count);
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying filters");
        }
    }

    /// <summary>
    /// Refreshes the indexing data.
    /// </summary>
    public async Task RefreshAsync()
    {
        if (_viewLifetimeCts == null || _viewLifetimeCts.IsCancellationRequested)
            return;

        await LoadDataAsync(_viewLifetimeCts.Token).ConfigureAwait(false);
    }

    /// <summary>
    /// Clears all filters.
    /// </summary>
    public async Task ClearFiltersAsync()
    {
        SearchText = string.Empty;
        StatusFilter = null;
        FormatFilter = null;
        await ApplyFiltersAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Cleans up resources when the view is disposed.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        _logger.LogInformation("Disposing IndexingViewModel");

        _viewLifetimeCts?.Cancel();
        _viewLifetimeCts?.Dispose();
        _viewLifetimeCts = null;

        await Task.CompletedTask;
    }
}
