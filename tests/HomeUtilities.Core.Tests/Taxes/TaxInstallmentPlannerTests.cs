using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Taxes;
using static HomeUtilities.Core.Tests.Billing.BillingFixtures;

namespace HomeUtilities.Core.Tests.Taxes;

public class TaxInstallmentPlannerTests
{
    private static DateOnly D(int year, int month, int day) => new(year, month, day);

    [Fact]
    public void Default_due_dates_per_count()
    {
        Assert.Equal([D(2027, 3, 31)], TaxInstallmentPlanner.DefaultDueDates(2027, 1));
        Assert.Equal([D(2027, 3, 31), D(2027, 8, 15)], TaxInstallmentPlanner.DefaultDueDates(2027, 2));
        Assert.Equal([D(2028, 3, 31), D(2028, 5, 15), D(2028, 8, 15), D(2028, 11, 15)], TaxInstallmentPlanner.DefaultDueDates(2028, 4));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(12)]
    public void Default_due_dates_reject_other_counts(int count) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => TaxInstallmentPlanner.DefaultDueDates(2027, count));

    [Fact]
    public void Even_split_101_by_4_is_25_25_each() =>
        Assert.Equal([25.25m, 25.25m, 25.25m, 25.25m], TaxInstallmentPlanner.Split(101.00m, 4));

    [Fact]
    public void Remainder_cents_go_to_the_first_installment() =>
        // 100,03 / 4 -> 25,00 x 4 = 100,00; remainder 0,03 on the first -> 25,03 + 25,00 x 3
        Assert.Equal([25.03m, 25.00m, 25.00m, 25.00m], TaxInstallmentPlanner.Split(100.03m, 4));

    [Fact]
    public void Split_into_two_and_one()
    {
        Assert.Equal([50.01m, 50.00m], TaxInstallmentPlanner.Split(100.01m, 2));
        Assert.Equal([87.65m], TaxInstallmentPlanner.Split(87.65m, 1));
    }

    [Fact]
    public void Split_always_adds_up_to_the_total()
    {
        foreach (var total in new[] { 0.01m, 0.03m, 99.99m, 123.45m, 1000.07m })
        {
            Assert.Equal(total, TaxInstallmentPlanner.Split(total, 4).Sum());
        }
    }

    [Fact]
    public void Valid_input_has_no_errors() =>
        Assert.True(TaxInstallmentPlanner.Validate(2027, 101m, TaxInstallmentPlanner.DefaultDueDates(2027, 4)).IsValid);

    [Fact]
    public void Amount_must_be_above_zero() =>
        Assert.Equal(ErrorCodes.TaxYearlyAmountNotPositive,
            Assert.Single(TaxInstallmentPlanner.Validate(2027, 0m, [D(2027, 3, 31)]).Errors).Code);

    [Fact]
    public void Count_must_be_1_2_or_4() =>
        Assert.Contains(TaxInstallmentPlanner.Validate(2027, 10m, [D(2027, 3, 31), D(2027, 5, 15), D(2027, 8, 15)]).Errors,
            e => e.Code == ErrorCodes.TaxInstallmentCountInvalid);

    [Fact]
    public void Due_dates_must_lie_inside_the_year()
    {
        var result = TaxInstallmentPlanner.Validate(2027, 10m, [D(2027, 3, 31), D(2028, 1, 15)]);
        var error = Assert.Single(result.Errors);
        Assert.Equal((ErrorCodes.TaxDueDateOutsideYear, "DueDates[1]"), (error.Code, error.Field));
    }

    [Fact]
    public void Due_dates_must_ascend_strictly()
    {
        var result = TaxInstallmentPlanner.Validate(2027, 10m, [D(2027, 5, 15), D(2027, 5, 15)]);
        Assert.Equal(ErrorCodes.TaxDueDatesNotAscending, Assert.Single(result.Errors).Code);
        Assert.False(TaxInstallmentPlanner.Validate(2027, 10m, [D(2027, 8, 15), D(2027, 5, 15)]).IsValid);
    }

    [Fact]
    public void Leap_year_dates_are_valid() =>
        Assert.True(TaxInstallmentPlanner.Validate(2028, 40m, [D(2028, 2, 29), D(2028, 8, 15)]).IsValid);

    [Fact]
    public void Plan_numbers_and_dates_the_installments()
    {
        var plan = TaxInstallmentPlanner.Plan(100.03m, TaxInstallmentPlanner.DefaultDueDates(2027, 4));
        Assert.Equal(
            [(1, 4, D(2027, 3, 31), 25.03m), (2, 4, D(2027, 5, 15), 25.00m), (3, 4, D(2027, 8, 15), 25.00m), (4, 4, D(2027, 11, 15), 25.00m)],
            plan.Select(p => (p.Number, p.Count, p.DueOn, p.Amount)));
    }

    [Fact]
    public void Plan_with_a_paid_installment_splits_only_the_rest()
    {
        // yearly 120,00, first installment of 25,25 stays paid; 94,75 / 3 = 31,58 + 31,58 + 31,59 -> remainder on the first new one
        var plan = TaxInstallmentPlanner.Plan(120m, TaxInstallmentPlanner.DefaultDueDates(2027, 4), keptCount: 1, keptTotal: 25.25m);
        Assert.Equal([2, 3, 4], plan.Select(p => p.Number));
        Assert.Equal([31.59m, 31.58m, 31.58m], plan.Select(p => p.Amount));
        Assert.Equal(94.75m, plan.Sum(p => p.Amount));
        Assert.Equal([D(2027, 5, 15), D(2027, 8, 15), D(2027, 11, 15)], plan.Select(p => p.DueOn));
    }

    [Fact]
    public void Plan_is_empty_when_everything_is_kept_or_paid()
    {
        var dates = TaxInstallmentPlanner.DefaultDueDates(2027, 2);
        Assert.Empty(TaxInstallmentPlanner.Plan(100m, dates, keptCount: 2, keptTotal: 100m));
        Assert.Empty(TaxInstallmentPlanner.Plan(100m, dates, keptCount: 1, keptTotal: 100m));
    }

    [Fact]
    public void Bills_follow_the_installments()
    {
        var contract = Contract(BillingMode.Assessed);
        var plan = TaxInstallmentPlanner.Plan(101m, TaxInstallmentPlanner.DefaultDueDates(2027, 4));

        var bills = TaxInstallmentPlanner.CreateBills(contract, 2027, plan, p => $"Property tax 2027, installment {p.Number} of {p.Count}", Clock);

        Assert.Equal(4, bills.Count);
        var second = bills[1];
        Assert.Equal("Property tax 2027, installment 2 of 4", second.Title);
        Assert.Equal(BillKind.TaxInstallment, second.Kind);
        Assert.Equal(new YearMonth(2027, 5), second.Period);
        Assert.Equal(D(2027, 5, 15), second.DueOn);
        Assert.Equal((D(2027, 1, 1), D(2027, 12, 31)), (second.ServiceFrom, second.ServiceTo));
        Assert.Equal(25.25m, second.Gross);
        Assert.Equal((contract.Id, contract.PropertyId, contract.CategoryId, contract.SupplierId), (second.ContractId, second.PropertyId, second.CategoryId, second.SupplierId));
        Assert.Equal(101m, bills.Sum(b => b.Gross));
    }

    [Fact]
    public void ForYear_picks_the_installments_of_the_contract_and_year_by_due_date()
    {
        var contract = Contract(BillingMode.Assessed);
        var other = Contract(BillingMode.Assessed);
        var dates = TaxInstallmentPlanner.DefaultDueDates(2027, 4);
        var mine = TaxInstallmentPlanner.CreateBills(contract, 2027, TaxInstallmentPlanner.Plan(100m, dates), p => "t", Clock);
        var lastYear = TaxInstallmentPlanner.CreateBills(contract, 2026, TaxInstallmentPlanner.Plan(100m, TaxInstallmentPlanner.DefaultDueDates(2026, 1)), p => "t", Clock);
        var foreign = TaxInstallmentPlanner.CreateBills(other, 2027, TaxInstallmentPlanner.Plan(100m, dates), p => "t", Clock);
        mine[3].IsDeleted = true;
        var regular = BillFor(contract, 5m);

        var found = TaxInstallmentPlanner.ForYear([.. mine.Reverse(), .. lastYear, .. foreign, regular], contract.Id, 2027);

        Assert.Equal([mine[0].Id, mine[1].Id, mine[2].Id], found.Select(b => b.Id));
    }
}
