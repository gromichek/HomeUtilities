using HomeUtilities.Core.Sync;
using HomeUtilities.Data.Backup;
using HomeUtilities.Data.Sync;
using Microsoft.EntityFrameworkCore;

namespace HomeUtilities.Data.Tests.Sync;

/// <summary>T-037: after a database clean-up the cloud must be replaced, or sync stays off until the device re-joins.</summary>
public class SyncCleanUpTests
{
    private static async Task<(SyncDevice A, SyncDevice B, FakeSyncStorage Cloud)> TwoDevicesAsync()
    {
        var cloud = new FakeSyncStorage();
        var a = await SyncDevice.CreateAsync(cloud);
        var b = await SyncDevice.CreateAsync(cloud);
        await a.Engine.EnableAsync(FirstJoinChoice.StartHere);
        await b.Engine.EnableAsync(FirstJoinChoice.Replace);
        return (a, b, cloud);
    }

    private static SyncEngine NewEngineOver(SyncDevice device) => new(
        device.Factory, device.Cloud, device.Fixture.Clock, device.Notifier, new SyncOptions("test", AttachmentsFolder: device.Fixture.AttachmentsFolder),
        null, () => device.Email, device.Connectivity, () => device.MobileDataAllowed);

    private static async Task<Dictionary<string, string>> StateOfAsync(SyncDevice device)
    {
        await using var db = await device.Factory.CreateDbContextAsync();
        var rows = await db.Database.SqlQueryRaw<string>("SELECT \"Key\" || '=' || \"Value\" AS \"Value\" FROM \"SyncState\"").ToListAsync();
        return rows.Select(r => r.Split('=', 2)).ToDictionary(p => p[0], p => p[1]);
    }

    [Fact]
    public async Task MarkNeedsRejoin_switches_sync_off_and_the_mark_survives_a_restart()
    {
        var (a, b, _) = await TwoDevicesAsync();
        using (a)
        using (b)
        {
            Assert.True(b.Engine.IsEnabled);

            await b.Engine.MarkNeedsRejoinAsync();

            Assert.False(b.Engine.IsEnabled);
            Assert.True(b.Engine.NeedsRejoin);
            Assert.Equal(SyncStateKind.Off, b.Engine.Status.Kind);
            Assert.False(await b.SyncAsync());
            var state = await StateOfAsync(b);
            Assert.Equal("0", state["Enabled"]);
            Assert.Equal("1", state["NeedsRejoin"]);

            var restarted = NewEngineOver(b);
            await restarted.InitializeAsync();
            Assert.False(restarted.IsEnabled);
            Assert.True(restarted.NeedsRejoin);
            var info = await restarted.InspectAsync();
            Assert.True(info.NeedsRejoin);
            Assert.False(info.CanResume);
        }
    }

    [Fact]
    public async Task MarkNeedsRejoin_does_nothing_on_a_device_that_never_joined()
    {
        using var device = await SyncDevice.CreateAsync(new FakeSyncStorage());

        await device.Engine.MarkNeedsRejoinAsync();

        Assert.False(device.Engine.NeedsRejoin);
        Assert.DoesNotContain("NeedsRejoin", (await StateOfAsync(device)).Keys);
    }

    [Fact]
    public async Task A_cleaned_device_that_cancels_the_replace_ends_off_and_needing_a_rejoin_and_the_purged_row_stays_gone()
    {
        var (a, b, _) = await TwoDevicesAsync();
        using (a)
        using (b)
        {
            await b.AddPropertyAsync("Kept");
            var goneId = await b.AddPropertyAsync("Mistake");
            await b.SyncAsync();
            await a.SyncAsync();
            await b.DeleteAsync(goneId);
            await b.SyncAsync();

            var result = await new DatabaseCleaner(b.Fixture.Options).CleanAsync();
            await b.Engine.MarkNeedsRejoinAsync(); // the user said "Not now" to replacing the cloud

            Assert.True(result.RecordsRemoved >= 1);
            Assert.Null(await b.PropertyAsync(goneId));
            Assert.False(b.Engine.IsEnabled);
            Assert.True(b.Engine.NeedsRejoin);
            Assert.False(await b.SyncAsync());
            Assert.Null(await b.PropertyAsync(goneId));
            Assert.Equal(["Kept"], await b.LivePropertyNamesAsync());
        }
    }

    [Fact]
    public async Task A_cleaned_device_that_replaces_the_cloud_makes_the_other_device_ask()
    {
        var (a, b, cloud) = await TwoDevicesAsync();
        using (a)
        using (b)
        {
            var goneId = await b.AddPropertyAsync("Mistake");
            await b.AddPropertyAsync("Kept");
            await b.SyncAsync();
            await a.SyncAsync();
            await b.DeleteAsync(goneId);
            await b.SyncAsync();
            await a.SyncAsync();

            await new DatabaseCleaner(b.Fixture.Options).CleanAsync();
            await b.Engine.EnableAsync(FirstJoinChoice.ReplaceCloud);

            Assert.True(b.Engine.IsEnabled);
            Assert.Contains(cloud.Files.Keys, k => k.EndsWith("-g1.json.gz", StringComparison.Ordinal));
            await a.SyncAsync();
            Assert.Equal(SyncStateKind.CloudReplaced, a.Engine.Status.Kind);
            Assert.Null(await b.PropertyAsync(goneId));
            // The new snapshot holds the live row only, so a device that replaces its data with it never sees the purged one.
            await a.Engine.EnableAsync(FirstJoinChoice.Replace);
            Assert.Null(await a.PropertyAsync(goneId));
            Assert.Equal(["Kept"], await a.LivePropertyNamesAsync());
        }
    }
}
