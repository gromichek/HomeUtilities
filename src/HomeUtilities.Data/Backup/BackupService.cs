using System.IO.Compression;
using System.Text.Json;
using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Backup;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HomeUtilities.Data.Backup;

/// <summary>
/// Backup file = zip with <c>manifest.json</c>, <c>homeutilities.db</c> (a consistent copy made with the SQLite online
/// backup API) and <c>attachments/…</c>. A restore is staged as <c>pending.zip</c> and applied at the next start,
/// before the database is opened, so no file is replaced while it is in use.
/// </summary>
public sealed class BackupService(BackupOptions options, IClock clock) : IBackupService
{
    /// <summary>Entry name of the manifest.</summary>
    public const string ManifestEntry = "manifest.json";

    /// <summary>Entry name of the database copy.</summary>
    public const string DatabaseEntry = "homeutilities.db";

    /// <summary>Prefix of the attachment entries.</summary>
    public const string AttachmentsPrefix = "attachments/";

    /// <summary>How many <c>before-restore-*.zip</c> copies are kept.</summary>
    public const int KeptBeforeRestoreCopies = 3;

    private const string PendingFileName = "pending.zip";
    private const string FailedFileName = "pending.failed.zip";
    private const string WipeMarkerName = "wipe.flag";
    private static readonly byte[] SqliteHeader = "SQLite format 3\0"u8.ToArray();
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly Lazy<string> LatestKnownMigration = new(() =>
    {
        using var db = new HomeUtilitiesDbContext(new DbContextOptionsBuilder<HomeUtilitiesDbContext>()
            .UseSqlite("Data Source=:memory:").Options);
        return db.Database.GetMigrations().Max() ?? string.Empty;
    });

    private string PendingPath => Path.Combine(options.RestoreFolder, PendingFileName);

    private string WipeMarkerPath => Path.Combine(options.RestoreFolder, WipeMarkerName);

    /// <inheritdoc />
    public string SuggestFileName() =>
        $"HomeUtilities-backup-{clock.UtcNow.ToLocalTime():yyyy-MM-dd-HHmm}.zip";

    /// <inheritdoc />
    public async Task<BackupManifest> CreateAsync(Stream output, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(output);

        var work = NewWorkFolder();
        try
        {
            var dbCopy = Path.Combine(work, DatabaseEntry);
            CopyDatabase(options.DatabasePath, dbCopy);
            var attachments = ListAttachments();
            var manifest = ReadManifestData(dbCopy, attachments.Count);

            using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
            var manifestEntry = zip.CreateEntry(ManifestEntry, CompressionLevel.Optimal);
            await using (var stream = manifestEntry.Open())
            {
                await JsonSerializer.SerializeAsync(stream, manifest, Json, ct);
            }

            await AddFileAsync(zip, DatabaseEntry, dbCopy, ct);
            foreach (var (relative, full) in attachments)
            {
                await AddFileAsync(zip, AttachmentsPrefix + relative, full, ct);
            }

            return manifest;
        }
        finally
        {
            TryDeleteDirectory(work);
        }
    }

    /// <inheritdoc />
    public Task<BackupValidation> ValidateAsync(Stream backup, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(backup);
        return Task.FromResult(Validate(backup));
    }

    /// <inheritdoc />
    public async Task<BackupValidation> StageRestoreAsync(Stream backup, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(backup);

        var validation = Validate(backup);
        if (!validation.IsValid)
        {
            return validation;
        }

        Directory.CreateDirectory(options.RestoreFolder);
        var temp = PendingPath + ".tmp";
        backup.Position = 0;
        await using (var file = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await backup.CopyToAsync(file, ct);
        }

        File.Move(temp, PendingPath, overwrite: true);
        return validation;
    }

    /// <inheritdoc />
    public void StageWipe()
    {
        Directory.CreateDirectory(options.RestoreFolder);
        File.WriteAllText(WipeMarkerPath, string.Empty);
    }

    /// <inheritdoc />
    public RestoreOutcome? ApplyPendingRestore()
    {
        if (File.Exists(WipeMarkerPath))
        {
            return Run(Wipe);
        }

        return File.Exists(PendingPath) ? Run(ApplyRestore) : null;
    }

    private static RestoreOutcome Run(Action action)
    {
        try
        {
            action();
            return RestoreOutcome.Success;
        }
        catch (Exception ex)
        {
            return RestoreOutcome.Failure(ex);
        }
    }

