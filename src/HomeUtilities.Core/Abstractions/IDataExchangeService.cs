namespace HomeUtilities.Core.Abstractions;

/// <summary>What is wrong with a data file that cannot be imported.</summary>
public enum DataImportIssueCode
{
    /// <summary>Not a JSON data export of this app (not JSON, not a zip with <c>data.json</c>, no format version).</summary>
    NotADataFile,

    /// <summary>The file was exported by a newer version of the app (format version or database version).</summary>
    NewerFormat,

    /// <summary>A table the app does not know.</summary>
    UnknownTable,

    /// <summary>A row is not a JSON object.</summary>
    BadRow,

    /// <summary>A column the app does not know.</summary>
    UnknownColumn,

    /// <summary>A required column is missing.</summary>
    MissingColumn,

    /// <summary>A value has the wrong type or format.</summary>
    BadValue,

    /// <summary>The rows break a database rule (duplicate key, missing referenced row).</summary>
    ConstraintViolation,
}

/// <summary>One problem found in a data file.</summary>
/// <param name="Code">The kind of problem.</param>
/// <param name="Table">Table (entity type) name, if the problem belongs to one.</param>
/// <param name="Row">Zero-based row index in the table's array.</param>
/// <param name="Column">Column name, if the problem belongs to one.</param>
public sealed record DataImportIssue(DataImportIssueCode Code, string? Table = null, int? Row = null, string? Column = null);

/// <summary>Result of checking (or staging) a JSON data file.</summary>
/// <param name="Issues">The problems found; empty if the file is usable. At most <see cref="Max"/> are collected.</param>
/// <param name="ExportedAt">When the file was exported (UTC), if known.</param>
/// <param name="RowCount">Number of rows in the file, all tables together.</param>
public sealed record DataImportResult(IReadOnlyList<DataImportIssue> Issues, DateTimeOffset? ExportedAt, int RowCount)
{
    /// <summary>The most issues that are collected before the check stops.</summary>
    public const int Max = 20;

    /// <summary>True if the file can be imported.</summary>
    public bool IsValid => Issues.Count == 0;
}

/// <summary>
/// Full export of all data (including soft-deleted rows) as JSON, and import of such a file. An import replaces all data
/// through the staged restore (applied at the next start); there is no merge.
/// </summary>
public interface IDataExchangeService
{
    /// <summary>Current version of the file layout; a file with a higher <c>formatVersion</c> is refused.</summary>
    public const int CurrentFormatVersion = 1;

    /// <summary>File name for an export made now: <c>HomeUtilities-data-YYYY-MM-DD-HHmm.json</c> (or <c>.zip</c> with attachments).</summary>
    string SuggestFileName(bool withAttachments);

    /// <summary>
    /// Writes every table as an array of full rows. Without attachments the output is the JSON document itself; with them it is
    /// a zip holding <c>data.json</c> and <c>attachments/…</c>.
    /// </summary>
    Task ExportAsync(Stream output, bool withAttachments, CancellationToken ct = default);

    /// <summary>Checks a (seekable) <c>.json</c> or <c>.zip</c> file without writing anything.</summary>
    Task<DataImportResult> ValidateAsync(Stream input, CancellationToken ct = default);

    /// <summary>Checks the file and, if it is usable, stages it to replace all data at the next start. Nothing is staged otherwise.</summary>
    Task<DataImportResult> StageImportAsync(Stream input, CancellationToken ct = default);
}
