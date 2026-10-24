using HomeUtilities.Core.Charges;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Tests.Charges;

public class ChargePeriodDefaultsTests
{
    private static Contract Contract(int? from, int? to, DateOnly? validTo = null) => new()
    {
        Title = "Water",
        ValidFrom = new DateOnly(2020, 1, 1),
        ValidTo = validTo,
        ReadingDayFrom = from,
        ReadingDayTo = to,
    };

    [Theory]
    [InlineData(2026, 10, 24, 25, 28, 2026, 9)]
    [InlineData(2026, 10, 25, 25, 28, 2026, 10)]
    [InlineData(2026, 10, 28, 25, 28, 2026, 10)]
    [InlineData(2026, 10, 27, 28, 3, 2026, 9)]
    [InlineData(2026, 10, 28, 28, 3, 2026, 10)]
    [InlineData(2026, 11, 3, 28, 3, 2026, 10)]
    public void Window_DecidesBetweenCurrentAndPreviousMonth(int year, int month, int day, int from, int to, int expectedYear, int expectedMonth)
    {
        var result = ChargePeriodDefaults.For([Contract(from, to)], new DateOnly(year, month, day));

        Assert.Equal(new YearMonth(expectedYear, expectedMonth), result);
    }

    [Fact]
    public void NoWindows_GivePreviousMonth()
    {
        Assert.Equal(new YearMonth(2026, 9), ChargePeriodDefaults.For([Contract(null, null)], new DateOnly(2026, 10, 30)));
        Assert.Equal(new YearMonth(2026, 9), ChargePeriodDefaults.For([], new DateOnly(2026, 10, 30)));
    }

    [Fact]
    public void January_GivesDecemberOfThePreviousYear()
    {
        Assert.Equal(new YearMonth(2025, 12), ChargePeriodDefaults.For([Contract(25, 28)], new DateOnly(2026, 1, 10)));
        Assert.Equal(new YearMonth(2026, 1), ChargePeriodDefaults.For([Contract(25, 28)], new DateOnly(2026, 1, 26)));
    }

    [Fact]
    public void AnyContractWithAStartedWindowIsEnough_AndEndedOrDeletedContractsAreIgnored()
    {
        var late = Contract(28, 30);
        var early = Contract(1, 5);
        var ended = Contract(1, 5, new DateOnly(2026, 9, 30));
        var deleted = Contract(1, 5);
        deleted.IsDeleted = true;
        var today = new DateOnly(2026, 10, 20);

        Assert.Equal(new YearMonth(2026, 10), ChargePeriodDefaults.For([late, early], today));
        Assert.Equal(new YearMonth(2026, 9), ChargePeriodDefaults.For([late, ended, deleted], today));
    }

    [Fact]
    public void Window_AtTheMonthEnd_IsClamped()
    {
        // window 31–31 in February starts on the 28th
        Assert.Equal(new YearMonth(2026, 1), ChargePeriodDefaults.For([Contract(31, 31)], new DateOnly(2026, 2, 27)));
        Assert.Equal(new YearMonth(2026, 2), ChargePeriodDefaults.For([Contract(31, 31)], new DateOnly(2026, 2, 28)));
    }
}
