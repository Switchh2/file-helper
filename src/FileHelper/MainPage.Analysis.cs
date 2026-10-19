using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FileHelper;

public sealed partial class MainPage
{
    private static readonly System.Net.Http.HttpClient AnalysisHttp = FileAnalysisClient.CreateHttpClient();
    internal FileAnalysisClient AnalysisClient { get; set; } = new(AnalysisHttp);
    private CancellationTokenSource? _analysisCancellation;
    private string? _lastAnalysis;

    private async Task AnalyzeSelectionAsync(FileItem[] files, string payload, string key)
    {
        using var cancellation = new CancellationTokenSource();
        _analysisCancellation = cancellation;
        CancelAnalysisButton.Visibility = Visibility.Visible;
        CancelAnalysisButton.IsEnabled = true;
        _lastAnalysis = null;
        ResultsButton.IsEnabled = false;
        AnalysisStatus.Text = AppText.Get("AiWorking");
        try
        {
            var results = await AnalysisClient.AnalyzeAsync(key, payload, AppText.Get("AiLanguage"), cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            var display = new StringBuilder(AppText.Get("AiDisclaimer")).AppendLine().AppendLine();
            foreach (var result in results)
            {
                display.AppendLine(files[result.Index].Name);
                display.AppendLine(AppText.Format("AiResultLine", AppText.Get("AiPriority_" + result.Priority), result.Purpose));
                display.AppendLine(result.Reason).AppendLine();
            }
            _lastAnalysis = display.ToString();
            AnalysisStatus.Text = AppText.Get("AiDone");
            // The result stays in memory and can be reopened without another paid call.
        }
        catch (OperationCanceledException) { AnalysisStatus.Text = AppText.Get("AiCancelled"); }
        catch (AnalysisException ex) { AnalysisStatus.Text = AppText.Get(ex.ResourceKey); }
        catch (Exception) { AnalysisStatus.Text = AppText.Get("AiRequestError"); }
        finally
        {
            key = string.Empty;
            _analysisCancellation = null;
            CancelAnalysisButton.Visibility = Visibility.Collapsed;
        }
    }

    private void CancelAnalysis_Click(object sender, RoutedEventArgs e)
    {
        CancelAnalysisButton.IsEnabled = false;
        _analysisCancellation?.Cancel();
    }

    private async Task ShowAnalysisAsync()
    {
        if (_lastAnalysis is null) return;
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = AppText.Get("AiResultsTitle"),
            Content = new ScrollViewer
            {
                MaxHeight = 440,
                Content = new TextBlock { Text = _lastAnalysis, TextWrapping = TextWrapping.Wrap }
            },
            CloseButtonText = AppText.Get("PreviewClose"),
            DefaultButton = ContentDialogButton.Close
        };
        await dialog.ShowAsync();
    }

    private async void Results_Click(object sender, RoutedEventArgs e)
    {
        if (_isPreviewOpen || _lastAnalysis is null) return;
        _isPreviewOpen = true;
        UpdateSelectionSummary();
        try { await ShowAnalysisAsync(); }
        catch (Exception) { AnalysisStatus.Text = AppText.Get("AiRequestError"); }
        finally { _isPreviewOpen = false; UpdateSelectionSummary(); }
    }
}
