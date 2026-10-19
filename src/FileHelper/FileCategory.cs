using System;
using System.IO;

namespace FileHelper;

// Values match the category selector order. Classification uses the last extension,
// never file contents. A category is not a safety or cleanup recommendation.
public enum FileCategory { All, Document, Image, Media, Archive, Application, Other }

public static class FileCategories
{
    public static FileCategory Classify(string name) => Path.GetExtension(name).ToLowerInvariant() switch
    {
        ".txt" or ".md" or ".pdf" or ".doc" or ".docx" or ".xls" or ".xlsx" or
        ".ppt" or ".pptx" or ".csv" or ".rtf" or ".hwp" or ".hwpx" or ".odt" or
        ".ods" or ".odp" => FileCategory.Document,
        ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" or ".svg" or
        ".tif" or ".tiff" or ".ico" or ".heic" or ".heif" or ".avif" or ".raw" => FileCategory.Image,
        ".mp4" or ".mkv" or ".avi" or ".mov" or ".wmv" or ".webm" or ".m4v" or
        ".mp3" or ".wav" or ".flac" or ".aac" or ".m4a" or ".ogg" or ".wma" or ".opus" => FileCategory.Media,
        ".zip" or ".7z" or ".rar" or ".tar" or ".gz" or ".bz2" or ".xz" or
        ".tgz" or ".alz" or ".egg" => FileCategory.Archive,
        ".exe" or ".msi" or ".msix" or ".msixbundle" or ".appx" or ".appxbundle" or
        ".com" or ".bat" or ".cmd" or ".ps1" => FileCategory.Application,
        _ => FileCategory.Other
    };

    public static bool Matches(string name, int selectedIndex)
    {
        if (!Enum.IsDefined(typeof(FileCategory), selectedIndex))
            throw new ArgumentOutOfRangeException(nameof(selectedIndex));
        return selectedIndex == 0 || Classify(name) == (FileCategory)selectedIndex;
    }
}
