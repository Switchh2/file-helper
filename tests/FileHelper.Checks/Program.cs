using System.Reflection;
using System.Diagnostics;
using FileHelper;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.Storage.Pickers;

// Runs the actual page handler with UI/picker test doubles and real test files.
// This does NOT test WinUI rendering, scrolling, focus, or real picker behavior.
var root = Path.Combine(AppContext.BaseDirectory, "fixtures", Guid.NewGuid().ToString("N"));
var empty = Directory.CreateDirectory(Path.Combine(root, "empty")).FullName;
var many = Directory.CreateDirectory(Path.Combine(root, "many")).FullName;
var nested = Directory.CreateDirectory(Path.Combine(many, "nested")).FullName;
for (var i = 0; i < 1000; i++)
    File.WriteAllText(Path.Combine(many, $"sample-{i:D4}.txt"), "test fixture");
File.WriteAllText(Path.Combine(nested, "must-not-appear.txt"), "nested test fixture");
var small = Directory.CreateDirectory(Path.Combine(root, "small")).FullName;
File.WriteAllText(Path.Combine(small, "한글 이름.txt"), "unicode name test");

var page = new MainPage();
if (page.EmptyState.Visibility != Microsoft.UI.Xaml.Visibility.Visible)
    throw new Exception("Initial empty state must be visible");
var button = new Button();
var handler = typeof(MainPage).GetMethod("SelectFolder_Click", BindingFlags.Instance | BindingFlags.NonPublic)!;
async Task Run(string? path)
{
    FolderPicker.NextPath = path;
    handler.Invoke(page, [button, new Microsoft.UI.Xaml.RoutedEventArgs()]);
    var timeout = Stopwatch.StartNew();
    while (!button.IsEnabled && timeout.ElapsedMilliseconds < 10000)
        await Task.Delay(5);
    if (!button.IsEnabled) throw new Exception("Handler did not finish / button not restored");
}
void Check(bool ok, string label)
{
    if (!ok) throw new Exception("FAIL: " + label);
    Console.WriteLine("PASS: " + label);
}
await Run(many);
string[] ListedNames() => ((FileItem[])page.FileList.ItemsSource!).Select(file => file.Name).ToArray();
var names = ListedNames();
Check(names.Length == 1000 && names.Distinct().Count() == 1000 &&
    names.Contains("sample-0000.txt") && names.Contains("sample-0999.txt") &&
    !names.Contains("must-not-appear.txt"), "1000 files, names only, no recursive files");
Check((string?)page.FileList.Header == "파일 목록 (1000개)" && page.StatusText.Text == many,
    "Count and folder path agree with listing");
Check(page.EmptyState.Visibility == Microsoft.UI.Xaml.Visibility.Collapsed, "Empty hint hidden for populated list");
await Run(empty);
Check(ListedNames().Length == 0 &&
    (string?)page.FileList.Header == "파일 목록 (0개)" &&
    page.StatusText.Text.Contains("파일이 없습니다"), "Empty folder clears old list and shows empty message");
Check(page.EmptyState.Visibility == Microsoft.UI.Xaml.Visibility.Visible, "Empty hint visible for empty folder");
await Run(small);
Check(ListedNames().SequenceEqual(["한글 이름.txt"]),
    "Switching folder replaces list; Unicode/spaces preserved");
var oldList = page.FileList.ItemsSource;
var oldStatus = page.StatusText.Text;
var oldHeader = page.FileList.Header;
await Run(null);
Check(ReferenceEquals(oldList, page.FileList.ItemsSource) && oldStatus == page.StatusText.Text &&
    Equals(oldHeader, page.FileList.Header), "Cancel preserves list, path, and count");
await Run(Path.Combine(root, "does-not-exist"));
Check(page.FileList.ItemsSource is null && page.StatusText.Text.Contains("폴더를 읽지 못했습니다"),
    "Missing folder clears stale list and shows error; button restored");
Check(page.EmptyState.Visibility == Microsoft.UI.Xaml.Visibility.Visible, "Failed load leaves no blank unexplained list");
await Run(small);
Check(ListedNames().SequenceEqual(["한글 이름.txt"]), "Successful retry after error");
Console.WriteLine("10 list/state checks passed. UI rendering/responsiveness NOT tested by this harness.");
var sorting = Directory.CreateDirectory(Path.Combine(root, "sorting")).FullName;
string[] sortNames = ["zebra.txt", "Banana.txt", "apple.txt", "file2.txt", "file10.txt", "나무.txt", "가방.txt"];
foreach (var name in sortNames)
    File.WriteAllText(Path.Combine(sorting, name), "sorting fixture");