    private void Wipe()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(options.DatabasePath);
        File.Delete(options.DatabasePath + "-wal");
        File.Delete(options.DatabasePath + "-shm");
        if (Directory.Exists(options.AttachmentsFolder))
        {
            Directory.Delete(options.AttachmentsFolder, recursive: true);
        }

        File.Delete(WipeMarkerPath);
    }

    private void ApplyRestore()
    {
        try
        {
            ApplyPending();
        }
        catch
        {
            // Keep the file for diagnosis but make sure the next start does not try (and fail) again.
            try
            {
                if (File.Exists(PendingPath))
                {
                    File.Move(PendingPath, Path.Combine(options.RestoreFolder, FailedFileName), overwrite: true);
                }
            }
            catch (Exception)
            {
                // Nothing more can be done; the original failure is reported.
            }

            throw;
        }
    }

    private void ApplyPending()
    {
        SqliteConnection.ClearAllPools();
        var newDb = options.DatabasePath + ".restoring";
        var newAttachments = options.AttachmentsFolder + ".restoring";
        var oldAttachments = options.AttachmentsFolder + ".old";
        TryDeleteDirectory(newAttachments);
        TryDeleteDirectory(oldAttachments);
        TryDeleteFile(newDb);

        using (var pending = new FileStream(PendingPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var validation = Validate(pending);
            if (!validation.IsValid)
            {
                throw new InvalidDataException($"The staged file is not a usable backup ({validation.Error}).");
            }

            pending.Position = 0;
            using var zip = new ZipArchive(pending, ZipArchiveMode.Read, leaveOpen: true);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(newDb))!);
            zip.GetEntry(DatabaseEntry)!.ExtractToFile(newDb, overwrite: true);
            ExtractAttachments(zip, newAttachments);
        }

        try
        {
            SaveCopyOfCurrentData();

            var hadAttachments = Directory.Exists(options.AttachmentsFolder);
            if (hadAttachments)
            {
                Directory.Move(options.AttachmentsFolder, oldAttachments);
            }

            try
            {
                Directory.Move(newAttachments, options.AttachmentsFolder);
                DeleteSidecars();
                File.Move(newDb, options.DatabasePath, overwrite: true);
            }
            catch
            {
                TryDeleteDirectory(options.AttachmentsFolder);
                if (hadAttachments)
                {
                    Directory.Move(oldAttachments, options.AttachmentsFolder);
                }

                throw;
            }

            TryDeleteDirectory(oldAttachments);
        }
        catch
        {
            TryDeleteFile(newDb);
            TryDeleteDirectory(newAttachments);
            throw;
        }

        File.Delete(PendingPath);
    }

    private void SaveCopyOfCurrentData()
    {
        if (!File.Exists(options.DatabasePath))
        {
            return;
        }

        Directory.CreateDirectory(options.BackupsFolder);
        var name = $"before-restore-{clock.UtcNow.ToLocalTime():yyyy-MM-dd-HHmm}.zip";
        using (var file = new FileStream(Path.Combine(options.BackupsFolder, name), FileMode.Create, FileAccess.Write, FileShare.None))
        {
            CreateAsync(file).GetAwaiter().GetResult();
        }

        var old = Directory.GetFiles(options.BackupsFolder, "before-restore-*.zip")
            .OrderByDescending(Path.GetFileName, StringComparer.Ordinal)
            .Skip(KeptBeforeRestoreCopies);
        foreach (var path in old)
        {
            TryDeleteFile(path);
        }
    }

    private BackupValidation Validate(Stream backup)
    {
        try
        {
            backup.Position = 0;
            using var zip = new ZipArchive(backup, ZipArchiveMode.Read, leaveOpen: true);
            if (zip.Entries.Any(e => !IsSafeEntryName(e.FullName)))
            {
                return BackupValidation.Invalid(BackupError.NotABackup);
            }

            var manifestEntry = zip.GetEntry(ManifestEntry);
            var dbEntry = zip.GetEntry(DatabaseEntry);
            if (manifestEntry is null || dbEntry is null)
            {
                return BackupValidation.Invalid(BackupError.NotABackup);
            }

            BackupManifest? manifest;
            using (var stream = manifestEntry.Open())
            {
                manifest = JsonSerializer.Deserialize<BackupManifest>(stream, Json);
            }

            if (manifest is null
                || manifest.FormatVersion != BackupManifest.CurrentFormatVersion
                || string.IsNullOrWhiteSpace(manifest.LastMigration))
            {
                return BackupValidation.Invalid(BackupError.NotABackup);
            }

            var header = new byte[SqliteHeader.Length];
            using (var stream = dbEntry.Open())
            {
                if (stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false) < header.Length
                    || !header.AsSpan().SequenceEqual(SqliteHeader))
                {
                    return BackupValidation.Invalid(BackupError.NotABackup);
                }
            }

            return string.CompareOrdinal(manifest.LastMigration, LatestKnownMigration.Value) > 0
                ? BackupValidation.Invalid(BackupError.NewerVersion)
                : BackupValidation.Valid(manifest);
        }
        catch (Exception ex) when (ex is InvalidDataException or JsonException or IOException or NotSupportedException or ArgumentException)
        {
            return BackupValidation.Invalid(BackupError.NotABackup);
        }
    }

    private static bool IsSafeEntryName(string name) =>
        !name.StartsWith('/') && !name.Contains('\\') && !name.Contains(':')
        && !name.Split('/').Contains("..");

    private static void ExtractAttachments(ZipArchive zip, string folder)
    {
        Directory.CreateDirectory(folder);
        var root = Path.GetFullPath(folder) + Path.DirectorySeparatorChar;
        foreach (var entry in zip.Entries.Where(e => e.FullName.StartsWith(AttachmentsPrefix, StringComparison.Ordinal) && e.Name.Length > 0))
        {
            var target = Path.GetFullPath(Path.Combine(folder, entry.FullName[AttachmentsPrefix.Length..]));
            if (!target.StartsWith(root, StringComparison.Ordinal))
            {
                throw new InvalidDataException("An attachment path leaves the attachments folder.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, overwrite: true);
        }
    }

    private BackupManifest ReadManifestData(string databaseCopy, int attachmentCount)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = databaseCopy, Pooling = false }.ToString());
        connection.Open();
        var migration = Scalar<string>(connection, "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC LIMIT 1") ?? string.Empty;
        return new BackupManifest(
            BackupManifest.CurrentFormatVersion,
            options.AppVersion,
            migration,
            clock.UtcNow,
            options.Platform,
            (int)Scalar<long>(connection, "SELECT COUNT(*) FROM Properties WHERE IsDeleted = 0"),
            (int)Scalar<long>(connection, "SELECT COUNT(*) FROM MeterReadings WHERE IsDeleted = 0"),
            attachmentCount);
    }

    private static T? Scalar<T>(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = command.ExecuteScalar();
        return value is null or DBNull ? default : (T)Convert.ChangeType(value, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void CopyDatabase(string source, string destination)
    {
        if (!File.Exists(source))
        {
            throw new FileNotFoundException("There is no database to back up yet.", source);
        }

        using (var from = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = source, Mode = SqliteOpenMode.ReadWrite, Pooling = false }.ToString()))
        using (var to = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = destination, Pooling = false }.ToString()))
        {
            from.Open();
            to.Open();
            from.BackupDatabase(to);

            // A single self-contained file: no WAL mode flag in the copy's header.
            using var command = to.CreateCommand();
            command.CommandText = "PRAGMA journal_mode=DELETE;";
            command.ExecuteNonQuery();
        }
    }

    private List<(string Relative, string Full)> ListAttachments()
    {
        if (!Directory.Exists(options.AttachmentsFolder))
        {
            return [];
        }

        var root = Path.GetFullPath(options.AttachmentsFolder);
        return [.. Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(f => (Relative: Path.GetRelativePath(root, f).Replace('\\', '/'), Full: f))
            .OrderBy(f => f.Relative, StringComparer.Ordinal)];
    }

    private static async Task AddFileAsync(ZipArchive zip, string entryName, string path, CancellationToken ct)
    {
        var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        await using var target = entry.Open();
        await using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, useAsync: true);
        await source.CopyToAsync(target, ct);
    }

    private string NewWorkFolder()
    {
        var path = Path.Combine(options.RestoreFolder, "work-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private void DeleteSidecars()
    {
        TryDeleteFile(options.DatabasePath + "-wal");
        TryDeleteFile(options.DatabasePath + "-shm");
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort; a leftover temporary file is harmless.
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort.
        }
    }
}
