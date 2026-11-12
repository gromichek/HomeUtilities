using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Overview;

/// <summary>The money of one property in one currency.</summary>
/// <param name="PropertyId">The property.</param>
/// <param name="Currency">The currency of the amounts.</param>
/// <param name="OpenBills">Sum of the open amounts of all bills that are not fully paid (the "Open" total of the Bills screen).</param>
/// <param name="OverdueBills">The part of <paramref name="OpenBills"/> that is past its due date.</param>
/// <param name="Prepayments">Money of payments that no bill uses (the "Prepayment" total of the Payments screen).</param>
/// <param name="Balance">Sum of the balances of the property's contracts (negative = debt, positive = paid in advance).</param>
public sealed record PropertyMoney(
    Guid PropertyId, Currency Currency, decimal OpenBills, decimal OverdueBills, decimal Prepayments, decimal Balance);

/// <summary>What an equalized contract expects to settle.</summary>
/// <param name="PropertyId">The property.</param>
/// <param name="ContractId">The equalized contract.</param>
/// <param name="Title">The contract's title.</param>
/// <param name="Currency">The contract's currency.</param>
/// <param name="Expected">Positive = surcharge, negative = refund.</param>
public sealed record ExpectedSettlement(Guid PropertyId, Guid ContractId, string Title, Currency Currency, decimal Expected);

/// <summary>The bills of this year up to the current month and of the same months of last year.</summary>
/// <param name="PropertyId">The property.</param>
/// <param name="Currency">The currency of the amounts.</param>
/// <param name="Year">This year.</param>
/// <param name="ThisYear">Gross of the bills with a period from January to <paramref name="ThroughMonth"/> of this year.</param>
/// <param name="LastYear">Gross of the bills with a period in the same months (January to <paramref name="ThroughMonth"/>) of the year before.</param>
/// <param name="HasLastYear">Whether there is any bill in those months; without one "0,00" would be a false figure (UI_GUIDELINES §9).</param>
/// <param name="ThroughMonth">The latest of the contracts' own end months (informational; each contract is counted up to its own last month with a bill this year).</param>
public sealed record YearComparison(Guid PropertyId, Currency Currency, int Year, decimal ThisYear, decimal LastYear, bool HasLastYear = true, int ThroughMonth = 12)
{
    /// <summary>This year against last year in percent, rounded to one decimal; null when last year is zero.</summary>
    public decimal? ChangePercent => LastYear == 0 ? null : Math.Round((ThisYear - LastYear) / LastYear * 100m, 1, MidpointRounding.AwayFromZero);
}

/// <summary>The figures of the Money and This year cards of the Overview.</summary>
public static class OverviewMoney
{
    /// <summary>
    /// Per property and currency: open and overdue bills, prepayments and the contracts' balance on <paramref name="today"/>.
    /// A currency appears only if the property has a bill, a payment or a contract in it. Properties in list order.
    /// </summary>
    public static IReadOnlyList<PropertyMoney> PerProperty(OverviewData data, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(data);

        var bills = data.Bills.Where(b => !b.IsDeleted && !b.IsDraft).ToList();
        var payments = data.Payments.Where(p => !p.IsDeleted).ToList();
        var contracts = data.Contracts.Where(c => !c.IsDeleted).ToList();
        var contractProperty = contracts.ToDictionary(c => c.Id, c => c.PropertyId);
        var billProperty = bills.ToDictionary(b => b.Id, b => b.PropertyId);
        var allocationsByBill = payments
            .SelectMany(p => p.Allocations.Where(a => !a.IsDeleted))
            .GroupBy(a => a.BillId)
            .ToDictionary(g => g.Key, g => g.Sum(a => a.Amount));

        var result = new List<PropertyMoney>();
        foreach (var property in data.Properties.Where(p => !p.IsDeleted))
        {
            var propertyBills = bills.Where(b => b.PropertyId == property.Id).ToList();
            var propertyPayments = payments.Where(p => PropertyOf(p, contractProperty, billProperty) == property.Id).ToList();
            var propertyContracts = contracts.Where(c => c.PropertyId == property.Id).ToList();

            var currencies = propertyBills.Select(b => b.Currency)
                .Concat(propertyPayments.Select(p => p.Currency))
                .Concat(propertyContracts.Select(c => c.Currency))
                .Distinct()
                .Order();
            foreach (var currency in currencies)
            {
                decimal open = 0, overdue = 0;
                foreach (var bill in propertyBills.Where(b => b.Currency == currency))
                {
                    var rest = bill.Gross - allocationsByBill.GetValueOrDefault(bill.Id);
                    if (rest == 0)
                    {
                        continue;
                    }

                    open += rest;
                    if (rest > 0 && bill.DueOn is { } due && due < today)
                    {
                        overdue += rest;
                    }
                }

                var prepayments = propertyPayments.Where(p => p.Currency == currency).Sum(p => Math.Max(0m, PaymentRules.Unallocated(p)));
                var balance = propertyContracts
                    .Where(c => c.Currency == currency)
                    .Sum(c => ContractBalance.Of(c.Id, bills, payments, today).Value);
                result.Add(new PropertyMoney(property.Id, currency, open, overdue, prepayments, balance));
            }
        }

        return result;
    }

