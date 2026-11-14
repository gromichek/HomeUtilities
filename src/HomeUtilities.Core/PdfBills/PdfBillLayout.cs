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

    /// <summary>The name of the contract's tariff line (<see cref="PdfBillField.Quantity"/>, <see cref="PdfBillField.ComponentAmount"/>).</summary>
    public string? Component { get; set; }

    /// <summary>The name of the bill's custom field (<see cref="PdfBillField.CustomField"/>).</summary>
    public string? Name { get; set; }

    /// <summary>What to do with several matches; default: the first one.</summary>
    public PdfCombine Combine { get; set; } = PdfCombine.First;

    /// <summary>A .NET substitution string applied to each match (<see cref="PdfBillField.Notes"/> only).</summary>
    public string? Format { get; set; }
}

/// <summary>How several matches of one pattern make one value.</summary>
public enum PdfCombine
{
    /// <summary>The first match.</summary>
    First = 0,

    /// <summary>Every match parsed as a decimal and added.</summary>
    Sum = 1,

    /// <summary>Every match (Notes: joined with "; "; Lines: one line per match).</summary>
    All = 2,
}
