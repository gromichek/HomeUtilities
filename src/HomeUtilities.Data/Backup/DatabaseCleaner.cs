using System.Globalization;
using HomeUtilities.Core.Abstractions;
using Microsoft.Data.Sqlite;

namespace HomeUtilities.Data.Backup;

#pragma warning disable CA2100 // table and column names come from the EF model and the SQLite schema, never from user input

/// <summary>
/// "Clean up the database" (T-037). Soft-deleted rows are removed with raw SQL on its own connection, so the soft-delete
/// interceptor and the sync outbox never see them (like <c>RowApplier</c>). The tables are processed children first with foreign
/// keys switched on; a deleted row that a remaining row still references is kept and reported instead of failing the clean-up.
/// </summary>
public sealed class DatabaseCleaner(BackupOptions options) : IDatabaseCleaner
{
    private const string DeletedColumn = "IsDeleted";

    /// <inheritdoc />
    public async Task<CleanUpPreview> PreviewAsync(CancellationToken ct = default) =>
        await Task.Run(() =>
        {
            if (!File.Exists(options.DatabasePath))
            {
                return new CleanUpPreview(0, 0, 0);
            }

            using var connection = Open();
            var records = 0;
            foreach (var table in SoftDeleteTables(connection))
            {
                ct.ThrowIfCancellationRequested();
                records += (int)Scalar(connection, $"SELECT COUNT(*) FROM {DataExchangeService.Quote(table)} WHERE {DeletedColumn} = 1");
            }

            var files = OrphanFiles(LiveAttachmentIds(connection));
            return new CleanUpPreview(records, files.Count, files.Sum(f => f.Length));
        }, ct);

    /// <inheritdoc />
    public async Task<CleanUpResult> CleanAsync(CancellationToken ct = default) =>
        await Task.Run(() => Clean(ct), ct);

    private CleanUpResult Clean(CancellationToken ct)
    {
        if (!File.Exists(options.DatabasePath))
        {
            return new CleanUpResult(0, 0, 0, 0, 0, []);
        }

        var removed = 0;
        var kept = new List<string>();
        long before;
        HashSet<Guid> live;
        using (var connection = Open())
        {
            Execute(connection, "PRAGMA wal_checkpoint(TRUNCATE)");
            before = new FileInfo(options.DatabasePath).Length;

            // Foreign keys are enforced inside the transaction: a mistake in the order fails loudly instead of corrupting links.
            Execute(connection, "PRAGMA foreign_keys = ON");
            using var transaction = connection.BeginTransaction();
            var tables = SoftDeleteTables(connection);
            var references = ReadReferences(connection);
            foreach (var table in DeleteOrder(tables, references))
            {
                ct.ThrowIfCancellationRequested();
                removed += DeleteDeleted(connection, transaction, table, references);
                var left = (int)Scalar(connection, $"SELECT COUNT(*) FROM {DataExchangeService.Quote(table)} WHERE {DeletedColumn} = 1", transaction);
                if (left > 0)
                {
                    kept.Add($"{table}: {left} deleted record(s) kept because live records still refer to them");
                }
            }

            RemoveOutboxOfMissingRows(connection, transaction, tables);
            transaction.Commit();
            live = LiveAttachmentIds(connection);
        }

        // The database is final now; files go afterwards, so a failed transaction never loses a file.
        var filesRemoved = 0;
        long bytesRemoved = 0;
        foreach (var file in OrphanFiles(live))
        {
            try
            {
                File.Delete(file.FullName);
                filesRemoved++;
                bytesRemoved += file.Length;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Best effort: the file stays and is found by the next clean-up.
            }
        }

        var (after, compactionError) = Compact();
        return new CleanUpResult(removed, filesRemoved, bytesRemoved, before, after, kept, compactionError);
    }

