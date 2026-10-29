using System.Globalization;
using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Import;

/// <summary>Turns sheet cells into numbers, dates and months. Never throws; a bad cell is reported by returning false.</summary>
public static class ImportCellParser
{
    // Prefixes of month names in Russian, Latvian and English, lower case. A token matches when it starts with a prefix.
    private static readonly (string Stem, int Month)[] MonthStems =
    [
        ("jan", 1), ("janv", 1), ("янв", 1),
        ("feb", 2), ("febr", 2), ("фев", 2),
        ("mar", 3), ("мар", 3),
        ("apr", 4), ("апр", 4),
        ("may", 5), ("mai", 5), ("мая", 5), ("май", 5),
        ("jun", 6), ("jūn", 6), ("июн", 6),
        ("jul", 7), ("jūl", 7), ("июл", 7),
        ("aug", 8), ("авг", 8),
        ("sep", 9), ("sept", 9), ("сен", 9),
        ("oct", 10), ("okt", 10), ("окт", 10),
        ("nov", 11), ("ноя", 11),
        ("dec", 12), ("дек", 12),
    ];

    private static readonly string[] CurrencyWords = ["€", "eur", "lvl", "lats", "ls"];

    /// <summary>
    /// Reads a number: a numeric cell, or text such as <c>1 234,56</c>, <c>12.5</c>, <c>7,05 €</c>. A comma and a dot are both
    /// accepted as the decimal separator; when both occur the last one is the decimal separator.
    /// </summary>
    public static bool TryParseDecimal(SheetCell cell, out decimal value)
    {
        value = 0;
        switch (cell.Value)
        {
            case decimal number:
                value = number;
                return true;
            case DateOnly or null:
                return cell.Value is null && TryParseDecimalText(cell.Text, out value);
            default:
                return TryParseDecimalText(cell.Value as string ?? cell.Text, out value);
        }
    }

    /// <summary>Reads a number from text (see <see cref="TryParseDecimal(SheetCell, out decimal)"/>).</summary>
    public static bool TryParseDecimalText(string? text, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        foreach (var currency in CurrencyWords)
        {
            text = text.Replace(currency, string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        var cleaned = new System.Text.StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (char.IsDigit(ch) || ch is '.' or ',' or '-' or '+' or '−')
            {
                cleaned.Append(ch == '−' ? '-' : ch);
            }
            else if (char.IsWhiteSpace(ch) || ch == '%')
            {
                continue;
            }
            else
            {
                return false;
            }
        }

        var s = cleaned.ToString();
        var lastComma = s.LastIndexOf(',');
        var lastDot = s.LastIndexOf('.');
        if (lastComma >= 0 && lastDot >= 0)
        {
            var decimalAt = Math.Max(lastComma, lastDot);
            var separator = s[decimalAt];
            var group = separator == ',' ? '.' : ',';
            s = s.Replace(group.ToString(), string.Empty).Replace(separator, '.');
        }
        else if (lastComma >= 0)
        {
            s = s.Replace(',', '.');
        }

        if (s.Count(c => c == '.') > 1 || s.IndexOf('-') > 0 || s.IndexOf('+') > 0)
        {
            return false;
        }

        return decimal.TryParse(s, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>Reads a date: a date cell, or text such as <c>25.10.2012</c>, <c>2012-10-25</c>, <c>25/10/2012</c>.</summary>
    public static bool TryParseDate(SheetCell cell, out DateOnly value)
    {
        value = default;
        if (cell.Value is DateOnly date)
        {
            value = date;
            return true;
        }

        var text = (cell.Value as string ?? cell.Text).Trim();
        if (text.Length == 0)
        {
            return false;
        }

        string[] formats = ["d.M.yyyy", "dd.MM.yyyy", "yyyy-MM-dd", "d/M/yyyy", "dd/MM/yyyy", "yyyy.MM.dd", "d.M.yy"];
        return DateOnly.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
    }

    /// <summary>
    /// Reads a billing month: a date cell, <c>MM.yyyy</c>, <c>yyyy-MM</c>, a full date, or a month name (Russian, Latvian or
    /// English) with a year, e.g. <c>Январь 2012</c>, <c>janvāris 2012</c>, <c>Jan-12</c>.
    /// </summary>
    public static bool TryParsePeriod(SheetCell cell, out YearMonth value)
    {
        value = default;
        if (cell.Value is DateOnly date)
        {
            value = YearMonth.From(date);
            return true;
        }

        var text = (cell.Value as string ?? cell.Text).Trim();
        return text.Length > 0 && TryParsePeriodText(text, out value);
    }

    private static bool TryParsePeriodText(string text, out YearMonth value)
    {
        value = default;
        var tokens = Tokenize(text);
        if (tokens.Count is 0 or > 4)
        {
            return false;
        }

        var numbers = tokens.Where(t => t.All(char.IsDigit)).ToList();
        var words = tokens.Where(t => !t.All(char.IsDigit)).ToList();

        int year;
        int month;
        if (words.Count == 0)
        {
            // 01.2012, 2012-01, 25.01.2012, 2012-01-25
            var yearToken = numbers.FirstOrDefault(t => t.Length == 4);
            if (yearToken is null || numbers.Count is < 2 or > 3 || numbers.Any(t => t.Length > 4))
            {
                return false;
            }

            year = int.Parse(yearToken, CultureInfo.InvariantCulture);
            month = numbers.Count == 3 ? int.Parse(numbers[1], CultureInfo.InvariantCulture) : int.Parse(numbers.First(t => t != yearToken), CultureInfo.InvariantCulture);
        }
        else
        {
            if (words.Count != 1 || !TryMonthName(words[0], out month) || numbers.Count == 0 || numbers.Count > 2)
            {
                return false;
            }

            var yearToken = numbers.FirstOrDefault(t => t.Length == 4);
            if (yearToken is not null)
            {
                year = int.Parse(yearToken, CultureInfo.InvariantCulture);
            }
            else if (numbers.Count == 1 && numbers[0].Length == 2)
            {
                var yy = int.Parse(numbers[0], CultureInfo.InvariantCulture);
                year = yy <= 69 ? 2000 + yy : 1900 + yy;
            }
            else
            {
                return false;
            }
        }

        if (year is < 1900 or > 2200 || month is < 1 or > 12)
        {
            return false;
        }

        value = new YearMonth(year, month);
        return true;
    }

    private static bool TryMonthName(string word, out int month)
    {
        month = 0;
        var lower = word.ToLowerInvariant();
        if (lower.Length > 12)
        {
            return false;
        }

        foreach (var (stem, number) in MonthStems.OrderByDescending(s => s.Stem.Length))
        {
            if (lower.StartsWith(stem, StringComparison.Ordinal))
            {
                month = number;
                return true;
            }
        }

        return false;
    }

    private static List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        var current = new System.Text.StringBuilder();
        foreach (var ch in text)
        {
            if (char.IsLetterOrDigit(ch))
            {
                current.Append(ch);
            }
            else if (current.Length > 0)
            {
                tokens.Add(current.ToString());
                current.Clear();
            }
        }

        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
        }

        return tokens;
    }
}
