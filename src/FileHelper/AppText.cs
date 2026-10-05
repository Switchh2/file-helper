using System.Globalization;
using Microsoft.Windows.ApplicationModel.Resources;

namespace FileHelper;

// Shared lookup for C# messages. Windows chooses the matching resource language.
internal static class AppText
{
    private static readonly ResourceLoader Loader = new();

    public static string Get(string key)
    {
        // XAML property keys such as FileList.Header use '/' in resource lookups.
        return Loader.GetString(key.Replace('.', '/'));
    }

    public static string Format(string key, params object[] arguments)
    {
        return string.Format(CultureInfo.CurrentCulture, Get(key), arguments);
    }
}