    /// <summary>The expected settlement of every equalized contract that is not archived (also the small ones).</summary>
    public static IReadOnlyList<ExpectedSettlement> Settlements(OverviewData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var visible = data.Properties.Where(p => !p.IsDeleted).Select(p => p.Id).ToHashSet();
        return
        [
            .. data.Contracts
                .Where(c => !c.IsDeleted && !c.IsArchived && visible.Contains(c.PropertyId) && data.ExpectedSettlements.ContainsKey(c.Id))
                .OrderBy(c => c.Title, StringComparer.OrdinalIgnoreCase)
                .Select(c => new ExpectedSettlement(c.PropertyId, c.Id, c.Title, c.Currency, data.ExpectedSettlements[c.Id])),
        ];
    }

    /// <summary>
    /// Per property and currency: the gross of the bills of this year, for each contract from January to its own last month with a bill
    /// (at most the month of <paramref name="today"/>), and of the same months of the same contract last year (by the bill's period, like the
    /// period filter of the Bills screen). Bills without a contract form one group. A contract without a bill this year counts in neither year.
    /// </summary>
    public static IReadOnlyList<YearComparison> YearToDate(OverviewData data, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(data);

        var bills = data.Bills.Where(b => !b.IsDeleted && !b.IsDraft && b.Period.Month <= today.Month).ToList();
        var result = new List<YearComparison>();
        foreach (var property in data.Properties.Where(p => !p.IsDeleted))
        {
            foreach (var group in bills.Where(b => b.PropertyId == property.Id && b.Period.Year >= today.Year - 1 && b.Period.Year <= today.Year)
                .GroupBy(b => b.Currency).OrderBy(g => g.Key))
            {
                // Every contract (and the one group of bills without a contract) is counted up to ITS OWN last month with a bill this
                // year, and last year up to the same month: bills arrive in different months for different contracts.
                decimal thisYear = 0, lastYear = 0;
                var hasLastYear = false;
                var throughMonth = 0;
                foreach (var contract in group.GroupBy(b => b.ContractId))
                {
                    var thisYearBills = contract.Where(b => b.Period.Year == today.Year).ToList();
                    if (thisYearBills.Count == 0)
                    {
                        continue;   // no bill this year (maybe the contract ended): in neither year
                    }

                    var contractMonth = thisYearBills.Max(b => b.Period.Month);
                    var lastYearBills = contract.Where(b => b.Period.Year == today.Year - 1 && b.Period.Month <= contractMonth).ToList();
                    thisYear += thisYearBills.Sum(b => b.Gross);
                    lastYear += lastYearBills.Sum(b => b.Gross);
                    hasLastYear |= lastYearBills.Count > 0;
                    throughMonth = Math.Max(throughMonth, contractMonth);
                }

                if (throughMonth == 0)
                {
                    continue;
                }

                result.Add(new YearComparison(property.Id, group.Key, today.Year, thisYear, lastYear, hasLastYear, throughMonth));
            }
        }

        return result;
    }

    /// <summary>The property a payment is counted under: its own, its contract's, or that of the first bill it settles.</summary>
    /// <summary>
    /// Whether there is any bill (not a draft) or payment, of all properties or only of <paramref name="propertyId"/>.
    /// Tells "nothing is open" (everything paid) from "no records at all" on the Money card.
    /// </summary>
    public static bool HasRecords(OverviewData data, Guid? propertyId = null)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (data.Bills.Any(b => !b.IsDeleted && !b.IsDraft && (propertyId is null || b.PropertyId == propertyId)))
        {
            return true;
        }

        var contractProperty = data.Contracts.Where(c => !c.IsDeleted).ToDictionary(c => c.Id, c => c.PropertyId);
        var billProperty = data.Bills.Where(b => !b.IsDeleted && !b.IsDraft).ToDictionary(b => b.Id, b => b.PropertyId);
        return data.Payments.Any(p => !p.IsDeleted && (propertyId is null || PropertyOf(p, contractProperty, billProperty) == propertyId));
    }

    private static Guid? PropertyOf(Payment payment, Dictionary<Guid, Guid> contractProperty, Dictionary<Guid, Guid> billProperty)
    {
        if (payment.PropertyId is { } own)
        {
            return own;
        }

        if (payment.ContractId is { } contractId && contractProperty.TryGetValue(contractId, out var fromContract))
        {
            return fromContract;
        }

        foreach (var allocation in payment.Allocations.Where(a => !a.IsDeleted))
        {
            if (billProperty.TryGetValue(allocation.BillId, out var fromBill))
            {
                return fromBill;
            }
        }

        return null;
    }
}
