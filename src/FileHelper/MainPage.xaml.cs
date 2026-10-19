using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.Windows.Storage.Pickers;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace FileHelper;

/// <summary>
/// The main content page displayed inside the application window.
/// Add your UI logic, event handlers, and data binding here.
/// </summary>
public sealed partial class MainPage : Page
{
    private FileItem[] _files = [];
    private bool _hasLoadedFolder;
    private bool _isLoadingFolder;
    private bool _isUpdatingSelection;
    private bool _isPreviewOpen;

    public MainPage()
    {
        InitializeComponent();

        // Subscribe after XAML initialization so all named controls are ready.
        SortSelector.SelectionChanged += SortSelector_SelectionChanged;
        CategorySelector.SelectionChanged += CategorySelector_SelectionChanged;
        FileList.SelectionChanged += FileList_SelectionChanged;
        UpdateSelectionSummary();
    }

    private static FileItem[] SortFiles(FileItem[] files, int sortIndex)
    {
        // Sort raw values, not formatted strings such as "1.5 MiB".
        // Descending nullable values put unavailable metadata at the end.
        var ordered = sortIndex switch
        {
            1 => files.OrderByDescending(file => file.SizeBytes),
            2 => files.OrderByDescending(file => file.ModifiedAt),
            _ => files.OrderBy(file => file.Name, System.StringComparer.CurrentCultureIgnoreCase)
        };
        return ordered
            .ThenBy(file => file.Name, System.StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(file => file.Name, System.StringComparer.Ordinal)
            .ToArray();
    }

    private void SortSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_hasLoadedFolder || _isLoadingFolder || _isPreviewOpen) return;
        ApplyFileView(clearSelection: false);
    }

    private void CategorySelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_hasLoadedFolder || _isLoadingFolder || _isPreviewOpen) return;
        // Hidden selections must never be transmitted by accident.
        _lastAnalysis = null;
        AnalysisStatus.Text = string.Empty;
        ApplyFileView(clearSelection: true);
    }

    private void ApplyFileView(bool clearSelection)
    {
        var selected = clearSelection ? [] : FileList.SelectedItems.Cast<FileItem>().ToArray();
        var visible = SortFiles(_files.Where(file => FileCategories.Matches(file.Name, CategorySelector.SelectedIndex)).ToArray(),
            SortSelector.SelectedIndex);
        _isUpdatingSelection = true;
        try
        {
            FileList.ItemsSource = visible;
            foreach (var file in selected.Where(visible.Contains)) FileList.SelectedItems.Add(file);
            FileList.Header = CategorySelector.SelectedIndex == 0
                ? AppText.Format("FileCount", visible.Length)
                : AppText.Format("FilteredFileCount", visible.Length, _files.Length);
            EmptyState.Visibility = visible.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            var noMatches = _files.Length > 0 && visible.Length == 0;
            EmptyTitle.Text = AppText.Get(noMatches ? "FilterEmptyTitle" : "EmptyListTitle.Text");
            EmptyHint.Text = AppText.Get(noMatches ? "FilterEmptyHint" : "EmptyListHint.Text");
        }
        finally
        {
            _isUpdatingSelection = false;
            UpdateSelectionSummary();
        }
    }

    private void FileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isUpdatingSelection) UpdateSelectionSummary();
    }

    private void UpdateSelectionSummary()
    {
        var selected = FileList.SelectedItems.Cast<FileItem>().ToHashSet();
        foreach (var file in _files) file.IsIncluded = selected.Contains(file);
        var count = FileList.SelectedItems.Count;
        var visibleCount = (FileList.ItemsSource as FileItem[])?.Length ?? 0;
        SelectionSummary.Text = count > AnalysisPreview.MaxFiles
            ? AppText.Format("PreviewLimitExceeded", count, AnalysisPreview.MaxFiles)
            : AppText.Format("InclusionSummary", count, visibleCount - count, AnalysisPreview.MaxFiles);
        PreviewButton.IsEnabled = !_isLoadingFolder && !_isPreviewOpen && count > 0 && count <= AnalysisPreview.MaxFiles;
        ClearSelectionButton.IsEnabled = !_isLoadingFolder && !_isPreviewOpen && count > 0;
        SelectFolderAction.IsEnabled = !_isLoadingFolder && !_isPreviewOpen;
        SelectFilesAction.IsEnabled = !_isLoadingFolder && !_isPreviewOpen;
        SortSelector.IsEnabled = _hasLoadedFolder && !_isLoadingFolder && !_isPreviewOpen;
        CategorySelector.IsEnabled = _hasLoadedFolder && !_isLoadingFolder && !_isPreviewOpen;
        FileList.IsEnabled = !_isLoadingFolder && !_isPreviewOpen;
        ResultsButton.IsEnabled = !_isLoadingFolder && !_isPreviewOpen && _lastAnalysis is not null;
    }

    private void FileIncluded_Click(object sender, RoutedEventArgs e)
    {
        // Click is user-driven; do not use Checked/Unchecked, which also fire
        // while bindings and recycled rows are being initialized.
        if (_isLoadingFolder || _isPreviewOpen || _isUpdatingSelection) return;
        if (sender is not CheckBox { Tag: FileItem file } checkbox ||
            FileList.ItemsSource is not FileItem[] visible || !visible.Contains(file)) return;
        if (checkbox.IsChecked == true)
        {
            if (!FileList.SelectedItems.Contains(file)) FileList.SelectedItems.Add(file);
        }
        else FileList.SelectedItems.Remove(file);
        UpdateSelectionSummary();
    }

    private void ClearSelection_Click(object sender, RoutedEventArgs e)
    {
        FileList.SelectedItems.Clear();
        UpdateSelectionSummary();
    }

    private async void Preview_Click(object sender, RoutedEventArgs e)
    {
        if (!PreviewButton.IsEnabled) return;
        _isPreviewOpen = true;
        UpdateSelectionSummary();
        try
        {
            // Use visible order and only the checked objects from the current folder.
            var selected = FileList.SelectedItems.Cast<FileItem>().ToHashSet();
            if (FileList.ItemsSource is not FileItem[] currentFiles)
                throw new System.InvalidOperationException("No current file list.");
            var files = currentFiles.Where(selected.Contains).ToArray();
            var payload = AnalysisPreview.CreateJson(files);
            var content = new StackPanel { Spacing = 12 };
            content.Children.Add(new TextBlock
            {
                Text = AppText.Get("PreviewPrivacyNotice"),
                TextWrapping = TextWrapping.Wrap
            });
            content.Children.Add(new TextBlock
            {
                Text = AppText.Get("PreviewFieldsNotice"),
                TextWrapping = TextWrapping.Wrap
            });
            content.Children.Add(new TextBlock
            {
                Text = AppText.Format("PreviewPayloadHeader", files.Length),
                TextWrapping = TextWrapping.Wrap
            });
            var fileDetails = new StackPanel { Spacing = 16 };
            foreach (var file in files)
            {
                var entry = new StackPanel { Spacing = 4 };
                entry.Children.Add(new TextBlock
                {
                    Text = file.Name, FontSize = 16,
                    TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true
                });
                var extension = Path.GetExtension(file.Name);
                entry.Children.Add(new TextBlock
                {
                    Text = AppText.Format("PreviewFileDetails",
                        string.IsNullOrEmpty(extension) ? AppText.Get("PreviewNoExtension") : extension,
                        file.SizeText,
                        file.ModifiedAt?.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.CurrentCulture) ?? "—"),
                    FontSize = 12, TextWrapping = TextWrapping.Wrap
                });
                fileDetails.Children.Add(entry);
            }
            content.Children.Add(new ScrollViewer
            {
                Content = fileDetails,
                Height = System.Math.Min(180, files.Length * 100),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            });
            content.Children.Add(new Expander
            {
                Header = AppText.Get("PreviewRawJson"),
                IsExpanded = false,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Content = new TextBox
                {
                    Text = payload, IsReadOnly = true, AcceptsReturn = true,
                    TextWrapping = TextWrapping.Wrap, Height = 160
                }
            });
            var keyInput = new PasswordBox { Header = AppText.Get("AiKeyHeader") };
            content.Children.Add(keyInput);
            content.Children.Add(new TextBlock { Text = AppText.Get("AiConsent"), TextWrapping = TextWrapping.Wrap });
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = AppText.Get("PreviewDialogTitle"),
                Content = new ScrollViewer
                {
                    Content = content, MaxHeight = 480,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
                },
                PrimaryButtonText = AppText.Get("AiSend"),
                CloseButtonText = AppText.Get("PreviewClose"),
                DefaultButton = ContentDialogButton.Close
            };
            var decision = await dialog.ShowAsync();
            var key = keyInput.Password;
            keyInput.Password = string.Empty;
            if (decision != ContentDialogResult.Primary) return;
            if (string.IsNullOrWhiteSpace(key)) key = System.Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (string.IsNullOrWhiteSpace(key))
            {
                AnalysisStatus.Text = AppText.Get("AiMissingKey");
                return;
            }
            await AnalyzeSelectionAsync(files, payload, key);
            key = string.Empty;
            await ShowAnalysisAsync();
        }
        catch (System.Exception)
        {
            // Do not log the selected filenames or payload on failure.
            AnalysisStatus.Text = AppText.Get("PreviewFailed");
        }
        finally
        {
            _isPreviewOpen = false;
            UpdateSelectionSummary();
        }
    }

    private async void SelectFiles_Click(object sender, RoutedEventArgs e)
    {
        if (_isPreviewOpen || _isLoadingFolder) return;
        _isLoadingFolder = true;
        UpdateSelectionSummary();
        try
        {
            var picker = new FileOpenPicker(XamlRoot.ContentIslandEnvironment.AppWindowId)
            {
                CommitButtonText = AppText.Get("PickFilesCommit")
            };
            // No type restriction: users can inspect and choose any extension.
            var picked = await picker.PickMultipleFilesAsync();
            if (picked.Count == 0) return; // Keep the current list, selection and results.

            var paths = picked.Select(file => file.Path).Distinct(System.StringComparer.OrdinalIgnoreCase).ToArray();
            var files = await Task.Run(() => paths.Select(path => new FileItem(new FileInfo(path))).ToArray());
            // Commit only after loading: failures must not replace the previous source.
            _files = files;
            _hasLoadedFolder = true;
            _lastAnalysis = null;
            AnalysisStatus.Text = string.Empty;
            CategorySelector.SelectedIndex = 0; // Do not hide an explicitly picked file.
            ApplyFileView(clearSelection: true);
            _isUpdatingSelection = true;
            try
            {
                foreach (var file in (FileItem[])FileList.ItemsSource!) FileList.SelectedItems.Add(file);
            }
            finally { _isUpdatingSelection = false; }
            StatusText.Text = AppText.Format("PickedFilesStatus", files.Length);
            // Selection is not consent to send; preview and Send are still required.
            // More than 20 are kept visible, but the existing analysis limit blocks Send.
        }
        catch (System.Exception)
        {
            AnalysisStatus.Text = AppText.Get("PickFilesFailed");
        }
        finally
        {
            _isLoadingFolder = false;
            UpdateSelectionSummary();
        }
    }

    private async void SelectFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_isPreviewOpen || _isLoadingFolder) return;
        var button = (Button)sender;
        _isLoadingFolder = true;
        button.IsEnabled = false;
        SortSelector.IsEnabled = false;
        FileList.IsEnabled = false;
        UpdateSelectionSummary();

        try
        {
            var windowId = XamlRoot.ContentIslandEnvironment.AppWindowId;
            var picker = new FolderPicker(windowId);
            var result = await picker.PickSingleFolderAsync();

            // Cancel leaves the previously displayed folder and files untouched.
            if (result is null)
            {
                return;
            }

            _files = [];
            _lastAnalysis = null;
            AnalysisStatus.Text = string.Empty;
            _hasLoadedFolder = false;
            FileList.SelectedItems.Clear();
            FileList.ItemsSource = null;
            EmptyState.Visibility = Visibility.Collapsed;
            FileList.Header = AppText.Get("FileList.Header");
            EmptyTitle.Text = AppText.Get("EmptyListTitle.Text");
            EmptyHint.Text = AppText.Get("EmptyListHint.Text");
            StatusText.Text = AppText.Format("LoadingFiles", result.Path);

            // Capture the control value before moving work off the UI thread.
            var sortIndex = SortSelector.SelectedIndex;
            var files = await Task.Run(() =>
                SortFiles(new DirectoryInfo(result.Path)
                    .EnumerateFiles("*", SearchOption.TopDirectoryOnly)
                    .Select(file => new FileItem(file))
                    .ToArray(), sortIndex));

            _files = files;
            _hasLoadedFolder = true;
            ApplyFileView(clearSelection: true);
            StatusText.Text = files.Length == 0
                ? AppText.Format("EmptyFolder", result.Path)
                : result.Path;
        }
        catch (UnauthorizedAccessException)
        {
            StatusText.Text = AppText.Get("FolderAccessDenied");
        }
        catch (IOException)
        {
            StatusText.Text = AppText.Get("FolderReadFailed");
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            StatusText.Text = AppText.Get("FolderOpenFailed");
        }
        finally
        {
            _isLoadingFolder = false;
            FileList.IsEnabled = true;
            button.IsEnabled = true;
            SortSelector.IsEnabled = _hasLoadedFolder;
            EmptyState.Visibility = FileList.ItemsSource is FileItem[] { Length: > 0 }
                ? Visibility.Collapsed : Visibility.Visible;
            UpdateSelectionSummary();
        }
    }
}
