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

    /// <summary>
    /// The plans that run into <paramref name="candidate"/> and must end the day before it starts: earlier plans that are
    /// open-ended or end on or after the candidate's start. A new plan "from next month" thereby closes the current one.
    /// </summary>
    public static IReadOnlyList<EqualizedPlan> PredecessorsToClose(IEnumerable<EqualizedPlan> others, EqualizedPlan candidate)
    {
        ArgumentNullException.ThrowIfNull(others);
        ArgumentNullException.ThrowIfNull(candidate);

        return [.. others.Where(p => !p.IsDeleted && p.Id != candidate.Id && p.ValidFrom < candidate.ValidFrom
            && (p.ValidTo is null || p.ValidTo >= candidate.ValidFrom))];
    }

    /// <summary>
    /// Validates a plan that is added or changed: on its own and together with the other plans of the contract, after the
    /// predecessors it runs into have been closed (see <see cref="PredecessorsToClose"/>).
    /// </summary>
    public static ValidationResult ValidateWith(IEnumerable<EqualizedPlan> others, EqualizedPlan candidate)
    {
        ArgumentNullException.ThrowIfNull(others);
        ArgumentNullException.ThrowIfNull(candidate);

        var list = others.Where(p => !p.IsDeleted && p.Id != candidate.Id).ToList();
        var closing = PredecessorsToClose(list, candidate).Select(p => p.Id).ToHashSet();
        var adjusted = list.Select(p => closing.Contains(p.Id)
            ? new EqualizedPlan { Id = p.Id, ContractId = p.ContractId, ValidFrom = p.ValidFrom, ValidTo = candidate.ValidFrom.AddDays(-1), MonthlyAmount = p.MonthlyAmount }
            : p);
        return EqualizedPlanValidator.ValidateSet([.. adjusted, candidate]);
    }
}
