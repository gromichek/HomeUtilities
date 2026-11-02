using System.Globalization;
using HomeUtilities.Data.Backup;
using Microsoft.Data.Sqlite;

namespace HomeUtilities.Data.Sync;

/// <summary>
/// Writes received rows with raw SQL, so the interceptor does not stamp them and the outbox does not see them (no echo).
/// Merge per row: the newer <c>UpdatedAt</c> wins; on equal values the higher device id wins. A soft-deleted local row that comes
/// back newer is updated in place (resurrect). Unique-index clashes are resolved deterministically (see <see cref="ResolveUnique"/>).
/// </summary>
internal sealed class RowApplier(SqliteConnection connection, SqliteTransaction transaction, string localDevice)
{
    private readonly Dictionary<string, string> _upserts = [];

    internal int Written { get; private set; }

    /// <summary>Texts of the unique-index clashes that were resolved or could not be resolved.</summary>
    internal List<string> Conflicts { get; } = [];

    internal void Purge(DataExchangeService.TableInfo table)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"DELETE FROM {DataExchangeService.Quote(table.Name)}";
        command.ExecuteNonQuery();
    }

    internal void Apply(DataExchangeService.TableInfo table, List<object?[]> rows, string senderDevice, bool force)
    {
        var idIndex = IndexOf(table, "Id");
        var updatedIndex = IndexOf(table, "UpdatedAt");
        foreach (var values in rows)
        {
            var id = (string)values[idIndex]!;
            if (!force && ExistingUpdatedAt(table, id) is { } existing)
            {
                var incoming = (long)values[updatedIndex]!;
                if (incoming < existing || (incoming == existing && string.CompareOrdinal(senderDevice, localDevice) <= 0))
                {
                    continue;
                }
            }

            try
            {
                Upsert(table, values);
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 19 && ex.Message.Contains("UNIQUE", StringComparison.Ordinal))
            {
                if (!ResolveUnique(table, values, id))
                {
                    Conflicts.Add($"{table.Name} {id}: duplicate key, row skipped");
                    continue;
                }

                Upsert(table, values);
            }

            Written++;
        }
    }

    private long? ExistingUpdatedAt(DataExchangeService.TableInfo table, string id)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT \"UpdatedAt\" FROM {DataExchangeService.Quote(table.Name)} WHERE \"Id\" = $id";
        command.Parameters.AddWithValue("$id", id);
        return command.ExecuteScalar() is { } value and not DBNull ? Convert.ToInt64(value, CultureInfo.InvariantCulture) : null;
    }

    private void Upsert(DataExchangeService.TableInfo table, object?[] values)
    {
        if (!_upserts.TryGetValue(table.Name, out var sql))
        {
            var columns = table.Columns.Select(c => DataExchangeService.Quote(c.Name)).ToList();
            var updates = table.Columns.Where(c => c.Name != "Id").Select(c => $"{DataExchangeService.Quote(c.Name)} = excluded.{DataExchangeService.Quote(c.Name)}");
            sql = $"INSERT INTO {DataExchangeService.Quote(table.Name)} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", columns.Select((_, i) => "$p" + i))}) "
                + $"ON CONFLICT(\"Id\") DO UPDATE SET {string.Join(", ", updates)}";
            _upserts[table.Name] = sql;
        }

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        for (var i = 0; i < values.Length; i++)
        {
            command.Parameters.AddWithValue("$p" + i, values[i] ?? DBNull.Value);
        }

        command.ExecuteNonQuery();
    }

    /// <summary>
    /// The two unique indexes of the model are both filtered on <c>IsDeleted = 0</c>:
    /// <list type="bullet">
    /// <item><c>Categories.SystemKey</c> (every device seeds the built-in categories with its own ids): the row with the lower id keeps
    /// the key, the other one becomes an ordinary category (<c>SystemKey = NULL</c>) — both devices reach the same state.</item>
    /// <item><c>VatRates.Code</c>: the row with the lower id keeps the code, the other one gets <c>-</c> and the first 8 characters
    /// of its id appended.</item>
    /// </list>
    /// Returns false for any other table; the row is then skipped and counted as a conflict.
    /// </summary>
    private bool ResolveUnique(DataExchangeService.TableInfo table, object?[] values, string id)
    {
        switch (table.Name)
        {
            case "Categories":
                return Resolve(table, values, id, "SystemKey", otherId => $"UPDATE \"Categories\" SET \"SystemKey\" = NULL WHERE \"Id\" = '{otherId}'", _ => null);
            case "VatRates":
                return Resolve(
                    table,
                    values,
                    id,
                    "Code",
                    otherId => $"UPDATE \"VatRates\" SET \"Code\" = \"Code\" || '-' || substr(\"Id\", 1, 8) WHERE \"Id\" = '{otherId}'",
                    current => current + "-" + id[..8]);
            default:
                return false;
        }
    }

    private bool Resolve(DataExchangeService.TableInfo table, object?[] values, string id, string column, Func<string, string> renameLocal, Func<string, string?> renameIncoming)
    {
        var index = IndexOf(table, column);
        if (values[index] is not { } key)
        {
            return false;
        }

        using var find = connection.CreateCommand();
        find.Transaction = transaction;
        find.CommandText = $"SELECT \"Id\" FROM {DataExchangeService.Quote(table.Name)} WHERE {DataExchangeService.Quote(column)} = $key AND \"IsDeleted\" = 0 AND \"Id\" <> $id";
        find.Parameters.AddWithValue("$key", key);
        find.Parameters.AddWithValue("$id", id);
        if (find.ExecuteScalar() is not string otherId)
        {
            return false;
        }

        if (string.CompareOrdinal(id, otherId) < 0)
        {
            using var rename = connection.CreateCommand();
            rename.Transaction = transaction;
            rename.CommandText = renameLocal(otherId);
            rename.ExecuteNonQuery();
        }
        else
        {
            values[index] = renameIncoming(Convert.ToString(key, CultureInfo.InvariantCulture)!);
        }

        Conflicts.Add($"{table.Name}: the same {column} ({key}) on two devices, resolved");
        return true;
    }

    private static int IndexOf(DataExchangeService.TableInfo table, string column)
    {
        for (var i = 0; i < table.Columns.Count; i++)
        {
            if (table.Columns[i].Name == column)
            {
                return i;
            }
        }

        throw new InvalidOperationException($"{table.Name} has no column {column}.");
    }
}
