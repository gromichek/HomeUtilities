using System.Globalization;
using System.IO.Compression;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using HomeUtilities.Data.Backup;
using Microsoft.Data.Sqlite;

namespace HomeUtilities.Data.Sync;

/// <summary>Version of the sync file layout; a device refuses files with a higher number (T-031).</summary>
internal static class SyncFormat
{
    internal const int Version = 1;
    internal const string ChangesPrefix = "changes/";
    internal const string SnapshotPrefix = "snapshot/";

    private static readonly Regex ChangePath = new(@"^changes/(?<device>[^/]+)/(?<seq>\d{10})\.json$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex SnapshotPath = new(@"^snapshot/(?<device>[^/]+)-(?<at>\d{14})\.json\.gz$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    internal static string ChangePathFor(string device, long sequence) => $"{ChangesPrefix}{device}/{sequence:D10}.json";

    internal static string SnapshotPathFor(string device, DateTimeOffset at) =>
        $"{SnapshotPrefix}{device}-{at.UtcDateTime.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)}.json.gz";

    /// <summary>Device and sequence of a change file path; null for anything else (foreign files are ignored).</summary>
    internal static (string Device, long Sequence)? ParseChange(string path)
    {
        var m = ChangePath.Match(path);
        return m.Success ? (m.Groups["device"].Value, long.Parse(m.Groups["seq"].Value, CultureInfo.InvariantCulture)) : null;
    }

    /// <summary>Device and creation time of a snapshot path; null for anything else.</summary>
    internal static (string Device, DateTimeOffset At)? ParseSnapshot(string path)
    {
        var m = SnapshotPath.Match(path);
        if (!m.Success
            || !DateTimeOffset.TryParseExact(m.Groups["at"].Value, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var at))
        {
            return null;
        }

        return (m.Groups["device"].Value, at);
    }

    /// <summary>The tables the sync handles: every synced entity table (Id, UpdatedAt, IsDeleted).</summary>
    internal static IReadOnlyList<DataExchangeService.TableInfo> Tables { get; } =
    [
        .. DataExchangeService.Tables.Value.Where(t =>
            t.Columns.Any(c => c.Name == "Id") && t.Columns.Any(c => c.Name == "UpdatedAt") && t.Columns.Any(c => c.Name == "IsDeleted")),
    ];

    internal static byte[] Gzip(byte[] data)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            gzip.Write(data);
        }

        return output.ToArray();
    }

    internal static byte[] Gunzip(byte[] data)
    {
        using var input = new MemoryStream(data);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }

    /// <summary>Writes the document header (without the closing brace) and the opening of the <c>rows</c> object.</summary>
    internal static Utf8JsonWriter NewWriter(Stream stream) =>
        new(stream, new JsonWriterOptions { Indented = false, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

    internal static void WriteHeader(Utf8JsonWriter writer, string appVersion, string device, DateTimeOffset createdAt)
    {
        writer.WriteNumber("formatVersion", Version);
        writer.WriteString("appVersion", appVersion);
        writer.WriteString("lastMigration", DataExchangeService.LatestMigration.Value);
        writer.WriteString("deviceId", device);
        writer.WriteString("createdAt", createdAt.UtcDateTime.ToString(DataExchangeService.TimestampFormat, CultureInfo.InvariantCulture));
    }

    /// <summary>Writes the rows of a table (all rows, or only the given ids) in the row shape of the data export.</summary>
    internal static int WriteRows(SqliteConnection connection, SqliteTransaction? transaction, DataExchangeService.TableInfo table, Utf8JsonWriter writer, IReadOnlyList<string>? ids)
    {
        var count = 0;
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        var columns = string.Join(", ", table.Columns.Select(c => DataExchangeService.Quote(c.Name)));
        if (ids is null)
        {
            command.CommandText = $"SELECT {columns} FROM {DataExchangeService.Quote(table.Name)} ORDER BY \"Id\"";
        }
        else
        {
            command.CommandText = $"SELECT {columns} FROM {DataExchangeService.Quote(table.Name)} WHERE \"Id\" IN ({string.Join(", ", ids.Select((_, i) => "$i" + i))}) ORDER BY \"Id\"";
            for (var i = 0; i < ids.Count; i++)
            {
                command.Parameters.AddWithValue("$i" + i, ids[i]);
            }
        }

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            writer.WriteStartObject();
            for (var i = 0; i < table.Columns.Count; i++)
            {
                writer.WritePropertyName(table.Columns[i].Name);
                if (reader.IsDBNull(i))
                {
                    writer.WriteNullValue();
                }
                else
                {
                    DataExchangeService.WriteValue(writer, table.Columns[i], reader.GetValue(i));
                }
            }

            writer.WriteEndObject();
            count++;
        }

        return count;
    }
}
