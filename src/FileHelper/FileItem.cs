using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;

namespace FileHelper;

// A snapshot of one file's metadata. File contents are never opened here.
public sealed class FileItem : INotifyPropertyChanged
{
    private bool _isIncluded;
    // UI state only; AnalysisPreview explicitly excludes it from the API payload.
    public bool IsIncluded
    {
        get => _isIncluded;
        internal set
        {
            if (_isIncluded == value) return;
            _isIncluded = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsIncluded)));
        }
    }
    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name { get; }
    public long? SizeBytes { get; }
    public DateTime? ModifiedAt { get; }

    public FileItem(FileInfo file)
    {
        Name = file.Name;
        try
        {
            var size = file.Length;
            var modified = file.LastWriteTime;
            SizeBytes = size;
            ModifiedAt = modified;
        }
        // Files can disappear or become inaccessible during enumeration.
        // Keep the name visible, with unavailable metadata shown as a dash.
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public string SizeText
    {
        get
        {
            if (SizeBytes is not long bytes) return "—";
            string[] units = ["B", "KiB", "MiB", "GiB", "TiB", "PiB", "EiB"];
            double size = bytes;
            var unit = 0;
            while (size >= 1024 && unit < units.Length - 1)
            {
                size /= 1024;
                unit++;
            }
            return $"{size.ToString("0.#", CultureInfo.CurrentCulture)} {units[unit]}";
        }
    }

    public string ModifiedText => ModifiedAt?.ToString("g", CultureInfo.CurrentCulture) ?? "—";
}
