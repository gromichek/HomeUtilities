using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.PdfBills;

/// <summary>Parses the texts a PDF layout finds: amounts, dates and months (with Latvian month names).</summary>
public static partial class PdfValueParsers
{
    // Stems of the Latvian month names without diacritics; nominative (janvāris) and locative (janvārī) share the stem.
    private static readonly (string Stem, int Month)[] MonthStems =
    [
        ("janvar", 1), ("februar", 2), ("mart", 3), ("april", 4), ("maij", 5), ("junij", 6),
        ("julij", 7), ("august", 8), ("septembr", 9), ("oktobr", 10), ("novembr", 11), ("decembr", 12),
    ];

    /// <summary>Parses an amount or percentage: <c>59.49</c>, <c>59,49</c>, <c>1 234,56 EUR</c>, <c>€ 12</c>, <c>21 %</c>.</summary>
    public static bool TryParseDecimal(string? text, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        // Spaces (also non-breaking) group thousands; the currency and percent signs are decoration.
        var s = new string([.. text.Where(c => !char.IsWhiteSpace(c) && c is not ('€' or '%'))]);
        s = EurRegex().Replace(s, string.Empty);
        if (s.Length == 0)
        {
            return false;
        }

        var lastDot = s.LastIndexOf('.');
        var lastComma = s.LastIndexOf(',');
        if (lastDot >= 0 && lastComma >= 0)
        {
            // Both present: the last one is the decimal separator, the other one groups thousands.
            var decimalSeparator = lastDot > lastComma ? '.' : ',';
            var group = decimalSeparator == '.' ? ',' : '.';
            s = s.Replace(group.ToString(), string.Empty, StringComparison.Ordinal).Replace(decimalSeparator, '.');
        }
        else if (lastComma >= 0)
        {
            s = s.Replace(',', '.');
        }

        if (!MoneyRegex().IsMatch(s))
        {
            return false;
        }

        return decimal.TryParse(s, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    /// Parses a date: <c>30.11.2026</c>, <c>30.11.2026.</c>, <c>2026-11-30</c>, <c>2026.gada 30. septembris</c>,
    /// <c>2026. gada 1. jūlijs</c>.
    /// </summary>
    public static bool TryParseDate(string? text, out DateOnly value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var t = text.Trim();
        var numeric = NumericDateRegex().Match(t);
        if (numeric.Success)
        {
            return TryDate(numeric.Groups["y"].Value, numeric.Groups["m"].Value, numeric.Groups["d"].Value, out value);
        }

        var iso = IsoDateRegex().Match(t);
        if (iso.Success)
        {
            return TryDate(iso.Groups["y"].Value, iso.Groups["m"].Value, iso.Groups["d"].Value, out value);
        }

        var words = WordDateRegex().Match(t);
        if (words.Success && MonthOf(words.Groups["m"].Value) is { } month)
        {
            return TryDate(words.Groups["y"].Value, month.ToString(CultureInfo.InvariantCulture), words.Groups["d"].Value, out value);
        }

        return false;
    }

    /// <summary>Parses a month: <c>2026. gada septembris</c>, <c>2026.GADA septembrī</c>, <c>09.2026</c>, <c>2026-09</c>.</summary>
    public static bool TryParsePeriod(string? text, out YearMonth value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var t = text.Trim();
        var words = WordPeriodRegex().Match(t);
        if (words.Success && MonthOf(words.Groups["m"].Value) is { } month)
        {
            return TryMonth(words.Groups["y"].Value, month, out value);
        }

        var numeric = NumericPeriodRegex().Match(t);
        if (numeric.Success && int.TryParse(numeric.Groups["m"].Value, CultureInfo.InvariantCulture, out var m))
        {
            return TryMonth(numeric.Groups["y"].Value, m, out value);
        }

        var iso = IsoPeriodRegex().Match(t);
        if (iso.Success && int.TryParse(iso.Groups["m"].Value, CultureInfo.InvariantCulture, out var im))
        {
            return TryMonth(iso.Groups["y"].Value, im, out value);
        }

        return false;
    }

    /// <summary>The month number (1–12) of a Latvian month name in any case and form, with or without diacritics; null if it is none.</summary>
    public static int? MonthOf(string word)
    {
        var normalized = RemoveDiacritics(word).ToLowerInvariant();
        foreach (var (stem, month) in MonthStems)
        {
            if (normalized.StartsWith(stem, StringComparison.Ordinal))
            {
                return month;
            }
        }

        return null;
    }

    private static bool TryDate(string year, string month, string day, out DateOnly value)
    {
        value = default;
        if (!int.TryParse(year, CultureInfo.InvariantCulture, out var y)
            || !int.TryParse(month, CultureInfo.InvariantCulture, out var m)
            || !int.TryParse(day, CultureInfo.InvariantCulture, out var d)
            || y is < 1900 or > 2200 || m is < 1 or > 12 || d < 1 || d > DateTime.DaysInMonth(y, m))
        {
            return false;
        }

        value = new DateOnly(y, m, d);
        return true;
    }

    private static bool TryMonth(string year, int month, out YearMonth value)
    {
        value = default;
        if (!int.TryParse(year, CultureInfo.InvariantCulture, out var y) || y is < 1900 or > 2200 || month is < 1 or > 12)
        {
            return false;
        }

        value = new YearMonth(y, month);
        return true;
    }

    private static string RemoveDiacritics(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    [GeneratedRegex(@"^[-+]?\d+(\.\d+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex MoneyRegex();

    [GeneratedRegex("EUR", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex EurRegex();

    [GeneratedRegex(@"^(?<d>\d{1,2})\.(?<m>\d{1,2})\.(?<y>\d{4})\.?$", RegexOptions.CultureInvariant)]
    private static partial Regex NumericDateRegex();

    [GeneratedRegex(@"^(?<y>\d{4})-(?<m>\d{1,2})-(?<d>\d{1,2})$", RegexOptions.CultureInvariant)]
    private static partial Regex IsoDateRegex();

    [GeneratedRegex(@"^(?<y>\d{4})\s*\.?\s*gada\s+(?<d>\d{1,2})\s*\.?\s*(?<m>\p{L}+)\.?$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex WordDateRegex();

    [GeneratedRegex(@"^(?<y>\d{4})\s*\.?\s*gada\s+(?<m>\p{L}+)\.?$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex WordPeriodRegex();

    [GeneratedRegex(@"^(?<m>\d{1,2})\.(?<y>\d{4})\.?$", RegexOptions.CultureInvariant)]
    private static partial Regex NumericPeriodRegex();

    [GeneratedRegex(@"^(?<y>\d{4})-(?<m>\d{1,2})$", RegexOptions.CultureInvariant)]
    private static partial Regex IsoPeriodRegex();
}
