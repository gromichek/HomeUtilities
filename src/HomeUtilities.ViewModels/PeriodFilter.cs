using HomeUtilities.Core.Common;

namespace HomeUtilities.ViewModels;

/// <summary>How wide the period filter of a list is.</summary>
public enum PeriodScope
{
    /// <summary>Every year.</summary>
    AllYears,

    /// <summary>One calendar year.</summary>
    Year,

    /// <summary>One calendar month.</summary>
    Month,
}

/// <summary>
/// The period filter shared by the Bills and Payments screens: all years, one year or one month. In "all years" mode
/// <see cref="Year"/> remembers the last year shown, so switching back to a year continues where the user was.
/// </summary>
public readonly record struct PeriodFilter
{
    private const int MinYear = 1900;
    private const int MaxYear = 2200;

    private PeriodFilter(PeriodScope scope, int year, int month)
    {
        Scope = scope;
        Year = year;
        Month = month;
    }

    /// <summary>All years, remembering <paramref name="lastYear"/> for the switch back to a year.</summary>
    public static PeriodFilter ForAllYears(int lastYear) => new(PeriodScope.AllYears, Math.Clamp(lastYear, MinYear, MaxYear), 1);

    /// <summary>One year (clamped to 1900–2200).</summary>
    public static PeriodFilter ForYear(int year) => new(PeriodScope.Year, Math.Clamp(year, MinYear, MaxYear), 1);

    /// <summary>One month.</summary>
    public static PeriodFilter ForMonth(YearMonth month) => new(PeriodScope.Month, month.Year, month.Month);

    /// <summary>What a screen shows when it is opened for the first time: the year of <paramref name="today"/>.</summary>
    public static PeriodFilter Default(DateOnly today) => ForYear(today.Year);

    /// <summary>The scope of the filter.</summary>
    public PeriodScope Scope { get; }

    /// <summary>The year (the last shown year in "all years" mode).</summary>
    public int Year { get; }

    /// <summary>The month 1–12; meaningful only in <see cref="PeriodScope.Month"/> mode.</summary>
    public int Month { get; }

    /// <summary>The filtered month (<see cref="PeriodScope.Month"/> mode only).</summary>
    public YearMonth MonthValue => new(Year, Month);

    /// <summary>
    /// Switches the scope: Year → Month uses the current month when it lies in the selected year, otherwise January;
    /// Month → Year keeps the year.
    /// </summary>
    public PeriodFilter WithScope(PeriodScope scope, DateOnly today) => scope switch
    {
        PeriodScope.AllYears => ForAllYears(Year),
        PeriodScope.Year => ForYear(Year),
        _ when Scope == PeriodScope.Month => this,
        _ => ForMonth(new YearMonth(Year, today.Year == Year ? today.Month : 1)),
    };

    /// <summary>One step back (-1) or forward (+1): a year in year mode, a month in month mode; nothing in "all years" mode.</summary>
    public PeriodFilter Step(int direction)
    {
        switch (Scope)
        {
            case PeriodScope.Year:
                return ForYear(Year + direction);
            case PeriodScope.Month:
                var index = Year * 12 + (Month - 1) + direction;
                var year = index / 12;
                return year is < MinYear or > MaxYear ? this : ForMonth(new YearMonth(year, index % 12 + 1));
            default:
                return this;
        }
    }

    /// <summary>True if a billing period passes the filter.</summary>
    public bool Contains(YearMonth period) => Scope switch
    {
        PeriodScope.AllYears => true,
        PeriodScope.Year => period.Year == Year,
        _ => period.Year == Year && period.Month == Month,
    };

    /// <summary>True if a date passes the filter.</summary>
    public bool Contains(DateOnly date) => Contains(YearMonth.From(date));
}
