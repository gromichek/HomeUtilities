using HomeUtilities.Core.Sync;
using HomeUtilities.Data.Sync;
using Microsoft.EntityFrameworkCore;

namespace HomeUtilities.Data.Tests.Sync;

public class SyncSnapshotAndJoinTests
{
    [Fact]
    public async Task A_new_device_starts_from_the_snapshot_and_gets_the_same_data_as_A_and_B()
    {
        var cloud = new FakeSyncStorage();
        var options = new SyncOptions("test", SnapshotEvery: 2);
        using var a = await SyncDevice.CreateAsync(cloud, options);
        using var b = await SyncDevice.CreateAsync(cloud, options);
        await a.Engine.EnableAsync(FirstJoinChoice.StartHere);
        await b.Engine.EnableAsync(FirstJoinChoice.Replace);

        // Several syncs on A produce change files and (every 2 files) a snapshot.
        for (var i = 0; i < 4; i++)
        {
            a.Now = a.Now.AddMinutes(1);
            await a.AddPropertyAsync("A" + i);
            await a.SyncAsync();
        }

        await b.AddPropertyAsync("B0");
        await b.SyncAsync();
        await a.SyncAsync();
        Assert.True(cloud.Files.Keys.Count(k => k.StartsWith("snapshot/", StringComparison.Ordinal)) >= 2);

        using var c = await SyncDevice.CreateAsync(cloud, options);
        await c.Engine.EnableAsync(FirstJoinChoice.Replace);

        Assert.Equal(await a.ExportAsync(), await c.ExportAsync());
        Assert.Equal(await b.ExportAsync(), await c.ExportAsync());
        Assert.Equal(["A0", "A1", "A2", "A3", "B0"], await c.LivePropertyNamesAsync());
    }

    [Fact]
    public async Task Change_files_covered_by_a_snapshot_older_than_30_days_are_deleted_by_their_device_only()
    {
        var cloud = new FakeSyncStorage();
        var options = new SyncOptions("test", SnapshotEvery: 1);
        using var a = await SyncDevice.CreateAsync(cloud, options);
        using var b = await SyncDevice.CreateAsync(cloud, options);
        await a.Engine.EnableAsync(FirstJoinChoice.StartHere);
        await b.Engine.EnableAsync(FirstJoinChoice.Replace);

        await a.AddPropertyAsync("Old");
        await a.SyncAsync(); // change file 1 + snapshot covering it
        await b.AddPropertyAsync("Other");
        await b.SyncAsync();
        var aFile = $"changes/{a.Engine.DeviceId}/0000000001.json";
        var bFile = $"changes/{b.Engine.DeviceId}/0000000001.json";
        Assert.Contains(aFile, cloud.Files.Keys);

        // 29 days later: nothing is deleted yet.
        a.Now = a.Now.AddDays(29);
        await a.SyncAsync();
        Assert.Contains(aFile, cloud.Files.Keys);

        // 31 days later: A deletes its own covered file; B's file stays (B deletes its own).
        a.Now = a.Now.AddDays(3);
        await a.SyncAsync();
        Assert.DoesNotContain(aFile, cloud.Files.Keys);
        Assert.Contains(bFile, cloud.Files.Keys);

        // A device that never saw the deleted file still gets everything through the snapshot.
        using var c = await SyncDevice.CreateAsync(cloud, options);
        await c.Engine.EnableAsync(FirstJoinChoice.Replace);
        await c.SyncAsync();
        Assert.Equal(["Old", "Other"], await c.LivePropertyNamesAsync());
    }

    [Fact]
    public async Task First_join_with_an_empty_cloud_uploads_this_devices_data_as_the_first_snapshot()
    {
        var cloud = new FakeSyncStorage();
        using var a = await SyncDevice.CreateAsync(cloud);
        await a.AddPropertyAsync("Mine");

        var info = await a.Engine.InspectAsync();
        Assert.Equal(new FirstJoinInfo(false, true), info);
        Assert.False(info.NeedsChoice);

        await a.Engine.EnableAsync(FirstJoinChoice.StartHere);
        Assert.Single(cloud.Files.Keys, k => k.StartsWith("snapshot/", StringComparison.Ordinal));
        Assert.True(a.Engine.IsEnabled);
        Assert.Equal(0, await a.OutboxCountAsync());
    }