foreach (var culture in new[] { "ko-KR", "en-US" })
{
    System.Globalization.CultureInfo.CurrentCulture = new(culture);
    await Run(sorting);
    var sorted = ListedNames();
    Check(sorted.Length == sortNames.Length && sorted.ToHashSet().SetEquals(sortNames),
        culture + ": sorting preserves all names");
    Check(Array.IndexOf(sorted, "apple.txt") < Array.IndexOf(sorted, "Banana.txt") &&
        Array.IndexOf(sorted, "Banana.txt") < Array.IndexOf(sorted, "zebra.txt") &&
        Array.IndexOf(sorted, "가방.txt") < Array.IndexOf(sorted, "나무.txt") &&
        Array.IndexOf(sorted, "file10.txt") < Array.IndexOf(sorted, "file2.txt"),
        culture + ": case-insensitive alphabetical order, Korean order, lexical numbers");
    await Run(sorting);
    Check(sorted.SequenceEqual(ListedNames()),
        culture + ": repeat selection keeps order");
}
Check(Directory.GetFiles(sorting).Length == sortNames.Length && sortNames.All(name =>
    File.ReadAllText(Path.Combine(sorting, name)) == "sorting fixture"), "Sorting does not change test files");
Console.WriteLine("7 sorting checks passed with actual page handler and UI test doubles.");
var metadataPath = Path.Combine(root, "metadata.bin");
foreach (var example in new[] { (0, "0 B"), (1023, "1023 B"), (1024, "1 KiB"), (1536, "1.5 KiB"), (1048576, "1 MiB") })
{
    File.WriteAllBytes(metadataPath, new byte[example.Item1]);
    var item = new FileItem(new FileInfo(metadataPath));
    Check(item.SizeBytes == example.Item1 && item.SizeText == example.Item2, "Size: " + example.Item2);
}
var expectedTime = new DateTime(2026, 10, 5, 14, 30, 0);
File.SetLastWriteTime(metadataPath, expectedTime);
var metadata = new FileItem(new FileInfo(metadataPath));
Check(metadata.ModifiedAt == expectedTime && metadata.ModifiedText == expectedTime.ToString("g"),
    "Metadata preserves local modified time and uses regional date format");
var unavailable = new FileItem(new FileInfo(Path.Combine(root, "missing.bin")));
Check(unavailable.Name == "missing.bin" && unavailable.SizeText == "—" && unavailable.ModifiedText == "—",
    "Missing file keeps name and shows unavailable metadata");
Console.WriteLine("7 metadata checks passed.");
var sortOptions = Directory.CreateDirectory(Path.Combine(root, "sort-options")).FullName;
foreach (var example in new[] { ("alpha.bin", 10, 1), ("beta.bin", 2048, 2), ("gamma.bin", 2048, 3) })
{
    var path = Path.Combine(sortOptions, example.Item1);
    File.WriteAllBytes(path, new byte[example.Item2]);
    File.SetLastWriteTime(path, expectedTime.AddDays(example.Item3));
}
await Run(sortOptions);
Check(page.SortSelector.IsEnabled && ListedNames().SequenceEqual(["alpha.bin", "beta.bin", "gamma.bin"]),
    "Sort selector enabled after load; name is default");
page.SortSelector.SelectedIndex = 1;
Check(ListedNames().SequenceEqual(["beta.bin", "gamma.bin", "alpha.bin"]), "Largest first; equal sizes use name order");
page.SortSelector.SelectedIndex = 2;
Check(ListedNames().SequenceEqual(["gamma.bin", "beta.bin", "alpha.bin"]), "Newest first");
page.SortSelector.SelectedIndex = 0;
Check(ListedNames().SequenceEqual(["alpha.bin", "beta.bin", "gamma.bin"]), "Switch back to name order");
File.WriteAllBytes(Path.Combine(sortOptions, "alpha.bin"), new byte[4096]);
page.SortSelector.SelectedIndex = 1;
Check(ListedNames().Last() == "alpha.bin", "Changing sort uses stored metadata, not a disk rescan");
var beforeCancel = page.FileList.ItemsSource;
await Run(null);
Check(ReferenceEquals(beforeCancel, page.FileList.ItemsSource) && page.SortSelector.IsEnabled && page.SortSelector.SelectedIndex == 1,
    "Cancel preserves selected sort and current list");
await Run(sortOptions);
Check(ListedNames().First() == "alpha.bin" && page.SortSelector.SelectedIndex == 1,
    "Folder reload refreshes metadata and preserves sort choice");
