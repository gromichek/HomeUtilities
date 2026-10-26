using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Charges;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Equalized;

namespace HomeUtilities.ViewModels;

/// <summary>The plans of an equalized contract and the settlement table built from them.</summary>
/// <param name="Plans">The non-deleted plans, oldest first.</param>
/// <param name="Settlement">The months since the last settlement bill with their totals.</param>
public sealed record EqualizedReport(IReadOnlyList<EqualizedPlan> Plans, EqualizedSettlementResult Settlement);

/// <summary>
/// Loads what <see cref="EqualizedSettlement"/> needs for one contract — plans, bills and the calculated charge of every month
/// since the last settlement — and returns the table. Used by the contract editor, the settlement page and the charges screen.
/// </summary>
public sealed class EqualizedSettlementService(
    IEqualizedPlanStore plans, IBillStore bills, IChargeInputLoader loader, IClock clock)
{
    /// <summary>The report of the contract for the months up to the current one.</summary>
    public async Task<EqualizedReport> LoadAsync(Guid contractId, CancellationToken ct = default)
    {
        var planList = await plans.ListByContractAsync(contractId, ct);
        var billList = await bills.ListByContractAsync(contractId, ct);
        var current = YearMonth.From(clock.Today);
        var (from, to) = EqualizedSettlement.Range(planList, billList, current);

        var charges = new Dictionary<YearMonth, CalculatedCharge>();
        for (var month = from; month <= to; month = month.AddMonths(1))
        {
            if (await loader.LoadAsync(contractId, month, ct) is { } input)
            {
                charges[month] = ChargeCalculator.Calculate(input);
            }
        }

        return new EqualizedReport(planList, EqualizedSettlement.Calculate(planList, billList, charges, from, to));
    }
}
