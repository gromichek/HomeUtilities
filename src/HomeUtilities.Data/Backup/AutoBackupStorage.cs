using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Backup;

namespace HomeUtilities.Data.Backup;

/// <summary>
/// The automatic copies in <see cref="BackupOptions.AutoBackupsFolder"/>. A copy is a normal backup zip made by
/// <see cref="BackupService"/>; it is written to a temporary name and renamed when complete, so a half-written file is never
/// listed. Old copies are pruned with <see cref="AutoBackupPolicy.SelectExpired"/>. Manual backups and
/// <c>before-restore-*.zip</c> copies live elsewhere and are never touched.
/// </summary>
public sealed class AutoBackupStorage(BackupOptions options, BackupService backup, IClock clock) : IAutoBackupStorage
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <inheritdoc />
    public event EventHandler<AutoBackupResult>? Completed;

    /// <inheritdoc />
    public int KeepCopies { get; set; } = AutoBackupSettings.Default.KeepCopies;

    /// <inheritdoc />
    public bool DatabaseExists => File.Exists(options.DatabasePath);

    /// <inheritdoc />
    public async Task<AutoBackupResult> CreateAsync(AutoBackupReason reason, CancellationToken ct = default)
    {
        AutoBackupResult result;
        try
        {
            await _gate.WaitAsync(ct);
            try
            {
                result = await CreateCoreAsync(reason, ct);
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (Exception ex)
        {
            result = new AutoBackupResult(reason, null, ex);
        }

        RaiseCompleted(result);
        return result;
    }

    /// <inheritdoc />
    public IReadOnlyList<AutoBackupInfo> List()
    {
        var copies = new Dictionary<string, AutoBackupInfo>(StringComparer.Ordinal);
        AddFrom(options.AutoBackupsFolder, copies);

        // After a reinstall only the system copy may exist (Android Auto Backup restored just that folder).
        if (options.SystemCopyFolder is { } system)
        {
            AddFrom(system, copies);
        }

        return [.. copies.Values.OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Name, StringComparer.Ordinal)];
    }

    /// <inheritdoc />
    public Stream? Open(string name)
    {
        if (!AutoBackupNames.TryParse(name, out _, out _))
        {
            return null;
        }

        foreach (var folder in new[] { options.AutoBackupsFolder, options.SystemCopyFolder })
        {
            if (folder is null)
            {
                continue;
            }

            var path = Path.Combine(folder, name);
            if (File.Exists(path))
            {
                return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
        }

        return null;
    }

    private async Task<AutoBackupResult> CreateCoreAsync(AutoBackupReason reason, CancellationToken ct)
    {
        if (!DatabaseExists)
        {
            throw new FileNotFoundException("There is no database to back up yet.", options.DatabasePath);
        }

        Directory.CreateDirectory(options.AutoBackupsFolder);
        var startedAt = clock.UtcNow;
        var name = AutoBackupNames.Format(startedAt, reason);
        var target = Path.Combine(options.AutoBackupsFolder, name);
        var temp = target + ".tmp";
        try
        {
            await using (var file = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await backup.CreateAsync(file, ct);
            }

            File.Move(temp, target, overwrite: true);
        }
        catch
        {
            BackupService.TryDeleteFile(temp);
            throw;
        }

        var copy = new AutoBackupInfo(name, AutoBackupNames.TryParse(name, out var created, out _) ? created : startedAt, new FileInfo(target).Length, reason);
        Prune();
        UpdateSystemCopy(target, name);
        return new AutoBackupResult(reason, copy, null);
    }

    private void Prune()
    {
        try
        {
            foreach (var expired in AutoBackupPolicy.SelectExpired(ListFolder(options.AutoBackupsFolder), KeepCopies, clock.UtcNow))
            {
                BackupService.TryDeleteFile(Path.Combine(options.AutoBackupsFolder, expired.Name));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Pruning is best effort; the next copy tries again.
        }
    }

    // The Android system backup carries only this folder: the newest copy, so it stays far below the 25 MB limit when it can.
    private void UpdateSystemCopy(string source, string name)
    {
        if (options.SystemCopyFolder is not { } folder)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(folder);
            var temp = Path.Combine(folder, name + ".tmp");
            File.Copy(source, temp, overwrite: true);
            File.Move(temp, Path.Combine(folder, name), overwrite: true);
            foreach (var old in Directory.GetFiles(folder).Where(f => !string.Equals(Path.GetFileName(f), name, StringComparison.Ordinal)))
            {
                BackupService.TryDeleteFile(old);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The system copy is a bonus; the real copy exists.
        }
    }

    private static void AddFrom(string folder, Dictionary<string, AutoBackupInfo> into)
    {
        foreach (var copy in ListFolder(folder))
        {
            into.TryAdd(copy.Name, copy);
        }
    }

    private static List<AutoBackupInfo> ListFolder(string folder)
    {
        if (!Directory.Exists(folder))
        {
            return [];
        }

        var copies = new List<AutoBackupInfo>();
        foreach (var path in Directory.EnumerateFiles(folder, "auto-*.zip"))
        {
            var name = Path.GetFileName(path);
            if (AutoBackupNames.TryParse(name, out var createdAt, out var reason))
            {
                copies.Add(new AutoBackupInfo(name, createdAt, new FileInfo(path).Length, reason));
            }
        }

        return copies;
    }

    private void RaiseCompleted(AutoBackupResult result)
    {
        try
        {
            Completed?.Invoke(this, result);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"An automatic backup handler failed: {ex}");
        }
    }
}
