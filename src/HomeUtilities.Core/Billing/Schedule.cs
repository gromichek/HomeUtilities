using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Billing;

/// <summary>When a bill template creates its bills. Periods are the months the bills belong to.</summary>
public sealed class Schedule
{
    /// <summary>Smallest allowed <see cref="EveryMonths"/>.</summary>
    public const int MinEveryMonths = 2;

    /// <summary>Largest allowed <see cref="EveryMonths"/>.</summary>
    public const int MaxEveryMonths = 24;

    /// <summary>How often a bill is created.</summary>
    public ScheduleKind Kind { get; set; } = ScheduleKind.Once;

    /// <summary>N of <see cref="ScheduleKind.EveryNMonths"/>; null for the other kinds.</summary>
    public int? EveryMonths { get; set; }

    /// <summary>The first period; every other period is counted from it. Null for <see cref="ScheduleKind.Once"/>.</summary>
    public YearMonth? StartMonth { get; set; }

    /// <summary>The last period that may be created; null = no end.</summary>
    public YearMonth? EndMonth { get; set; }

    /// <summary>Day of the month a bill is issued (1–31; a day beyond the month's length becomes its last day).</summary>
    public int? DayOfMonth { get; set; }

    /// <summary>A copy that can be edited without touching this schedule.</summary>
    public Schedule Clone() => new()
    {
        Kind = Kind,
        EveryMonths = EveryMonths,
        StartMonth = StartMonth,
        EndMonth = EndMonth,
        DayOfMonth = DayOfMonth,
    };
}
