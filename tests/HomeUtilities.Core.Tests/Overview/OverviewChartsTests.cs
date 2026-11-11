using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Overview;

namespace HomeUtilities.Core.Tests.Overview;

public class OverviewChartsTests
{
    // Today is in November, so the last complete month is October 2026 and the line covers November 2025 – October 2026.
    private static readonly DateOnly Today = new(2026, 11, 10);

    private static Bill Bill(OverviewScenario s, Contract contract, int year, int month, decimal gross, Property? property = null, Guid? category = null)
    {
        var bill = s.AddBill(contract, gross, null, new YearMonth(year, month), property: property);
        if (category is { } id)
        {
            bill.CategoryId = id;
        }

        return bill;
    }

    // ---- A: this year by month -----------------------------------------------------------------------------------

    [Fact]
    public void Two_years_of_bills_give_twelve_plus_twelve_values_and_future_months_stay_empty()
    {
        var s = new OverviewScenario();
        var water = s.AddContract("Water");
        for (var month = 1; month <= 12; month++)
        {
            Bill(s, water, 2025, month, 100m + month);       // 2025: 101 … 112
        }

        for (var month = 1; month <= 10; month++)
        {
            Bill(s, water, 2026, month, 200m + month);       // 2026: 201 … 210
        }

        Bill(s, water, 2026, 10, 5m);                          // a second bill of October: 210 + 5 = 215 total in the month

        var bars = OverviewCharts.MonthlyComparison(s.Data, Today, null)!;

        Assert.Equal(2026, bars.Year);
        Assert.Equal(12, bars.ThisYear.Count);
        Assert.Equal(12, bars.LastYear.Count);
        Assert.Equal(201m, bars.ThisYear[0]);
        Assert.Equal(215m, bars.ThisYear[9]);
        Assert.Null(bars.ThisYear[10]);   // November: this month, no bill yet
        Assert.Null(bars.ThisYear[11]);   // December: future
        Assert.Equal(101m, bars.LastYear[0]);
        Assert.Equal(112m, bars.LastYear[11]);
        Assert.True(bars.HasLastYear);
    }

    [Fact]
    public void Without_bills_of_the_previous_year_there_is_one_series()
    {
        var s = new OverviewScenario();
        var water = s.AddContract("Water");
        Bill(s, water, 2026, 3, 40m);

        var bars = OverviewCharts.MonthlyComparison(s.Data, Today, null)!;

        Assert.False(bars.HasLastYear);
        Assert.All(bars.LastYear, v => Assert.Null(v));
        Assert.Equal(40m, bars.ThisYear[2]);
    }

    [Fact]
    public void No_bill_this_year_means_no_chart()
    {
        var s = new OverviewScenario();
        var water = s.AddContract("Water");
        Bill(s, water, 2025, 3, 40m);

        Assert.Null(OverviewCharts.MonthlyComparison(s.Data, Today, null));
    }

    [Fact]
    public void A_draft_bill_marks_its_month_as_incomplete_and_is_not_counted()
    {
        var s = new OverviewScenario();
        var water = s.AddContract("Water");
        Bill(s, water, 2026, 9, 50m);
        Bill(s, water, 2026, 10, 60m);
        var draft = Bill(s, water, 2026, 10, 99m);
        s.Bills.Remove(draft);
        draft.IsDraft = true;
        s.Drafts.Add(draft);

        var bars = OverviewCharts.MonthlyComparison(s.Data, Today, null)!;

        Assert.Equal(60m, bars.ThisYear[9]);       // the draft's 99 does not count
        Assert.True(bars.ThisYearApproximate[9]);  // but the month is drawn muted
        Assert.False(bars.ThisYearApproximate[8]);
    }