await Run(empty);
page.SortSelector.SelectedIndex = 2;
Check(ListedNames().Length == 0, "Sorting an empty folder is safe");
await Run(Path.Combine(root, "missing-sort-folder"));
page.SortSelector.SelectedIndex = 0;
Check(!page.SortSelector.IsEnabled && page.FileList.ItemsSource is null, "Failed folder load cannot resurrect old items");
var sorter = typeof(MainPage).GetMethod("SortFiles", BindingFlags.Static | BindingFlags.NonPublic)!;
foreach (var index in new[] { 1, 2 })
{
    var result = (FileItem[])sorter.Invoke(null, new object[] { new[] { unavailable, metadata }, index })!;
    Check(ReferenceEquals(result.Last(), unavailable), "Unknown metadata placed last for sort " + index);
}
Console.WriteLine("11 sort option checks passed; actual ComboBox rendering NOT tested.");
// New preview checks use only synthetic files created above.
await Run(sortOptions);
Check(!page.PreviewButton.IsEnabled, "Preview disabled without selection");
var selectedFile = ((FileItem[])page.FileList.ItemsSource!)[0];
page.FileList.SelectedItems.Add(selectedFile);
Check(page.PreviewButton.IsEnabled && page.ClearSelectionButton.IsEnabled, "Selection enables preview and clear");
page.SortSelector.SelectedIndex = 1;
Check(page.FileList.SelectedItems.Count == 1 && ReferenceEquals(page.FileList.SelectedItems[0], selectedFile),
    "Sort preserves selected file identity");
var previewHandler = typeof(MainPage).GetMethod("Preview_Click", BindingFlags.Instance | BindingFlags.NonPublic)!;
previewHandler.Invoke(page, [page.PreviewButton, new Microsoft.UI.Xaml.RoutedEventArgs()]);
var dialog = ContentDialog.LastShown!;
StackPanel PreviewPanel(ContentDialog shown) => (StackPanel)((ScrollViewer)shown.Content!).Content!;
TextBox RawPreview(ContentDialog shown) => (TextBox)PreviewPanel(shown).Children.OfType<Expander>().Single().Content!;
var payloadText = RawPreview(dialog).Text;
var previewRows = (StackPanel)PreviewPanel(dialog).Children.OfType<ScrollViewer>().Single().Content!;
Check(previewRows.Children.Count == 1 && ((StackPanel)previewRows.Children[0]).Children.OfType<TextBlock>().First().Text == selectedFile.Name,
    "Preview displays a readable row for the selected file");
Check(((StackPanel)previewRows.Children[0]).Children.OfType<TextBlock>().Last().Text.Contains(selectedFile.SizeText),
    "Readable preview includes file size");
Check(!PreviewPanel(dialog).Children.OfType<Expander>().Single().IsExpanded && RawPreview(dialog).Height == 160,
    "Raw JSON starts collapsed and has explicit multiline height");
Check(((ScrollViewer)dialog.Content!).MaxHeight == 480, "Preview content scrolls within bounded height");
using (var json = System.Text.Json.JsonDocument.Parse(payloadText))
{
    var entry = json.RootElement[0];
    Check(json.RootElement.GetArrayLength() == 1 && entry.GetProperty("name").GetString() == selectedFile.Name,
        "Dialog previews only selected file");
    Check(entry.EnumerateObject().Select(p => p.Name).ToHashSet().SetEquals(["name", "extension", "sizeBytes", "modifiedAtLocal"]),
        "Preview has exactly four allowed fields");
    Check(entry.GetProperty("sizeBytes").GetInt64() == selectedFile.SizeBytes && entry.GetProperty("extension").GetString() == ".bin",
        "Preview retains numeric byte size and extension");
}
Check(!payloadText.Contains(root) && !payloadText.Contains("sorting fixture"), "No absolute path or file contents in preview");
Check(!string.IsNullOrEmpty(dialog.CloseButtonText) && page.PreviewButton.IsEnabled, "Dialog close restores preview action");
await Run(null);
Check(page.FileList.SelectedItems.Count == 1 && page.PreviewButton.IsEnabled, "Folder cancellation preserves preview selection");
await Run(small);
Check(page.FileList.SelectedItems.Count == 0 && !page.PreviewButton.IsEnabled, "Folder change clears selection and preview availability");
await Run(many);
foreach (var file in ((FileItem[])page.FileList.ItemsSource!).Take(21)) page.FileList.SelectedItems.Add(file);
Check(!page.PreviewButton.IsEnabled && page.SelectionSummary.Text.Contains("21"), "Over-limit selection blocks preview with count");
page.FileList.SelectedItems.RemoveAt(20);
Check(page.PreviewButton.IsEnabled, "Exactly 20 files allowed");
typeof(MainPage).GetMethod("ClearSelection_Click", BindingFlags.Instance | BindingFlags.NonPublic)!
    .Invoke(page, [page.ClearSelectionButton, new Microsoft.UI.Xaml.RoutedEventArgs()]);
