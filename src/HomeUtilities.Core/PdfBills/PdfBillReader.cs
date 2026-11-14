using System.Text.RegularExpressions;
using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.PdfBills;

/// <summary>Chooses the layout that fits the text of a PDF and reads the header fields with it.</summary>
public static class PdfBillReader
{
    /// <summary>Time a single pattern may take.</summary>
    public static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);

    /// <summary>The regular expression options every pattern runs with.</summary>
    public const RegexOptions PatternOptions = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline;

    /// <summary>Reads the text with the layout whose markers all occur (the one with most markers wins).</summary>
    public static PdfBillResult Read(string text, IEnumerable<PdfBillLayout> layouts)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(layouts);

        var layout = Choose(text, layouts);
        return layout is null ? new PdfBillResult() : Read(text, layout);
    }

    /// <summary>The fitting layout with most markers, or null.</summary>
    public static PdfBillLayout? Choose(string text, IEnumerable<PdfBillLayout> layouts) =>
        layouts
            .Where(l => !l.IsDeleted && Fits(text, l))
            .OrderByDescending(l => l.Markers.Count(m => !string.IsNullOrWhiteSpace(m)))
            .ThenBy(l => l.Name, StringComparer.CurrentCultureIgnoreCase)
            .FirstOrDefault();

    /// <summary>True when the layout has at least one marker and every marker occurs in the text.</summary>
    public static bool Fits(string text, PdfBillLayout layout)
    {
        var markers = layout.Markers.Where(m => !string.IsNullOrWhiteSpace(m)).ToList();
        return markers.Count > 0 && markers.All(m => text.Contains(m.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Reads the fields of one layout.</summary>
    public static PdfBillResult Read(string text, PdfBillLayout layout)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(layout);

        var warnings = new List<PdfBillWarning>();
        string? number = null;
        DateOnly? issuedOn = null, dueOn = null;
        YearMonth? period = null;
        decimal? gross = null, vatPercent = null, vat = null;

        foreach (var field in Enum.GetValues<PdfBillField>())
        {
            var raw = FindRaw(text, layout, field, warnings);
            if (raw is null)
            {
                continue;
            }

            var ok = field switch
            {
                PdfBillField.Number => (number = raw.Trim()).Length > 0,
                PdfBillField.IssuedOn => PdfValueParsers.TryParseDate(raw, out var d1) && (issuedOn = d1) is not null,
                PdfBillField.DueOn => PdfValueParsers.TryParseDate(raw, out var d2) && (dueOn = d2) is not null,
                PdfBillField.Period => PdfValueParsers.TryParsePeriod(raw, out var p) && (period = p) is not null,
                PdfBillField.Gross => PdfValueParsers.TryParseDecimal(raw, out var g) && (gross = g) is not null,
                PdfBillField.VatPercent => PdfValueParsers.TryParseDecimal(raw, out var vp) && (vatPercent = vp) is not null,
                PdfBillField.Vat => PdfValueParsers.TryParseDecimal(raw, out var v) && (vat = v) is not null,
                _ => false,
            };
            if (!ok)
            {
                number = field == PdfBillField.Number ? null : number;
                warnings.Add(new PdfBillWarning(field, PdfBillWarningKind.Unparsable, raw));
            }
        }

        var values = new PdfBillValues
        {
            Number = number,
            IssuedOn = issuedOn,
            Period = period,
            DueOn = dueOn,
            Gross = gross,
            VatPercent = vatPercent,
            Vat = vat,
        };
        var notFound = Enum.GetValues<PdfBillField>().Where(f => !IsSet(values, f)).ToList();
        return new PdfBillResult { Layout = layout, Values = values, NotFound = notFound, Warnings = warnings };
    }

    private static bool IsSet(PdfBillValues values, PdfBillField field) => field switch
    {
        PdfBillField.Number => values.Number is not null,
        PdfBillField.IssuedOn => values.IssuedOn is not null,
        PdfBillField.Period => values.Period is not null,
        PdfBillField.DueOn => values.DueOn is not null,
        PdfBillField.Gross => values.Gross is not null,
        PdfBillField.VatPercent => values.VatPercent is not null,
        PdfBillField.Vat => values.Vat is not null,
        _ => false,
    };

    private static string? FindRaw(string text, PdfBillLayout layout, PdfBillField field, List<PdfBillWarning> warnings)
    {
        var pattern = layout.Fields.FirstOrDefault(f => f.Field == field && !string.IsNullOrWhiteSpace(f.Pattern));
        if (pattern is null)
        {
            return null;
        }

        try
        {
            var match = new Regex(pattern.Pattern, PatternOptions, MatchTimeout).Match(text);
            if (!match.Success)
            {
                return null;
            }

            var group = match.Groups["v"];
            if (!group.Success)
            {
                group = match.Groups.Count > 1 ? match.Groups[1] : match.Groups[0];
            }

            return group.Success ? group.Value : null;
        }
        catch (RegexMatchTimeoutException)
        {
            warnings.Add(new PdfBillWarning(field, PdfBillWarningKind.Timeout, null));
            return null;
        }
        catch (ArgumentException)
        {
            // An invalid pattern is refused when the layout is imported; a stored one that is broken just finds nothing.
            return null;
        }
    }
}
