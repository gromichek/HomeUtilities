using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Reports;
using static HomeUtilities.Core.Tests.Reports.ReportScenario;

namespace HomeUtilities.Core.Tests.Reports;

public class CostsReportTests
{
    private static readonly YearMonth Jan = new(2026, 1);
    private static readonly YearMonth Feb = new(2026, 2);
    private static readonly YearMonth Mar = new(2026, 3);

    private static decimal?[] Values(ReportRow row) => [.. row.Cells.Select(c => c.Value)];

    /// <summary>
    /// Flat: water 20.00 (Jan) and 22.00 (Feb); maintenance 15.50 (Jan, Feb); one-off boiler repair 100.00 (Feb, maintenance category).
    /// Cottage: electricity 40.00 (Jan).
    /// </summary>
    private static (ReportScenario S, Property Cottage) TwoProperties()
    {
        var s = new ReportScenario();
        var cottage = s.AddProperty("Cottage");
        var power = s.AddCategory("Power", 3, ServiceKind.Electricity, nameless: true);
        var water = s.AddContract("Water supply");
        var maintenance = s.AddContract("House maintenance", s.Maintenance, service: ServiceKind.Maintenance);
        var electricity = s.AddContract("Power", power, cottage, service: ServiceKind.Electricity);
        s.AddBill(water, 20m, Jan);
        s.AddBill(water, 22m, Feb);
        s.AddBill(maintenance, 15.50m, Jan);
        s.AddBill(maintenance, 15.50m, Feb);
        s.AddBill(null, 100m, Feb, title: "Boiler repair");
        s.AddBill(electricity, 40m, Jan);
        return (s, cottage);
    }

    [Fact]
    public void A_bill_over_a_service_period_is_spread_by_days_and_adds_up_exactly()
    {
        // 30.00 over 1 Jan - 31 Mar 2026 = 90 days: Jan 31 days 10.33, Feb 28 days 9.33, March gets the rest 10.34.
        var s = new ReportScenario();
        var maintenance = s.AddContract("Maintenance", s.Maintenance, service: ServiceKind.Maintenance);
        var bill = s.AddBill(maintenance, 30m, Jan);
        bill.ServiceFrom = new DateOnly(2026, 1, 1);
        bill.ServiceTo = new DateOnly(2026, 3, 31);

        var table = ReportBuilder.Costs(s.Data, Filter(Jan, Mar));

        Assert.Equal([10.33m, 9.33m, 10.34m], table.Rows.Select(r => r.Cells[0].Value));
        Assert.All(table.Rows, r => Assert.True(r.Cells[0].IsApproximate));
        Assert.Equal(30m, table.Totals!.Cells[0].Value);
        Assert.Equal(30m, table.Totals.Total!.Value);
    }

    [Fact]
    public void A_range_inside_a_spread_bill_shows_only_its_months()
    {
        var s = new ReportScenario();
        var maintenance = s.AddContract("Maintenance", s.Maintenance, service: ServiceKind.Maintenance);
        var bill = s.AddBill(maintenance, 30m, Jan);
        bill.ServiceFrom = new DateOnly(2026, 1, 1);
        bill.ServiceTo = new DateOnly(2026, 3, 31);

        var table = ReportBuilder.Costs(s.Data, Filter(Feb, Feb));

        Assert.Equal(9.33m, Assert.Single(table.Rows).Cells[0].Value);
    }

    [Fact]
    public void By_category_with_row_totals_column_totals_subtotals_and_grand_total()
    {
        var (s, _) = TwoProperties();

        var table = ReportBuilder.Costs(s.Data, Filter(Jan, Feb));

        // Flat: Water, Maintenance, subtotal; Cottage: Power (one column: no subtotal).
        Assert.Equal(
            [ColumnSubject.Category, ColumnSubject.Category, ColumnSubject.Property, ColumnSubject.Category],
            table.Columns.Select(c => c.Subject));
        Assert.Equal([false, false, true, false], table.Columns.Select(c => c.IsSubtotal));
        Assert.Equal(["Flat", "Flat", "Flat", "Cottage"], table.Columns.Select(c => c.PropertyName));
        Assert.Equal([ServiceKind.ColdWater, ServiceKind.Maintenance, null, ServiceKind.Electricity], table.Columns.Select(c => c.Service));

        // Jan: 20.00 + 15.50 = 35.50 for the flat, 40.00 for the cottage; Feb: 22.00, 15.50 + 100.00 = 115.50, nothing at the cottage.
        Assert.Equal([20m, 15.50m, 35.50m, 40m], Values(table.Rows[0]));
        Assert.Equal([22m, 115.50m, 137.50m, null], Values(table.Rows[1]));
        Assert.True(table.HasRowTotals);
        Assert.Equal([75.50m, 137.50m], table.Rows.Select(r => r.Total!.Value));
        Assert.Equal([42m, 131m, 173m, 40m], Values(table.Totals!));
        Assert.Equal(213m, table.Totals!.Total!.Value);
    }