    // VACUUM needs the connections of the app closed (like the backup service): pooled ones are closed here, then a fresh
    // unpooled connection does the work. A busy database is reported, not fatal: the rows are already gone.
    private (long Size, string? Error) Compact()
    {
        try
        {
            SqliteConnection.ClearAllPools();
            using var connection = Open();
            Execute(connection, "VACUUM");
            Execute(connection, "PRAGMA wal_checkpoint(TRUNCATE)");
            return (new FileInfo(options.DatabasePath).Length, null);
        }
        catch (SqliteException ex)
        {
            return (new FileInfo(options.DatabasePath).Length, ex.Message);
        }
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = options.DatabasePath, Mode = SqliteOpenMode.ReadWrite, Pooling = false }.ToString());
        connection.Open();
        return connection;
    }

    /// <summary>The data tables that have an <c>IsDeleted</c> column, by their real names in the file.</summary>
    private static List<string> SoftDeleteTables(SqliteConnection connection)
    {
        var tables = new List<string>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                tables.Add(reader.GetString(0));
            }
        }

        return [.. tables.Where(t => ColumnNames(connection, t).Contains(DeletedColumn, StringComparer.Ordinal))];
    }

    private static List<string> ColumnNames(SqliteConnection connection, string table)
    {
        var names = new List<string>();
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({DataExchangeService.Quote(table)})";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            names.Add(reader.GetString(1));
        }

        return names;
    }

    /// <summary>One foreign key: <paramref name="Child"/> columns point at <paramref name="Parent"/> columns.</summary>
    private sealed record Reference(string Child, string Parent, IReadOnlyList<string> ChildColumns, IReadOnlyList<string> ParentColumns);

    private static List<Reference> ReadReferences(SqliteConnection connection)
    {
        var tables = new List<string>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                tables.Add(reader.GetString(0));
            }
        }

        var references = new List<Reference>();
        foreach (var child in tables)
        {
            var byId = new Dictionary<long, (string Parent, List<string> From, List<string> To)>();
            using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA foreign_key_list({DataExchangeService.Quote(child)})";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var id = reader.GetInt64(0);
                if (!byId.TryGetValue(id, out var entry))
                {
                    entry = (reader.GetString(2), [], []);
                    byId[id] = entry;
                }

                entry.From.Add(reader.GetString(3));
                entry.To.Add(reader.IsDBNull(4) ? "Id" : reader.GetString(4));
            }

            references.AddRange(byId.Values.Select(e => new Reference(child, e.Parent, e.From, e.To)));
        }

        return references;
    }

    /// <summary>Tables ordered so that a table comes before every table it points at (children first).</summary>
    private static List<string> DeleteOrder(List<string> tables, List<Reference> references)
    {
        var ordered = new List<string>();
        var remaining = new HashSet<string>(tables, StringComparer.Ordinal);
        while (remaining.Count > 0)
        {
            // A table is ready when no other remaining table refers to it.
            var ready = remaining
                .Where(t => !references.Any(r => r.Parent == t && r.Child != t && remaining.Contains(r.Child)))
                .OrderBy(t => t, StringComparer.Ordinal)
                .ToList();
            if (ready.Count == 0)
            {
                // A cycle between tables (there is none in the model): fall back to name order, the checks below still protect live rows.
                ready = [remaining.OrderBy(t => t, StringComparer.Ordinal).First()];
            }

            foreach (var table in ready)
            {
                ordered.Add(table);
                remaining.Remove(table);
            }
        }

        return ordered;
    }

    // A deleted row goes unless some other row still refers to it. Children were processed first, so every referrer left is a
    // live one (or a deleted one that is kept itself). RESTRICT foreign keys are checked row by row, not at the end of the
    // statement, so a table that points at itself (Meters.ReplacedMeterId) is cleaned in passes: each pass removes the rows
    // nobody refers to any more, until a pass removes nothing.
    private static int DeleteDeleted(SqliteConnection connection, SqliteTransaction transaction, string table, List<Reference> references)
    {
        var quoted = DataExchangeService.Quote(table);
        var guards = new List<string>();
        foreach (var reference in references.Where(r => r.Parent == table))
        {
            var childQuoted = DataExchangeService.Quote(reference.Child);
            var join = string.Join(" AND ", reference.ChildColumns.Select((c, i) =>
                $"c.{DataExchangeService.Quote(c)} = {quoted}.{DataExchangeService.Quote(reference.ParentColumns[i])}"));
            guards.Add($"NOT EXISTS (SELECT 1 FROM {childQuoted} c WHERE {join})");
        }

        var where = $"{DeletedColumn} = 1" + string.Concat(guards.Select(g => " AND " + g));
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"DELETE FROM {quoted} WHERE {where}";
        var selfReferencing = references.Any(r => r.Child == table && r.Parent == table);
        var removed = 0;
        try
        {
            int pass;
            do
            {
                pass = command.ExecuteNonQuery();
                removed += pass;
            }
            while (selfReferencing && pass > 0);

            return removed;
        }
        catch (SqliteException ex)
        {
            // Names the table in the error log; the transaction is rolled back, nothing was removed.
            throw new InvalidOperationException($"The clean-up failed in the table {table}: {ex.Message}", ex);
        }
    }

    // Outbox entries of rows that no longer exist would make the next sync look for rows that are gone.
    private static void RemoveOutboxOfMissingRows(SqliteConnection connection, SqliteTransaction transaction, List<string> tables)
    {
        if (Scalar(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'SyncOutbox'", transaction) == 0)
        {
            return;
        }

        foreach (var table in tables)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"DELETE FROM \"SyncOutbox\" WHERE \"TableName\" = $table AND \"RowId\" NOT IN (SELECT \"Id\" FROM {DataExchangeService.Quote(table)})";
            command.Parameters.AddWithValue("$table", table);
            command.ExecuteNonQuery();
        }
    }

    private static HashSet<Guid> LiveAttachmentIds(SqliteConnection connection)
    {
        var ids = new HashSet<Guid>();
        if (Scalar(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'Attachments'") == 0)
        {
            return ids;
        }

        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT Id FROM Attachments WHERE {DeletedColumn} = 0";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (Guid.TryParse(reader.GetString(0), out var id))
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    // Files are named "<attachment id><extension>". Anything else in the folder (e.g. a "*.part" download in progress) is not ours to touch.
    private List<FileInfo> OrphanFiles(HashSet<Guid> live)
    {
        if (!Directory.Exists(options.AttachmentsFolder))
        {
            return [];
        }

        return [.. new DirectoryInfo(options.AttachmentsFolder).EnumerateFiles()
            .Where(f => Guid.TryParse(Path.GetFileNameWithoutExtension(f.Name), out var id)
                && !live.Contains(id)
                && !string.Equals(f.Extension, ".part", StringComparison.OrdinalIgnoreCase))];
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static long Scalar(SqliteConnection connection, string sql, SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }
}
