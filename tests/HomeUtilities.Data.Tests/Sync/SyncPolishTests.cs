using System.Security.Cryptography;
using System.Text;
using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Attachments;
using HomeUtilities.Core.Sync;
using HomeUtilities.Data.Sync;
using Microsoft.EntityFrameworkCore;

namespace HomeUtilities.Data.Tests.Sync;

/// <summary>T-032: pause/resume, restore and re-join, replacing the cloud, the file overview and the attachment files.</summary>
public class SyncPolishTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);

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

    // ---- C14 pause / resume ----

    [Fact]
    public async Task Switching_off_and_on_resumes_with_the_same_device_and_uploads_the_changes_made_while_paused()
    {
        var (a, b, _) = await TwoDevicesAsync();
        using (a)
        using (b)
        {
            var deviceBefore = b.Engine.DeviceId;
            await b.Engine.DisableAsync();
            await b.AddPropertyAsync("Made while paused");
            var inA = await a.AddPropertyAsync("Made on A meanwhile");
            await a.SyncAsync();

            var info = await b.Engine.InspectAsync();
            Assert.True(info.CanResume);
            Assert.False(info.NeedsChoice);

            await b.Engine.EnableAsync(FirstJoinChoice.Resume);
            await a.SyncAsync();

            Assert.Equal(deviceBefore, b.Engine.DeviceId);
            Assert.True(b.Engine.IsEnabled);
            Assert.Contains("Made while paused", await a.LivePropertyNamesAsync());
            Assert.Contains("Made on A meanwhile", await b.LivePropertyNamesAsync());
            Assert.NotNull(await b.PropertyAsync(inA));
            Assert.Equal(await a.ExportAsync(), await b.ExportAsync());
        }
    }

    [Fact]
    public async Task A_paused_device_survives_a_restart_and_still_resumes()
    {
        var (a, b, _) = await TwoDevicesAsync();
        using (a)
        using (b)
        {
            await b.Engine.DisableAsync();
            var restarted = NewEngineOver(b);
            await restarted.InitializeAsync();

            Assert.False(restarted.IsEnabled);
            Assert.Equal(b.Engine.DeviceId, restarted.DeviceId);
            Assert.True((await restarted.InspectAsync()).CanResume);
        }
    }

    [Fact]
    public async Task Another_account_or_a_disconnect_is_a_first_join_again()
    {
        var (a, b, _) = await TwoDevicesAsync();
        using (a)
        using (b)
        {
            await b.Engine.DisableAsync();
            b.Email = "other@example.test";
            Assert.False((await b.Engine.InspectAsync()).CanResume);

            b.Email = "anna@example.test";
            Assert.True((await b.Engine.InspectAsync()).CanResume);

            await b.Engine.ForgetJoinAsync();
            Assert.False((await b.Engine.InspectAsync()).CanResume);
            Assert.Equal(string.Empty, b.Engine.DeviceId);
        }
    }

    // ---- C9 restore, re-join, replace the cloud ----

    private static async Task RestoreAsync(SyncDevice device, MemoryStream backup)
    {
        Assert.True((await device.Fixture.Backup.StageRestoreAsync(backup)).IsValid);
        Assert.True(device.Fixture.Backup.ApplyPendingRestore()!.Succeeded);
    }

    [Fact]
    public async Task A_restore_switches_sync_off_and_asks_to_join_again()
    {
        var (a, b, _) = await TwoDevicesAsync();
        using (a)
        using (b)
        {
            await b.AddPropertyAsync("Before the copy");
            var copy = await b.Fixture.BackupToMemoryAsync();
            await b.AddPropertyAsync("After the copy");
            await b.SyncAsync();

            await RestoreAsync(b, copy);
            var restarted = NewEngineOver(b);
            await restarted.InitializeAsync();

            Assert.False(restarted.IsEnabled);
            Assert.True(restarted.NeedsRejoin);
            var info = await restarted.InspectAsync();
            Assert.True(info.NeedsRejoin);
            Assert.False(info.CanResume);
            Assert.False(await restarted.SyncAsync(SyncTrigger.Manual));
        }
    }

    [Fact]
    public async Task Replacing_the_cloud_with_a_restored_device_makes_the_other_device_ask_and_then_follow()
    {
        var (a, b, cloud) = await TwoDevicesAsync();
        using (a)
        using (b)
        {
            await b.AddPropertyAsync("Kept in the copy");
            await b.SyncAsync();
            await a.SyncAsync();
            var copy = await b.Fixture.BackupToMemoryAsync();
            await b.AddPropertyAsync("Not in the copy");
            await b.SyncAsync();
            await a.SyncAsync();
            Assert.Contains("Not in the copy", await a.LivePropertyNamesAsync());

            await RestoreAsync(b, copy);
            var restored = NewEngineOver(b);
            await restored.InitializeAsync();
            await restored.EnableAsync(FirstJoinChoice.ReplaceCloud);

            // One snapshot of generation 1 is left; the old change files and snapshots are gone.
            Assert.Single(cloud.Files.Keys, k => k.StartsWith("snapshot/", StringComparison.Ordinal));
            Assert.Contains(cloud.Files.Keys, k => k.EndsWith("-g1.json.gz", StringComparison.Ordinal));
            Assert.DoesNotContain(cloud.Files.Keys, k => k.StartsWith("changes/", StringComparison.Ordinal));

            // A's next sync finds the replaced cloud: nothing is uploaded or applied, the status asks.
            await a.AddPropertyAsync("Made on A after the replace");
            var uploadsBefore = cloud.Uploads;
            await a.SyncAsync();
            Assert.Equal(SyncStateKind.CloudReplaced, a.Engine.Status.Kind);
            Assert.Equal(uploadsBefore, cloud.Uploads);
            Assert.True((await a.Engine.InspectAsync()).CloudReplaced);

            await a.Engine.EnableAsync(FirstJoinChoice.Replace);
            Assert.Equal(SyncStateKind.Synced, a.Engine.Status.Kind);
            Assert.Contains("Kept in the copy", await a.LivePropertyNamesAsync());
            Assert.DoesNotContain("Not in the copy", await a.LivePropertyNamesAsync());
            Assert.DoesNotContain("Made on A after the replace", await a.LivePropertyNamesAsync());

            // Both devices work together again in the new generation.
            var id = await a.AddPropertyAsync("New generation");
            await a.SyncAsync();
            await restored.SyncAsync(SyncTrigger.Manual);
            Assert.NotNull(await b.PropertyAsync(id));
        }
    }

    // ---- C12 files in the cloud ----

    [Fact]
    public async Task The_cloud_file_list_groups_change_files_by_device_snapshots_attachments_and_foreign_files()
    {
        var (a, b, cloud) = await TwoDevicesAsync();
        using (a)
        using (b)
        {
            await a.AddPropertyAsync("One");
            await a.SyncAsync();
            await b.AddPropertyAsync("Two");
            await b.SyncAsync();
            cloud.Files["attachments/11111111-1111-1111-1111-111111111111.jpg"] = new byte[2048];
            cloud.Files["notes/readme.txt"] = new byte[10];
            cloud.Modified = T0;

            var report = await a.Engine.ListCloudFilesAsync();

            Assert.Equal(2, report.Devices.Count);
            Assert.True(report.Devices[0].IsThisDevice);
            Assert.Equal(a.Engine.DeviceId, report.Devices[0].DeviceId);
            Assert.Equal(8, report.Devices[0].ShortId.Length);
            Assert.False(report.Devices[1].IsThisDevice);
            Assert.True(report.Devices.All(d => d.Count >= 1 && d.Bytes > 0 && d.Newest == T0));
            Assert.Single(report.Snapshots);
            Assert.Equal(1, report.AttachmentCount);
            Assert.Equal(2048, report.AttachmentBytes);
            Assert.Equal(1, report.ForeignCount);
            Assert.Equal(10, report.ForeignBytes);
            Assert.Equal(cloud.Files.Values.Sum(v => (long)v.Length), report.TotalBytes);
            Assert.Equal(cloud.Files.Count, report.TotalCount);
        }
    }

    // ---- Part B attachment files ----

    private static async Task<Attachment> AddAttachmentAsync(SyncDevice device, string content)
    {
        var store = device.Fixture.Get<IAttachmentStore>();
        return await store.AddAsync(
            new Attachment { FileName = "bill.pdf", ContentType = AttachmentContentTypes.Pdf, OwnerKind = AttachmentOwnerKind.Bill, OwnerId = Guid.NewGuid() },
            new MemoryStream(Encoding.UTF8.GetBytes(content)));
    }

    private static string CloudPathOf(Attachment a) => $"attachments/{a.Id}.pdf";

    private static string LocalPathOf(SyncDevice device, Attachment a) => Path.Combine(device.Fixture.AttachmentsFolder, a.Id + ".pdf");

    [Fact]
    public async Task The_file_of_a_new_attachment_goes_to_the_cloud_once_after_its_row()
    {
        var (a, b, cloud) = await TwoDevicesAsync();
        using (a)
        using (b)
        {
            var attachment = await AddAttachmentAsync(a, "bill content");
            await a.SyncAsync();

            Assert.Equal("bill content", Encoding.UTF8.GetString(cloud.Files[CloudPathOf(attachment)]));
            Assert.Contains(cloud.Files.Keys, k => k.StartsWith("changes/", StringComparison.Ordinal));

            var uploads = cloud.Uploads;
            await a.SyncAsync();
            Assert.Equal(uploads, cloud.Uploads);
        }
    }

    [Fact]
    public async Task A_failed_file_upload_is_retried_by_the_next_run()
    {
        var (a, b, cloud) = await TwoDevicesAsync();
        using (a)
        using (b)
        {
            var attachment = await AddAttachmentAsync(a, "bill content");
            // The change file with the row goes up, the attachment file does not.
            cloud.FailAfterUploads = cloud.Uploads + 1;
            await a.SyncAsync();
            Assert.Equal(SyncStateKind.WaitingForNetwork, a.Engine.Status.Kind);
            Assert.DoesNotContain(CloudPathOf(attachment), cloud.Files.Keys);

            cloud.FailAfterUploads = null;
            await a.SyncAsync();
            Assert.Contains(CloudPathOf(attachment), cloud.Files.Keys);
        }
    }

    [Fact]
    public async Task On_Wi_Fi_the_file_is_downloaded_by_the_sync_and_the_checksum_is_checked()
    {
        var (a, b, _) = await TwoDevicesAsync();
        using (a)
        using (b)
        {
            var attachment = await AddAttachmentAsync(a, "photo bytes");
            await a.SyncAsync();
            b.Connectivity.IsUnmetered = true;
            await b.SyncAsync();

            var path = LocalPathOf(b, attachment);
            Assert.True(File.Exists(path));
            Assert.Equal(attachment.Sha256, Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))));
            Assert.False(File.Exists(path + ".part"));
        }
    }

    [Fact]
    public async Task On_mobile_data_the_file_waits_for_the_user_unless_the_setting_allows_it()
    {
        var (a, b, _) = await TwoDevicesAsync();
        using (a)
        using (b)
        {
            var attachment = await AddAttachmentAsync(a, "photo bytes");
            await a.SyncAsync();
            b.Connectivity.IsUnmetered = false;
            await b.SyncAsync();
            Assert.False(File.Exists(LocalPathOf(b, attachment)));

            // "Download" pressed by the user.
            Assert.True(await b.Engine.EnsureDownloadedAsync(attachment.Id));
            Assert.True(File.Exists(LocalPathOf(b, attachment)));

            File.Delete(LocalPathOf(b, attachment));
            b.MobileDataAllowed = true;
            await b.SyncAsync();
            Assert.True(File.Exists(LocalPathOf(b, attachment)));
        }
    }

    [Fact]
    public async Task A_file_with_a_wrong_checksum_is_logged_tried_twice_and_not_kept()
    {
        var (a, b, cloud) = await TwoDevicesAsync();
        using (a)
        using (b)
        {
            var attachment = await AddAttachmentAsync(a, "photo bytes");
            await a.SyncAsync();
            cloud.Files[CloudPathOf(attachment)] = Encoding.UTF8.GetBytes("damaged");

            await b.SyncAsync();

            Assert.Equal(SyncStateKind.Synced, b.Engine.Status.Kind);
            Assert.False(File.Exists(LocalPathOf(b, attachment)));
            Assert.False(File.Exists(LocalPathOf(b, attachment) + ".part"));
            Assert.Equal(2, b.Logged.Count);
            Assert.False(await b.Engine.EnsureDownloadedAsync(attachment.Id));

            // Repaired in the cloud: the next run gets it.
            cloud.Files[CloudPathOf(attachment)] = Encoding.UTF8.GetBytes("photo bytes");
            await b.SyncAsync();
            Assert.True(File.Exists(LocalPathOf(b, attachment)));
        }
    }

    [Fact]
    public async Task A_missing_cloud_file_is_not_an_error_and_a_new_device_gets_the_files_by_the_same_rules()
    {
        var (a, b, cloud) = await TwoDevicesAsync();
        using (a)
        using (b)
        {
            var attachment = await AddAttachmentAsync(a, "photo bytes");
            await a.SyncAsync();
            cloud.Files.Remove(CloudPathOf(attachment));
            await b.SyncAsync();
            Assert.Equal(SyncStateKind.Synced, b.Engine.Status.Kind);
            Assert.False(await b.Engine.EnsureDownloadedAsync(attachment.Id));

            // A new device joins with Replace: the row comes with the snapshot, the file by the Wi-Fi rule.
            await a.SyncAsync();
            var third = await SyncDevice.CreateAsync(cloud);
            using (third)
            {
                third.Connectivity.IsUnmetered = true;
                await third.Engine.EnableAsync(FirstJoinChoice.Replace);
                Assert.Contains(CloudPathOf(attachment), cloud.Files.Keys);
                Assert.True(File.Exists(LocalPathOf(third, attachment)));
            }
        }
    }

    [Fact]
    public async Task The_device_that_deleted_an_attachment_removes_its_cloud_file_after_thirty_days()
    {
        var (a, b, cloud) = await TwoDevicesAsync();
        using (a)
        using (b)
        {
            var attachment = await AddAttachmentAsync(a, "photo bytes");
            await a.SyncAsync();
            await b.SyncAsync();

            a.Now = T0.AddDays(1);
            await a.Fixture.Get<IAttachmentStore>().DeleteAsync(attachment.Id);
            await a.SyncAsync();
            await b.SyncAsync();

            a.Now = T0.AddDays(30);
            b.Now = T0.AddDays(60);
            await a.SyncAsync();
            Assert.Contains(CloudPathOf(attachment), cloud.Files.Keys);

            // Another device never removes it.
            await b.SyncAsync();
            Assert.Contains(CloudPathOf(attachment), cloud.Files.Keys);

            a.Now = T0.AddDays(32);
            await a.SyncAsync();
            Assert.DoesNotContain(CloudPathOf(attachment), cloud.Files.Keys);
            Assert.True(File.Exists(LocalPathOf(a, attachment)), "local files of deleted rows stay (known tech debt)");
        }
    }
}