    [Fact]
    public void By_service_the_one_off_expense_joins_the_service_of_its_category()
    {
        var (s, _) = TwoProperties();

        var table = ReportBuilder.Costs(s.Data, Filter(Jan, Feb, b => b.Grouping = CostGrouping.Service));

        var flat = table.Columns.Select((c, i) => (c, i)).Where(x => x.c is { PropertyName: "Flat", IsSubtotal: false }).ToList();
        Assert.Equal([ServiceKind.ColdWater, ServiceKind.Maintenance], flat.Select(x => x.c.Service));
        Assert.Equal(115.50m, table.Rows[1].Cells[flat[1].i].Value);
    }

    [Fact]
    public void By_contract_the_one_off_expense_has_its_own_column_after_the_contracts()
    {
        var (s, _) = TwoProperties();

        var table = ReportBuilder.Costs(s.Data, Filter(Jan, Feb, b =>
        {
            b.Grouping = CostGrouping.Contract;
            b.PropertyIds = [s.Flat.Id];
        }));

        Assert.Equal([ColumnSubject.Contract, ColumnSubject.Contract, ColumnSubject.OneOff], table.Columns.Select(c => c.Subject));
        Assert.Equal(["House maintenance", "Water supply", null], table.Columns.Select(c => c.Name));
        Assert.Equal([15.50m, 20m, null], Values(table.Rows[0]));
        Assert.Equal([15.50m, 22m, 100m], Values(table.Rows[1]));
        Assert.Equal(173m, table.Totals!.Total!.Value);
    }

    [Fact]
    public void By_property_has_one_column_per_property()
    {
        var (s, cottage) = TwoProperties();

        var table = ReportBuilder.Costs(s.Data, Filter(Jan, Feb, b => b.Grouping = CostGrouping.Property));

        Assert.Equal([s.Flat.Id, cottage.Id], table.Columns.Select(c => c.PropertyId));
        Assert.Equal([35.50m, 40m], Values(table.Rows[0]));
        Assert.Equal([137.50m, null], Values(table.Rows[1]));
    }

    [Fact]
    public void Property_and_category_filters_limit_the_bills()
    {
        var (s, cottage) = TwoProperties();

        var onlyCottage = ReportBuilder.Costs(s.Data, Filter(Jan, Feb, b => b.PropertyIds = [cottage.Id]));
        var onlyWater = ReportBuilder.Costs(s.Data, Filter(Jan, Feb, b => b.CategoryIds = [s.Water.Id]));

        Assert.Equal(40m, onlyCottage.Totals!.Total!.Value);
        Assert.Equal(42m, onlyWater.Totals!.Total!.Value);
    }

    [Fact]
    public void Net_and_vat_measures_use_the_bill_split_and_derive_missing_parts()
    {
        var s = new ReportScenario();
        var water = s.AddContract("Water supply");
        var stated = s.AddBill(water, 121m, Jan);
        stated.Net = 100m;
        stated.Vat = 21m;
        var onlyVat = s.AddBill(water, 60.50m, Feb);
        onlyVat.Vat = 10.50m;
        s.AddBill(water, 30m, Mar);

        var net = ReportBuilder.Costs(s.Data, Filter(Jan, Mar, b => b.Measure = ReportMeasure.Net));
        var vat = ReportBuilder.Costs(s.Data, Filter(Jan, Mar, b => b.Measure = ReportMeasure.Vat));

        // Only the gross is known in March: the net is the gross, the VAT 0.
        Assert.Equal([100m, 50m, 30m], net.Rows.Select(r => r.Cells[0].Value));
        Assert.Equal([21m, 10.50m, 0m], vat.Rows.Select(r => r.Cells[0].Value));
        Assert.Equal(ReportMeasure.Net, net.Columns[0].Measure);
    }

    [Fact]
    public void Amounts_in_lats_are_shown_in_euro()
    {
        // 7.02804 LVL / 0.702804 = 10.00 EUR
        var s = new ReportScenario();
        var water = s.AddContract("Water supply");
        var bill = s.AddBill(water, 7.02804m, new YearMonth(2013, 6));
        bill.Currency = Currency.LVL;

        var table = ReportBuilder.Costs(s.Data, Filter(new YearMonth(2013, 6), new YearMonth(2013, 6)));

        Assert.Equal(10m, table.Rows[0].Cells[0].Value);
    }

