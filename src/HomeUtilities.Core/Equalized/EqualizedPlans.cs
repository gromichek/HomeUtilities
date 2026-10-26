using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Equalized;

/// <summary>Lookup in the plan versions of a contract.</summary>
public static class EqualizedPlans
{
    /// <summary>
    /// The plan of a month: the plan valid on its first day, or else the first plan that starts later in that month
    /// (a contract whose first plan starts mid-month is billed from that month). Null when no plan covers the month.
    /// </summary>
    public static EqualizedPlan? For(IEnumerable<EqualizedPlan> plans, YearMonth month)
    {
        ArgumentNullException.ThrowIfNull(plans);

        var active = plans.Where(p => !p.IsDeleted).ToList();
        var first = month.FirstDay;
        return active
                   .Where(p => p.ValidFrom <= first && (p.ValidTo is null || p.ValidTo >= first))
                   .OrderByDescending(p => p.ValidFrom)
                   .FirstOrDefault()
               ?? active
                   .Where(p => p.ValidFrom > first && p.ValidFrom <= month.LastDay)
                   .OrderBy(p => p.ValidFrom)
                   .FirstOrDefault();
    }

    /// <summary>The monthly amount of a month, or null when no plan covers it.</summary>
    public static decimal? AmountFor(IEnumerable<EqualizedPlan> plans, YearMonth month) => For(plans, month)?.MonthlyAmount;
}
