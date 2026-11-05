using System.Globalization;

namespace HomeUtilities.Core.Backup;

/// <summary>Why an automatic backup copy was made.</summary>
public enum AutoBackupReason
{
    /// <summary>The regular copy at app start (interval elapsed and data changed).</summary>
    Scheduled,

    /// <summary>Made just before an Excel import.</summary>
    BeforeImport,

    /// <summary>Made just before a restore or a full JSON import replaces all data.</summary>
    BeforeRestore,

    /// <summary>Made just before "Delete all data".</summary>
    BeforeWipe,

    /// <summary>Made at startup just before pending database migrations are applied.</summary>
    BeforeMigration,

    /// <summary>Made just before "Clean up the database" removes the deleted records for good.</summary>
    BeforeCleanUp,
}

/// <summary>One automatic backup copy on this device.</summary>
/// <param name="Name">File name, e.g. <c>auto-20261102-143005.zip</c>.</param>
/// <param name="CreatedAt">When the copy was started (UTC), read from the file name.</param>
/// <param name="SizeBytes">Size of the zip file.</param>
/// <param name="Reason">Why the copy was made, read from the file name.</param>
public sealed record AutoBackupInfo(string Name, DateTimeOffset CreatedAt, long SizeBytes, AutoBackupReason Reason);

/// <summary>Outcome of one attempt to make an automatic copy. An attempt never throws; a failure is carried here.</summary>
/// <param name="Reason">Why the copy was requested.</param>
/// <param name="Copy">The new copy, or null if the attempt failed.</param>
/// <param name="Error">The failure, or null on success.</param>
public sealed record AutoBackupResult(AutoBackupReason Reason, AutoBackupInfo? Copy, Exception? Error)
{
    /// <summary>True if the copy was made.</summary>
    public bool Succeeded => Copy is not null;
}

/// <summary>User settings of the automatic backups (Settings → Backup).</summary>
/// <param name="Enabled">Whether the regular copy at app start is made. Copies before risky operations are always made.</param>
/// <param name="IntervalDays">Minimum days between regular copies (1, 3 or 7).</param>
/// <param name="KeepCopies">How many of the newest copies are kept (3, 7 or 14); copies of the previous 3 months are kept in addition.</param>
public sealed record AutoBackupSettings(bool Enabled, int IntervalDays, int KeepCopies)
{
    /// <summary>The offered intervals in days.</summary>
    public static IReadOnlyList<int> IntervalChoices { get; } = [1, 3, 7];

    /// <summary>The offered numbers of kept copies.</summary>
    public static IReadOnlyList<int> KeepChoices { get; } = [3, 7, 14];

    /// <summary>On, daily, 7 copies.</summary>
    public static AutoBackupSettings Default { get; } = new(true, 1, 7);

    /// <summary>The same settings with values outside the offered choices replaced by the defaults.</summary>
    public AutoBackupSettings Normalized() => new(
        Enabled,
        IntervalChoices.Contains(IntervalDays) ? IntervalDays : Default.IntervalDays,
        KeepChoices.Contains(KeepCopies) ? KeepCopies : Default.KeepCopies);
}

/// <summary>File names of the automatic copies: <c>auto-yyyyMMdd-HHmmss[-reason].zip</c>, time in UTC.</summary>
public static class AutoBackupNames
{
    private const string Prefix = "auto-";
    private const string Extension = ".zip";
    private const string TimeFormat = "yyyyMMdd-HHmmss";

    /// <summary>The file name of a copy started at the given time.</summary>
    public static string Format(DateTimeOffset createdAt, AutoBackupReason reason) =>
        Prefix + createdAt.UtcDateTime.ToString(TimeFormat, CultureInfo.InvariantCulture) + Suffix(reason) + Extension;

