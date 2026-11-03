using Microsoft.EntityFrameworkCore;

namespace HomeUtilities.Data;

/// <summary>
/// The local outbox of the sync (T-031): one entry per changed row <c>(table, id)</c>, not a <c>SyncEntity</c>, never synced.
/// Re-recording a row gets a newer stamp, so the uploader removes only what it really sent.
/// </summary>
internal static class SyncOutbox
{
    /// <summary>Records the rows (table name, id) as changed.</summary>
    internal static void Record(DbContext context, IEnumerable<(string Table, Guid Id)> rows)
    {
        // Raw SQL on the context's own connection: the rows join the save's transaction when there is one.
        foreach (var (table, id) in rows.Distinct())
        {
            context.Database.ExecuteSqlRaw(
                "INSERT OR REPLACE INTO \"SyncOutbox\" (\"TableName\", \"RowId\", \"Stamp\") "
                + "VALUES ({0}, {1}, (SELECT COALESCE(MAX(\"Stamp\"), 0) + 1 FROM \"SyncOutbox\"))",
                table,
                id.ToString().ToUpperInvariant());
        }
    }
}