Check(page.FileList.SelectedItems.Count == 0 && !page.PreviewButton.IsEnabled, "Clear selection resets preview");
foreach (var count in new[] { 0, 21 })
{
    var rejected = false;
    try { AnalysisPreview.CreateJson(Enumerable.Repeat(selectedFile, count)); }
    catch (ArgumentOutOfRangeException) { rejected = true; }
    Check(rejected, "Preview builder rejects count " + count);
}
using (var json = System.Text.Json.JsonDocument.Parse(AnalysisPreview.CreateJson([unavailable])))
    Check(json.RootElement[0].GetProperty("sizeBytes").ValueKind == System.Text.Json.JsonValueKind.Null &&
          json.RootElement[0].GetProperty("modifiedAtLocal").ValueKind == System.Text.Json.JsonValueKind.Null,
          "Unknown metadata is JSON null");
var koreanName = new FileItem(new FileInfo(Path.Combine(small, "한글 이름.txt")));
using (var json = System.Text.Json.JsonDocument.Parse(AnalysisPreview.CreateJson([koreanName])))
    Check(json.RootElement[0].GetProperty("name").GetString() == "한글 이름.txt", "Unicode filename round trips");
page.FileList.SelectedItems.Add(((FileItem[])page.FileList.ItemsSource!)[0]);
await Run(Path.Combine(root, "missing-preview-folder"));
Check(page.FileList.SelectedItems.Count == 0 && !page.PreviewButton.IsEnabled, "Failed folder load cannot expose old selection");
Console.WriteLine("18 preview checks passed with UI test doubles; no network calls were made.");
Check(AppText.Get("SelectFolderButton.Content") == "폴더 선택", "Shared lookup normalizes XAML property keys");
Microsoft.Windows.ApplicationModel.Resources.ResourceLoader.TestLanguage = "en-US";
Check(AppText.Get("SelectFolderButton.Content") == "Select folder", "English resource text lookup");
Check(AppText.Format("FileCount", 42) == "Files (42)", "English count placeholder");
Check(AppText.Format("EmptyFolder", @"E:\테스트 {folder}").StartsWith(@"E:\테스트 {folder}"),
    "Folder path remains untranslated, including braces and Unicode");
Console.WriteLine("4 helper checks passed using a test resource loader; Windows language matching NOT simulated.");
Console.WriteLine("Fixtures: " + root);

await AnalysisChecks.Run();
// Exercise the actual consent/request/result flow with fake HTTP and dialogs.
Microsoft.Windows.ApplicationModel.Resources.ResourceLoader.TestLanguage = "ko-KR";
await Run(small);
page.FileList.SelectedItems.Add(((FileItem[])page.FileList.ItemsSource!)[0]);
var aiHandler = new AnalysisChecks.FakeHandler(System.Net.HttpStatusCode.OK, AnalysisChecks.Envelope(AnalysisChecks.Good));
using var aiHttp = new System.Net.Http.HttpClient(aiHandler);
page.AnalysisClient = new FileAnalysisClient(aiHttp);
void OpenPreview(ContentDialogResult decision)
{
    ContentDialog.NextResult = decision;
    ContentDialog.NextPassword = "synthetic-test-key";
    previewHandler.Invoke(page, [page.PreviewButton, new Microsoft.UI.Xaml.RoutedEventArgs()]);
}
async Task WaitPreview()
{
    var wait = Stopwatch.StartNew();
    while (!page.PreviewButton.IsEnabled && wait.ElapsedMilliseconds < 5000) await Task.Delay(5);
    Check(page.PreviewButton.IsEnabled, "Preview interaction restores controls");
}
OpenPreview(ContentDialogResult.None);
await WaitPreview();
Check(aiHandler.Calls == 0, "Closing consent dialog sends no request");
Check(PreviewPanel(ContentDialog.LastShown!).Children.OfType<PasswordBox>().Single().Password == "",
    "Password input cleared after closing preview");
OpenPreview(ContentDialogResult.Primary);
await WaitPreview();
Check(aiHandler.Calls == 1 && page.ResultsButton.IsEnabled && page.AnalysisStatus.Text.Contains("완료"),
    "Explicit consent calls once and retains results");
Check(((TextBlock)((ScrollViewer)ContentDialog.LastShown!.Content!).Content!).Text.Contains("한글 이름.txt"),
    "Results use selected local name, not a model-provided filename");
typeof(MainPage).GetMethod("Results_Click", BindingFlags.Instance | BindingFlags.NonPublic)!
    .Invoke(page, [page.ResultsButton, new Microsoft.UI.Xaml.RoutedEventArgs()]);
await WaitPreview();
Check(aiHandler.Calls == 1, "Reopening results makes no paid request");
aiHandler.Gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
OpenPreview(ContentDialogResult.Primary);
Check(!page.PreviewButton.IsEnabled && !page.SelectFolderAction.IsEnabled && !page.FileList.IsEnabled && !page.ResultsButton.IsEnabled,
    "In-flight request locks selection, folder and results");