    [Fact]
    public void The_chart_follows_the_property_filter()
    {
        var s = new OverviewScenario();
        var cottage = new Property { Name = "Cottage" };
        s.Properties.Add(cottage);
        var water = s.AddContract("Water");
        var power = s.AddContract("Power", cottage);
        Bill(s, water, 2026, 5, 30m);
        Bill(s, power, 2026, 5, 70m, cottage);

        Assert.Equal(100m, OverviewCharts.MonthlyComparison(s.Data, Today, null)!.ThisYear[4]);
        Assert.Equal(30m, OverviewCharts.MonthlyComparison(s.Data, Today, s.Flat.Id)!.ThisYear[4]);
        Assert.Equal(70m, OverviewCharts.MonthlyComparison(s.Data, Today, cottage.Id)!.ThisYear[4]);
    }

    // ---- B: spending by category ---------------------------------------------------------------------------------

    private static (OverviewScenario Scenario, Contract Contract, Guid[] Categories) Categorized(params decimal[] amounts)
    {
        var s = new OverviewScenario();
        var contract = s.AddContract("Any");
        var ids = new Guid[amounts.Length];
        for (var i = 0; i < amounts.Length; i++)
        {
            var category = new Category { Name = "C" + i };
            s.Categories.Add(category);
            ids[i] = category.Id;
            Bill(s, contract, 2026, 1 + i % 10, amounts[i], category: category.Id);
        }

        return (s, contract, ids);
    }

    [Fact]
    public void Seven_categories_give_seven_slices_sorted_by_amount()
    {
        // 10, 70, 30, 60, 20, 50, 40 -> 70, 60, 50, 40, 30, 20, 10; total 280.
        var (s, _, ids) = Categorized(10m, 70m, 30m, 60m, 20m, 50m, 40m);

        var spending = OverviewCharts.SpendingByCategory(s.Data, Today, null)!;

        Assert.Equal(280m, spending.Total);
        Assert.Equal([ids[1], ids[3], ids[5], ids[6], ids[2], ids[4], ids[0]], spending.Slices.Select(x => x.CategoryId));
        Assert.Equal([70m, 60m, 50m, 40m, 30m, 20m, 10m], spending.Slices.Select(x => x.Amount));
        Assert.Equal(spending.Total, spending.Slices.Sum(x => x.Amount));
        Assert.Equal((2026, 7, Currency.EUR), (spending.Year, spending.ThroughMonth, spending.Currency));   // the last bill is in July (month 7 of 1..7)
    }

    [Fact]
    public void Three_categories_give_three_slices()
    {
        var (s, _, ids) = Categorized(10m, 20m, 30m);

        var spending = OverviewCharts.SpendingByCategory(s.Data, Today, null)!;

        Assert.Equal([ids[2], ids[1], ids[0]], spending.Slices.Select(x => x.CategoryId));
        Assert.Equal(60m, spending.Total);
    }

    [Fact]
    public void Only_the_months_up_to_the_current_one_of_this_year_count()
    {
        var s = new OverviewScenario();
        var water = s.AddContract("Water");
        var category = new Category { Name = "Water" };
        s.Categories.Add(category);
        Bill(s, water, 2026, 1, 10m, category: category.Id);
        Bill(s, water, 2026, 11, 20m, category: category.Id);
        Bill(s, water, 2026, 12, 400m, category: category.Id);   // prepaid December: later than the current month
        Bill(s, water, 2025, 6, 300m, category: category.Id);    // last year

        var spending = OverviewCharts.SpendingByCategory(s.Data, Today, null)!;

        Assert.Equal(30m, spending.Total);
    }

    [Fact]
    public void Spending_stops_at_the_last_month_with_a_bill_and_drafts_do_not_move_it()
    {
        var s = new OverviewScenario();
        var water = s.AddContract("Water");
        var category = new Category { Name = "Water" };
        s.Categories.Add(category);
        Bill(s, water, 2026, 1, 10m, category: category.Id);
        Bill(s, water, 2026, 10, 20m, category: category.Id);
        Bill(s, water, 2026, 11, 500m, category: category.Id).IsDraft = true;   // a draft is not counted

        var spending = OverviewCharts.SpendingByCategory(s.Data, Today, null)!;

        Assert.Equal((10, 30m), (spending.ThroughMonth, spending.Total));
    }

