using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core.Backup;

namespace HomeUtilities.ViewModels;

/// <summary>One automatic copy in the Backup section: when and why it was made, its size, and the two actions.</summary>
public sealed class AutoBackupRowViewModel
{
    /// <summary>Creates a row; the commands run <paramref name="restore"/> and <paramref name="save"/> for the copy.</summary>
    public AutoBackupRowViewModel(
        AutoBackupInfo copy,
        string title,
        string details,
        string restoreText,
        string saveText,
        Func<AutoBackupInfo, Task> restore,
        Func<AutoBackupInfo, Task> save)
    {
        Copy = copy;
        Title = title;
        Details = details;
        RestoreText = restoreText;
        SaveText = saveText;
        RestoreCommand = new AsyncRelayCommand(() => restore(copy));
        SaveCommand = new AsyncRelayCommand(() => save(copy));
    }

    public AutoBackupInfo Copy { get; }

    /// <summary>Date and time of the copy (local time).</summary>
    public string Title { get; }

    /// <summary>Reason and size, e.g. "Before import · 2.3 MB".</summary>
    public string Details { get; }

    public string RestoreText { get; }

    public string SaveText { get; }

    public IAsyncRelayCommand RestoreCommand { get; }

    public IAsyncRelayCommand SaveCommand { get; }
}

/// <summary>Passes writes through and counts the bytes, so the size of a saved file is known even if the target cannot tell.</summary>
internal sealed class CountingStream(Stream inner) : Stream
{
    public long Written { get; private set; }

    /// <summary>The final size: the stream length if it can tell (a seekable zip is patched in place), else the bytes written.</summary>
    public long Size => inner.CanSeek ? inner.Length : Written;

    public override bool CanRead => false;

    public override bool CanSeek => inner.CanSeek;

    public override bool CanWrite => true;

    public override long Length => inner.Length;

    public override long Position
    {
        get => inner.Position;
        set => inner.Position = value;
    }

    public override void Flush() => inner.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);

    public override void SetLength(long value) => inner.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count)
    {
        inner.Write(buffer, offset, count);
        Written += count;
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        inner.Write(buffer);
        Written += buffer.Length;
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await inner.WriteAsync(buffer, cancellationToken);
        Written += buffer.Length;
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
}