previewHandler.Invoke(page, [page.PreviewButton, new Microsoft.UI.Xaml.RoutedEventArgs()]);
Check(aiHandler.Calls == 2, "Double click cannot issue another request");
typeof(MainPage).GetMethod("CancelAnalysis_Click", BindingFlags.Instance | BindingFlags.NonPublic)!
    .Invoke(page, [page.CancelAnalysisButton, new Microsoft.UI.Xaml.RoutedEventArgs()]);
await WaitPreview();
Check(page.AnalysisStatus.Text.Contains("취소") && !page.ResultsButton.IsEnabled,
    "Cancellation clears prior result and reports possible charges");
aiHandler.Gate = null;
OpenPreview(ContentDialogResult.Primary);
await WaitPreview();
await Run(null);
Check(page.ResultsButton.IsEnabled, "Folder picker cancellation retains result");
await Run(sortOptions);
Check(!page.ResultsButton.IsEnabled && page.AnalysisStatus.Text == "", "Changing folder clears previous analysis");
Console.WriteLine("AI consent and result flow checks passed with fake HTTP/UI only.");

// Category filtering: synthetic files only, including unknown and missing extensions.
var mixed = Directory.CreateDirectory(Path.Combine(root, "mixed-types")).FullName;
var examples = new[] {
    ("report.PDF", FileCategory.Document), ("노트.hwpx", FileCategory.Document),
    ("photo.JpG", FileCategory.Image), ("vector.svg", FileCategory.Image),
    ("clip.mp4", FileCategory.Media), ("song.flac", FileCategory.Media),
    ("backup.tar.gz", FileCategory.Archive), ("bundle.ZIP", FileCategory.Archive),
    ("setup.msi", FileCategory.Application), ("image.jpg.exe", FileCategory.Application),
    ("README", FileCategory.Other), ("custom.unknown", FileCategory.Other)
};
foreach (var (name, category) in examples)
{
    File.WriteAllText(Path.Combine(mixed, name), "Harmless test fixture, not real file content.");
    Check(FileCategories.Classify(name) == category, "Extension classification: " + name);
}
await Run(mixed);
Check(ListedNames().Length == examples.Length, "All includes every file type");
foreach (var category in Enum.GetValues<FileCategory>().Where(c => c != FileCategory.All))
{
    page.CategorySelector.SelectedIndex = (int)category;
    var expected = examples.Where(x => x.Item2 == category).Select(x => x.Item1).ToHashSet();
    Check(expected.SetEquals(ListedNames()) && page.FileList.Header!.ToString()!.Contains("2개 표시 / 전체 12개"),
        "Category list and filtered/total count: " + category);
}
page.FileList.SelectedItems.Add(((FileItem[])page.FileList.ItemsSource!)[0]);
page.CategorySelector.SelectedIndex = (int)FileCategory.Document;
Check(page.FileList.SelectedItems.Count == 0 && !page.PreviewButton.IsEnabled, "Changing category clears hidden selection");
var chosen = ((FileItem[])page.FileList.ItemsSource!)[0];
page.FileList.SelectedItems.Add(chosen);
page.SortSelector.SelectedIndex = 1;
Check(ListedNames().Length == 2 && page.FileList.SelectedItems.Contains(chosen), "Sort stays within filter and preserves selection");
OpenPreview(ContentDialogResult.None);
await WaitPreview();
var preview = RawPreview(ContentDialog.LastShown!).Text;
using (var parsed = System.Text.Json.JsonDocument.Parse(preview))
    Check(parsed.RootElement.GetArrayLength() == 1 && parsed.RootElement[0].GetProperty("name").GetString() == chosen.Name,
        "AI preview only includes explicitly selected visible file");
OpenPreview(ContentDialogResult.Primary);
await WaitPreview();
Check(page.ResultsButton.IsEnabled, "Analysis works after filtering through fake HTTP");
page.CategorySelector.SelectedIndex = (int)FileCategory.Image;
Check(!page.ResultsButton.IsEnabled && page.AnalysisStatus.Text == "", "Filter change clears previous result");
var filteredList = page.FileList.ItemsSource;
await Run(null);
Check(ReferenceEquals(filteredList, page.FileList.ItemsSource) && page.CategorySelector.SelectedIndex == (int)FileCategory.Image,
    "Cancel folder selection preserves category and list");
await Run(small);
Check(ListedNames().Length == 0 && page.EmptyTitle.Text == "이 종류의 파일이 없습니다" &&
    page.EmptyState.Visibility == Microsoft.UI.Xaml.Visibility.Visible, "No matching type is distinct from empty folder");
page.CategorySelector.SelectedIndex = 0;
Check(ListedNames().SequenceEqual(["한글 이름.txt"]) && page.EmptyState.Visibility == Microsoft.UI.Xaml.Visibility.Collapsed,
    "All restores files without rescanning");
