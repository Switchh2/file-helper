using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace FileHelper;

// Local preview only. No file reads, persistence, or network calls.
public static class AnalysisPreview
{
    public const int MaxFiles = 20;

    public static string CreateJson(IEnumerable<FileItem> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var selected = files.Take(MaxFiles + 1).ToArray();
        if (selected.Length is 0 or > MaxFiles)
            throw new ArgumentOutOfRangeException(nameof(files));

        // Explicit allowlist: never serialize FileInfo or an entire UI object.
        var payload = selected.Select(file => new
        {
            name = file.Name,
            extension = Path.GetExtension(file.Name),
            sizeBytes = file.SizeBytes,
            modifiedAtLocal = file.ModifiedAt?.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture)
        });
        return JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        });
    }
}
