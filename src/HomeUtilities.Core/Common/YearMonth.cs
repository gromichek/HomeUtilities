using System.Globalization;

namespace HomeUtilities.Core.Common;

/// <summary>A calendar month of a year, used for billing periods. Formats as <c>yyyy-MM</c>.</summary>
public readonly record struct YearMonth : IComparable<YearMonth>
{
    private const int MinYear = 1900;
    private const int MaxYear = 2200;

    /// <summary>Creates a year-month.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Year is not 1900–2200 or month is not 1–12.</exception>
    public YearMonth(int year, int month)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(year, MinYear);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(year, MaxYear);
        ArgumentOutOfRangeException.ThrowIfLessThan(month, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(month, 12);

        Year = year;
        Month = month;
    }

    /// <summary>Year (1900–2200).</summary>
    public int Year { get; }

    /// <summary>Month (1–12).</summary>
    public int Month { get; }

    /// <summary>First day of the month.</summary>
    public DateOnly FirstDay => new(Year, Month, 1);

    /// <summary>Last day of the month.</summary>
    public DateOnly LastDay => new(Year, Month, DateTime.DaysInMonth(Year, Month));

    /// <summary>The month containing the given date.</summary>
    public static YearMonth From(DateOnly date) => new(date.Year, date.Month);

    /// <summary>Returns the month <paramref name="n"/> months after this one (negative goes back).</summary>
    public YearMonth AddMonths(int n)
    {
        var index = (long)Year * 12 + (Month - 1) + n;
        return new YearMonth(checked((int)(index / 12)), (int)(index % 12) + 1);
    }

    /// <summary>Number of months from this month to <paramref name="other"/> (other − this, may be negative).</summary>
    public int MonthsUntil(YearMonth other) => (other.Year - Year) * 12 + (other.Month - Month);

    /// <summary>True if the date falls into this month.</summary>
    public bool Contains(DateOnly date) => date.Year == Year && date.Month == Month;

    /// <summary>Parses exactly <c>yyyy-MM</c>.</summary>
    /// <exception cref="FormatException">The text is not in the expected format.</exception>
    public static YearMonth Parse(string text) =>
        TryParse(text, out var value) ? value : throw new FormatException($"'{text}' is not a valid yyyy-MM value.");

    /// <summary>Tries to parse exactly <c>yyyy-MM</c> (invariant, no culture).</summary>
    public static bool TryParse(string? text, out YearMonth value)
    {
        value = default;
        if (text is not { Length: 7 } || text[4] != '-')
        {
            return false;
        }

        for (var i = 0; i < 7; i++)
        {
            if (i != 4 && !char.IsAsciiDigit(text[i]))
            {
                return false;
            }
        }

        var year = int.Parse(text.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture);
        var month = int.Parse(text.AsSpan(5, 2), NumberStyles.None, CultureInfo.InvariantCulture);
        if (year is < MinYear or > MaxYear || month is < 1 or > 12)
        {
            return false;
        }

        value = new YearMonth(year, month);
        return true;
    }

    /// <inheritdoc />
    public int CompareTo(YearMonth other) => (Year * 12 + Month).CompareTo(other.Year * 12 + other.Month);

    /// <summary>Less-than comparison.</summary>
    public static bool operator <(YearMonth left, YearMonth right) => left.CompareTo(right) < 0;

    /// <summary>Less-than-or-equal comparison.</summary>
    public static bool operator <=(YearMonth left, YearMonth right) => left.CompareTo(right) <= 0;

    /// <summary>Greater-than comparison.</summary>
    public static bool operator >(YearMonth left, YearMonth right) => left.CompareTo(right) > 0;

    /// <summary>Greater-than-or-equal comparison.</summary>
    public static bool operator >=(YearMonth left, YearMonth right) => left.CompareTo(right) >= 0;

    /// <summary>Formats as <c>yyyy-MM</c>.</summary>
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Year:D4}-{Month:D2}");
}
