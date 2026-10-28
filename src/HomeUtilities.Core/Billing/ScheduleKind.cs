namespace HomeUtilities.Core.Billing;

/// <summary>How often a bill template creates a bill.</summary>
public enum ScheduleKind
{
    /// <summary>Never automatically; the template is only used by hand ("New bill from template").</summary>
    Once = 0,

    /// <summary>Every month.</summary>
    Monthly = 1,

    /// <summary>Every N months (3 = quarterly, 6 = half-year).</summary>
    EveryNMonths = 2,

    /// <summary>Once a year, in the month of <see cref="Schedule.StartMonth"/>.</summary>
    Yearly = 3,
}