page.CategorySelector.SelectedIndex = (int)FileCategory.Image;
await Run(empty);
Check(page.EmptyTitle.Text == "표시할 파일이 없습니다", "Actually empty folder uses normal empty state");
await Run(Path.Combine(root, "missing-category-folder"));
Check(!page.CategorySelector.IsEnabled && page.FileList.ItemsSource is null, "Failed folder disables filter and clears items");
page.CategorySelector.SelectedIndex = 0;
Check(page.FileList.ItemsSource is null, "Filter cannot resurrect files after load failure");
Console.WriteLine("Category checks passed using harmless synthetic fixtures; no actual API requests.");

var pickHandler = typeof(MainPage).GetMethod("SelectFiles_Click", BindingFlags.Instance | BindingFlags.NonPublic)!;
async Task PickFiles(params string[] paths)
{
    FileOpenPicker.NextPaths = paths;
    pickHandler.Invoke(page, [page.SelectFilesAction, new Microsoft.UI.Xaml.RoutedEventArgs()]);
    var timer = Stopwatch.StartNew();
    while (!page.SelectFilesAction.IsEnabled && timer.ElapsedMilliseconds < 5000) await Task.Delay(5);
    Check(page.SelectFilesAction.IsEnabled, "File picker restores controls");
}
page.CategorySelector.SelectedIndex = (int)FileCategory.Image;
var callsBeforePick = aiHandler.Calls;
await PickFiles(Path.Combine(mixed, "report.PDF"));
Check(ListedNames().SequenceEqual(["report.PDF"]) && page.CategorySelector.SelectedIndex == 0 &&
    page.FileList.SelectedItems.Count == 1 && page.PreviewButton.IsEnabled,
    "Single picked file replaces list, resets filter and is selected");
Check(aiHandler.Calls == callsBeforePick, "Picking a file never sends an API request");
var pickedList = page.FileList.ItemsSource;
var pickedStatus = page.StatusText.Text;
await PickFiles();
Check(ReferenceEquals(pickedList, page.FileList.ItemsSource) && page.StatusText.Text == pickedStatus && page.FileList.SelectedItems.Count == 1,
    "File picker cancellation preserves list and selection");
await PickFiles(Path.Combine(mixed, "report.PDF"), Path.Combine(mixed, "song.flac"), Path.Combine(mixed, "report.PDF"));
Check(ListedNames().Length == 2 && page.FileList.SelectedItems.Count == 2, "Multiple picked paths are deduplicated");
OpenPreview(ContentDialogResult.None);
await WaitPreview();
var directPayload = RawPreview(ContentDialog.LastShown!).Text;
using (var parsed = System.Text.Json.JsonDocument.Parse(directPayload))
    Check(parsed.RootElement.GetArrayLength() == 2 && !directPayload.Contains(mixed), "Direct file preview includes only metadata, no full path");
await PickFiles(Enumerable.Range(0, 21).Select(i => Path.Combine(many, $"sample-{i:D4}.txt")).ToArray());
Check(ListedNames().Length == 21 && page.FileList.SelectedItems.Count == 21 && !page.PreviewButton.IsEnabled,
    "21 picks are visible, not truncated; AI analysis remains limited to 20");
FileOpenPicker.Failure = new IOException("Simulated picker failure");
var beforeFailure = page.FileList.ItemsSource;
await PickFiles(Path.Combine(mixed, "report.PDF"));
Check(ReferenceEquals(beforeFailure, page.FileList.ItemsSource) && page.AnalysisStatus.Text.Contains("이전 목록"),
    "Picker failure retains previous list and reports failure");
FileOpenPicker.Failure = null;
FileOpenPicker.Gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
FileOpenPicker.NextPaths = [];
pickHandler.Invoke(page, [page.SelectFilesAction, new Microsoft.UI.Xaml.RoutedEventArgs()]);
Check(!page.SelectFilesAction.IsEnabled && !page.SelectFolderAction.IsEnabled && !page.CategorySelector.IsEnabled,
    "Open file picker locks other source controls");
var pickerCalls = FileOpenPicker.Calls;
pickHandler.Invoke(page, [page.SelectFilesAction, new Microsoft.UI.Xaml.RoutedEventArgs()]);
Check(FileOpenPicker.Calls == pickerCalls, "Duplicate file picker launch blocked");
FileOpenPicker.Gate.SetResult();
while (!page.SelectFilesAction.IsEnabled) await Task.Delay(5);
FileOpenPicker.Gate = null;
await Run(small);
Check(ListedNames().SequenceEqual(["한글 이름.txt"]) && page.FileList.SelectedItems.Count == 0,
    "Folder selection still works after direct file selection");
Console.WriteLine("Direct file selection checks passed with picker/HTTP test doubles.");