    [Fact]
    public void Years_as_rows_add_up_the_months_of_each_year()
    {
        var s = new ReportScenario();
        var water = s.AddContract("Water supply");
        s.AddBill(water, 10m, new YearMonth(2025, 11));
        s.AddBill(water, 12m, new YearMonth(2025, 12));
        s.AddBill(water, 20m, Jan);
        s.AddBill(water, 25m, Feb);

        var table = ReportBuilder.Costs(s.Data, Filter(new YearMonth(2025, 11), Feb, b => b.Rows = ReportRows.Years));

        Assert.Equal([ReportRowKind.Year, ReportRowKind.Year], table.Rows.Select(r => r.Kind));
        Assert.Equal([2025, 2026], table.Rows.Select(r => r.Year));
        Assert.Equal([22m, 45m], table.Rows.Select(r => r.Cells[0].Value));
        Assert.Equal(67m, table.Totals!.Cells[0].Value);
    }

    [Fact]
    public void Year_over_year_puts_the_same_months_of_each_year_side_by_side()
    {
        var s = new ReportScenario();
        var water = s.AddContract("Water supply");
        s.AddBill(water, 10m, new YearMonth(2025, 1));
        s.AddBill(water, 11m, new YearMonth(2025, 2));
        s.AddBill(water, 20m, Jan);
        s.AddBill(water, 25m, Feb);

        var table = ReportBuilder.Costs(s.Data, Filter(new YearMonth(2025, 1), Feb, b => b.YearOverYear = true));

        Assert.Equal([2025, 2026], table.Columns.Select(c => c.Year));
        Assert.Equal(12, table.Rows.Count);
        Assert.All(table.Rows, r => Assert.Equal(ReportRowKind.MonthOfYear, r.Kind));
        Assert.Equal([10m, 20m], Values(table.Rows[0]));
        Assert.Equal([11m, 25m], Values(table.Rows[1]));
        // The range ends in February 2026: later months of 2026 stay empty, those of 2025 too (no bills there).
        Assert.Equal([null, null], Values(table.Rows[2]));
        Assert.Equal([21m, 45m], Values(table.Totals!));
        Assert.False(table.HasRowTotals);
    }

    [Fact]
    public void Months_outside_the_range_are_not_counted_in_year_over_year()
    {
        var s = new ReportScenario();
        var water = s.AddContract("Water supply");
        s.AddBill(water, 10m, new YearMonth(2025, 3));
        s.AddBill(water, 20m, Mar);

        var table = ReportBuilder.Costs(s.Data, Filter(new YearMonth(2025, 6), Mar, b => b.YearOverYear = true));

        // 2025: March is before the range; 2026: March is in it.
        Assert.Equal([null, 20m], Values(table.Rows[2]));
    }

    [Fact]
    public void Deleted_bills_and_properties_without_bills_do_not_appear()
    {
        var (s, _) = TwoProperties();
        s.Bills.First(b => b.Title == "Boiler repair").IsDeleted = true;
        s.AddProperty("Garage", 3);

        var table = ReportBuilder.Costs(s.Data, Filter(Jan, Feb));

        Assert.DoesNotContain(table.Columns, c => c.PropertyName == "Garage");
        Assert.Equal(113m, table.Totals!.Total!.Value);
    }

    [Fact]
    public void Without_bills_the_table_is_empty_and_a_backwards_range_too()
    {
        var s = new ReportScenario();
        s.AddContract("Water supply");

        Assert.True(ReportBuilder.Costs(s.Data, Filter(Jan, Feb)).IsEmpty);
        Assert.True(ReportBuilder.Costs(TwoProperties().S.Data, Filter(Feb, Jan)).IsEmpty);
    }

    [Fact]
    public void Cells_drill_down_to_the_bills_of_their_property_month_and_category()
    {
        var (s, _) = TwoProperties();

        var table = ReportBuilder.Costs(s.Data, Filter(Jan, Feb));

        var drill = table.Rows[0].Cells[0].Drill!;
        Assert.Equal((DrillTarget.Bills, s.Flat.Id, Jan, Jan, s.Water.Id), (drill.Target, drill.PropertyId, drill.From, drill.To, drill.CategoryId));
        var total = table.Totals!.Cells[0].Drill!;
        Assert.Equal((Jan, Feb), (total.From, total.To));
    }
}
