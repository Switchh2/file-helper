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

    public MainPage()
    {
        InitializeComponent();

        // Subscribe after XAML initialization so all named controls are ready.
        SortSelector.SelectionChanged += SortSelector_SelectionChanged;
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
        if (!_hasLoadedFolder) return;
        FileList.ItemsSource = SortFiles(_files, SortSelector.SelectedIndex);
    }

    private async void SelectFolder_Click(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        button.IsEnabled = false;
        SortSelector.IsEnabled = false;

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
            _hasLoadedFolder = false;
            FileList.ItemsSource = null;
            EmptyState.Visibility = Visibility.Collapsed;
            FileList.Header = AppText.Get("FileList.Header");
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
            FileList.ItemsSource = files;
            FileList.Header = AppText.Format("FileCount", files.Length);
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
            button.IsEnabled = true;
            SortSelector.IsEnabled = _hasLoadedFolder;
            EmptyState.Visibility = _files.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
