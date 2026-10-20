using System.Globalization;

namespace HomeUtilities.ViewModels.Common;

/// <summary>Culture-tolerant parsing and formatting of decimal numbers typed by the user.</summary>
public static class DecimalInput
{
    private const char TypographicMinus = '−';

    /// <summary>
    /// Parses a number written with <c>,</c> or <c>.</c> as the decimal separator. Any white space (including
    /// non-breaking spaces) is ignored as a group separator. Letters, empty text and several separators are rejected.
    /// </summary>
    public static bool TryParse(string? text, CultureInfo culture, out decimal value)
    {
        ArgumentNullException.ThrowIfNull(culture);

        value = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var cleaned = new string([.. text.Where(c => !char.IsWhiteSpace(c))])
            .Replace(TypographicMinus, '-')
            .Replace(culture.NumberFormat.NegativeSign, "-", StringComparison.Ordinal);

        if (cleaned.Count(c => c is ',' or '.') > 1)
        {
            return false;
        }

        return decimal.TryParse(
            cleaned.Replace(',', '.'),
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out value);
    }

    /// <summary>Formats a number with at most <paramref name="maxDecimals"/> decimals and no trailing zeros.</summary>
    public static string Format(decimal value, CultureInfo culture, int maxDecimals)
    {
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentOutOfRangeException.ThrowIfNegative(maxDecimals);

        var pattern = maxDecimals == 0 ? "0" : "0." + new string('#', maxDecimals);
        return value.ToString(pattern, culture);
    }
}
