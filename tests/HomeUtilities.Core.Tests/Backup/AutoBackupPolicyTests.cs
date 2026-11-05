using HomeUtilities.Core.Backup;

namespace HomeUtilities.Core.Tests.Backup;

public class AutoBackupPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    private static AutoBackupSettings Daily => new(true, 1, 7);

    [Fact]
    public void FirstRun_WithADatabase_IsDue()
    {
        Assert.True(AutoBackupPolicy.IsDue(Daily, Now, null, null, databaseExists: true));
    }

    [Fact]
    public void WithoutADatabase_NothingIsDue()
    {
        Assert.False(AutoBackupPolicy.IsDue(Daily, Now, null, null, databaseExists: false));
    }

    [Fact]
    public void WhenSwitchedOff_NothingIsDue()
    {
        Assert.False(AutoBackupPolicy.IsDue(Daily with { Enabled = false }, Now, null, Now, databaseExists: true));
    }

    [Fact]
    public void OlderThanTheInterval_WithChanges_IsDue()
    {
        var last = Now.AddDays(-1);

        Assert.True(AutoBackupPolicy.IsDue(Daily, Now, last, last.AddHours(3), databaseExists: true));
    }

    [Fact]
    public void YoungerThanTheInterval_IsNotDueEvenWithChanges()
    {
        var last = Now.AddHours(-23);

        Assert.False(AutoBackupPolicy.IsDue(Daily, Now, last, Now.AddMinutes(-5), databaseExists: true));
    }

    [Fact]
    public void OlderThanTheInterval_WithoutChangesSinceTheCopy_IsNotDue()
    {
        var last = Now.AddDays(-5);

        Assert.False(AutoBackupPolicy.IsDue(Daily, Now, last, last.AddMinutes(-1), databaseExists: true));
        Assert.False(AutoBackupPolicy.IsDue(Daily, Now, last, null, databaseExists: true));
    }

    [Theory]
    [InlineData(1, 23, false)]
    [InlineData(1, 24, true)]
    [InlineData(3, 71, false)]
    [InlineData(3, 72, true)]
    [InlineData(7, 24 * 7 - 1, false)]
    [InlineData(7, 24 * 7, true)]
    public void IntervalIsInDays(int days, int hoursSinceCopy, bool due)
    {
        var last = Now.AddHours(-hoursSinceCopy);

        Assert.Equal(due, AutoBackupPolicy.IsDue(Daily with { IntervalDays = days }, Now, last, last.AddMinutes(1), databaseExists: true));
    }

    [Fact]
    public void Settings_OutsideTheChoices_FallBackToTheDefaults()
    {
        Assert.Equal(new AutoBackupSettings(false, 1, 7), new AutoBackupSettings(false, 5, 10).Normalized());
        Assert.Equal(new AutoBackupSettings(true, 3, 14), new AutoBackupSettings(true, 3, 14).Normalized());
    }

    [Fact]
    public void Retention_KeepsTheNewestCopiesAndTheNewestOfEachOfThreePreviousMonths()
    {
        // Now is 2 Nov 2026: 3 newest copies + newest of October, September, August. July and the rest go.
        var copies = new[]
        {
            Copy(2026, 11, 2), Copy(2026, 11, 1), Copy(2026, 10, 31), Copy(2026, 10, 30), Copy(2026, 10, 2),
            Copy(2026, 9, 30), Copy(2026, 9, 1), Copy(2026, 8, 15), Copy(2026, 8, 1), Copy(2026, 7, 31),
        };

        var expired = AutoBackupPolicy.SelectExpired(copies, keepCopies: 3, Now);

        Assert.Equal(
            ["auto-20261030-090000.zip", "auto-20261002-090000.zip", "auto-20260901-090000.zip", "auto-20260801-090000.zip", "auto-20260731-090000.zip"],
            expired.Select(c => c.Name));
    }

    [Fact]
    public void Retention_WithSevenDailyCopies_DeletesNothingFromTheLastWeek()
    {
        var copies = Enumerable.Range(0, 7).Select(d => Copy(2026, 11, 2).Back(d)).ToList();

        Assert.Empty(AutoBackupPolicy.SelectExpired(copies, keepCopies: 7, Now));
    }

    [Fact]
    public void Retention_Deletes_TheEighthDailyCopy()
    {
        var copies = Enumerable.Range(0, 8).Select(d => Copy(2026, 11, 2).Back(d)).ToList();

        var expired = AutoBackupPolicy.SelectExpired(copies, keepCopies: 7, Now);

        // 2 Nov … 27 Oct are the seven newest; 26 Oct is neither among them nor the newest of October (31 Oct is).
        Assert.Equal(["auto-20261026-090000.zip"], expired.Select(c => c.Name));
    }

    [Fact]
    public void Retention_AlwaysKeepsAtLeastOneCopy()
    {
        Assert.Empty(AutoBackupPolicy.SelectExpired([Copy(2026, 11, 2)], keepCopies: 0, Now));
    }

    [Theory]
    [InlineData(AutoBackupReason.Scheduled, "auto-20261102-143005.zip")]
    [InlineData(AutoBackupReason.BeforeImport, "auto-20261102-143005-import.zip")]
    [InlineData(AutoBackupReason.BeforeRestore, "auto-20261102-143005-restore.zip")]
    [InlineData(AutoBackupReason.BeforeWipe, "auto-20261102-143005-wipe.zip")]
    [InlineData(AutoBackupReason.BeforeMigration, "auto-20261102-143005-migration.zip")]
    [InlineData(AutoBackupReason.BeforeCleanUp, "auto-20261102-143005-cleanup.zip")]
    public void Names_RoundTripTimeAndReason(AutoBackupReason reason, string name)
    {
        var at = new DateTimeOffset(2026, 11, 2, 14, 30, 5, TimeSpan.Zero);

        Assert.Equal(name, AutoBackupNames.Format(at, reason));
        Assert.True(AutoBackupNames.TryParse(name, out var parsed, out var parsedReason));
        Assert.Equal((at, reason), (parsed, parsedReason));
    }

    [Theory]
    [InlineData("HomeUtilities-backup-2026-11-02-1430.zip")]
    [InlineData("before-restore-2026-11-02-1430.zip")]
    [InlineData("auto-20261102-143005.zip.tmp")]
    [InlineData("auto-20261302-143005.zip")]
    [InlineData("auto-20261102-143005-other.zip")]
    [InlineData("auto-.zip")]
    public void OtherFiles_AreNotAutomaticCopies(string name)
    {
        Assert.False(AutoBackupNames.TryParse(name, out _, out _));
    }

    private static AutoBackupInfo Copy(int year, int month, int day) =>
        new(AutoBackupNames.Format(new DateTimeOffset(year, month, day, 9, 0, 0, TimeSpan.Zero), AutoBackupReason.Scheduled),
            new DateTimeOffset(year, month, day, 9, 0, 0, TimeSpan.Zero), 1000, AutoBackupReason.Scheduled);
}

internal static class AutoBackupInfoExtensions
{
    public static AutoBackupInfo Back(this AutoBackupInfo copy, int days)
    {
        var at = copy.CreatedAt.AddDays(-days);
        return copy with { Name = AutoBackupNames.Format(at, copy.Reason), CreatedAt = at };
    }
}
