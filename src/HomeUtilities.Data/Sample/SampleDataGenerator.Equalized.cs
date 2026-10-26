using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Charges;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Equalized;
using HomeUtilities.Core.Taxes;

namespace HomeUtilities.Data.Sample;

// Equalized electricity of the cottage and the property tax installments of the garage and the cottage.
public sealed partial class SampleDataGenerator
{
    /// <summary>Months of equalized bills (the twelve months before the current one).</summary>
    public const int EqualizedBillMonths = 12;

    /// <summary>The first plan ends and the second starts this many months ago.</summary>
    private const int PlanChangeMonthsAgo = 6;

    /// <summary>The settlement bill belongs to the month before the twelve equalized months.</summary>
    private const int SettlementMonthsAgo = EqualizedBillMonths + 1;

    /// <summary>
    /// The cottage electricity is billed in equal monthly amounts: two plan versions (the amount rose six months ago), twelve
    /// monthly Equalized bills (all paid) and last year's Settlement bill (a small surcharge, paid). The plan is chosen a little
    /// below the average calculated charge, so the settlement page expects a small surcharge; the current month has no
    /// reading yet and shows as incomplete.
    /// </summary>
    private async Task AddEqualizedAsync(Contract contract, IReadOnlyDictionary<Guid, Supplier> supplierOf, CancellationToken ct)
    {
        var current = YearMonth.From(_today);
        decimal total = 0;
        var months = 0;
        for (var ago = 1; ago <= EqualizedBillMonths; ago++)
        {
            var input = await chargeLoader.LoadAsync(contract.Id, current.AddMonths(-ago), ct);
            if (input is not null && ChargeCalculator.Calculate(input) is { IsComplete: true } charge)
            {
                total += charge.TotalGross;
                months++;
            }
        }

        var average = months == 0 ? 20m : total / months;
        var recent = Math.Max(6m, Math.Round(average, 0, MidpointRounding.AwayFromZero) - 1m);
        var earlier = recent - 2m;
        var changeMonth = current.AddMonths(-PlanChangeMonthsAgo);
        EqualizedPlan[] plans =
        [
            new() { ContractId = contract.Id, ValidFrom = current.AddMonths(-18).FirstDay, ValidTo = changeMonth.AddMonths(-1).LastDay, MonthlyAmount = earlier, Notes = "Invented sample plan." },
            new() { ContractId = contract.Id, ValidFrom = changeMonth.FirstDay, MonthlyAmount = recent, Notes = "Amount raised after the yearly review (invented)." },
        ];
        foreach (var plan in plans)
        {
            await equalizedPlans.SaveAsync(plan, ct);
        }

        for (var ago = EqualizedBillMonths; ago >= 1; ago--)
        {
            var period = current.AddMonths(-ago);
            var bill = Bill.CreateFor(contract, period, clock);
            bill.Number = $"EQ-{period.Year}-{period.Month:00}";
            bill.IssuedOn = DayOf(period.AddMonths(1), 5);
            bill.DueOn = DayOf(period.AddMonths(1), 20);
            ApplyAmounts(bill, EqualizedPlans.AmountFor(plans, period) ?? recent, 21m);
            await bills.SaveAsync(bill, ct);
            await payments.SaveAsync(NewPayment(bill, contract, supplierOf, bill.Gross, quick: true), ct);
        }

        var settlementPeriod = current.AddMonths(-SettlementMonthsAgo);
        var settlement = Bill.CreateFor(contract, settlementPeriod, clock);
        settlement.Kind = BillKind.Settlement;
        settlement.Number = $"SET-{settlementPeriod.Year}-0001";
        settlement.IssuedOn = DayOf(settlementPeriod.AddMonths(1), 10);
        settlement.DueOn = DayOf(settlementPeriod.AddMonths(1), 25);
        settlement.Notes = "Invented sample settlement: the yearly reconciliation asks for a small surcharge.";
        ApplyAmounts(settlement, 8.35m, 21m);
        await bills.SaveAsync(settlement, ct);
        await payments.SaveAsync(NewPayment(settlement, contract, supplierOf, settlement.Gross, quick: false), ct);
    }

    /// <summary>
    /// This year's property tax of a contract in installments with the Latvian due dates. Installments whose due date has passed
    /// are paid, except the newest of them on a two-installment tax, which stays unpaid and shows as overdue.
    /// </summary>
    private async Task AddTaxInstallmentsAsync(
        Contract contract, int count, decimal yearly, IReadOnlyDictionary<Guid, Supplier> supplierOf, CancellationToken ct)
    {
        var year = _today.Year;
        var dates = TaxInstallmentPlanner.DefaultDueDates(year, count);
        var planned = TaxInstallmentPlanner.Plan(yearly, dates);
        var installments = TaxInstallmentPlanner.CreateBills(
            contract, year, planned, p => $"Property tax {year}, installment {p.Number} of {p.Count}", clock);

        var due = installments.Where(b => b.DueOn < _today).ToList();
        var overdue = count == 2 ? due.LastOrDefault() : null;
        foreach (var bill in installments)
        {
            bill.Number = $"TAX-{year}-{bill.DueOn:MMdd}";
            bill.IssuedOn = bill.DueOn!.Value.AddDays(-30) > _today ? _today : bill.DueOn.Value.AddDays(-30);
            await bills.SaveAsync(bill, ct);
            if (due.Contains(bill) && bill != overdue)
            {
                await payments.SaveAsync(NewPayment(bill, contract, supplierOf, bill.Gross, quick: false), ct);
            }
        }
    }
}