    [Fact]
    public void A_bill_of_a_removed_category_has_its_own_slice_without_category()
    {
        var s = new OverviewScenario();
        var water = s.AddContract("Water");
        var category = new Category { Name = "Water" };
        s.Categories.Add(category);
        Bill(s, water, 2026, 2, 10m, category: category.Id);
        Bill(s, water, 2026, 2, 5m, category: Guid.NewGuid());

        var spending = OverviewCharts.SpendingByCategory(s.Data, Today, null)!;

        Assert.Equal([category.Id, null], spending.Slices.Select(x => x.CategoryId));
        Assert.Equal([10m, 5m], spending.Slices.Select(x => x.Amount));
    }

    [Fact]
    public void A_deleted_category_is_a_slice_without_category_and_the_total_still_adds_up()
    {
        var s = new OverviewScenario();
        var water = s.AddContract("Water");
        var live = new Category { Name = "Live" };
        var gone = new Category { Name = "Gone", IsDeleted = true };
        s.Categories.Add(live);
        s.Categories.Add(gone);
        Bill(s, water, 2026, 2, 10m, category: live.Id);
        Bill(s, water, 2026, 3, 4m, category: gone.Id);
        Bill(s, water, 2026, 4, 1m, category: Guid.NewGuid());

        var spending = OverviewCharts.SpendingByCategory(s.Data, Today, null)!;

        Assert.Equal([live.Id, null], spending.Slices.Select(x => x.CategoryId));
        Assert.Equal([10m, 5m], spending.Slices.Select(x => x.Amount));
        Assert.Equal(15m, spending.Total);
    }

    [Fact]
    public void The_category_slices_follow_the_property_filter()
    {
        var s = new OverviewScenario();
        var cottage = new Property { Name = "Cottage" };
        s.Properties.Add(cottage);
        var water = s.AddContract("Water");
        var power = s.AddContract("Power", cottage);
        var a = new Category { Name = "A" };
        var b = new Category { Name = "B" };
        s.Categories.Add(a);
        s.Categories.Add(b);
        Bill(s, water, 2026, 5, 30m, category: a.Id);
        Bill(s, power, 2026, 5, 70m, cottage, b.Id);

        Assert.Equal([b.Id, a.Id], OverviewCharts.SpendingByCategory(s.Data, Today, null)!.Slices.Select(x => x.CategoryId));
        Assert.Equal([a.Id], OverviewCharts.SpendingByCategory(s.Data, Today, s.Flat.Id)!.Slices.Select(x => x.CategoryId));
        Assert.Equal([b.Id], OverviewCharts.SpendingByCategory(s.Data, Today, cottage.Id)!.Slices.Select(x => x.CategoryId));
    }

    [Fact]
    public void No_bills_this_year_means_no_category_card_content()
    {
        var s = new OverviewScenario();
        var water = s.AddContract("Water");
        Bill(s, water, 2025, 2, 10m);

        Assert.Null(OverviewCharts.SpendingByCategory(s.Data, Today, null));
    }

    // ---- C: consumption ------------------------------------------------------------------------------------------

