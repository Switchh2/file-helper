using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace FileHelper;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? _window;
    
    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
#if DEBUG
        UnhandledException += (_, e) =>
        {
            try
            {
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(System.AppContext.BaseDirectory, "startup-error.log"),
                    $"{System.DateTimeOffset.Now:O}\n{e.Exception}\n");
            }
            catch { /* Diagnostics must not replace the original exception. */ }
        };
        // Read desktop process arguments before XAML loads any localized resources.
        var testLanguage = string.Empty;
        foreach (var argument in System.Environment.GetCommandLineArgs())
        {
            if (argument == "--test-language=en-US") testLanguage = "en-US";
            if (argument == "--test-language=ko-KR") testLanguage = "ko-KR";
        }
        // Packaged apps can use the OS API, which supports clearing the override.
        // The Windows App SDK wrapper currently throws for an empty override (#5335).
        Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = testLanguage;
#endif
        InitializeComponent();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
#if DEBUG
        // Packaged apps persist the override; clear it when this test window closes.
        _window.Closed += (_, _) =>
            Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = string.Empty;
#endif
        _window.Activate();
    }
}