await PickFiles(Path.Combine(mixed, "report.PDF"), Path.Combine(mixed, "song.flac"));
var checkboxItems = (FileItem[])page.FileList.ItemsSource!;
Check(checkboxItems.All(f => f.IsIncluded), "Directly picked files have checked model state");
var checkboxHandler = typeof(MainPage).GetMethod("FileIncluded_Click", BindingFlags.Instance | BindingFlags.NonPublic)!;
var box = new CheckBox { Tag = checkboxItems[0], IsChecked = false };
checkboxHandler.Invoke(page, [box, new Microsoft.UI.Xaml.RoutedEventArgs()]);
Check(!checkboxItems[0].IsIncluded && checkboxItems[1].IsIncluded && page.FileList.SelectedItems.Count == 1 &&
    ((FileItem[])page.FileList.ItemsSource!).Length == 2, "Unchecking excludes file but keeps it visible");
Check(page.SelectionSummary.Text.Contains("분석 포함 1개 · 제외 1개"), "Included/excluded counts reflect checkbox");
OpenPreview(ContentDialogResult.None);
await WaitPreview();
var checkboxPayload = RawPreview(ContentDialog.LastShown!).Text;
using (var parsed = System.Text.Json.JsonDocument.Parse(checkboxPayload))
    Check(parsed.RootElement.GetArrayLength() == 1 && parsed.RootElement[0].GetProperty("name").GetString() == checkboxItems[1].Name,
        "Unchecked file absent from AI preview");
page.SortSelector.SelectedIndex = 2;
Check(!checkboxItems[0].IsIncluded && checkboxItems[1].IsIncluded, "Sorting preserves checked and unchecked states");
box.IsChecked = true;
checkboxHandler.Invoke(page, [box, new Microsoft.UI.Xaml.RoutedEventArgs()]);
checkboxHandler.Invoke(page, [box, new Microsoft.UI.Xaml.RoutedEventArgs()]);
Check(checkboxItems[0].IsIncluded && page.FileList.SelectedItems.Count == 2, "Checking restores inclusion without duplicates");
typeof(MainPage).GetMethod("ClearSelection_Click", BindingFlags.Instance | BindingFlags.NonPublic)!
    .Invoke(page, [page.ClearSelectionButton, new Microsoft.UI.Xaml.RoutedEventArgs()]);
Check(checkboxItems.All(f => !f.IsIncluded) && !page.PreviewButton.IsEnabled, "Clear updates every checkbox and disables analysis");
page.FileList.SelectedItems.Add(checkboxItems[0]);
Check(checkboxItems[0].IsIncluded, "Native row/keyboard selection synchronizes checkbox state");
page.CategorySelector.SelectedIndex = (int)FileCategory.Image;
Check(checkboxItems.All(f => !f.IsIncluded), "Category change clears hidden checkbox states");
checkboxHandler.Invoke(page, [box, new Microsoft.UI.Xaml.RoutedEventArgs()]);
Check(page.FileList.SelectedItems.Count == 0, "Stale checkbox cannot reselect hidden file");
Console.WriteLine("Explicit inclusion checkbox checks passed; actual WinUI rendering remains a manual check.");