    /// <summary>
    /// A cold-water meter with a reading on the 28th of every month from October 2025 to October 2026, so that the month n
    /// (November 2025 = 0 … October 2026 = 11) consumes <paramref name="monthly"/>[n] m3. A null entry leaves the reading out.
    /// </summary>
    private static (OverviewScenario Scenario, Meter Meter) WaterMeter(decimal?[] monthly)
    {
        var s = new OverviewScenario();
        var contract = s.AddContract("Water");
        var meter = new Meter { PropertyId = s.Flat.Id, InstalledOn = new DateOnly(2020, 1, 1), Resource = MeterResource.ColdWater, Unit = MeterUnit.M3, Location = "Bath" };
        var register = new MeterRegister { MeterId = meter.Id, Kind = RegisterKind.Total };
        var total = 1000m;
        register.Readings.Add(new MeterReading { RegisterId = register.Id, Date = new DateOnly(2025, 10, 28), Value = total });
        for (var n = 0; n < monthly.Length; n++)
        {
            total += monthly[n] ?? 0m;
            if (monthly[n] is null)
            {
                continue;
            }

            var month = new YearMonth(2025, 11).AddMonths(n);
            register.Readings.Add(new MeterReading { RegisterId = register.Id, Date = new DateOnly(month.Year, month.Month, 28), Value = total });
        }

        meter.Registers.Add(register);
        s.Meters.Add(meter);
        s.Links.Add(new ContractMeter { ContractId = contract.Id, MeterId = meter.Id, ValidFrom = new DateOnly(2020, 1, 1) });
        return (s, meter);
    }

    [Fact]
    public void Last_complete_month_against_the_average_of_the_eleven_before()
    {
        // Eleven months of 10 m3, then 14 m3: average 10, +40 % -> a noticeable rise.
        var (s, _) = WaterMeter([10m, 10m, 10m, 10m, 10m, 10m, 10m, 10m, 10m, 10m, 10m, 14m]);

        var series = OverviewCharts.Consumption(s.Data, Today);
        var trend = Assert.Single(OverviewCharts.Trends(series, null));

        Assert.Equal((MeterResource.ColdWater, MeterUnit.M3), (trend.Resource, trend.Unit));
        Assert.Equal(14m, trend.Last);
        Assert.Equal(40m, trend.ChangePercent);
        Assert.True(trend.IsRise);
        Assert.True(trend.ShowLine);
        Assert.Equal(12, trend.Values.Count);
        Assert.Equal(10m, trend.Values[0]);
    }

    [Fact]
    public void Exactly_the_threshold_is_not_a_rise_and_a_drop_is_negative()
    {
        // 13 against an average of 10 is +30 %: not above the threshold.
        var (s, _) = WaterMeter([10m, 10m, 10m, 10m, 10m, 10m, 10m, 10m, 10m, 10m, 10m, 13m]);
        var trend = Assert.Single(OverviewCharts.Trends(OverviewCharts.Consumption(s.Data, Today), null));
        Assert.Equal(30m, trend.ChangePercent);
        Assert.False(trend.IsRise);

        var (dropping, _) = WaterMeter([10m, 10m, 10m, 10m, 10m, 10m, 10m, 10m, 10m, 10m, 10m, 5m]);
        var drop = Assert.Single(OverviewCharts.Trends(OverviewCharts.Consumption(dropping.Data, Today), null));
        Assert.Equal(-50m, drop.ChangePercent);
        Assert.False(drop.IsRise);
    }

    [Fact]
    public void The_threshold_constant_is_thirty_percent()
    {
        Assert.Equal(30m, OverviewCharts.RisePercent);
        Assert.Equal(3, OverviewCharts.MinMonthsForLine);
    }

    [Fact]
    public void A_month_without_a_reading_is_a_gap_and_stays_out_of_the_average()
    {
        // March 2026 (index 4) has no reading: it is a gap, not a zero; April then covers two months and is approximate.
        var (s, _) = WaterMeter([10m, 10m, 10m, 10m, null, 10m, 10m, 10m, 10m, 10m, 10m, 20m]);

        var trend = Assert.Single(OverviewCharts.Trends(OverviewCharts.Consumption(s.Data, Today), null));

        Assert.Null(trend.Values[4]);
        Assert.True(trend.Approximate[5]);
        Assert.Equal(20m, trend.Last);
        Assert.True(trend.ChangePercent > 0);
    }

