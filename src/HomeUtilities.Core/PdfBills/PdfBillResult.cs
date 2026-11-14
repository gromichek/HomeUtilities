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
}

/// <summary>Why a field was found but could not be used.</summary>
public enum PdfBillWarningKind
{
    /// <summary>The text found does not parse as the field's type; Raw holds the text.</summary>
    Unparsable = 0,

    /// <summary>The pattern took too long (regular expression time-out).</summary>
    Timeout = 1,
}

/// <summary>A problem with one field of a layout.</summary>
/// <param name="Field">The field.</param>
/// <param name="Kind">What went wrong.</param>
/// <param name="Raw">The text that did not parse, if any.</param>
public sealed record PdfBillWarning(PdfBillField Field, PdfBillWarningKind Kind, string? Raw);

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

    /// <summary>The fields of the layout (and the known ones it does not define) that gave no value.</summary>
    public IReadOnlyList<PdfBillField> NotFound { get; init; } = [];

    /// <summary>Problems with fields that were found but unusable.</summary>
    public IReadOnlyList<PdfBillWarning> Warnings { get; init; } = [];

    /// <summary>The fields that were found, in field order.</summary>
    public IReadOnlyList<PdfBillField> Found => [.. Enum.GetValues<PdfBillField>().Where(f => !NotFound.Contains(f))];
}
