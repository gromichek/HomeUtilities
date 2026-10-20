using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using static HomeUtilities.Core.Tests.Billing.BillingFixtures;

namespace HomeUtilities.Core.Tests.Billing;

public class BillSpreadTests
{
    private static Bill Make(decimal gross, DateOnly? from, DateOnly? to)
    {
        var bill = BillFor(Contract(), gross, new YearMonth(2026, 1));
        bill.ServiceFrom = from;
        bill.ServiceTo = to;
        return bill;
    }

    [Fact]
    public void Without_service_period_whole_amount_goes_to_period()
    {
        var shares = BillSpread.ByMonth(Make(55.25m, null, null));
        Assert.Equal([(new YearMonth(2026, 1), 55.25m)], shares);
    }

    [Fact]
    public void Yearly_insurance_is_spread_by_days()
    {
        // January: 31 / 365 * 120 = 10.19
        var shares = BillSpread.ByMonth(Make(120m, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)));
        Assert.Equal(12, shares.Count);
        Assert.Equal((new YearMonth(2026, 1), 10.19m), shares[0]);
        Assert.Equal(new YearMonth(2026, 12), shares[^1].Month);
        Assert.Equal(120m, shares.Sum(s => s.Gross));
    }

    [Fact]
    public void Quarter_remainder_goes_to_last_month()
    {
        // 92 days: Oct 31 -> 7.73, Nov 30 -> 7.48, Dec = 22.95 - 7.73 - 7.48 = 7.74
        var shares = BillSpread.ByMonth(Make(22.95m, new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 31)));
        Assert.Equal(
            [(new YearMonth(2026, 10), 7.73m), (new YearMonth(2026, 11), 7.48m), (new YearMonth(2026, 12), 7.74m)],
            shares);
    }

    [Fact]
    public void Period_starting_mid_month_counts_only_the_service_days()
    {
        // 2026-10-16..2026-11-15: 16 days in October, 15 in November, 31 total; 31.00 -> 16.00 / 15.00
        var shares = BillSpread.ByMonth(Make(31m, new DateOnly(2026, 10, 16), new DateOnly(2026, 11, 15)));
        Assert.Equal([(new YearMonth(2026, 10), 16m), (new YearMonth(2026, 11), 15m)], shares);
    }

    [Fact]
    public void Single_month_service_period_keeps_whole_amount()
    {
        var shares = BillSpread.ByMonth(Make(10m, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30)));
        Assert.Equal([(new YearMonth(2026, 11), 10m)], shares);
    }

    [Fact]
    public void Negative_amount_sums_exactly()
    {
        var shares = BillSpread.ByMonth(Make(-22.95m, new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 31)));
        Assert.Equal(-22.95m, shares.Sum(s => s.Gross));
    }

    [Fact]
    public void Reversed_service_period_falls_back_to_period()
    {
        var shares = BillSpread.ByMonth(Make(10m, new DateOnly(2026, 12, 1), new DateOnly(2026, 10, 1)));
        Assert.Equal([(new YearMonth(2026, 1), 10m)], shares);
    }
}
