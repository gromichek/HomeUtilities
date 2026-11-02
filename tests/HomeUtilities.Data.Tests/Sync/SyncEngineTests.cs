using System.Text;
using System.Text.Json;
using HomeUtilities.Core.Sync;
using HomeUtilities.Data.Sync;
using Microsoft.EntityFrameworkCore;

namespace HomeUtilities.Data.Tests.Sync;

public class SyncEngineTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);

    // Device A starts the sync, device B joins with "Replace" (the cloud has data, B only its built-in data).
    private static async Task<(SyncDevice A, SyncDevice B, FakeSyncStorage Cloud)> TwoDevicesAsync(SyncOptions? options = null)
    {
        var cloud = new FakeSyncStorage();
        var a = await SyncDevice.CreateAsync(cloud, options);
        var b = await SyncDevice.CreateAsync(cloud, options);
        await a.Engine.EnableAsync(FirstJoinChoice.StartHere);
        await b.Engine.EnableAsync(FirstJoinChoice.Replace);
        return (a, b, cloud);
    }

    [Fact]
    public async Task Created_on_A_appears_on_B_and_an_edit_on_B_comes_back_to_A()
    {
        var (a, b, _) = await TwoDevicesAsync();
        using (a)
        using (b)
        {
            var id = await a.AddPropertyAsync("Flat");
            await a.SyncAsync();
            await b.SyncAsync();
            Assert.Equal(["Flat"], await b.LivePropertyNamesAsync());

            b.Now = T0.AddHours(1);
            await b.RenameAsync(id, "Flat 2");
            await b.SyncAsync();
            await a.SyncAsync();

            Assert.Equal(["Flat 2"], await a.LivePropertyNamesAsync());
            Assert.Equal(await a.ExportAsync(), await b.ExportAsync());
        }
    }

    [Fact]
    public async Task A_delete_beats_an_older_edit_and_a_newer_edit_beats_an_older_delete()
    {
        var (a, b, _) = await TwoDevicesAsync();
        using (a)
        using (b)
        {
            var first = await a.AddPropertyAsync("Delete me");
            var second = await a.AddPropertyAsync("Keep me");
            await a.SyncAsync();
            await b.SyncAsync();

            // First: B edits at 09:00, A deletes at 10:00 -> deleted everywhere.
            b.Now = T0.AddHours(1);
            await b.RenameAsync(first, "Edited");
            a.Now = T0.AddHours(2);
            await a.DeleteAsync(first);

            // Second: A deletes at 11:00, B edits at 12:00 -> kept with B's name.
            a.Now = T0.AddHours(3);
            await a.DeleteAsync(second);
            b.Now = T0.AddHours(4);
            await b.RenameAsync(second, "Kept");

            await a.SyncAsync();
            await b.SyncAsync();
            await a.SyncAsync();

            Assert.True((await a.PropertyAsync(first))!.Value.IsDeleted);
            Assert.True((await b.PropertyAsync(first))!.Value.IsDeleted);
            Assert.Equal(("Kept", false), ((await a.PropertyAsync(second))!.Value.Name, (await a.PropertyAsync(second))!.Value.IsDeleted));
            Assert.Equal(("Kept", false), ((await b.PropertyAsync(second))!.Value.Name, (await b.PropertyAsync(second))!.Value.IsDeleted));
        }
    }

    [Fact]
    public async Task Equal_timestamps_are_decided_by_the_higher_device_id_on_both_devices()
    {
        var (a, b, _) = await TwoDevicesAsync();
        using (a)
        using (b)
        {
            var id = await a.AddPropertyAsync("Start");
            await a.SyncAsync();
            await b.SyncAsync();

            a.Now = T0.AddHours(1);
            b.Now = T0.AddHours(1);
            await a.RenameAsync(id, "From A");
            await b.RenameAsync(id, "From B");
            await a.SyncAsync();
            await b.SyncAsync();
            await a.SyncAsync();

            var expected = string.CompareOrdinal(a.Engine.DeviceId, b.Engine.DeviceId) > 0 ? "From A" : "From B";
            Assert.Equal(expected, (await a.PropertyAsync(id))!.Value.Name);
            Assert.Equal(expected, (await b.PropertyAsync(id))!.Value.Name);
        }
    }

    [Fact]
    public async Task A_soft_deleted_row_comes_back_when_a_newer_version_arrives()
    {
        var (a, b, _) = await TwoDevicesAsync();
        using (a)
        using (b)
        {
            var id = await a.AddPropertyAsync("Back");
            await a.SyncAsync();
            await b.SyncAsync();
            b.Now = T0.AddHours(1);
            await b.DeleteAsync(id);
            await b.SyncAsync();
            await a.SyncAsync();
            Assert.True((await a.PropertyAsync(id))!.Value.IsDeleted);

            // Restored later on B (the same row, deleted flag cleared).
            b.Now = T0.AddHours(2);
            await using (var db = await b.Factory.CreateDbContextAsync())
            {
                var row = await db.Properties.IgnoreQueryFilters().SingleAsync(p => p.Id == id);
                row.IsDeleted = false;
                row.Name = "Resurrected";
                await db.SaveChangesAsync();
            }

            await b.SyncAsync();
            await a.SyncAsync();

            Assert.Equal(("Resurrected", false), ((await a.PropertyAsync(id))!.Value.Name, (await a.PropertyAsync(id))!.Value.IsDeleted));
        }
    }

    [Fact]
    public async Task Received_rows_are_not_uploaded_again_and_applying_twice_changes_nothing()
    {
        var (a, b, cloud) = await TwoDevicesAsync();
        using (a)
        using (b)
        {
            await a.AddPropertyAsync("One");
            await a.SyncAsync();
            await b.SyncAsync();
            var filesAfterB = cloud.Files.Keys.Where(k => k.StartsWith("changes/", StringComparison.Ordinal)).ToList();

            Assert.Equal(0, await b.OutboxCountAsync());
            Assert.DoesNotContain(filesAfterB, f => f.Contains(b.Engine.DeviceId, StringComparison.Ordinal));

            var before = await b.ExportAsync();
            await b.SyncAsync();
            await b.SyncAsync();
            Assert.Equal(before, await b.ExportAsync());

            // Forget what B applied: the same files are applied once more without any change.
            await using (var db = await b.Factory.CreateDbContextAsync())
            {
                await db.Database.ExecuteSqlRawAsync("DELETE FROM \"SyncState\" WHERE \"Key\" LIKE 'Applied:%'");
            }

            await b.SyncAsync();
            Assert.Equal(before, await b.ExportAsync());
            Assert.Equal(0, await b.OutboxCountAsync());
        }
    }

    [Fact]
    public async Task An_interrupted_upload_is_completed_by_the_next_sync_without_duplicates()
    {
        var cloud = new FakeSyncStorage();
        using var a = await SyncDevice.CreateAsync(cloud, new SyncOptions("test", MaxRowsPerFile: 2));
        using var b = await SyncDevice.CreateAsync(cloud, new SyncOptions("test", MaxRowsPerFile: 2));
        await a.Engine.EnableAsync(FirstJoinChoice.StartHere);
        await b.Engine.EnableAsync(FirstJoinChoice.Replace);
        for (var i = 0; i < 5; i++)
        {
            await a.AddPropertyAsync("P" + i);
        }

        cloud.FailAfterUploads = cloud.Uploads + 2;
        await a.SyncAsync();
        Assert.Equal(SyncStateKind.WaitingForNetwork, a.Engine.Status.Kind);
        Assert.True(await a.OutboxCountAsync() > 0);

        cloud.FailAfterUploads = null;
        await a.SyncAsync();
        Assert.Equal(0, await a.OutboxCountAsync());
        await b.SyncAsync();

        Assert.Equal(["P0", "P1", "P2", "P3", "P4"], await b.LivePropertyNamesAsync());
        var own = cloud.Files.Keys.Where(k => k.StartsWith("changes/" + a.Engine.DeviceId, StringComparison.Ordinal)).OrderBy(k => k).ToList();
        Assert.Equal(own.Count, own.Distinct().Count());
        Assert.Equal(await a.ExportAsync(), await b.ExportAsync());
    }

    [Fact]
    public async Task Offline_keeps_the_outbox_and_reports_waiting_for_network()
    {
        var (a, b, cloud) = await TwoDevicesAsync();
        using (a)
        using (b)
        {
            await a.AddPropertyAsync("Later");
            cloud.Offline = true;
            await a.SyncAsync();
            Assert.Equal(SyncStateKind.WaitingForNetwork, a.Engine.Status.Kind);
            Assert.True(await a.OutboxCountAsync() > 0);

            cloud.Offline = false;
            await a.SyncAsync();
            Assert.Equal(SyncStateKind.Synced, a.Engine.Status.Kind);
            await b.SyncAsync();
            Assert.Equal(["Later"], await b.LivePropertyNamesAsync());
        }
    }

    [Fact]
    public async Task Built_in_categories_created_on_both_devices_do_not_break_the_merge()
    {
        var cloud = new FakeSyncStorage();
        using var a = await SyncDevice.CreateAsync(cloud);
        using var b = await SyncDevice.CreateAsync(cloud);
        await a.Engine.EnableAsync(FirstJoinChoice.StartHere);
        await b.Engine.EnableAsync(FirstJoinChoice.Merge);
        await a.SyncAsync();
        await b.SyncAsync();
        await a.SyncAsync();

        Assert.NotEqual(SyncStateKind.Failed, a.Engine.Status.Kind);
        Assert.NotEqual(SyncStateKind.Failed, b.Engine.Status.Kind);
        await using var db = await a.Factory.CreateDbContextAsync();
        var keys = await db.Categories.Where(c => c.SystemKey != null).Select(c => c.SystemKey).ToListAsync();
        Assert.Equal(keys.Count, keys.Distinct().Count());
        Assert.Equal(await a.ExportAsync(), await b.ExportAsync());
    }

    [Fact]
    public async Task The_same_vat_code_on_two_devices_is_kept_once_and_the_other_one_renamed()
    {
        var (a, b, _) = await TwoDevicesAsync();
        using (a)
        using (b)
        {
            var idA = Guid.Parse("00000000-0000-0000-0000-00000000000A");
            var idB = Guid.Parse("00000000-0000-0000-0000-00000000000B");
            await AddVatAsync(a, idA, "NEW1");
            await AddVatAsync(b, idB, "NEW1");
            await a.SyncAsync();
            await b.SyncAsync();
            await a.SyncAsync();

            foreach (var device in new[] { a, b })
            {
                await using var db = await device.Factory.CreateDbContextAsync();
                var rows = await db.VatRates.Where(v => v.Code.StartsWith("NEW1")).OrderBy(v => v.Id).Select(v => v.Code).ToListAsync();
                Assert.Equal(["NEW1", "NEW1-00000000"], rows);
            }

            static async Task AddVatAsync(SyncDevice device, Guid id, string code)
            {
                await using var db = await device.Factory.CreateDbContextAsync();
                db.VatRates.Add(new HomeUtilities.Core.Tariffs.VatRate { Id = id, Code = code, Name = "Test " + id });
                await db.SaveChangesAsync();
            }
        }
    }

    [Fact]
    public async Task A_file_from_a_newer_app_stops_the_sync_and_applies_nothing()
    {
        var (a, b, cloud) = await TwoDevicesAsync();
        using (a)
        using (b)
        {
            await a.AddPropertyAsync("Never seen");
            await a.SyncAsync();
            var path = cloud.Files.Keys.Single(k => k.StartsWith("changes/" + a.Engine.DeviceId, StringComparison.Ordinal));
            var text = Encoding.UTF8.GetString(cloud.Files[path]).Replace("\"formatVersion\":1", "\"formatVersion\":99", StringComparison.Ordinal);
            cloud.Files[path] = Encoding.UTF8.GetBytes(text);

            await b.SyncAsync();
            Assert.Equal(SyncStateKind.UpdateRequired, b.Engine.Status.Kind);
            Assert.Empty(await b.LivePropertyNamesAsync());

            var newerMigration = Encoding.UTF8.GetString(cloud.Files[path]).Replace("\"formatVersion\":99", "\"formatVersion\":1", StringComparison.Ordinal);
            using var doc = JsonDocument.Parse(newerMigration);
            var migration = doc.RootElement.GetProperty("lastMigration").GetString()!;
            cloud.Files[path] = Encoding.UTF8.GetBytes(newerMigration.Replace(migration, "99999999999999_Future", StringComparison.Ordinal));
            await b.SyncAsync();
            Assert.Equal(SyncStateKind.UpdateRequired, b.Engine.Status.Kind);
            Assert.Empty(await b.LivePropertyNamesAsync());
        }
    }

    [Fact]
    public async Task Foreign_files_in_the_folder_are_ignored()
    {
        var (a, b, cloud) = await TwoDevicesAsync();
        using (a)
        using (b)
        {
            cloud.Files["notes.txt"] = Encoding.UTF8.GetBytes("hello");
            cloud.Files["changes/readme.md"] = Encoding.UTF8.GetBytes("hello");
            cloud.Files["changes/" + a.Engine.DeviceId + "/abc.json"] = Encoding.UTF8.GetBytes("{}");
            await a.AddPropertyAsync("Fine");
            await a.SyncAsync();
            await b.SyncAsync();

            Assert.Equal(SyncStateKind.Synced, b.Engine.Status.Kind);
            Assert.Equal(["Fine"], await b.LivePropertyNamesAsync());
        }
    }
}
