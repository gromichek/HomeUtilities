using System.Globalization;
using System.Security.Cryptography;
using HomeUtilities.Core.Attachments;
using HomeUtilities.Core.Sync;
using Microsoft.EntityFrameworkCore;

namespace HomeUtilities.Data.Sync;

// Attachment files (T-032 part B) and the cloud file overview (C12). The files of the attachments live in the same storage as
// the change files, under attachments/<attachmentId><extension>; a file never changes, so it is uploaded once.
public sealed partial class SyncEngine
{
    private readonly SemaphoreSlim _downloadGate = new(1, 1);

    /// <inheritdoc />
    public async Task<CloudFilesReport> ListCloudFilesAsync(CancellationToken ct = default) =>
        BuildReport(await _storage.ListAsync(string.Empty, ct), _deviceId);

    /// <summary>Groups the files of the cloud folder for the "Files in the cloud" list.</summary>
    internal static CloudFilesReport BuildReport(IReadOnlyList<SyncFileInfo> files, string thisDevice)
    {
        var devices = files.Select(f => (File: f, Info: SyncFormat.ParseChange(f.Path))).Where(x => x.Info is not null)
            .GroupBy(x => x.Info!.Value.Device, StringComparer.Ordinal)
            .Select(g => new CloudDeviceFiles(
                g.Key,
                g.Key == thisDevice,
                g.Count(),
                g.Sum(x => x.File.Size),
                g.Max(x => x.File.ModifiedAt)))
            .OrderByDescending(d => d.IsThisDevice)
            .ThenBy(d => d.DeviceId, StringComparer.Ordinal)
            .ToList();
        var snapshots = files.Select(f => (File: f, Info: SyncFormat.ParseSnapshot(f.Path))).Where(x => x.Info is not null)
            .Select(x => new CloudSnapshotFile(x.Info!.Value.Device, x.Info.Value.At, x.File.Size, x.Info.Value.Generation))
            .OrderByDescending(s => s.At)
            .ToList();
        var attachments = files.Where(f => f.Path.StartsWith(SyncFormat.AttachmentsPrefix, StringComparison.Ordinal)).ToList();
        var foreign = files.Where(f => SyncFormat.ParseChange(f.Path) is null
            && SyncFormat.ParseSnapshot(f.Path) is null
            && !f.Path.StartsWith(SyncFormat.AttachmentsPrefix, StringComparison.Ordinal)).ToList();
        return new CloudFilesReport(devices, snapshots, attachments.Count, attachments.Sum(f => f.Size), foreign.Count, foreign.Sum(f => f.Size));
    }

    private string? LocalPath(Attachment a) =>
        _options.AttachmentsFolder is { Length: > 0 } folder
            ? Path.Combine(folder, a.Id.ToString() + AttachmentContentTypes.ExtensionFor(a.ContentType))
            : null;

    private static string CloudPath(Attachment a) => SyncFormat.AttachmentPathFor(a.Id, AttachmentContentTypes.ExtensionFor(a.ContentType) ?? string.Empty);

    // Runs after the change files: the row is in the cloud before its file. Every run reconciles, so a failed upload or download
    // is simply tried again by the next run (no separate queue to keep in step with the outbox).
    private async Task SyncAttachmentFilesAsync(IReadOnlyList<SyncFileInfo> files, CancellationToken ct)
    {
        if (_options.AttachmentsFolder is not { Length: > 0 })
        {
            return;
        }

        List<Attachment> rows;
        await using (var db = await _factory.CreateDbContextAsync(ct))
        {
            rows = await db.Attachments.AsNoTracking().Where(a => !a.IsDeleted).ToListAsync(ct);
        }

        var inCloud = files.Where(f => f.Path.StartsWith(SyncFormat.AttachmentsPrefix, StringComparison.Ordinal))
            .Select(f => f.Path).ToHashSet(StringComparer.Ordinal);
        var automatic = (_connectivity?.IsUnmetered ?? true) || (_downloadOnMobile?.Invoke() ?? false);
        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            var local = LocalPath(row)!;
            var cloud = CloudPath(row);
            if (File.Exists(local))
            {
                if (!inCloud.Contains(cloud))
                {
                    await using var stream = new FileStream(local, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
                    await _storage.UploadAsync(cloud, stream, ct);
                    AddDetail(SyncDetailKind.AttachmentUploaded);
                }
            }
            else if (automatic && inCloud.Contains(cloud))
            {
                await DownloadFileAsync(row, ct);
            }
        }
    }

