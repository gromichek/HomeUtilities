using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.PdfBills;

/// <summary>Chooses the layout that fits the text of a PDF and reads the fields of the bill with it.</summary>
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

    /// <summary>Largest difference between total and net + VAT that is not reported.</summary>
    public const decimal AmountTolerance = 0.01m;

    private const int RawClip = 60;

    /// <summary>Reads the fields of one layout.</summary>
    public static PdfBillResult Read(string text, PdfBillLayout layout)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(layout);

        var warnings = new List<PdfBillWarning>();
        string? number = null, title = null, notes = null;
        DateOnly? issuedOn = null, dueOn = null, serviceFrom = null, serviceTo = null;
        YearMonth? period = null;
        decimal? gross = null, vatPercent = null, vat = null, net = null, consumption = null;
        IReadOnlyList<PdfBillLineValue> lines = [];

        foreach (var field in Enum.GetValues<PdfBillField>())
        {
            if (IsNamed(field))
            {
                continue;
            }

            var entry = EntryOf(layout, field);
            if (entry is null)
            {
                continue;
            }

            switch (field)
            {
                case PdfBillField.Number:
                    number = ReadText(text, entry, field, warnings);
                    break;
                case PdfBillField.Title:
                    title = ReadText(text, entry, field, warnings);
                    break;
                case PdfBillField.IssuedOn:
                    issuedOn = ReadDate(text, entry, field, warnings);
                    break;
                case PdfBillField.DueOn:
                    dueOn = ReadDate(text, entry, field, warnings);
                    break;
                case PdfBillField.ServiceFrom:
                    serviceFrom = ReadDate(text, entry, field, warnings);
                    break;
                case PdfBillField.ServiceTo:
                    serviceTo = ReadDate(text, entry, field, warnings);
                    break;
                case PdfBillField.Period:
                    period = ReadPeriod(text, entry, warnings);
                    break;
                case PdfBillField.Gross:
                    gross = ReadNumber(text, entry, field, null, warnings);
                    break;
                case PdfBillField.VatPercent:
                    vatPercent = ReadNumber(text, entry, field, null, warnings);
                    break;
                case PdfBillField.Vat:
                    vat = ReadNumber(text, entry, field, null, warnings);
                    break;
                case PdfBillField.Net:
                    net = ReadNumber(text, entry, field, null, warnings);
                    break;
                case PdfBillField.Consumption:
                    consumption = ReadNumber(text, entry, field, null, warnings);
                    break;
                case PdfBillField.Notes:
                    notes = ReadNotes(text, entry, warnings);
                    break;
                case PdfBillField.Lines:
                    lines = ReadLines(text, entry, warnings);
                    break;
            }
        }

        var quantities = new List<PdfBillNamedNumber>();
        var amounts = new List<PdfBillNamedNumber>();
        var customs = new List<PdfBillCustomValue>();
        var foundNamed = new List<PdfBillNamedEntry>();
        var notFoundNamed = new List<PdfBillNamedEntry>();
        foreach (var entry in layout.Fields.Where(f => IsNamed(f.Field) && !string.IsNullOrWhiteSpace(f.Pattern)))
        {
            var field = entry.Field;
            var subject = (field == PdfBillField.CustomField ? entry.Name : entry.Component)?.Trim();
            if (string.IsNullOrEmpty(subject))
            {
                continue;
            }

            var key = new PdfBillNamedEntry(field, subject);
            if (field == PdfBillField.CustomField)
            {
                var custom = ReadCustom(text, entry, subject, warnings);
                if (custom is null)
                {
                    notFoundNamed.Add(key);
                    continue;
                }

                customs.Add(custom);
            }
            else
            {
                var value = ReadNumber(text, entry, field, subject, warnings);
                if (value is not { } found)
                {
                    notFoundNamed.Add(key);
                    continue;
                }

                (field == PdfBillField.Quantity ? quantities : amounts).Add(new PdfBillNamedNumber(subject, found));
            }

            foundNamed.Add(key);
        }

        if (gross is { } total && net is { } netAmount && vat is { } vatAmount && Math.Abs(netAmount + vatAmount - total) > AmountTolerance)
        {
            warnings.Add(new PdfBillWarning(PdfBillField.Net, PdfBillWarningKind.AmountsMismatch, null));
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
            Net = net,
            ServiceFrom = serviceFrom,
            ServiceTo = serviceTo,
            Title = title,
            Consumption = consumption,
            Notes = notes,
            Quantities = quantities,
            ComponentAmounts = amounts,
            CustomFields = customs,
            Lines = lines,
        };

        var foundFields = new List<PdfBillField>();
        var notFound = new List<PdfBillField>();
        foreach (var field in Enum.GetValues<PdfBillField>().Where(f => !IsNamed(f)))
        {
            if (IsSet(values, field))
            {
                foundFields.Add(field);
            }
            else if (IsHeader(field) || EntryOf(layout, field) is not null)
            {
                notFound.Add(field);
            }
        }

        return new PdfBillResult
        {
            Layout = layout,
            Values = values,
            Found = foundFields,
            NotFound = notFound,
            FoundNamed = foundNamed,
            NotFoundNamed = notFoundNamed,
            Warnings = warnings,
        };
    }

    /// <summary>True for the entries that are named by a tariff line or a custom field.</summary>
    public static bool IsNamed(PdfBillField field) =>
        field is PdfBillField.Quantity or PdfBillField.ComponentAmount or PdfBillField.CustomField;

    // The fields of T-055: always listed as "not found" when the layout gives no value for them.
    private static bool IsHeader(PdfBillField field) => field <= PdfBillField.Vat;

    private static bool IsSet(PdfBillValues values, PdfBillField field) => field switch
    {
        PdfBillField.Number => values.Number is not null,
        PdfBillField.IssuedOn => values.IssuedOn is not null,
        PdfBillField.Period => values.Period is not null,
        PdfBillField.DueOn => values.DueOn is not null,
        PdfBillField.Gross => values.Gross is not null,
        PdfBillField.VatPercent => values.VatPercent is not null,
        PdfBillField.Vat => values.Vat is not null,
        PdfBillField.Net => values.Net is not null,
        PdfBillField.ServiceFrom => values.ServiceFrom is not null,
        PdfBillField.ServiceTo => values.ServiceTo is not null,
        PdfBillField.Title => values.Title is not null,
        PdfBillField.Consumption => values.Consumption is not null,
        PdfBillField.Notes => values.Notes is not null,
        PdfBillField.Lines => values.Lines.Count > 0,
        _ => false,
    };

    private static PdfBillFieldPattern? EntryOf(PdfBillLayout layout, PdfBillField field) =>
        layout.Fields.FirstOrDefault(f => f.Field == field && !string.IsNullOrWhiteSpace(f.Pattern));

    private static string? ReadText(string text, PdfBillFieldPattern entry, PdfBillField field, List<PdfBillWarning> warnings)
    {
        var raw = Matches(text, entry, field, null, all: false, warnings)?[0] is { } match ? ValueOf(match) : null;
        return raw is null || raw.Trim().Length == 0 ? null : raw.Trim();
    }

    private static DateOnly? ReadDate(string text, PdfBillFieldPattern entry, PdfBillField field, List<PdfBillWarning> warnings)
    {
        var raw = Matches(text, entry, field, null, all: false, warnings)?[0] is { } match ? ValueOf(match) : null;
        if (raw is null)
        {
            return null;
        }

        if (PdfValueParsers.TryParseDate(raw, out var date))
        {
            return date;
        }

        warnings.Add(new PdfBillWarning(field, PdfBillWarningKind.Unparsable, raw));
        return null;
    }

    private static YearMonth? ReadPeriod(string text, PdfBillFieldPattern entry, List<PdfBillWarning> warnings)
    {
        var raw = Matches(text, entry, PdfBillField.Period, null, all: false, warnings)?[0] is { } match ? ValueOf(match) : null;
        if (raw is null)
        {
            return null;
        }

        if (PdfValueParsers.TryParsePeriod(raw, out var month))
        {
            return month;
        }

        warnings.Add(new PdfBillWarning(PdfBillField.Period, PdfBillWarningKind.Unparsable, raw));
        return null;
    }

    // A number: the first match, or with combine "sum" every match added (one that does not parse → not found + warning).
    private static decimal? ReadNumber(string text, PdfBillFieldPattern entry, PdfBillField field, string? subject, List<PdfBillWarning> warnings)
    {
        var sum = entry.Combine == PdfCombine.Sum;
        var matches = Matches(text, entry, field, subject, all: sum, warnings);
        if (matches is null)
        {
            return null;
        }

        decimal total = 0;
        foreach (var match in matches)
        {
            var raw = ValueOf(match);
            if (!PdfValueParsers.TryParseDecimal(raw, out var value))
            {
                warnings.Add(new PdfBillWarning(field, PdfBillWarningKind.Unparsable, raw, subject));
                return null;
            }

            total += value;
        }

        return total;
    }

    private static PdfBillCustomValue? ReadCustom(string text, PdfBillFieldPattern entry, string name, List<PdfBillWarning> warnings)
    {
        if (entry.Combine == PdfCombine.Sum)
        {
            return ReadNumber(text, entry, PdfBillField.CustomField, name, warnings) is { } sum
                ? new PdfBillCustomValue(name, sum.ToString(CultureInfo.InvariantCulture), sum)
                : null;
        }

        return Matches(text, entry, PdfBillField.CustomField, name, all: false, warnings)?[0] is { } match && ValueOf(match).Trim() is { Length: > 0 } raw
            ? new PdfBillCustomValue(name, raw)
            : null;
    }

    // The comment: the value (or the format applied) of the first / of every match, joined with "; ".
    private static string? ReadNotes(string text, PdfBillFieldPattern entry, List<PdfBillWarning> warnings)
    {
        var matches = Matches(text, entry, PdfBillField.Notes, null, all: entry.Combine == PdfCombine.All, warnings);
        if (matches is null)
        {
            return null;
        }

        var parts = new List<string>();
        foreach (var match in matches)
        {
            string value;
            try
            {
                value = string.IsNullOrEmpty(entry.Format) ? ValueOf(match) : match.Result(entry.Format);
            }
            catch (ArgumentException)
            {
                value = ValueOf(match);
            }

            if (value.Trim().Length > 0)
            {
                parts.Add(value.Trim());
            }
        }

        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    // One bill line per match, from the named groups description (required), quantity, unit, price, net and vat.
    private static List<PdfBillLineValue> ReadLines(string text, PdfBillFieldPattern entry, List<PdfBillWarning> warnings)
    {
        var lines = new List<PdfBillLineValue>();
        var matches = Matches(text, entry, PdfBillField.Lines, null, all: true, warnings);
        if (matches is null)
        {
            return lines;
        }

        foreach (var match in matches)
        {
            var description = match.Groups["description"] is { Success: true } d ? d.Value.Trim() : string.Empty;
            if (description.Length == 0)
            {
                continue;
            }

            if (!TryGroup(match, "quantity", out var quantity)
                || !TryGroup(match, "price", out var price)
                || !TryGroup(match, "net", out var net)
                || !TryGroup(match, "vat", out var vat))
            {
                var raw = match.Value.Trim();
                warnings.Add(new PdfBillWarning(PdfBillField.Lines, PdfBillWarningKind.Unparsable, raw.Length > RawClip ? raw[..RawClip] : raw));
                continue;
            }

            var unit = match.Groups["unit"] is { Success: true } u && u.Value.Trim().Length > 0 ? u.Value.Trim() : null;
            lines.Add(new PdfBillLineValue(description, quantity, unit, price, net, vat));
        }

        return lines;
    }

    // An absent group is fine (null); a present one must parse.
    private static bool TryGroup(Match match, string name, out decimal? value)
    {
        value = null;
        var group = match.Groups[name];
        if (!group.Success || group.Value.Trim().Length == 0)
        {
            return true;
        }

        if (!PdfValueParsers.TryParseDecimal(group.Value, out var parsed))
        {
            return false;
        }

        value = parsed;
        return true;
    }

    private static string ValueOf(Match match)
    {
        var group = match.Groups["v"];
        if (!group.Success)
        {
            group = match.Groups.Count > 1 ? match.Groups[1] : match.Groups[0];
        }

        return group.Success ? group.Value : string.Empty;
    }

    /// <summary>
    /// The matches of the entry's pattern: the first one, or (<paramref name="all"/>) every one. Null when there is none,
    /// the pattern is broken or the search took longer than <see cref="MatchTimeout"/> (the whole loop counts).
    /// </summary>
    private static List<Match>? Matches(
        string text, PdfBillFieldPattern entry, PdfBillField field, string? subject, bool all, List<PdfBillWarning> warnings)
    {
        try
        {
            var regex = new Regex(entry.Pattern, PatternOptions, MatchTimeout);
            var found = new List<Match>();
            var clock = Stopwatch.StartNew();
            for (var match = regex.Match(text); match.Success; match = match.NextMatch())
            {
                found.Add(match);
                if (!all)
                {
                    break;
                }

                if (clock.Elapsed > MatchTimeout)
                {
                    throw new RegexMatchTimeoutException(text, entry.Pattern, MatchTimeout);
                }
            }

            return found.Count == 0 ? null : found;
        }
        catch (RegexMatchTimeoutException)
        {
            warnings.Add(new PdfBillWarning(field, PdfBillWarningKind.Timeout, null, subject));
            return null;
        }
        catch (ArgumentException)
        {
            // An invalid pattern is refused when the layout is imported; a stored one that is broken just finds nothing.
            return null;
        }
    }
}
