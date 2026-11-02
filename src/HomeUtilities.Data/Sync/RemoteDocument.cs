using System.Globalization;
using System.Text.Json;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Data.Backup;

namespace HomeUtilities.Data.Sync;

/// <summary>A downloaded change file or snapshot, parsed and converted to database values.</summary>
internal sealed class RemoteDocument
{
    internal required string Path { get; init; }

    /// <summary>The device that wrote the file.</summary>
    internal required string Device { get; init; }

    /// <summary>Sequence of a change file; for a snapshot the sequence of its own device.</summary>
    internal required long Sequence { get; init; }

    internal required bool IsSnapshot { get; init; }

    internal required DateTimeOffset CreatedAt { get; init; }

    internal required bool NeedsNewerApp { get; init; }

    /// <summary>True when a row could not be read; the file is not applied.</summary>
    internal bool IsBroken { get; init; }

    /// <summary>Snapshot only: the last applied sequence of every device at the time of the snapshot.</summary>
    internal IReadOnlyDictionary<string, long> Sequences { get; init; } = new Dictionary<string, long>();

    internal IReadOnlyList<(DataExchangeService.TableInfo Table, List<object?[]> Rows)> Tables { get; init; } = [];

    internal int RowCount => Tables.Sum(t => t.Rows.Count);

    /// <summary>Parses the bytes (already gunzipped for snapshots). Never throws for bad content: <see cref="IsBroken"/> is set.</summary>
    internal static RemoteDocument Parse(string path, byte[] json, string device, long sequence, bool isSnapshot, DateTimeOffset fallbackAt)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("formatVersion", out var version) || !version.TryGetInt32(out var formatVersion))
            {
                return Broken(path, device, sequence, isSnapshot, fallbackAt);
            }

            var newerMigration = root.TryGetProperty("lastMigration", out var migration) && migration.ValueKind == JsonValueKind.String
                && string.CompareOrdinal(migration.GetString(), DataExchangeService.LatestMigration.Value) > 0;
            var needsNewer = formatVersion > SyncFormat.Version || newerMigration;
            var createdAt = fallbackAt;
            if (root.TryGetProperty("createdAt", out var created) && created.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(created.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsedAt))
            {
                createdAt = parsedAt;
            }

            if (needsNewer)
            {
                return new RemoteDocument { Path = path, Device = device, Sequence = sequence, IsSnapshot = isSnapshot, CreatedAt = createdAt, NeedsNewerApp = true };
            }

            var sequences = new Dictionary<string, long>(StringComparer.Ordinal);
            if (root.TryGetProperty("sequences", out var seqs) && seqs.ValueKind == JsonValueKind.Object)
            {
                foreach (var item in seqs.EnumerateObject())
                {
                    if (item.Value.TryGetInt64(out var value))
                    {
                        sequences[item.Name] = value;
                    }
                }
            }

            var tables = new List<(DataExchangeService.TableInfo, List<object?[]>)>();
            if (root.TryGetProperty("rows", out var rows) && rows.ValueKind == JsonValueKind.Object)
            {
                foreach (var tableElement in rows.EnumerateObject())
                {
                    // A table this app does not know cannot exist with the same migration: treat the file as unreadable.
                    var table = SyncFormat.Tables.FirstOrDefault(t => t.Name == tableElement.Name);
                    if (table is null || tableElement.Value.ValueKind != JsonValueKind.Array)
                    {
                        return Broken(path, device, sequence, isSnapshot, fallbackAt);
                    }

                    var issues = new List<DataImportIssue>();
                    var list = new List<object?[]>();
                    var index = 0;
                    foreach (var row in tableElement.Value.EnumerateArray())
                    {
                        list.Add(DataExchangeService.ConvertRow(table, row, index++, issues));
                        if (issues.Count > 0)
                        {
                            return Broken(path, device, sequence, isSnapshot, fallbackAt);
                        }
                    }

                    tables.Add((table, list));
                }
            }

            return new RemoteDocument
            {
                Path = path,
                Device = device,
                Sequence = sequence,
                IsSnapshot = isSnapshot,
                CreatedAt = createdAt,
                NeedsNewerApp = false,
                Sequences = sequences,
                Tables = tables,
            };
        }
        catch (JsonException)
        {
            return Broken(path, device, sequence, isSnapshot, fallbackAt);
        }
    }

    private static RemoteDocument Broken(string path, string device, long sequence, bool isSnapshot, DateTimeOffset at) =>
        new() { Path = path, Device = device, Sequence = sequence, IsSnapshot = isSnapshot, CreatedAt = at, NeedsNewerApp = false, IsBroken = true };
}
