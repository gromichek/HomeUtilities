using HomeUtilities.Core;
using HomeUtilities.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HomeUtilities.Data.Tests;

/// <summary>An in-memory SQLite database with migrations applied; contexts share one open connection.</summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public TestDb()
    {
        _connection.Open();
        using var db = NewContext();
        DatabaseMigrator.MigrateAsync(db, CancellationToken.None).GetAwaiter().GetResult();
    }

    public FakeClock Clock { get; } = new(new DateTimeOffset(2026, 10, 20, 8, 0, 0, TimeSpan.Zero));

    public HomeUtilitiesDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<HomeUtilitiesDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new SyncEntityInterceptor(Clock))
            .Options;
        return new HomeUtilitiesDbContext(options);
    }

    public IDbContextFactory<HomeUtilitiesDbContext> Factory => new TestFactory(this);

    public void Dispose() => _connection.Dispose();
}

internal sealed class TestFactory(TestDb test) : IDbContextFactory<HomeUtilitiesDbContext>
{
    public HomeUtilitiesDbContext CreateDbContext() => test.NewContext();
}

public sealed class FakeClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = now;

    public DateOnly Today => DateOnly.FromDateTime(UtcNow.UtcDateTime);
}
