using HomeUtilities.Core.Common;
using HomeUtilities.Core.Equalized;

namespace HomeUtilities.Core.Tests.Equalized;

public class EqualizedPlanTests
{
    private static EqualizedPlan Plan(string from, decimal amount, string? to = null, bool deleted = false) => new()
    {
        ContractId = Guid.Empty,
        ValidFrom = DateOnly.Parse(from),
        ValidTo = to is null ? null : DateOnly.Parse(to),
        MonthlyAmount = amount,
        IsDeleted = deleted,
    };

    [Fact]
    public void Valid_plan_has_no_errors() =>
        Assert.True(EqualizedPlanValidator.Validate(Plan("2026-06-01", 40m, "2026-12-31")).IsValid);

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    public void Amount_must_be_above_zero(string amount)
    {
        var result = EqualizedPlanValidator.Validate(Plan("2026-06-01", decimal.Parse(amount)));
        Assert.Equal(ErrorCodes.EqualizedPlanAmountNotPositive, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void ValidTo_may_equal_ValidFrom_but_not_precede_it()
    {
        Assert.True(EqualizedPlanValidator.Validate(Plan("2026-06-01", 40m, "2026-06-01")).IsValid);
        var result = EqualizedPlanValidator.Validate(Plan("2026-06-01", 40m, "2026-05-31"));
        Assert.Equal(ErrorCodes.EqualizedPlanValidToBeforeValidFrom, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void Notes_over_the_limit_are_rejected()
    {
        var plan = Plan("2026-06-01", 40m);
        plan.Notes = new string('x', 2001);
        Assert.Equal(ErrorCodes.NotesTooLong, Assert.Single(EqualizedPlanValidator.Validate(plan).Errors).Code);
    }

    [Fact]
    public void Adjacent_plans_do_not_overlap() =>
        Assert.True(EqualizedPlanValidator.ValidateSet([Plan("2026-06-01", 40m, "2026-11-30"), Plan("2026-12-01", 45m)]).IsValid);

    [Fact]
    public void Open_ended_plan_overlaps_a_later_one()
    {
        var result = EqualizedPlanValidator.ValidateSet([Plan("2026-06-01", 40m), Plan("2026-12-01", 45m)]);
        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorCodes.EqualizedPlanOverlap, error.Code);
    }

    [Fact]
    public void Plans_sharing_a_day_overlap_and_deleted_plans_are_ignored()
    {
        Assert.False(EqualizedPlanValidator.ValidateSet([Plan("2026-06-01", 40m, "2026-12-01"), Plan("2026-12-01", 45m)]).IsValid);
        Assert.True(EqualizedPlanValidator.ValidateSet([Plan("2026-06-01", 40m, "2026-12-01"), Plan("2026-12-01", 45m, deleted: true)]).IsValid);
    }

    [Fact]
    public void AmountFor_follows_the_plan_versions()
    {
        // 40,00 until 30.11.2026, 45,00 from 01.12.2026
        EqualizedPlan[] plans = [Plan("2026-06-01", 40m, "2026-11-30"), Plan("2026-12-01", 45m)];
        Assert.Null(EqualizedPlans.AmountFor(plans, new YearMonth(2026, 5)));
        Assert.Equal(40m, EqualizedPlans.AmountFor(plans, new YearMonth(2026, 6)));
        Assert.Equal(40m, EqualizedPlans.AmountFor(plans, new YearMonth(2026, 11)));
        Assert.Equal(45m, EqualizedPlans.AmountFor(plans, new YearMonth(2026, 12)));
        Assert.Equal(45m, EqualizedPlans.AmountFor(plans, new YearMonth(2030, 1)));
    }

    [Fact]
    public void A_plan_starting_mid_month_applies_to_that_month_when_nothing_else_does()
    {
        EqualizedPlan[] plans = [Plan("2026-06-15", 40m)];
        Assert.Null(EqualizedPlans.AmountFor(plans, new YearMonth(2026, 5)));
        Assert.Equal(40m, EqualizedPlans.AmountFor(plans, new YearMonth(2026, 6)));
    }

    [Fact]
    public void A_mid_month_change_applies_from_the_next_month()
    {
        EqualizedPlan[] plans = [Plan("2026-06-01", 40m, "2026-06-14"), Plan("2026-06-15", 50m)];
        Assert.Equal(40m, EqualizedPlans.AmountFor(plans, new YearMonth(2026, 6)));
        Assert.Equal(50m, EqualizedPlans.AmountFor(plans, new YearMonth(2026, 7)));
    }

    [Fact]
    public void Deleted_plans_are_ignored_by_AmountFor() =>
        Assert.Null(EqualizedPlans.AmountFor([Plan("2026-06-01", 40m, deleted: true)], new YearMonth(2026, 7)));

    [Fact]
    public void A_new_plan_closes_the_open_ended_predecessor_the_day_before()
    {
        var current = Plan("2026-06-01", 40m);
        var added = Plan("2026-12-01", 45m);

        Assert.Equal([current.Id], EqualizedPlans.PredecessorsToClose([current], added).Select(p => p.Id));
        Assert.True(EqualizedPlans.ValidateWith([current], added).IsValid);
    }

    [Fact]
    public void A_predecessor_that_already_ended_earlier_stays_as_it_is()
    {
        var ended = Plan("2026-01-01", 35m, "2026-05-31");
        var added = Plan("2026-06-01", 40m);

        Assert.Empty(EqualizedPlans.PredecessorsToClose([ended], added));
        Assert.True(EqualizedPlans.ValidateWith([ended], added).IsValid);
    }

    [Fact]
    public void A_plan_starting_inside_a_later_plan_overlaps_it()
    {
        var later = Plan("2026-12-01", 45m);
        var added = Plan("2026-06-01", 40m, "2026-12-31");

        var error = Assert.Single(EqualizedPlans.ValidateWith([later], added).Errors);
        Assert.Equal(ErrorCodes.EqualizedPlanOverlap, error.Code);
    }

    [Fact]
    public void Editing_a_plan_ignores_its_own_old_version()
    {
        var existing = Plan("2026-06-01", 40m);
        var edited = new EqualizedPlan { Id = existing.Id, ValidFrom = existing.ValidFrom, MonthlyAmount = 42m };

        Assert.Empty(EqualizedPlans.PredecessorsToClose([existing], edited));
        Assert.True(EqualizedPlans.ValidateWith([existing], edited).IsValid);
    }
}