    [Fact]
    public async Task First_join_replace_makes_this_device_equal_to_the_cloud_and_drops_its_own_data()
    {
        var cloud = new FakeSyncStorage();
        using var a = await SyncDevice.CreateAsync(cloud);
        using var b = await SyncDevice.CreateAsync(cloud);
        await a.AddPropertyAsync("Cloud data");
        await a.Engine.EnableAsync(FirstJoinChoice.StartHere);
        await b.AddPropertyAsync("Local only");

        var info = await b.Engine.InspectAsync();
        Assert.True(info.NeedsChoice);
        await b.Engine.EnableAsync(FirstJoinChoice.Replace);

        Assert.Equal(["Cloud data"], await b.LivePropertyNamesAsync());
        Assert.Equal(await a.ExportAsync(), await b.ExportAsync());
        Assert.Equal(0, await b.OutboxCountAsync());
        await a.SyncAsync();
        Assert.Equal(["Cloud data"], await a.LivePropertyNamesAsync());
    }

    [Fact]
    public async Task First_join_merge_keeps_both_sets_of_records()
    {
        var cloud = new FakeSyncStorage();
        using var a = await SyncDevice.CreateAsync(cloud);
        using var b = await SyncDevice.CreateAsync(cloud);
        await a.AddPropertyAsync("From A");
        await a.Engine.EnableAsync(FirstJoinChoice.StartHere);
        await b.AddPropertyAsync("From B");
        await b.Engine.EnableAsync(FirstJoinChoice.Merge);
        await a.SyncAsync();

        Assert.Equal(["From A", "From B"], await a.LivePropertyNamesAsync());
        Assert.Equal(["From A", "From B"], await b.LivePropertyNamesAsync());
    }

    [Fact]
    public async Task First_join_of_an_empty_device_downloads_without_a_question()
    {
        var cloud = new FakeSyncStorage();
        using var a = await SyncDevice.CreateAsync(cloud);
        await a.AddPropertyAsync("Data");
        await a.Engine.EnableAsync(FirstJoinChoice.StartHere);

        using var empty = await SyncDevice.CreateAsync(cloud, seeded: false);
        var info = await empty.Engine.InspectAsync();
        Assert.True(info.CloudHasData);
        Assert.False(info.LocalHasData);
        Assert.False(info.NeedsChoice);

        await empty.Engine.EnableAsync(FirstJoinChoice.Replace);
        Assert.Equal(["Data"], await empty.LivePropertyNamesAsync());
    }

    [Fact]
    public async Task A_failed_first_join_leaves_sync_off_and_the_data_untouched()
    {
        var cloud = new FakeSyncStorage();
        using var a = await SyncDevice.CreateAsync(cloud);
        await a.Engine.EnableAsync(FirstJoinChoice.StartHere);
        using var b = await SyncDevice.CreateAsync(cloud);
        await b.AddPropertyAsync("Keep");
        cloud.Offline = true;

        await Assert.ThrowsAsync<SyncException>(() => b.Engine.EnableAsync(FirstJoinChoice.Replace));
        Assert.False(b.Engine.IsEnabled);
        Assert.Equal(["Keep"], await b.LivePropertyNamesAsync());
    }

    [Fact]
    public async Task Disabling_stops_syncing_and_keeps_the_local_data()
    {
        var cloud = new FakeSyncStorage();
        using var a = await SyncDevice.CreateAsync(cloud);
        await a.Engine.EnableAsync(FirstJoinChoice.StartHere);
        await a.Engine.DisableAsync();

        await a.AddPropertyAsync("Local");
        Assert.False(await a.SyncAsync());
        Assert.Equal(SyncStateKind.Off, a.Engine.Status.Kind);
        Assert.Equal(["Local"], await a.LivePropertyNamesAsync());
    }

    [Fact]
    public async Task Sample_data_round_trips_through_sync()
    {
        var cloud = new FakeSyncStorage();
        using var a = await SyncDevice.CreateAsync(cloud);
        using var b = await SyncDevice.CreateAsync(cloud);
        await a.Fixture.FillSampleAsync();
        await a.Engine.EnableAsync(FirstJoinChoice.StartHere);
        await b.Engine.EnableAsync(FirstJoinChoice.Replace);
        Assert.Equal(await a.ExportAsync(), await b.ExportAsync());

        // The same through change files only: Merge from the changes of a device that never wrote a snapshot of that data.
        using var c = await SyncDevice.CreateAsync(cloud);
        await c.Fixture.FillSampleAsync();
        await c.Engine.EnableAsync(FirstJoinChoice.Merge);
        await a.SyncAsync();
        await b.SyncAsync();
        await c.SyncAsync();
        Assert.Equal(await a.ExportAsync(), await b.ExportAsync());
        Assert.Equal(await a.ExportAsync(), await c.ExportAsync());
        await using var db = await a.Factory.CreateDbContextAsync();
        Assert.True(await db.Properties.CountAsync() >= 2);
    }
}