    /// <inheritdoc />
    public async Task<bool> EnsureDownloadedAsync(Guid attachmentId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var row = await db.Attachments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == attachmentId, ct);
        if (row is null || LocalPath(row) is not { } local)
        {
            return false;
        }

        return File.Exists(local) || await DownloadFileAsync(row, ct);
    }

    // Streams the file into "<name>.part", checks the SHA-256 and only then moves it into place. A wrong hash is logged and the
    // download is repeated once; after the second failure the file is not kept.
    private async Task<bool> DownloadFileAsync(Attachment row, CancellationToken ct)
    {
        var local = LocalPath(row)!;
        await _downloadGate.WaitAsync(ct);
        try
        {
            if (File.Exists(local))
            {
                return true;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(local)!);
            var part = local + ".part";
            for (var attempt = 1; attempt <= 2; attempt++)
            {
                string hash;
                try
                {
                    await using var source = await _storage.DownloadAsync(CloudPath(row), ct);
                    await using var target = new FileStream(part, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
                    using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                    var buffer = new byte[81920];
                    int read;
                    while ((read = await source.ReadAsync(buffer, ct)) > 0)
                    {
                        hasher.AppendData(buffer, 0, read);
                        await target.WriteAsync(buffer.AsMemory(0, read), ct);
                    }

                    hash = Convert.ToHexStringLower(hasher.GetHashAndReset());
                }
                catch (SyncException ex) when (ex.Kind == SyncErrorKind.NotFound)
                {
                    TryDelete(part);
                    return false;
                }
                catch
                {
                    TryDelete(part);
                    throw;
                }

                if (row.Sha256.Length == 0 || string.Equals(hash, row.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    File.Move(part, local, overwrite: true);
                    AddDetail(SyncDetailKind.AttachmentDownloaded);
                    _notifier?.Notify();
                    return true;
                }

                TryDelete(part);
                // The file name stays out of the Details list; the error log has it.
                var message = $"Checksum of {CloudPath(row)} does not match (attempt {attempt}).";
                AddDetail(SyncDetailKind.AttachmentChecksum);
                _log?.Invoke("Sync", new InvalidDataException(message));
            }

            return false;
        }
        finally
        {
            _downloadGate.Release();
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    // The device that deleted an attachment removes its cloud file after the retention period (30 days). It remembers the
    // deletion when it uploads the row; other devices never delete cloud files of attachments.
    private async Task RecordDeletedAttachmentsAsync((string Table, string Id, long Stamp)[] chunk, CancellationToken ct)
    {
        var ids = chunk.Where(c => c.Table == "Attachments").Select(c => Guid.TryParse(c.Id, out var id) ? id : (Guid?)null).OfType<Guid>().ToList();
        if (ids.Count == 0)
        {
            return;
        }

        await using var db = await _factory.CreateDbContextAsync(ct);
        var deleted = await db.Attachments.IgnoreQueryFilters().AsNoTracking().Where(a => ids.Contains(a.Id) && a.IsDeleted).Select(a => new { a.Id, a.UpdatedAt }).ToListAsync(ct);
        foreach (var row in deleted)
        {
            await _state.SetAsync(SyncStateStore.DeletedAttachmentPrefix + row.Id, row.UpdatedAt.UtcTicks.ToString(CultureInfo.InvariantCulture), ct);
        }
    }

    private async Task CleanUpDeletedAttachmentsAsync(CancellationToken ct)
    {
        foreach (var (key, value) in await _state.ListPrefixAsync(SyncStateStore.DeletedAttachmentPrefix, ct))
        {
            if (!Guid.TryParse(key, out var id) || !long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks))
            {
                await _state.RemoveAsync(SyncStateStore.DeletedAttachmentPrefix + key, ct);
                continue;
            }

            await using var db = await _factory.CreateDbContextAsync(ct);
            var row = await db.Attachments.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);
            if (row is not { IsDeleted: true })
            {
                // Restored or gone: the cloud file must stay (or nothing is left to remove).
                await _state.RemoveAsync(SyncStateStore.DeletedAttachmentPrefix + key, ct);
                continue;
            }

            if (_clock.UtcNow - new DateTimeOffset(ticks, TimeSpan.Zero) > _options.Retention)
            {
                await _storage.DeleteAsync(CloudPath(row), ct);
                await _state.RemoveAsync(SyncStateStore.DeletedAttachmentPrefix + key, ct);
                AddDetail(SyncDetailKind.AttachmentDeleted);
            }
        }
    }
}
