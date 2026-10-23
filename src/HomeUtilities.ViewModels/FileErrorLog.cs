using System.Globalization;
using System.Text;

namespace HomeUtilities.ViewModels;

/// <summary><see cref="IErrorLog"/> in a text file; when it exceeds <see cref="MaxBytes"/> only the newest part is kept.</summary>
public sealed class FileErrorLog : IErrorLog
{
    /// <summary>Size limit of the log file (1 MB).</summary>
    public const int MaxBytes = 1024 * 1024;

    private readonly object _gate = new();
    private readonly string _path;

    public FileErrorLog(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
    }

    public void Append(string source, Exception exception)
    {
        try
        {
            var entry = $"[{DateTimeOffset.UtcNow.ToString("u", CultureInfo.InvariantCulture)}] {source}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}";
            lock (_gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
                File.AppendAllText(_path, entry, Encoding.UTF8);
                Trim();
            }
        }
        catch (Exception)
        {
            // Logging must never cause another failure.
        }
    }

    public string ReadAll()
    {
        try
        {
            lock (_gate)
            {
                return File.Exists(_path) ? File.ReadAllText(_path, Encoding.UTF8) : string.Empty;
            }
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    public void Clear()
    {
        try
        {
            lock (_gate)
            {
                if (File.Exists(_path))
                {
                    File.Delete(_path);
                }
            }
        }
        catch (Exception)
        {
            // Nothing to do: the log is a convenience.
        }
    }

    private void Trim()
    {
        var info = new FileInfo(_path);
        if (info.Length <= MaxBytes)
        {
            return;
        }

        var bytes = File.ReadAllBytes(_path);
        var tail = bytes.AsSpan(bytes.Length - MaxBytes / 2).ToArray();
        // Start at the first complete entry: skip to the next line that begins a timestamp.
        var text = Encoding.UTF8.GetString(tail);
        var marker = text.IndexOf(Environment.NewLine + "[", StringComparison.Ordinal);
        if (marker >= 0)
        {
            text = text[(marker + Environment.NewLine.Length)..];
        }

        File.WriteAllText(_path, text, Encoding.UTF8);
    }
}
