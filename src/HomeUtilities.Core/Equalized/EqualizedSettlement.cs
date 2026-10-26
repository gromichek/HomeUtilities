using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Charges;
using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Equalized;

/// <summary>One month of the settlement table.</summary>
/// <param name="Period">The month.</param>
/// <param name="PlanAmount">The plan amount of the month; null when no plan covers it.</param>
/// <param name="Billed">Gross of the non-deleted Equalized bills of the month.</param>
/// <param name="Actual">Gross of the calculated charge; null when it could not be calculated at all.</param>
/// <param name="IsComplete">False when the calculation lacks readings or prices: the month is not counted in the totals.</param>
/// <param name="Difference">Actual − Billed; null when the month is incomplete.</param>
/// <param name="RunningTotal">Sum of the differences of the complete months up to and including this one.</param>
public sealed record SettlementMonth(
    YearMonth Period,
    decimal? PlanAmount,
    decimal Billed,
    decimal? Actual,
    bool IsComplete,
    decimal? Difference,
    decimal RunningTotal);

/// <summary>The settlement table of a contract and what it adds up to.</summary>
/// <param name="From">First month of the table.</param>
/// <param name="To">Last month of the table.</param>
/// <param name="Months">The months, oldest first.</param>
/// <param name="TotalBilled">Billed in the complete months.</param>
/// <param name="TotalActual">Actual in the complete months.</param>
/// <param name="Expected">Expected settlement = TotalActual − TotalBilled (positive = surcharge, negative = refund).</param>
/// <param name="MissingMonths">Number of incomplete months, left out of the totals.</param>
public sealed record EqualizedSettlementResult(
    YearMonth From,
    YearMonth To,
    IReadOnlyList<SettlementMonth> Months,
    decimal TotalBilled,
    decimal TotalActual,
    decimal Expected,
    int MissingMonths)
{
    /// <summary>True when every month could be calculated.</summary>
    public bool IsComplete => MissingMonths == 0;
}

/// <summary>
/// The running difference between what the readings cost and what the equalized plan billed since the last settlement:
/// the settlement amount the owner can expect.
/// </summary>
public static class EqualizedSettlement
{
    /// <summary>
    /// The months the settlement covers: from the month after the newest Settlement bill's period (or, without one, the
    /// month of the first plan; or of the first Equalized bill; or the current month) to the current month.
    /// </summary>
    public static (YearMonth From, YearMonth To) Range(
        IEnumerable<EqualizedPlan> plans, IEnumerable<Bill> bills, YearMonth current)
    {
        ArgumentNullException.ThrowIfNull(plans);
        ArgumentNullException.ThrowIfNull(bills);

        var billList = bills.Where(b => !b.IsDeleted).ToList();
        YearMonth from;
        var settlements = billList.Where(b => b.Kind == BillKind.Settlement).Select(b => b.Period).ToList();
        if (settlements.Count > 0)
        {
            from = settlements.Max().AddMonths(1);
        }
        else
        {
            var planMonths = plans.Where(p => !p.IsDeleted).Select(p => YearMonth.From(p.ValidFrom)).ToList();
            var billMonths = billList.Where(b => b.Kind == BillKind.Equalized).Select(b => b.Period).ToList();
            from = planMonths.Count > 0 ? planMonths.Min() : billMonths.Count > 0 ? billMonths.Min() : current;
        }

        return (from > current ? current : from, current);
    }

    /// <summary>
    /// Builds the table for the months from <paramref name="from"/> to <paramref name="to"/>. <paramref name="actual"/> holds the
    /// calculated charge per month; a missing month or an incomplete charge counts as "readings missing" and is left out of
    /// the totals on both sides (billed and actual), so one month without readings cannot turn a surcharge into a refund.
    /// </summary>
    public static EqualizedSettlementResult Calculate(
        IEnumerable<EqualizedPlan> plans,
        IEnumerable<Bill> bills,
        IReadOnlyDictionary<YearMonth, CalculatedCharge> actual,
        YearMonth from,
        YearMonth to)
    {
        ArgumentNullException.ThrowIfNull(plans);
        ArgumentNullException.ThrowIfNull(bills);
        ArgumentNullException.ThrowIfNull(actual);

        var planList = plans.ToList();
        var billed = bills
            .Where(b => !b.IsDeleted && b.Kind == BillKind.Equalized)
            .GroupBy(b => b.Period)
            .ToDictionary(g => g.Key, g => g.Sum(b => b.Gross));

        var months = new List<SettlementMonth>();
        decimal totalBilled = 0, totalActual = 0, running = 0;
        var missing = 0;
        for (var month = from; month <= to; month = month.AddMonths(1))
        {
            var monthBilled = billed.GetValueOrDefault(month);
            var charge = actual.GetValueOrDefault(month);
            var complete = charge is { IsComplete: true };
            decimal? difference = null;
            if (complete)
            {
                difference = charge!.TotalGross - monthBilled;
                totalBilled += monthBilled;
                totalActual += charge.TotalGross;
                running += difference.Value;
            }
            else
            {
                missing++;
            }

            months.Add(new SettlementMonth(
                month, EqualizedPlans.AmountFor(planList, month), monthBilled, charge?.TotalGross, complete, difference, running));
        }

        return new EqualizedSettlementResult(from, to, months, totalBilled, totalActual, totalActual - totalBilled, missing);
    }
}