namespace Microsoft.UI.Xaml
{
    public enum Visibility { Visible, Collapsed }
    public enum HorizontalAlignment { Stretch }
    public enum TextWrapping { Wrap }
    public class RoutedEventArgs : EventArgs { }
    public class XamlRoot { public Island ContentIslandEnvironment { get; } = new(); }
    public class Island { public int AppWindowId => 1; }
}
namespace Microsoft.UI.Xaml.Controls
{
    public class Page { public Microsoft.UI.Xaml.XamlRoot XamlRoot { get; } = new(); }
    public class StackPanel
    {
        public Microsoft.UI.Xaml.Visibility Visibility { get; set; }
        public double Spacing { get; set; }
        public List<object> Children { get; } = [];
    }
    public class Button { public volatile bool IsEnabled = true; public Microsoft.UI.Xaml.Visibility Visibility { get; set; } }
    public class CheckBox { public object? Tag { get; set; } public bool? IsChecked { get; set; } }
    public class PasswordBox { public string Header { get; set; } = ""; public string Password { get; set; } = ""; }
    public enum ScrollBarVisibility { Auto, Disabled }
    public class ScrollViewer
    {
        public double MaxHeight { get; set; }
        public double Height { get; set; }
        public object? Content { get; set; }
        public ScrollBarVisibility VerticalScrollBarVisibility { get; set; }
        public ScrollBarVisibility HorizontalScrollBarVisibility { get; set; }
    }
    public class Expander
    {
        public object? Header { get; set; }
        public object? Content { get; set; }
        public bool IsExpanded { get; set; }
        public Microsoft.UI.Xaml.HorizontalAlignment HorizontalAlignment { get; set; }
        public Microsoft.UI.Xaml.HorizontalAlignment HorizontalContentAlignment { get; set; }
    }
    public class SelectionChangedEventArgs : EventArgs { }
    public delegate void SelectionChangedEventHandler(object sender, SelectionChangedEventArgs e);
    public class ComboBox
    {
        private int _selectedIndex;
        public bool IsEnabled { get; set; }
        public event SelectionChangedEventHandler? SelectionChanged;
        public int SelectedIndex
        {
            get => _selectedIndex;
            set { _selectedIndex = value; SelectionChanged?.Invoke(this, new()); }
        }
    }
    public class TextBlock
    {
        public double FontSize { get; set; }
        public bool IsTextSelectionEnabled { get; set; }
        public string Text { get; set; } = "";
        public Microsoft.UI.Xaml.TextWrapping TextWrapping { get; set; }
    }
    public class TextBox : TextBlock
    {
        public string Header { get; set; } = "";
        public bool IsReadOnly { get; set; }
        public bool AcceptsReturn { get; set; }
        public double MaxHeight { get; set; }
        public double Height { get; set; }
    }
    public enum ContentDialogButton { Close }
    public enum ContentDialogResult { None, Primary }
    public class ContentDialog
    {
        public static ContentDialog? LastShown;
        public static ContentDialogResult NextResult;
        public static string NextPassword = "";
        public object? XamlRoot { get; set; }
        public string Title { get; set; } = "";
        public object? Content { get; set; }
        public string CloseButtonText { get; set; } = "";
        public string PrimaryButtonText { get; set; } = "";
        public ContentDialogButton DefaultButton { get; set; }
        public Task<ContentDialogResult> ShowAsync()
        {
            LastShown = this;
            if (Content is ScrollViewer { Content: StackPanel panel })
                foreach (var input in panel.Children.OfType<PasswordBox>()) input.Password = NextPassword;
            NextPassword = "";
            var result = NextResult;
            NextResult = ContentDialogResult.None;
            return Task.FromResult(result);
        }
    }
    public class ListView
    {
        private object? _itemsSource;
        public object? ItemsSource
        {
            get => _itemsSource;
            set { _itemsSource = value; SelectedItems.Clear(); }
        }
        public bool IsEnabled { get; set; } = true;
        public System.Collections.ObjectModel.ObservableCollection<object> SelectedItems { get; } = [];
        public event SelectionChangedEventHandler? SelectionChanged;
        public ListView() { SelectedItems.CollectionChanged += (_, _) => SelectionChanged?.Invoke(this, new()); }
        public object? Header { get; set; }
    }
}
namespace Microsoft.Windows.Storage.Pickers
{
    public class PickFileResult { public required string Path { get; init; } }
    public class FileOpenPicker(int windowId)
    {
        public static string[] NextPaths = [];
        public static Exception? Failure;
        public static int Calls;
        public static TaskCompletionSource? Gate;
        public string CommitButtonText { get; set; } = "";
        public async Task<IReadOnlyList<PickFileResult>> PickMultipleFilesAsync()
        {
            _ = windowId;
            Calls++;
            if (Gate is not null) await Gate.Task;
            if (Failure is not null) throw Failure;
            return NextPaths.Select(path => new PickFileResult { Path = path }).ToArray();
        }
    }
    public class PickFolderResult { public required string Path { get; init; } }
    public class FolderPicker(int windowId)
    {
        public static string? NextPath;
        public Task<PickFolderResult?> PickSingleFolderAsync()
        {
            _ = windowId;
            return Task.FromResult(NextPath is null ? null : new PickFolderResult { Path = NextPath });
        }
    }
}
namespace FileHelper
{
    public sealed partial class MainPage
    {
        public TextBlock StatusText { get; } = new();
        public ListView FileList { get; } = new();
        public ComboBox SortSelector { get; } = new();
        public ComboBox CategorySelector { get; } = new();
        public TextBlock EmptyTitle { get; } = new();
        public TextBlock EmptyHint { get; } = new();
        public StackPanel EmptyState { get; } = new();
        public TextBlock SelectionSummary { get; } = new();
        public Button PreviewButton { get; } = new();
        public Button ClearSelectionButton { get; } = new();
        public Button SelectFolderAction { get; } = new();
        public Button SelectFilesAction { get; } = new();
        public Button ResultsButton { get; } = new();
        public Button CancelAnalysisButton { get; } = new();
        public TextBlock AnalysisStatus { get; } = new();
        private void InitializeComponent() { }
    }
}
namespace Microsoft.Windows.ApplicationModel.Resources
{
    // Test double only: real MRT language selection is checked separately via PRI/build.
    public class ResourceLoader
    {
        public static string TestLanguage = "ko-KR";
        public string GetString(string key)
        {
            var document = System.Xml.Linq.XDocument.Load(
                Path.Combine(AppContext.BaseDirectory, "Strings", TestLanguage, "Resources.resw"));
            return document.Root!.Elements("data")
                .Single(entry => (string?)entry.Attribute("name") == key.Replace('/', '.'))
                .Element("value")!.Value;
        }
    }
}
