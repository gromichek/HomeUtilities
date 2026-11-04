using System.Globalization;
using HomeUtilities.Core.Sync;

namespace HomeUtilities.ViewModels;

/// <summary>Turns the report "Files in the cloud" (C12) into the lines the Sync page shows.</summary>
public static class SyncCloudFilesFormatter
{
    /// <summary>One line per group: change files per device, snapshots, attachment files, foreign files and the total.</summary>
    public static string Format(CloudFilesReport report, ILocalizer localizer)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(localizer);
        var lines = new List<string>();
        foreach (var device in report.Devices)
        {
            lines.Add(localizer.Get(
                device.IsThisDevice ? "SyncCloudDeviceThis" : "SyncCloudDevice",
                device.ShortId,
                device.Count,
                Size(device.Bytes),
                Date(device.Newest)));
        }

        foreach (var snapshot in report.Snapshots)
        {
            lines.Add(localizer.Get("SyncCloudSnapshot", Date(snapshot.At), Size(snapshot.Bytes)));
        }

        if (report.AttachmentCount > 0)
        {
            lines.Add(localizer.Get("SyncCloudAttachments", report.AttachmentCount, Size(report.AttachmentBytes)));
        }

        if (report.ForeignCount > 0)
        {
            lines.Add(localizer.Get("SyncCloudForeign", report.ForeignCount, Size(report.ForeignBytes)));
        }

        lines.Add(localizer.Get("SyncCloudTotal", report.TotalCount, Size(report.TotalBytes)));
        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>"1.5 MB", "12 KB", "300 B" (invariant digits are fine for a technical list).</summary>
    public static string Size(long bytes) => bytes switch
    {
        < 1024 => bytes.ToString(CultureInfo.CurrentCulture) + " B",
        < 1024 * 1024 => (bytes / 1024.0).ToString("0.#", CultureInfo.CurrentCulture) + " KB",
        _ => (bytes / (1024.0 * 1024.0)).ToString("0.#", CultureInfo.CurrentCulture) + " MB",
    };

    private static string Date(DateTimeOffset? at) =>
        at is { } value ? value.ToLocalTime().ToString("g", CultureInfo.CurrentCulture) : "—";
}
