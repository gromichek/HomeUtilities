#pragma warning disable EF1002, EF1003 // table names come from the EF model, never from user input
using System.Globalization;
using HomeUtilities.Data.Backup;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HomeUtilities.Data.Sync;

/// <summary>Reads and writes the local sync bookkeeping (<c>SyncState</c> and <c>SyncOutbox</c> tables); never synced.</summary>
internal sealed class SyncStateStore(IDbContextFactory<HomeUtilitiesDbContext> factory)
{
    internal const string DeviceIdKey = "DeviceId";
    internal const string EnabledKey = "Enabled";
    internal const string LocalSequenceKey = "LocalSequence";
    internal const string BootstrappedKey = "Bootstrapped";
    internal const string LastSyncKey = "LastSyncAt";
    internal const string LastSnapshotSequenceKey = "LastSnapshotSequence";
    internal const string OwnSnapshotsKey = "OwnSnapshots";
    internal const string AppliedPrefix = "Applied:";

    internal async Task<string?> GetAsync(string key, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return (await db.Database.SqlQueryRaw<string>("SELECT \"Value\" AS \"Value\" FROM \"SyncState\" WHERE \"Key\" = {0}", key).ToListAsync(ct)).FirstOrDefault();
    }

    internal async Task SetAsync(string key, string value, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.Database.ExecuteSqlRawAsync("INSERT OR REPLACE INTO \"SyncState\" (\"Key\", \"Value\") VALUES ({0}, {1})", [key, value], ct);
    }

    internal async Task<long> GetLongAsync(string key, CancellationToken ct) =>
        long.TryParse(await GetAsync(key, ct), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;

    internal Task SetLongAsync(string key, long value, CancellationToken ct) => SetAsync(key, value.ToString(CultureInfo.InvariantCulture), ct);

    /// <summary>Applied sequence of every other device.</summary>
    internal async Task<Dictionary<string, long>> GetAppliedAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var rows = await db.Database.SqlQueryRaw<string>(
            "SELECT \"Key\" || '=' || \"Value\" AS \"Value\" FROM \"SyncState\" WHERE \"Key\" LIKE 'Applied:%'").ToListAsync(ct);
        return rows.Select(r => r.Split('=', 2))
            .ToDictionary(p => p[0][AppliedPrefix.Length..], p => long.Parse(p[1], CultureInfo.InvariantCulture), StringComparer.Ordinal);
    }

    internal async Task ClearAppliedAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM \"SyncState\" WHERE \"Key\" LIKE 'Applied:%'", ct);
    }

    internal async Task<IReadOnlyList<(string Table, string Id, long Stamp)>> ReadOutboxAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            var connection = (SqliteConnection)db.Database.GetDbConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT \"TableName\", \"RowId\", \"Stamp\" FROM \"SyncOutbox\" ORDER BY \"Stamp\"";
            var list = new List<(string, string, long)>();
            using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                list.Add((reader.GetString(0), reader.GetString(1), reader.GetInt64(2)));
            }

            return list;
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    /// <summary>Removes the entries that were sent; an entry recorded again meanwhile (newer stamp) stays.</summary>
    internal async Task RemoveFromOutboxAsync(IEnumerable<(string Table, string Id, long Stamp)> sent, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        foreach (var (table, id, stamp) in sent)
        {
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM \"SyncOutbox\" WHERE \"TableName\" = {0} AND \"RowId\" = {1} AND \"Stamp\" = {2}", [table, id, stamp], ct);
        }
    }

    internal async Task ClearOutboxAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM \"SyncOutbox\"", ct);
    }

    /// <summary>Puts every row of every synced table into the outbox (first join with Merge).</summary>
    internal async Task EnqueueAllAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        foreach (var table in SyncFormat.Tables)
        {
            await db.Database.ExecuteSqlRawAsync(
                $"INSERT OR IGNORE INTO \"SyncOutbox\" (\"TableName\", \"RowId\", \"Stamp\") "
                + $"SELECT {{0}}, \"Id\", (SELECT COALESCE(MAX(\"Stamp\"), 0) + 1 FROM \"SyncOutbox\") FROM {DataExchangeService.Quote(table.Name)}",
                [table.Name], ct);
        }
    }

    /// <summary>True when the device holds user data besides the built-in categories and VAT rates.</summary>
    internal async Task<bool> HasUserDataAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        foreach (var table in SyncFormat.Tables.Where(t => t.Name is not ("Categories" or "VatRates" or "VatRateVersions")))
        {
            var count = (await db.Database.SqlQueryRaw<long>($"SELECT COUNT(*) AS \"Value\" FROM {DataExchangeService.Quote(table.Name)} WHERE \"IsDeleted\" = 0").ToListAsync(ct)).Single();
            if (count > 0)
            {
                return true;
            }
        }

        return false;
    }
}
