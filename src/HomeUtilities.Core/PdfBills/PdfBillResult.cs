using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.PdfBills;

/// <summary>The values a layout found in a PDF; a field that was not found (or did not parse) is null.</summary>
public sealed class PdfBillValues
{
    /// <summary>The bill number.</summary>
    public string? Number { get; init; }

    /// <summary>The issue date.</summary>
    public DateOnly? IssuedOn { get; init; }

    /// <summary>The service period.</summary>
    public YearMonth? Period { get; init; }

    /// <summary>The due date.</summary>
    public DateOnly? DueOn { get; init; }

    /// <summary>The total to pay.</summary>
    public decimal? Gross { get; init; }

    /// <summary>The VAT rate in percent.</summary>
    public decimal? VatPercent { get; init; }

    /// <summary>The VAT amount.</summary>
    public decimal? Vat { get; init; }

    /// <summary>The amount without VAT.</summary>
    public decimal? Net { get; init; }

    /// <summary>The start of the service period.</summary>
    public DateOnly? ServiceFrom { get; init; }

    /// <summary>The end of the service period.</summary>
    public DateOnly? ServiceTo { get; init; }

    /// <summary>The title.</summary>
    public string? Title { get; init; }

    /// <summary>The quantity on the bill.</summary>
    public decimal? Consumption { get; init; }

    /// <summary>The comment (several matches joined with "; ").</summary>
    public string? Notes { get; init; }

    /// <summary>Quantities of tariff lines, by the line's name.</summary>
    public IReadOnlyList<PdfBillNamedNumber> Quantities { get; init; } = [];

    /// <summary>Amounts of tariff lines, by the line's name.</summary>
    public IReadOnlyList<PdfBillNamedNumber> ComponentAmounts { get; init; } = [];

    /// <summary>Values of custom fields, by the field's name.</summary>
    public IReadOnlyList<PdfBillCustomValue> CustomFields { get; init; } = [];

    /// <summary>The bill lines found.</summary>
    public IReadOnlyList<PdfBillLineValue> Lines { get; init; } = [];
}

/// <summary>A number found for a named tariff line.</summary>
/// <param name="Name">The name of the tariff line as written in the layout.</param>
/// <param name="Value">The number.</param>
public sealed record PdfBillNamedNumber(string Name, decimal Value);

/// <summary>The raw value found for a named custom field; its kind is only known in the bill editor.</summary>
/// <param name="Name">The name of the custom field as written in the layout.</param>
/// <param name="Raw">The text found (the first match; with <c>sum</c> the matches added, formatted invariantly).</param>
/// <param name="Sum">The sum when the pattern combines with <c>sum</c> (every match parsed), else null.</param>
public sealed record PdfBillCustomValue(string Name, string Raw, decimal? Sum = null);

/// <summary>One bill line read from a table row; missing columns are null.</summary>
public sealed record PdfBillLineValue(string Description, decimal? Quantity, string? Unit, decimal? UnitPrice, decimal? Net, decimal? Vat);

/// <summary>Why a field was found but could not be used.</summary>
public enum PdfBillWarningKind
{
    /// <summary>The text found does not parse as the field's type; Raw holds the text.</summary>
    Unparsable = 0,

    /// <summary>The pattern took too long (regular expression time-out).</summary>
    Timeout = 1,

    /// <summary>Total, net amount and VAT amount do not add up (by more than 0.01); the total wins.</summary>
    AmountsMismatch = 2,
}

/// <summary>A problem with one field of a layout.</summary>
/// <param name="Field">The field.</param>
/// <param name="Kind">What went wrong.</param>
/// <param name="Raw">The text that did not parse, if any.</param>
/// <param name="Subject">The component or custom field name of a Quantity, ComponentAmount or CustomField entry.</param>
public sealed record PdfBillWarning(PdfBillField Field, PdfBillWarningKind Kind, string? Raw, string? Subject = null);

/// <summary>An entry of a layout that is named by a tariff line or a custom field (not by the field kind alone).</summary>
/// <param name="Field">Quantity, ComponentAmount or CustomField.</param>
/// <param name="Subject">The component or custom field name.</param>
public sealed record PdfBillNamedEntry(PdfBillField Field, string Subject);

/// <summary>The outcome of reading a PDF with the layouts.</summary>
public sealed class PdfBillResult
{
    /// <summary>True when a layout fits the text.</summary>
    public bool LayoutFound => Layout is not null;

    /// <summary>The layout that was used; null when none fits.</summary>
    public PdfBillLayout? Layout { get; init; }

    /// <summary>The name of the layout used.</summary>
    public string? LayoutName => Layout?.Name;

    /// <summary>What was found.</summary>
    public PdfBillValues Values { get; init; } = new();

    /// <summary>
    /// The header fields (also those the layout does not define) and the other fields the layout defines that gave no
    /// value; named entries (Quantity, ComponentAmount, CustomField) are in <see cref="NotFoundNamed"/>.
    /// </summary>
    public IReadOnlyList<PdfBillField> NotFound { get; init; } = [];

    /// <summary>Named entries (tariff line, custom field) that gave no value.</summary>
    public IReadOnlyList<PdfBillNamedEntry> NotFoundNamed { get; init; } = [];

    /// <summary>Named entries that gave a value.</summary>
    public IReadOnlyList<PdfBillNamedEntry> FoundNamed { get; init; } = [];

    /// <summary>Problems with fields that were found but unusable.</summary>
    public IReadOnlyList<PdfBillWarning> Warnings { get; init; } = [];

    /// <summary>The fields (not the named entries) that were found, in field order.</summary>
    public IReadOnlyList<PdfBillField> Found { get; init; } = [];
}
