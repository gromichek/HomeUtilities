using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Backup;
using Microsoft.EntityFrameworkCore;

namespace HomeUtilities.Data;

/// <summary>
/// Migrates the database and seeds the reference data. If an existing database has pending migrations, an automatic backup
/// copy is made first (when a backup storage is registered). Calls are serialized, so a second caller waits for the first.
/// </summary>
public sealed class DatabaseInitializer(IDbContextFactory<HomeUtilitiesDbContext> factory, IAutoBackupStorage? backups = null) : IDatabaseInitializer
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <inheritdoc />
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            await BackupBeforeMigrationAsync(db, ct);
            await DatabaseMigrator.MigrateAsync(db, ct);
            await DataSeeder.SeedAsync(db, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    // Only for a database that already holds data in an older schema: a new install has nothing to lose. A failed copy is
    // reported by the storage (error log, Backup section) and does not stop the migration.
    private async Task BackupBeforeMigrationAsync(HomeUtilitiesDbContext db, CancellationToken ct)
    {
        if (backups is null || !backups.DatabaseExists)
        {
            return;
        }

        if (!(await db.Database.GetAppliedMigrationsAsync(ct)).Any() || !(await db.Database.GetPendingMigrationsAsync(ct)).Any())
        {
            return;
        }

        await backups.CreateAsync(AutoBackupReason.BeforeMigration, ct);
    }
}
