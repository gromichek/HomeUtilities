using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.PdfBills;

/// <summary>
/// Says how to read the header of one supplier's PDF bill: which texts identify the supplier and, per field, a regular
/// expression that finds the value. Layouts are user data (stored in the database, synced, backed up); they are
/// never part of the repository because supplier names would tell where the owner lives.
/// </summary>
public class PdfBillLayout : SyncEntity
{
    /// <summary>Display name, e.g. "Example Water".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Texts that must <b>all</b> occur in the PDF text (case-insensitive) for the layout to fit.</summary>
    public List<string> Markers { get; set; } = [];

    /// <summary>The field patterns (stored as a JSON column).</summary>
    public List<PdfBillFieldPattern> Fields { get; set; } = [];

    /// <summary>Free-text notes.</summary>
    public string? Notes { get; set; }
}

/// <summary>A field of the bill and the regular expression that finds its text in the PDF text.</summary>
public sealed class PdfBillFieldPattern
{
    /// <summary>The field.</summary>
    public PdfBillField Field { get; set; }

    /// <summary>
    /// A .NET regular expression run on the whole text (lines joined by a line feed; IgnoreCase, CultureInvariant,
    /// Multiline). The value is the named group <c>v</c>, else group 1, else the whole match; the first match wins.
    /// </summary>
    public string Pattern { get; set; } = string.Empty;
}