    [Fact]
    public void Trend_math_uses_only_the_months_with_a_figure()
    {
        // Values: 2 and 4 (average 3) before, last 6 -> +100 %; two figures + the last = 3 months: the line is drawn.
        decimal?[] values = [null, null, null, null, null, null, null, null, null, 2m, 4m, 6m];
        var trend = OverviewCharts.Trend(MeterResource.Electricity, MeterUnit.KWh, values, new bool[12]);

        Assert.Equal(100m, trend.ChangePercent);
        Assert.True(trend.IsRise);
        Assert.True(trend.ShowLine);

        // Fewer than three months: only the number.
        decimal?[] few = [null, null, null, null, null, null, null, null, null, null, 4m, 6m];
        var short_ = OverviewCharts.Trend(MeterResource.Electricity, MeterUnit.KWh, few, new bool[12]);
        Assert.False(short_.ShowLine);
        Assert.Equal(50m, short_.ChangePercent);

        // The last month unknown: the latest month with a figure (11, index 10) against the average of the ten before it
        // (1..10 = 5.5): (11 - 5.5) / 5.5 = +100 %.
        decimal?[] missing = [1m, 2m, 3m, 4m, 5m, 6m, 7m, 8m, 9m, 10m, 11m, null];
        var gap = OverviewCharts.Trend(MeterResource.Electricity, MeterUnit.KWh, missing, new bool[12]);
        Assert.Equal(11m, gap.Last);
        Assert.Equal(10, gap.LastIndex);
        Assert.Equal(100m, gap.ChangePercent);

        // No figure at all: no number, no change, no rise.
        var none = OverviewCharts.Trend(MeterResource.Electricity, MeterUnit.KWh, new decimal?[12], new bool[12]);
        Assert.Null(none.Last);
        Assert.Equal(-1, none.LastIndex);
        Assert.Null(none.ChangePercent);
        Assert.False(none.IsRise);
    }

    [Fact]
    public void Consumption_follows_the_property_filter_and_adds_properties_up()
    {
        var (s, _) = WaterMeter([10m, 10m, 10m, 10m, 10m, 10m, 10m, 10m, 10m, 10m, 10m, 10m]);
        var cottage = new Property { Name = "Cottage" };
        s.Properties.Add(cottage);
        var contract = s.AddContract("Cottage water", cottage);
        var meter = new Meter { PropertyId = cottage.Id, InstalledOn = new DateOnly(2020, 1, 1), Resource = MeterResource.ColdWater, Unit = MeterUnit.M3, Location = "Well" };
        var register = new MeterRegister { MeterId = meter.Id, Kind = RegisterKind.Total };
        register.Readings.Add(new MeterReading { RegisterId = register.Id, Date = new DateOnly(2025, 10, 28), Value = 0m });
        for (var n = 0; n < 12; n++)
        {
            var month = new YearMonth(2025, 11).AddMonths(n);
            register.Readings.Add(new MeterReading { RegisterId = register.Id, Date = new DateOnly(month.Year, month.Month, 28), Value = 5m * (n + 1) });
        }

        meter.Registers.Add(register);
        s.Meters.Add(meter);
        s.Links.Add(new ContractMeter { ContractId = contract.Id, MeterId = meter.Id, ValidFrom = new DateOnly(2020, 1, 1) });

        var series = OverviewCharts.Consumption(s.Data, Today);

        Assert.Equal(15m, Assert.Single(OverviewCharts.Trends(series, null)).Last);        // 10 + 5
        Assert.Equal(10m, Assert.Single(OverviewCharts.Trends(series, s.Flat.Id)).Last);
        Assert.Equal(5m, Assert.Single(OverviewCharts.Trends(series, cottage.Id)).Last);
    }

    [Fact]
    public void No_meters_means_no_rows()
    {
        var s = new OverviewScenario();
        s.AddContract("Water");

        Assert.Empty(OverviewCharts.Consumption(s.Data, Today));
        Assert.Empty(OverviewCharts.Trends([], null));
    }
}
