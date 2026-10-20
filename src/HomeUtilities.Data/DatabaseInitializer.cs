using HomeUtilities.Core.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace HomeUtilities.Data;

/// <summary>Migrates the database and seeds the reference data.</summary>
public sealed class DatabaseInitializer(IDbContextFactory<HomeUtilitiesDbContext> factory) : IDatabaseInitializer
{
    /// <inheritdoc />
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await DatabaseMigrator.MigrateAsync(db, ct);
        await DataSeeder.SeedAsync(db, ct);
    }
}
