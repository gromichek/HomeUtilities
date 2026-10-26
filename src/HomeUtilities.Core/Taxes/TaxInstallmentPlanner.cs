using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Taxes;

/// <summary>One installment of a yearly property tax, before it becomes a bill.</summary>
/// <param name="Number">1-based position in the year.</param>
/// <param name="Count">How many installments the year has.</param>
/// <param name="DueOn">Due date.</param>
/// <param name="Amount">Gross amount.</param>
public sealed record PlannedInstallment(int Number, int Count, DateOnly DueOn, decimal Amount);

/// <summary>Splits a yearly property tax into installments and turns them into bills.</summary>
public static class TaxInstallmentPlanner
{
    /// <summary>The numbers of installments the app offers.</summary>
    public static readonly IReadOnlyList<int> SupportedCounts = [1, 2, 4];

    /// <summary>The Latvian due dates of the year: 31.03 / 31.03 + 15.08 / 31.03, 15.05, 15.08, 15.11.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The count is not one of <see cref="SupportedCounts"/>.</exception>
    public static IReadOnlyList<DateOnly> DefaultDueDates(int year, int count) => count switch
    {
        1 => [new(year, 3, 31)],
        2 => [new(year, 3, 31), new(year, 8, 15)],
        4 => [new(year, 3, 31), new(year, 5, 15), new(year, 8, 15), new(year, 11, 15)],
        _ => throw new ArgumentOutOfRangeException(nameof(count), count, "Only 1, 2 or 4 installments are supported."),
    };

    /// <summary>
    /// Splits an amount evenly into cents; the remainder cents go to the first installment
    /// (101.00 / 4 = 25.25 × 4; 100.03 / 4 = 25.03 + 25.00 × 3).
    /// </summary>
    public static IReadOnlyList<decimal> Split(decimal total, int count)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);

        var share = Math.Floor(total * 100m / count) / 100m;
        var amounts = Enumerable.Repeat(share, count).ToArray();
        amounts[0] += total - share * count;
        return amounts;
    }

    /// <summary>
    /// Validates the input of "Enter yearly tax": amount above zero, one of the supported counts, one due date per
    /// installment, all inside the year and strictly ascending.
    /// </summary>
    public static ValidationResult Validate(int year, decimal total, IReadOnlyList<DateOnly> dueDates)
    {
        ArgumentNullException.ThrowIfNull(dueDates);

        var errors = new List<ValidationError>();
        if (total <= 0)
        {
            errors.Add(new(ErrorCodes.TaxYearlyAmountNotPositive, "Total"));
        }

        if (!SupportedCounts.Contains(dueDates.Count))
        {
            errors.Add(new(ErrorCodes.TaxInstallmentCountInvalid, "Count"));
        }

        for (var i = 0; i < dueDates.Count; i++)
        {
            if (dueDates[i].Year != year)
            {
                errors.Add(new(ErrorCodes.TaxDueDateOutsideYear, $"DueDates[{i}]", [year]));
            }
            else if (i > 0 && dueDates[i] <= dueDates[i - 1])
            {
                errors.Add(new(ErrorCodes.TaxDueDatesNotAscending, $"DueDates[{i}]"));
            }
        }

        return errors.Count == 0 ? ValidationResult.Ok : ValidationResult.Fail([.. errors]);
    }

    /// <summary>
    /// The installments of a year: one per due date, the total split evenly. When installments were paid before
    /// (<paramref name="keptCount"/>, worth <paramref name="keptTotal"/>), they stay and take the first slots; the rest
    /// of the yearly amount is split over the remaining slots. Empty when nothing is left to plan.
    /// </summary>
    public static IReadOnlyList<PlannedInstallment> Plan(
        decimal total, IReadOnlyList<DateOnly> dueDates, int keptCount = 0, decimal keptTotal = 0)
    {
        ArgumentNullException.ThrowIfNull(dueDates);

        var remainingSlots = dueDates.Count - keptCount;
        var remaining = total - keptTotal;
        if (remainingSlots < 1 || remaining <= 0)
        {
            return [];
        }

        var amounts = Split(remaining, remainingSlots);
        return [.. Enumerable.Range(0, remainingSlots)
            .Select(i => new PlannedInstallment(keptCount + i + 1, dueDates.Count, dueDates[keptCount + i], amounts[i]))];
    }

    /// <summary>
    /// Creates the bills of planned installments: Kind TaxInstallment, <c>Period</c> = month of the due date,
    /// <c>DueOn</c> = due date, service period 1 January – 31 December of the year (reports spread the amount over it).
    /// </summary>
    public static IReadOnlyList<Bill> CreateBills(
        Contract contract, int year, IEnumerable<PlannedInstallment> installments, Func<PlannedInstallment, string> title, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(installments);
        ArgumentNullException.ThrowIfNull(title);

        return [.. installments.Select(installment =>
        {
            var bill = Bill.CreateFor(contract, YearMonth.From(installment.DueOn), clock);
            bill.Kind = BillKind.TaxInstallment;
            bill.Title = title(installment);
            bill.DueOn = installment.DueOn;
            bill.ServiceFrom = new DateOnly(year, 1, 1);
            bill.ServiceTo = new DateOnly(year, 12, 31);
            bill.Gross = installment.Amount;
            return bill;
        })];
    }

    /// <summary>The non-deleted installment bills of a contract for a tax year (identified by the service period), by due date.</summary>
    public static IReadOnlyList<Bill> ForYear(IEnumerable<Bill> bills, Guid contractId, int year)
    {
        ArgumentNullException.ThrowIfNull(bills);

        return [.. bills
            .Where(b => !b.IsDeleted && b.ContractId == contractId && b.Kind == BillKind.TaxInstallment
                && (b.ServiceFrom?.Year ?? b.Period.Year) == year)
            .OrderBy(b => b.DueOn ?? b.Period.FirstDay)
            .ThenBy(b => b.CreatedAt)];
    }
}
