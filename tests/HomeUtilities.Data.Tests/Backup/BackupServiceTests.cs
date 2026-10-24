using System.IO.Compression;
using System.Text;
using System.Text.Json;
using HomeUtilities.Core.Backup;
using HomeUtilities.Data.Backup;
using Microsoft.Data.Sqlite;

namespace HomeUtilities.Data.Tests.Backup;

public class BackupServiceTests
{
    [Fact]
    public async Task Create_validate_stage_apply_restores_the_same_data_and_attachment_files()
    {
        using var source = new BackupFixture();
        await source.InitializeAsync();
        await source.AddPropertiesAsync("Flat", "Cottage");
        source.WriteAttachment("a1.jpg", "photo one");
        source.WriteAttachment("sub/a2.pdf", "bill two");
        using var file = await source.BackupToMemoryAsync();

        using var target = new BackupFixture();
        var validation = await target.Backup.ValidateAsync(file);
        Assert.True(validation.IsValid);
        Assert.Equal((1, "1.0.0", 2, 0, 2), (validation.Manifest!.FormatVersion, validation.Manifest.AppVersion, validation.Manifest.PropertyCount, validation.Manifest.ReadingCount, validation.Manifest.AttachmentCount));
        Assert.NotEmpty(validation.Manifest.LastMigration);

        Assert.True((await target.Backup.StageRestoreAsync(file)).IsValid);
        var outcome = target.Backup.ApplyPendingRestore();

        Assert.True(outcome!.Succeeded);
        await target.InitializeAsync();
        Assert.Equal(["Cottage", "Flat"], (await target.Properties.ListAsync()).Select(p => p.Name).Order());
        Assert.Equal("photo one", File.ReadAllText(Path.Combine(target.AttachmentsFolder, "a1.jpg")));
        Assert.Equal("bill two", File.ReadAllText(Path.Combine(target.AttachmentsFolder, "sub", "a2.pdf")));
        Assert.False(File.Exists(Path.Combine(target.Options.RestoreFolder, "pending.zip")));
        Assert.Null(target.Backup.ApplyPendingRestore());
    }

