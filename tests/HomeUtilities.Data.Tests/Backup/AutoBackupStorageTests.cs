using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Backup;
using HomeUtilities.Data.Backup;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace HomeUtilities.Data.Tests.Backup;

public class AutoBackupStorageTests
{
    [Fact]
    public async Task Create_writes_a_valid_backup_with_the_time_in_its_name_and_raises_Completed()
    {
        using var device = new BackupFixture();
        await device.InitializeAsync();
        await device.AddPropertiesAsync("Flat");
        var raised = new List<AutoBackupResult>();
        device.AutoBackups.Completed += (_, r) => raised.Add(r);

        var result = await device.AutoBackups.CreateAsync(AutoBackupReason.Scheduled);

        Assert.True(result.Succeeded);
        Assert.Equal("auto-20261024-093000.zip", result.Copy!.Name);
        Assert.Equal(new DateTimeOffset(2026, 10, 24, 9, 30, 0, TimeSpan.Zero), result.Copy.CreatedAt);
        Assert.True(File.Exists(Path.Combine(device.Options.AutoBackupsFolder, "auto-20261024-093000.zip")));
        Assert.Empty(Directory.GetFiles(device.Options.AutoBackupsFolder, "*.tmp"));
        await using var copy = device.AutoBackups.Open(result.Copy.Name)!;
        var validation = await device.Backup.ValidateAsync(copy);
        Assert.True(validation.IsValid);
        Assert.Equal(1, validation.Manifest!.PropertyCount);
        Assert.Same(result, Assert.Single(raised));
        Assert.Equal([result.Copy], device.AutoBackups.List());
    }

    [Fact]
    public async Task Create_without_a_database_reports_the_failure_and_leaves_no_file()
    {
        using var device = new BackupFixture();
        var raised = new List<AutoBackupResult>();
        device.AutoBackups.Completed += (_, r) => raised.Add(r);

        var result = await device.AutoBackups.CreateAsync(AutoBackupReason.Scheduled);

        Assert.False(result.Succeeded);
        Assert.IsType<FileNotFoundException>(result.Error);
        Assert.Empty(device.AutoBackups.List());
        Assert.Single(raised);
    }

    [Fact]
    public async Task Reasons_are_part_of_the_file_name_and_listed_newest_first()
    {
        using var device = new BackupFixture();
        await device.InitializeAsync();
        await device.AutoBackups.CreateAsync(AutoBackupReason.Scheduled);
        device.Clock.UtcNow = device.Clock.UtcNow.AddMinutes(5);
        await device.AutoBackups.CreateAsync(AutoBackupReason.BeforeImport);

        Assert.Equal(
            [("auto-20261024-093500-import.zip", AutoBackupReason.BeforeImport), ("auto-20261024-093000.zip", AutoBackupReason.Scheduled)],
            device.AutoBackups.List().Select(c => (c.Name, c.Reason)));
    }

    [Fact]
    public async Task Old_copies_are_pruned_but_manual_and_before_restore_backups_stay()
    {
        using var device = new BackupFixture();
        await device.InitializeAsync();
        Directory.CreateDirectory(device.Options.BackupsFolder);
        var manual = Path.Combine(device.Options.BackupsFolder, "HomeUtilities-backup-2026-01-01-0800.zip");
        var beforeRestore = Path.Combine(device.Options.BackupsFolder, "before-restore-2026-01-01-0800.zip");
        File.WriteAllText(manual, "manual");
        File.WriteAllText(beforeRestore, "before");
        device.AutoBackups.KeepCopies = 3;

        for (var day = 0; day < 6; day++)
        {
            device.Clock.UtcNow = new DateTimeOffset(2026, 10, 10 + day, 9, 0, 0, TimeSpan.Zero);
            await device.AutoBackups.CreateAsync(AutoBackupReason.Scheduled);
        }

        Assert.Equal(
            ["auto-20261015-090000.zip", "auto-20261014-090000.zip", "auto-20261013-090000.zip"],
            device.AutoBackups.List().Select(c => c.Name));
        Assert.True(File.Exists(manual));
        Assert.True(File.Exists(beforeRestore));
    }

    [Fact]
    public async Task The_newest_copy_of_each_of_the_last_three_months_survives_pruning()
    {
        using var device = new BackupFixture();
        await device.InitializeAsync();
        device.AutoBackups.KeepCopies = 3;
        foreach (var start in new[] { new DateTimeOffset(2026, 7, 31, 9, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 8, 5, 9, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 8, 20, 9, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 9, 12, 9, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 2, 9, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 3, 9, 0, 0, TimeSpan.Zero)})
        {
            device.Clock.UtcNow = start;
            await device.AutoBackups.CreateAsync(AutoBackupReason.Scheduled);
        }

        // Now is 3 Oct: the 3 newest, plus 12 Sep (September), 20 Aug (August) and 31 Jul (July).
        Assert.Equal(
            ["auto-20261003-090000.zip", "auto-20261002-090000.zip", "auto-20261001-090000.zip", "auto-20260912-090000.zip", "auto-20260820-090000.zip", "auto-20260731-090000.zip"],
            device.AutoBackups.List().Select(c => c.Name));
    }