    /// <summary>Reads time and reason from a file name; false for any other file.</summary>
    public static bool TryParse(string fileName, out DateTimeOffset createdAt, out AutoBackupReason reason)
    {
        createdAt = default;
        reason = AutoBackupReason.Scheduled;
        if (fileName is null || !fileName.StartsWith(Prefix, StringComparison.Ordinal) || !fileName.EndsWith(Extension, StringComparison.Ordinal))
        {
            return false;
        }

        var middle = fileName[Prefix.Length..^Extension.Length];
        var timePart = middle.Length >= TimeFormat.Length ? middle[..TimeFormat.Length] : middle;
        var suffix = middle[timePart.Length..];
        if (!DateTime.TryParseExact(timePart, TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var utc))
        {
            return false;
        }

        var parsed = Enum.GetValues<AutoBackupReason>().Where(r => Suffix(r) == suffix).Cast<AutoBackupReason?>().FirstOrDefault();
        if (parsed is null)
        {
            return false;
        }

        createdAt = new DateTimeOffset(utc, TimeSpan.Zero);
        reason = parsed.Value;
        return true;
    }

    private static string Suffix(AutoBackupReason reason) => reason switch
    {
        AutoBackupReason.Scheduled => string.Empty,
        AutoBackupReason.BeforeImport => "-import",
        AutoBackupReason.BeforeRestore => "-restore",
        AutoBackupReason.BeforeWipe => "-wipe",
        AutoBackupReason.BeforeCleanUp => "-cleanup",
        _ => "-migration",
    };
}

/// <summary>The rules of the automatic backups: when a regular copy is due and which copies are deleted.</summary>
public static class AutoBackupPolicy
{
    /// <summary>How many previous calendar months keep their newest copy in addition to the newest copies.</summary>
    public const int KeptPreviousMonths = 3;

    /// <summary>
    /// True if a regular copy should be made now: backups are on, there is a database, and either no copy exists yet or the
    /// newest one is at least <see cref="AutoBackupSettings.IntervalDays"/> old <b>and</b> data changed after it was started.
    /// </summary>
    /// <param name="settings">The user settings.</param>
    /// <param name="now">Current time (UTC).</param>
    /// <param name="lastCopy">Start time of the newest automatic copy of any reason, or null if there is none.</param>
    /// <param name="lastChange">Time of the last known data change, or null if none was recorded.</param>
    /// <param name="databaseExists">Whether the database file exists.</param>
    public static bool IsDue(AutoBackupSettings settings, DateTimeOffset now, DateTimeOffset? lastCopy, DateTimeOffset? lastChange, bool databaseExists)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!settings.Enabled || !databaseExists)
        {
            return false;
        }

        if (lastCopy is not { } last)
        {
            return true;
        }

        return now - last >= TimeSpan.FromDays(settings.Normalized().IntervalDays) && lastChange is { } changed && changed > last;
    }

    /// <summary>
    /// The copies to delete: everything except the newest <paramref name="keepCopies"/> and the newest copy of each of the
    /// <see cref="KeptPreviousMonths"/> calendar months (UTC) before the month of <paramref name="now"/>.
    /// </summary>
    public static IReadOnlyList<AutoBackupInfo> SelectExpired(IEnumerable<AutoBackupInfo> copies, int keepCopies, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(copies);
        var newestFirst = copies.OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Name, StringComparer.Ordinal).ToList();
        var keep = new HashSet<string>(newestFirst.Take(Math.Max(keepCopies, 1)).Select(c => c.Name), StringComparer.Ordinal);

        var firstOfThisMonth = new DateTimeOffset(now.UtcDateTime.Year, now.UtcDateTime.Month, 1, 0, 0, 0, TimeSpan.Zero);
        for (var back = 1; back <= KeptPreviousMonths; back++)
        {
            var start = firstOfThisMonth.AddMonths(-back);
            var end = start.AddMonths(1);
            var newestOfMonth = newestFirst.FirstOrDefault(c => c.CreatedAt >= start && c.CreatedAt < end);
            if (newestOfMonth is not null)
            {
                keep.Add(newestOfMonth.Name);
            }
        }

        return [.. newestFirst.Where(c => !keep.Contains(c.Name))];
    }
}
