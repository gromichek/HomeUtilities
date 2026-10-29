using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Import;

namespace HomeUtilities.Import.Tests;

public class ImportPeriodResolverTests
{
    private static Contract Window(int from, int to) => new() { ReadingDayFrom = from, ReadingDayTo = to };

    [Theory]
    // Window 25–28: a date inside the window is that month; the 1st of the next month is a late reading of the previous one.
    [InlineData("2004-05-01", "2004-04")]
    [InlineData("2004-10-26", "2004-10")]
    [InlineData("2004-10-25", "2004-10")]
    [InlineData("2004-10-28", "2004-10")]
    [InlineData("2004-10-29", "2004-10")]
    [InlineData("2005-01-02", "2004-12")]
    [InlineData("2004-10-10", "2004-09")]
    public void Reading_window_25_to_28(string date, string expected) =>
        Assert.Equal(YearMonth.Parse(expected), ImportPeriodResolver.Resolve(DateOnly.Parse(date), ImportPeriodRule.ReadingWindow, 10, Window(25, 28)));

    [Theory]
    // Window 28–3 runs into the next month: 28 April … 3 May is April.
    [InlineData("2004-04-28", "2004-04")]
    [InlineData("2004-05-01", "2004-04")]
    [InlineData("2004-05-03", "2004-04")]
    [InlineData("2004-05-04", "2004-04")]
    [InlineData("2004-05-27", "2004-04")]
    [InlineData("2004-05-28", "2004-05")]
    [InlineData("2005-01-02", "2004-12")]
    public void Reading_window_28_to_3(string date, string expected) =>
        Assert.Equal(YearMonth.Parse(expected), ImportPeriodResolver.Resolve(DateOnly.Parse(date), ImportPeriodRule.ReadingWindow, 10, Window(28, 3)));

    [Fact]
    public void Reading_window_without_a_window_is_the_month_of_the_date() =>
        Assert.Equal(new YearMonth(2004, 5), ImportPeriodResolver.Resolve(new DateOnly(2004, 5, 1), ImportPeriodRule.ReadingWindow, 10, new Contract()));

    [Theory]
    [InlineData("2004-09-01", "2004-08")]
    [InlineData("2005-01-01", "2004-12")]
    public void Previous_month(string date, string expected) =>
        Assert.Equal(YearMonth.Parse(expected), ImportPeriodResolver.Resolve(DateOnly.Parse(date), ImportPeriodRule.PreviousMonth, 10, null));

    [Theory]
    [InlineData("2004-09-10", "2004-08")]
    [InlineData("2004-09-11", "2004-09")]
    [InlineData("2004-09-26", "2004-09")]
    [InlineData("2004-09-01", "2004-08")]
    public void Previous_month_if_the_day_is_at_most_ten(string date, string expected) =>
        Assert.Equal(YearMonth.Parse(expected), ImportPeriodResolver.Resolve(DateOnly.Parse(date), ImportPeriodRule.PreviousMonthIfDayUpTo, 10, null));

    [Fact]
    public void The_day_limit_is_configurable() =>
        Assert.Equal(new YearMonth(2004, 8), ImportPeriodResolver.Resolve(new DateOnly(2004, 9, 15), ImportPeriodRule.PreviousMonthIfDayUpTo, 15, null));

    [Fact]
    public void Month_of_the_date_and_next_month()
    {
        Assert.Equal(new YearMonth(2004, 9), ImportPeriodResolver.Resolve(new DateOnly(2004, 9, 2), ImportPeriodRule.MonthOfDate, 10, null));
        Assert.Equal(new YearMonth(2005, 1), ImportPeriodResolver.Resolve(new DateOnly(2004, 12, 2), ImportPeriodRule.NextMonth, 10, null));
    }

    [Fact]
    public void A_rule_change_applies_from_its_date()
    {
        var profile = new ImportProfile
        {
            PeriodRule = ImportPeriodRule.PreviousMonth,
            PeriodRuleChanges = [new PeriodRuleChange { FromDate = new DateOnly(2023, 2, 1), Rule = ImportPeriodRule.NextMonth }],
        };

        Assert.Equal(ImportPeriodRule.PreviousMonth, profile.RuleFor(new DateOnly(2023, 1, 31)).Rule);
        Assert.Equal(ImportPeriodRule.NextMonth, profile.RuleFor(new DateOnly(2023, 2, 1)).Rule);
        Assert.Equal(ImportPeriodRule.NextMonth, profile.RuleFor(new DateOnly(2030, 1, 1)).Rule);
    }
}
