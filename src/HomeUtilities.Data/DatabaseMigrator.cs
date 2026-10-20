using System.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeUtilities.Data;

/// <summary>Applies the migrations and the SQLite settings the app relies on.</summary>
public static class DatabaseMigrator
{
    /// <summary>Sets <c>journal_mode=WAL</c> and <c>foreign_keys=ON</c>, then applies pending migrations.</summary>
    public static async Task MigrateAsync(HomeUtilitiesDbContext db, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);

        var connection = db.Database.GetDbConnection();
        var wasClosed = connection.State != ConnectionState.Open;
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", ct);
            await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=ON;", ct);
            await db.Database.MigrateAsync(ct);
        }
        finally
        {
            if (wasClosed)
            {
                await db.Database.CloseConnectionAsync();
            }
        }
    }
}