    [Fact]
    public async Task The_system_copy_folder_holds_only_the_newest_copy_and_is_listed_after_a_reinstall()
    {
        using var device = new BackupFixture(systemCopy: true);
        await device.InitializeAsync();
        await device.AutoBackups.CreateAsync(AutoBackupReason.Scheduled);
        device.Clock.UtcNow = device.Clock.UtcNow.AddDays(1);
        await device.AutoBackups.CreateAsync(AutoBackupReason.Scheduled);

        Assert.Equal(["auto-20261025-093000.zip"], Directory.GetFiles(device.Options.SystemCopyFolder!).Select(Path.GetFileName));

        // A reinstall: Android restores only the system copy folder.
        Directory.Delete(device.Options.AutoBackupsFolder, recursive: true);
        var listed = Assert.Single(device.AutoBackups.List());
        Assert.Equal("auto-20261025-093000.zip", listed.Name);
        await using var copy = device.AutoBackups.Open(listed.Name)!;
        Assert.True((await device.Backup.ValidateAsync(copy)).IsValid);
    }

    [Fact]
    public async Task Open_refuses_names_that_are_not_automatic_copies()
    {
        using var device = new BackupFixture();
        await device.InitializeAsync();
        await device.AutoBackups.CreateAsync(AutoBackupReason.Scheduled);

        Assert.Null(device.AutoBackups.Open("../homeutilities.db"));
        Assert.Null(device.AutoBackups.Open("auto-20200101-000000.zip"));
    }

    [Fact]
    public async Task Pending_migrations_of_an_existing_database_make_a_copy_first()
    {
        using var device = new BackupFixture();
        var factory = device.Get<IDbContextFactory<HomeUtilitiesDbContext>>();
        await using (var db = await factory.CreateDbContextAsync())
        {
            db.GetService<IMigrator>().Migrate("20261020125612_InitialCreate");
        }

        await new DatabaseInitializer(factory, device.AutoBackups).InitializeAsync();

        var copy = Assert.Single(device.AutoBackups.List());
        Assert.Equal(AutoBackupReason.BeforeMigration, copy.Reason);
        await using var stream = device.AutoBackups.Open(copy.Name)!;
        Assert.Equal("20261020125612_InitialCreate", (await device.Backup.ValidateAsync(stream)).Manifest!.LastMigration);
    }

    [Fact]
    public async Task An_equalized_plan_from_before_the_carry_over_column_migrates_with_a_null_carry_over()
    {
        // An old backup restores into a database without CarryOverMonthly; the startup migration adds it (T-046).
        using var device = new BackupFixture();
        var factory = device.Get<IDbContextFactory<HomeUtilitiesDbContext>>();
        var id = Guid.CreateVersion7();
        await using (var db = await factory.CreateDbContextAsync())
        {
            db.GetService<IMigrator>().Migrate("20261102120000_AddSyncTables");
            await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF");
            await db.Database.ExecuteSqlAsync(
                $"INSERT INTO EqualizedPlans (Id, ContractId, CreatedAt, IsDeleted, MonthlyAmount, UpdatedAt, ValidFrom) VALUES ({id}, {Guid.CreateVersion7()}, 0, 0, '40.5', 0, '2026-06-01')");
        }

        await using (var db = await factory.CreateDbContextAsync())
        {
            await db.Database.MigrateAsync();
            var plan = await db.EqualizedPlans.SingleAsync(p => p.Id == id);
            Assert.Equal((40.5m, (decimal?)null), (plan.MonthlyAmount, plan.CarryOverMonthly));
        }
    }

    [Fact]
    public async Task A_database_from_before_the_reading_reminder_day_migrates_with_a_null_day()
    {
        // An old backup restores into a database without Contracts.ReadingReminderDay; the startup migration adds it (T-047).
        using var device = new BackupFixture();
        var factory = device.Get<IDbContextFactory<HomeUtilitiesDbContext>>();
        await using (var db = await factory.CreateDbContextAsync())
        {
            db.GetService<IMigrator>().Migrate("20261111120000_AddEqualizedCarryOver");
            Assert.DoesNotContain("ReadingReminderDay", await ColumnsOfAsync(db, "Contracts"));
        }

        await using (var db = await factory.CreateDbContextAsync())
        {
            await db.Database.MigrateAsync();
            Assert.Contains("ReadingReminderDay", await ColumnsOfAsync(db, "Contracts"));
            Assert.Empty(await db.Contracts.Where(c => c.ReadingReminderDay != null).ToListAsync());
        }
    }

    private static async Task<List<string>> ColumnsOfAsync(HomeUtilitiesDbContext db, string table)
    {
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT name FROM pragma_table_info('{table}')";
            await using var reader = await command.ExecuteReaderAsync();
            var names = new List<string>();
            while (await reader.ReadAsync())
            {
                names.Add(reader.GetString(0));
            }

            return names;
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    [Fact]
    public async Task A_new_or_up_to_date_database_makes_no_migration_copy()
    {
        using var device = new BackupFixture();
        var initializer = new DatabaseInitializer(device.Get<IDbContextFactory<HomeUtilitiesDbContext>>(), device.AutoBackups);

        await initializer.InitializeAsync();
        await initializer.InitializeAsync();

        Assert.Empty(device.AutoBackups.List());
    }
}
