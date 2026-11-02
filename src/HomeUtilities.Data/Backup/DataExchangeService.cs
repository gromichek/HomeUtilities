using System.Globalization;
using System.IO.Compression;
using System.Text.Encodings.Web;
using System.Text.Json;
using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace HomeUtilities.Data.Backup;

/// <summary>
/// Full JSON export of the database and import of such a file (layout in <c>docs/user-guide</c>, "Export all data").
/// <para>
/// Document: <c>{ formatVersion, appVersion, exportedAt, lastMigration, tables: { "Bills": [ {row}, … ], … } }</c>. A row has one
/// key per database column (the column name, e.g. <c>Pay_Iban</c>); soft-deleted rows are included. Values: decimals are JSON
/// <b>numbers</b> (exact, no rounding), dates <c>yyyy-MM-dd</c>, timestamps ISO-8601 UTC (<c>2026-10-24T09:30:00.0000000Z</c>),
/// year-months <c>yyyy-MM</c>, enums their names, booleans <c>true/false</c>, ids upper-case GUID text and JSON columns
/// (links, custom fields, snapshots) as nested JSON. Rows are ordered by <c>Id</c> and tables by name, so two exports of the
/// same data are identical except <c>exportedAt</c>.
/// </para>
/// The serializer only needs the EF model and plain SQLite access, so the sync change logs (T-031) can reuse the row shape.
/// </summary>
public sealed class DataExchangeService(BackupOptions options, IClock clock, BackupService backup) : IDataExchangeService
{
    /// <summary>Entry name of the JSON document inside an export zip.</summary>
    public const string DataEntry = "data.json";

    private const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'";
    private static readonly Lazy<IReadOnlyList<TableInfo>> Tables = new(BuildTables);
    private static readonly Lazy<string> LatestMigration = new(() =>
    {
        using var db = NewModelContext();
        return db.Database.GetMigrations().Max() ?? string.Empty;
    });

    /// <inheritdoc />
    public string SuggestFileName(bool withAttachments) =>
        $"HomeUtilities-data-{clock.UtcNow.ToLocalTime():yyyy-MM-dd-HHmm}{(withAttachments ? ".zip" : ".json")}";