    [Fact]
    public async Task Apply_on_a_blocked_ui_thread_with_existing_data_does_not_deadlock()
    {
        // Owner bug 2026-10-24: at app start the restore runs on the UI thread; copying the current data blocked on
        // an async method whose continuations were posted to that (blocked) thread, so the window never appeared.
        using var source = new BackupFixture();
        await source.InitializeAsync();
        await source.AddPropertiesAsync("From backup");
        using var file = await source.BackupToMemoryAsync();

        using var target = new BackupFixture();
        await target.InitializeAsync();
        await target.AddPropertiesAsync("Current");
        await target.Backup.StageRestoreAsync(file);

        RestoreOutcome? outcome = null;
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new NeverRunningContext());
            outcome = target.Backup.ApplyPendingRestore();
        });
        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "ApplyPendingRestore deadlocked");
        Assert.True(outcome!.Succeeded);
        Assert.Single(Directory.GetFiles(target.Options.BackupsFolder, "before-restore-*.zip"));
    }

    /// <summary>Like a UI thread that is busy: posted work never runs.</summary>
    private sealed class NeverRunningContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state)
        {
        }
    }

    [Fact]
    public async Task A_backup_made_while_the_wal_holds_uncommitted_pages_contains_all_rows()
    {
        using var source = new BackupFixture();
        await source.InitializeAsync();
        await source.AddPropertiesAsync("One", "Two", "Three");
        var wal = new FileInfo(source.DatabasePath + "-wal");
        Assert.True(wal.Exists && wal.Length > 0, "the scenario needs rows that live only in the WAL file");

        using var file = await source.BackupToMemoryAsync();

        using var target = new BackupFixture();
        await target.Backup.StageRestoreAsync(file);
        Assert.True(target.Backup.ApplyPendingRestore()!.Succeeded);
        await target.InitializeAsync();
        Assert.Equal(3, (await target.Properties.ListAsync()).Count);
    }

    [Fact]
    public async Task The_database_copy_in_the_zip_is_a_single_file()
    {
        using var source = new BackupFixture();
        await source.InitializeAsync();
        using var file = await source.BackupToMemoryAsync();

        using var zip = new ZipArchive(file, ZipArchiveMode.Read);
        Assert.Equal(["homeutilities.db", "manifest.json"], zip.Entries.Select(e => e.FullName).Order());
        var db = Path.Combine(source.Root, "extracted.db");
        zip.GetEntry("homeutilities.db")!.ExtractToFile(db);
        var header = File.ReadAllBytes(db).AsSpan(18, 2);
        Assert.Equal(new byte[] { 1, 1 }, header.ToArray()); // file format versions 1/1 = rollback journal, not WAL (2/2)
    }

    [Fact]
    public async Task A_backup_whose_migration_is_newer_than_the_app_is_refused()
    {
        using var source = new BackupFixture();
        await source.InitializeAsync();
        using var file = await source.BackupToMemoryAsync();
        var future = Rewrite(file, manifest => manifest with { LastMigration = "29991231235959_FutureChange" });

        var validation = await source.Backup.ValidateAsync(future);
        var staged = await source.Backup.StageRestoreAsync(future);

        Assert.Equal(BackupError.NewerVersion, validation.Error);
        Assert.Equal(BackupError.NewerVersion, staged.Error);
        Assert.False(File.Exists(Path.Combine(source.Options.RestoreFolder, "pending.zip")));
    }

    [Fact]
    public async Task An_older_migration_is_accepted()
    {
        using var source = new BackupFixture();
        await source.InitializeAsync();
        using var file = await source.BackupToMemoryAsync();
        var older = Rewrite(file, manifest => manifest with { LastMigration = "20200101000000_Old" });

        Assert.True((await source.Backup.ValidateAsync(older)).IsValid);
    }

    [Fact]
    public async Task Files_that_are_not_backups_are_refused_without_throwing()
    {
        using var fixture = new BackupFixture();
        await fixture.InitializeAsync();

        var cases = new Dictionary<string, MemoryStream>
        {
            ["empty"] = new(),
            ["garbage"] = new(Encoding.UTF8.GetBytes("this is not a zip file at all")),
            ["random bytes"] = new(Enumerable.Range(0, 5000).Select(i => (byte)(i * 31)).ToArray()),
            ["empty zip"] = Zip([]),
            ["no manifest"] = Zip([("homeutilities.db", SqliteHeaderBytes())]),
            ["no database"] = Zip([("manifest.json", ManifestJson())]),
            ["manifest is not json"] = Zip([("manifest.json", "{ nope"u8.ToArray()), ("homeutilities.db", SqliteHeaderBytes())]),
            ["database is not sqlite"] = Zip([("manifest.json", ManifestJson()), ("homeutilities.db", "not a database file"u8.ToArray())]),
            ["unknown format version"] = Zip([("manifest.json", ManifestJson(formatVersion: 7)), ("homeutilities.db", SqliteHeaderBytes())]),
            ["path escapes the folder"] = Zip([("manifest.json", ManifestJson()), ("homeutilities.db", SqliteHeaderBytes()), ("attachments/../../evil.txt", "x"u8.ToArray())]),
        };

        foreach (var (name, stream) in cases)
        {
            var validation = await fixture.Backup.ValidateAsync(stream);
            Assert.False(validation.IsValid, name);
            Assert.Equal(BackupError.NotABackup, validation.Error);
            Assert.False((await fixture.Backup.StageRestoreAsync(stream)).IsValid, name);
        }

        Assert.False(File.Exists(Path.Combine(fixture.Options.RestoreFolder, "pending.zip")));
    }

    [Fact]
    public async Task A_failed_apply_keeps_the_current_data_and_does_not_retry()
    {
        using var source = new BackupFixture();
        await source.InitializeAsync();
        await source.AddPropertiesAsync("From backup");
        using var file = await source.BackupToMemoryAsync();

        using var target = new BackupFixture();
        await target.InitializeAsync();
        await target.AddPropertiesAsync("Mine");
        target.WriteAttachment("mine.jpg", "my photo");
        await target.Backup.StageRestoreAsync(file);
        File.WriteAllText(Path.Combine(target.Options.RestoreFolder, "pending.zip"), "damaged after staging");

        var outcome = target.Backup.ApplyPendingRestore();

        Assert.False(outcome!.Succeeded);
        Assert.False(string.IsNullOrEmpty(outcome.Message));
        Assert.NotNull(outcome.Exception);
        Assert.Equal("my photo", File.ReadAllText(Path.Combine(target.AttachmentsFolder, "mine.jpg")));
        await target.InitializeAsync();
        Assert.Equal(["Mine"], (await target.Properties.ListAsync()).Select(p => p.Name));
        Assert.False(File.Exists(Path.Combine(target.Options.RestoreFolder, "pending.zip")));
        Assert.True(File.Exists(Path.Combine(target.Options.RestoreFolder, "pending.failed.zip")));
        Assert.Null(target.Backup.ApplyPendingRestore());
    }

    [Fact]
    public async Task A_restore_keeps_a_copy_of_the_replaced_data_that_is_itself_a_valid_backup()
    {
        using var source = new BackupFixture();
        await source.InitializeAsync();
        using var file = await source.BackupToMemoryAsync();

        using var target = new BackupFixture();
        await target.InitializeAsync();
        await target.AddPropertiesAsync("Mine");
        await target.Backup.StageRestoreAsync(file);
        Assert.True(target.Backup.ApplyPendingRestore()!.Succeeded);

        var copy = Assert.Single(Directory.GetFiles(target.Options.BackupsFolder));
        Assert.StartsWith("before-restore-", Path.GetFileName(copy));
        await using var stream = File.OpenRead(copy);
        var validation = await target.Backup.ValidateAsync(stream);
        Assert.Equal(1, validation.Manifest!.PropertyCount);
    }

    [Fact]
    public async Task Only_the_last_three_before_restore_copies_are_kept()
    {
        using var source = new BackupFixture();
        await source.InitializeAsync();
        using var file = await source.BackupToMemoryAsync();

        using var target = new BackupFixture();
        await target.InitializeAsync();
        Directory.CreateDirectory(target.Options.BackupsFolder);
        foreach (var day in new[] { "2020-01-01-0900", "2020-02-01-0900", "2020-03-01-0900", "2020-04-01-0900" })
        {
            File.WriteAllText(Path.Combine(target.Options.BackupsFolder, $"before-restore-{day}.zip"), "old");
        }

        await target.Backup.StageRestoreAsync(file);
        Assert.True(target.Backup.ApplyPendingRestore()!.Succeeded);

        var names = Directory.GetFiles(target.Options.BackupsFolder).Select(Path.GetFileName).Order().ToList();
        Assert.Equal(3, names.Count);
        Assert.Equal(["before-restore-2020-03-01-0900.zip", "before-restore-2020-04-01-0900.zip"], names.Take(2));
        Assert.Equal($"before-restore-{target.Clock.UtcNow.ToLocalTime():yyyy-MM-dd-HHmm}.zip", names[2]);
    }

    [Fact]
    public async Task Restore_replaces_attachments_instead_of_merging_them()
    {
        using var source = new BackupFixture();
        await source.InitializeAsync();
        source.WriteAttachment("kept.jpg", "from backup");
        using var file = await source.BackupToMemoryAsync();

        using var target = new BackupFixture();
        await target.InitializeAsync();
        target.WriteAttachment("other.jpg", "only here");
        await target.Backup.StageRestoreAsync(file);
        target.Backup.ApplyPendingRestore();

        Assert.Equal(["kept.jpg"], Directory.GetFiles(target.AttachmentsFolder).Select(Path.GetFileName));
        Assert.False(Directory.Exists(target.AttachmentsFolder + ".old"));
    }

    [Fact]
    public async Task A_staged_wipe_deletes_the_database_and_attachments_at_startup()
    {
        using var fixture = new BackupFixture();
        await fixture.InitializeAsync();
        await fixture.AddPropertiesAsync("Gone");
        fixture.WriteAttachment("a.jpg", "x");

        fixture.Backup.StageWipe();
        Assert.True(File.Exists(fixture.DatabasePath));
        var outcome = fixture.Backup.ApplyPendingRestore();

        Assert.True(outcome!.Succeeded);
        Assert.False(File.Exists(fixture.DatabasePath));
        Assert.False(File.Exists(fixture.DatabasePath + "-wal"));
        Assert.False(Directory.Exists(fixture.AttachmentsFolder));
        Assert.Null(fixture.Backup.ApplyPendingRestore());
    }

    [Fact]
    public void Nothing_staged_means_nothing_happens()
    {
        using var fixture = new BackupFixture();

        Assert.Null(fixture.Backup.ApplyPendingRestore());
    }

    [Fact]
    public async Task Creating_a_backup_without_a_database_fails_with_a_clear_error()
    {
        using var fixture = new BackupFixture();

        await Assert.ThrowsAsync<FileNotFoundException>(() => fixture.Backup.CreateAsync(new MemoryStream()));
    }

    [Fact]
    public void Suggested_file_name_follows_the_agreed_pattern()
    {
        using var fixture = new BackupFixture();

        Assert.Matches(@"^HomeUtilities-backup-\d{4}-\d{2}-\d{2}-\d{4}\.zip$", fixture.Backup.SuggestFileName());
        Assert.Equal($"HomeUtilities-backup-{fixture.Clock.UtcNow.ToLocalTime():yyyy-MM-dd-HHmm}.zip", fixture.Backup.SuggestFileName());
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static byte[] SqliteHeaderBytes() => [.. "SQLite format 3\0"u8.ToArray(), .. new byte[100]];

    private static byte[] ManifestJson(int formatVersion = 1) =>
        JsonSerializer.SerializeToUtf8Bytes(
            new BackupManifest(formatVersion, "1.0.0", "20261020125612_InitialCreate", DateTimeOffset.UnixEpoch, "Test", 0, 0, 0), Json);

    private static MemoryStream Zip(IEnumerable<(string Name, byte[] Content)> entries)
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                using var entry = zip.CreateEntry(name).Open();
                entry.Write(content);
            }
        }

        stream.Position = 0;
        return stream;
    }

    private static MemoryStream Rewrite(MemoryStream backup, Func<BackupManifest, BackupManifest> change)
    {
        backup.Position = 0;
        var result = new MemoryStream();
        using (var input = new ZipArchive(backup, ZipArchiveMode.Read, leaveOpen: true))
        using (var output = new ZipArchive(result, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in input.Entries)
            {
                using var from = entry.Open();
                using var to = output.CreateEntry(entry.FullName).Open();
                if (entry.FullName == "manifest.json")
                {
                    var manifest = JsonSerializer.Deserialize<BackupManifest>(from, Json)!;
                    JsonSerializer.Serialize(to, change(manifest), Json);
                }
                else
                {
                    from.CopyTo(to);
                }
            }
        }

        backup.Position = 0;
        result.Position = 0;
        return result;
    }
}