    /// <inheritdoc />
    public async Task ExportAsync(Stream output, bool withAttachments, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(output);

        var work = backup.NewWorkFolder();
        try
        {
            var dbCopy = Path.Combine(work, BackupService.DatabaseEntry);
            var json = Path.Combine(work, DataEntry);
            await Task.Run(() =>
            {
                BackupService.CopyDatabase(options.DatabasePath, dbCopy);
                WriteDocument(dbCopy, json, clock.UtcNow);
            }, ct);

            if (!withAttachments)
            {
                await using var source = new FileStream(json, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
                await source.CopyToAsync(output, ct);
                return;
            }

            using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
            await BackupService.AddFileAsync(zip, DataEntry, json, ct);
            foreach (var (relative, full) in backup.ListAttachments())
            {
                await BackupService.AddFileAsync(zip, BackupService.AttachmentsPrefix + relative, full, ct);
            }
        }
        finally
        {
            BackupService.TryDeleteDirectory(work);
        }
    }

    /// <inheritdoc />
    public Task<DataImportResult> ValidateAsync(Stream input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        return Task.Run(() =>
        {
            using var file = Open(input, out _);
            return file is null ? NotADataFile() : Check(file.RootElement, out _);
        }, ct);
    }

    /// <inheritdoc />
    public async Task<DataImportResult> StageImportAsync(Stream input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var work = backup.NewWorkFolder();
        ZipArchive? zip = null;
        try
        {
            DataImportResult result;
            var databaseFile = Path.Combine(work, BackupService.DatabaseEntry);
            using (var file = Open(input, out zip))
            {
                if (file is null)
                {
                    return NotADataFile();
                }

                result = Check(file.RootElement, out var rows);
                if (!result.IsValid)
                {
                    return result;
                }

                var failure = await Task.Run(() => BuildDatabase(databaseFile, rows!), ct);
                if (failure is not null)
                {
                    return result with { Issues = [failure] };
                }
            }

            await backup.StageDatabaseAsync(databaseFile, zip, ct);
            return result;
        }
        finally
        {
            zip?.Dispose();
            SqliteConnection.ClearAllPools();
            BackupService.TryDeleteDirectory(work);
        }
    }

    // ---- Export ---------------------------------------------------------------------------------------------------

    private void WriteDocument(string databaseCopy, string jsonPath, DateTimeOffset exportedAt)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = databaseCopy, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        connection.Open();
        var lastMigration = ScalarText(connection, "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC LIMIT 1") ?? string.Empty;

        using var stream = new FileStream(jsonPath, FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        writer.WriteStartObject();
        writer.WriteNumber("formatVersion", IDataExchangeService.CurrentFormatVersion);
        writer.WriteString("appVersion", options.AppVersion);
        writer.WriteString("exportedAt", exportedAt.UtcDateTime.ToString(TimestampFormat, CultureInfo.InvariantCulture));
        writer.WriteString("lastMigration", lastMigration);
        writer.WriteStartObject("tables");
        foreach (var table in Tables.Value)
        {
            writer.WriteStartArray(table.Name);
            WriteRows(connection, table, writer);
            writer.WriteEndArray();
        }

        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static void WriteRows(SqliteConnection connection, TableInfo table, Utf8JsonWriter writer)
    {
        using var command = connection.CreateCommand();
        var order = table.Columns.Any(c => c.Name == "Id") ? " ORDER BY \"Id\"" : string.Empty;
        command.CommandText = $"SELECT {string.Join(", ", table.Columns.Select(c => Quote(c.Name)))} FROM {Quote(table.Name)}{order}";
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
                    WriteValue(writer, table.Columns[i], reader.GetValue(i));
                }
            }

            writer.WriteEndObject();
        }
    }

    private static void WriteValue(Utf8JsonWriter writer, ColumnInfo column, object value)
    {
        switch (column.Kind)
        {
            case ColumnKind.Integer:
                writer.WriteNumberValue(Convert.ToInt64(value, CultureInfo.InvariantCulture));
                break;
            case ColumnKind.Bool:
                writer.WriteBooleanValue(Convert.ToInt64(value, CultureInfo.InvariantCulture) != 0);
                break;
            case ColumnKind.Decimal:
                writer.WriteNumberValue(value is string text
                    ? decimal.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture)
                    : Convert.ToDecimal(value, CultureInfo.InvariantCulture));
                break;
            case ColumnKind.Real:
                writer.WriteNumberValue(Convert.ToDouble(value, CultureInfo.InvariantCulture));
                break;
            case ColumnKind.Timestamp:
                writer.WriteStringValue(new DateTimeOffset(Convert.ToInt64(value, CultureInfo.InvariantCulture), TimeSpan.Zero)
                    .ToString(TimestampFormat, CultureInfo.InvariantCulture));
                break;
            case ColumnKind.YearMonth:
                var yearMonth = Convert.ToInt32(value, CultureInfo.InvariantCulture);
                writer.WriteStringValue($"{yearMonth / 100:D4}-{yearMonth % 100:D2}");
                break;
            case ColumnKind.Enum:
                var number = Convert.ToInt64(value, CultureInfo.InvariantCulture);
                var enumName = Enum.IsDefined(column.EnumType!, Enum.ToObject(column.EnumType!, number))
                    ? Enum.GetName(column.EnumType!, Enum.ToObject(column.EnumType!, number))
                    : null;
                if (enumName is null)
                {
                    writer.WriteNumberValue(number);
                }
                else
                {
                    writer.WriteStringValue(enumName);
                }

                break;
            case ColumnKind.Json:
                using (var document = JsonDocument.Parse(Convert.ToString(value, CultureInfo.InvariantCulture)!))
                {
                    document.RootElement.WriteTo(writer);
                }

                break;
            default:
                writer.WriteStringValue(Convert.ToString(value, CultureInfo.InvariantCulture));
                break;
        }
    }

    // ---- Import ---------------------------------------------------------------------------------------------------

    // Opens the JSON document of a .json file or of the data.json entry of a zip; null if the file is neither.
    private static JsonDocument? Open(Stream input, out ZipArchive? zip)
    {
        zip = null;
        try
        {
            input.Position = 0;
            var head = new byte[2];
            var isZip = input.ReadAtLeast(head, 2, throwOnEndOfStream: false) == 2 && head[0] == (byte)'P' && head[1] == (byte)'K';
            input.Position = 0;
            if (!isZip)
            {
                return JsonDocument.Parse(input);
            }

            zip = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
            var entry = zip.GetEntry(DataEntry);
            if (entry is null)
            {
                zip.Dispose();
                zip = null;
                return null;
            }

            using var stream = entry.Open();
            return JsonDocument.Parse(stream);
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or IOException or NotSupportedException)
        {
            zip?.Dispose();
            zip = null;
            return null;
        }
    }

    private static DataImportResult NotADataFile() =>
        new([new DataImportIssue(DataImportIssueCode.NotADataFile)], null, 0);

    // Checks the document and, if it is usable, returns the rows already converted to database values.
    private static DataImportResult Check(JsonElement root, out Dictionary<TableInfo, List<object?[]>>? rows)
    {
        rows = null;
        var issues = new List<DataImportIssue>();
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("formatVersion", out var version)
            || version.ValueKind != JsonValueKind.Number
            || !version.TryGetInt32(out var formatVersion)
            || formatVersion < 1
            || !root.TryGetProperty("tables", out var tables)
            || tables.ValueKind != JsonValueKind.Object)
        {
            return NotADataFile();
        }

        DateTimeOffset? exportedAt = null;
        if (root.TryGetProperty("exportedAt", out var exported) && exported.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(exported.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsedAt))
        {
            exportedAt = parsedAt;
        }

        var newerSchema = root.TryGetProperty("lastMigration", out var migration) && migration.ValueKind == JsonValueKind.String
            && string.CompareOrdinal(migration.GetString(), LatestMigration.Value) > 0;
        if (formatVersion > IDataExchangeService.CurrentFormatVersion || newerSchema)
        {
            return new DataImportResult([new DataImportIssue(DataImportIssueCode.NewerFormat)], exportedAt, 0);
        }

        var converted = new Dictionary<TableInfo, List<object?[]>>();
        var count = 0;
        foreach (var tableElement in tables.EnumerateObject())
        {
            var table = Tables.Value.FirstOrDefault(t => t.Name == tableElement.Name);
            if (table is null)
            {
                Add(issues, new DataImportIssue(DataImportIssueCode.UnknownTable, tableElement.Name));
                continue;
            }

            if (tableElement.Value.ValueKind != JsonValueKind.Array)
            {
                Add(issues, new DataImportIssue(DataImportIssueCode.BadRow, table.Name));
                continue;
            }

            var list = new List<object?[]>();
            var index = 0;
            foreach (var row in tableElement.Value.EnumerateArray())
            {
                if (issues.Count >= DataImportResult.Max)
                {
                    break;
                }

                list.Add(ConvertRow(table, row, index++, issues));
                count++;
            }

            converted[table] = list;
        }

        if (issues.Count > 0)
        {
            return new DataImportResult(issues, exportedAt, count);
        }

        rows = converted;
        return new DataImportResult([], exportedAt, count);
    }

    private static object?[] ConvertRow(TableInfo table, JsonElement row, int index, List<DataImportIssue> issues)
    {
        var values = new object?[table.Columns.Count];
        if (row.ValueKind != JsonValueKind.Object)
        {
            Add(issues, new DataImportIssue(DataImportIssueCode.BadRow, table.Name, index));
            return values;
        }

        foreach (var property in row.EnumerateObject())
        {
            if (table.Columns.All(c => c.Name != property.Name))
            {
                Add(issues, new DataImportIssue(DataImportIssueCode.UnknownColumn, table.Name, index, property.Name));
            }
        }

        for (var i = 0; i < table.Columns.Count; i++)
        {
            var column = table.Columns[i];
            if (!row.TryGetProperty(column.Name, out var element))
            {
                if (!column.Nullable)
                {
                    Add(issues, new DataImportIssue(DataImportIssueCode.MissingColumn, table.Name, index, column.Name));
                }

                continue;
            }

            if (element.ValueKind == JsonValueKind.Null)
            {
                if (!column.Nullable)
                {
                    Add(issues, new DataImportIssue(DataImportIssueCode.BadValue, table.Name, index, column.Name));
                }

                continue;
            }

            if (TryConvert(column, element, out var value))
            {
                values[i] = value;
            }
            else
            {
                Add(issues, new DataImportIssue(DataImportIssueCode.BadValue, table.Name, index, column.Name));
            }
        }

        return values;
    }

    private static void Add(List<DataImportIssue> issues, DataImportIssue issue)
    {
        if (issues.Count < DataImportResult.Max)
        {
            issues.Add(issue);
        }
    }

    private static bool TryConvert(ColumnInfo column, JsonElement element, out object? value)
    {
        value = null;
        switch (column.Kind)
        {
            case ColumnKind.Integer:
                if (element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out var integer))
                {
                    value = integer;
                    return true;
                }

                return false;
            case ColumnKind.Bool:
                if (element.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    value = element.ValueKind == JsonValueKind.True ? 1L : 0L;
                    return true;
                }

                return false;
            case ColumnKind.Decimal:
                if (element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out var number))
                {
                    value = number.ToString(CultureInfo.InvariantCulture);
                    return true;
                }

                return false;
            case ColumnKind.Real:
                if (element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out var real))
                {
                    value = real;
                    return true;
                }

                return false;
            case ColumnKind.Timestamp:
                if (element.ValueKind == JsonValueKind.String
                    && DateTimeOffset.TryParse(element.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var moment))
                {
                    value = moment.UtcTicks;
                    return true;
                }

                return false;
            case ColumnKind.YearMonth:
                if (element.ValueKind == JsonValueKind.String
                    && DateTime.TryParseExact(element.GetString(), "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var month))
                {
                    value = (long)(month.Year * 100 + month.Month);
                    return true;
                }

                return false;
            case ColumnKind.Enum:
                if (element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out var ordinal))
                {
                    value = ordinal;
                    return true;
                }

                if (element.ValueKind == JsonValueKind.String
                    && Enum.TryParse(column.EnumType!, element.GetString(), ignoreCase: false, out var named)
                    && Enum.IsDefined(column.EnumType!, named!))
                {
                    value = Convert.ToInt64(named, CultureInfo.InvariantCulture);
                    return true;
                }

                return false;
            case ColumnKind.Json:
                value = JsonSerializer.Serialize(element);
                return true;
            case ColumnKind.Guid:
                if (element.ValueKind == JsonValueKind.String && Guid.TryParse(element.GetString(), out var guid))
                {
                    value = guid.ToString().ToUpperInvariant();
                    return true;
                }

                return false;
            case ColumnKind.DateOnly:
                if (element.ValueKind == JsonValueKind.String && DateOnly.TryParse(element.GetString(), CultureInfo.InvariantCulture, out _))
                {
                    value = element.GetString();
                    return true;
                }

                return false;
            default:
                if (element.ValueKind == JsonValueKind.String)
                {
                    value = element.GetString();
                    return true;
                }

                return false;
        }
    }

    // Creates a migrated database at the path and inserts the rows; null if it worked, else the first problem found.
    private static DataImportIssue? BuildDatabase(string path, Dictionary<TableInfo, List<object?[]>> rows)
    {
        var connectionString = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString();
        using (var db = new HomeUtilitiesDbContext(new DbContextOptionsBuilder<HomeUtilitiesDbContext>().UseSqlite(connectionString).Options))
        {
            DatabaseMigrator.MigrateAsync(db, CancellationToken.None).GetAwaiter().GetResult();
        }

        SqliteConnection.ClearAllPools();
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        Execute(connection, "PRAGMA foreign_keys=ON;");
        string? currentTable = null;
        var currentRow = 0;
        try
        {
            using var transaction = connection.BeginTransaction();
            Execute(connection, "PRAGMA defer_foreign_keys=ON;");
            foreach (var (table, tableRows) in rows)
            {
                currentTable = table.Name;
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = $"INSERT INTO {Quote(table.Name)} ({string.Join(", ", table.Columns.Select(c => Quote(c.Name)))}) "
                    + $"VALUES ({string.Join(", ", table.Columns.Select((_, i) => "$p" + i))})";
                var parameters = table.Columns.Select((_, i) =>
                {
                    var parameter = command.CreateParameter();
                    parameter.ParameterName = "$p" + i;
                    command.Parameters.Add(parameter);
                    return parameter;
                }).ToList();
                for (currentRow = 0; currentRow < tableRows.Count; currentRow++)
                {
                    for (var i = 0; i < parameters.Count; i++)
                    {
                        parameters[i].Value = tableRows[currentRow][i] ?? DBNull.Value;
                    }

                    command.ExecuteNonQuery();
                }
            }

            currentTable = null;
            transaction.Commit();
        }
        catch (SqliteException)
        {
            return new DataImportIssue(DataImportIssueCode.ConstraintViolation, currentTable, currentTable is null ? null : currentRow);
        }

        Execute(connection, "PRAGMA journal_mode=DELETE;");
        return null;
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static string? ScalarText(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar() is { } value and not DBNull ? Convert.ToString(value, CultureInfo.InvariantCulture) : null;
    }

    private static string Quote(string name) => "\"" + name.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    // ---- Model ----------------------------------------------------------------------------------------------------

    private static HomeUtilitiesDbContext NewModelContext() =>
        new(new DbContextOptionsBuilder<HomeUtilitiesDbContext>().UseSqlite("Data Source=:memory:").Options);

    private static List<TableInfo> BuildTables()
    {
        using var db = NewModelContext();
        var model = db.GetService<IDesignTimeModel>().Model.GetRelationalModel();
        return [.. model.Tables
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .Select(t => new TableInfo(t.Name, [.. t.Columns.Select(c => ToColumn(c))]))];
    }

    private static ColumnInfo ToColumn(IColumn column)
    {
        var property = column.PropertyMappings.First().Property;
        var clr = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
        var converter = property.GetValueConverter();
        var kind = clr switch
        {
            _ when clr == typeof(DateTimeOffset) => ColumnKind.Timestamp,
            _ when clr == typeof(YearMonth) => ColumnKind.YearMonth,
            _ when converter is not null && converter.ProviderClrType == typeof(string) && clr != typeof(string) => ColumnKind.Json,
            _ when clr.IsEnum => ColumnKind.Enum,
            _ when clr == typeof(decimal) => ColumnKind.Decimal,
            _ when clr == typeof(bool) => ColumnKind.Bool,
            _ when clr == typeof(int) || clr == typeof(long) || clr == typeof(short) || clr == typeof(byte) => ColumnKind.Integer,
            _ when clr == typeof(double) || clr == typeof(float) => ColumnKind.Real,
            _ when clr == typeof(Guid) => ColumnKind.Guid,
            _ when clr == typeof(DateOnly) => ColumnKind.DateOnly,
            _ when clr == typeof(string) || clr == typeof(TimeOnly) => ColumnKind.Text,
            _ => throw new NotSupportedException($"Column {column.Table.Name}.{column.Name} has the unsupported type {clr}."),
        };
        return new ColumnInfo(column.Name, kind, clr.IsEnum ? clr : null, column.IsNullable);
    }

    private enum ColumnKind
    {
        Text,
        Guid,
        DateOnly,
        Integer,
        Bool,
        Decimal,
        Real,
        Timestamp,
        YearMonth,
        Enum,
        Json,
    }

    private sealed record ColumnInfo(string Name, ColumnKind Kind, Type? EnumType, bool Nullable);

    private sealed record TableInfo(string Name, IReadOnlyList<ColumnInfo> Columns);
}
