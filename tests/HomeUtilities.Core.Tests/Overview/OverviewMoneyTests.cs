using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Overview;

namespace HomeUtilities.Core.Tests.Overview;

public class OverviewMoneyTests
{
    private static readonly DateOnly Today = new(2026, 10, 27);

    private static DateOnly D(int month, int day, int year = 2026) => new(year, month, day);

    /// <summary>
    /// Flat: water (Aug 40 paid; Sep 50 due 20.10. paid 20; Oct 60 due 20.11.) and a 22 prepayment of the contract's own that no bill uses.
    /// Billed 150, paid 40 + 20 + 22 = 82, balance −68; open 30 + 60 = 90 (overdue 30).
    /// </summary>
    private static (OverviewScenario Scenario, Contract Water) Flat()
    {
        var scenario = new OverviewScenario();
        var water = scenario.AddContract("Water");
        var aug = scenario.AddBill(water, 40m, D(9, 20), new YearMonth(2026, 8));
        var sep = scenario.AddBill(water, 50m, D(10, 20), new YearMonth(2026, 9));
        scenario.AddBill(water, 60m, D(11, 20), new YearMonth(2026, 10));
        scenario.AddPayment(40m, D(9, 18), water, null, (aug, 40m));
        scenario.AddPayment(20m, D(10, 18), water, null, (sep, 20m));
        scenario.AddPayment(22m, D(10, 25), water);
        return (scenario, water);
    }

    [Fact]
    public void Money_of_a_property_open_overdue_prepayment_and_balance()
    {
        var (scenario, _) = Flat();

        var money = Assert.Single(OverviewMoney.PerProperty(scenario.Data, Today));

        // 30 (Sep) + 60 (Oct) = 90 open; Sep is past due: 30 overdue; the 22 without allocation is a prepayment.
        // Billed 40 + 50 + 60 = 150 (issue date missing -> first day of the period, all up to today), paid 40 + 20 + 22 = 82.
        Assert.Equal((Currency.EUR, 90m, 30m, 22m, -68m), (money.Currency, money.OpenBills, money.OverdueBills, money.Prepayments, money.Balance));
    }

    [Fact]
    public void Money_matches_the_totals_of_the_bills_and_payments_screens()
    {
        var (scenario, _) = Flat();
        var data = scenario.Data;

        var bills = data.Bills;
        var payments = data.Payments;
        var open = bills.Select(b => (Bill: b, State: BillStatus.Of(b, payments, Today))).ToList();

        var money = Assert.Single(OverviewMoney.PerProperty(data, Today));
        Assert.Equal(open.Where(o => !o.State.IsPaid).Sum(o => o.State.Open), money.OpenBills);
        Assert.Equal(open.Where(o => o.State.IsOverdue).Sum(o => o.State.Open), money.OverdueBills);
        Assert.Equal(payments.Sum(p => Math.Max(0m, PaymentRules.Unallocated(p))), money.Prepayments);
    }

    [Fact]
    public void Properties_are_separate_and_a_refund_bill_lowers_the_open_total()
    {
        var (scenario, _) = Flat();
        var cottage = new Property { Name = "Cottage" };
        scenario.Properties.Add(cottage);
        var power = scenario.AddContract("Power", cottage);
        scenario.AddBill(power, 100m, D(11, 5), new YearMonth(2026, 10), property: cottage);
        scenario.AddBill(power, -12.40m, D(10, 1), new YearMonth(2026, 9), BillKind.Settlement, cottage);

        var rows = OverviewMoney.PerProperty(scenario.Data, Today);

        var cottageMoney = Assert.Single(rows, r => r.PropertyId == cottage.Id);
        Assert.Equal((87.60m, 0m, 0m, -87.60m), (cottageMoney.OpenBills, cottageMoney.OverdueBills, cottageMoney.Prepayments, cottageMoney.Balance));
        Assert.Equal(90m, Assert.Single(rows, r => r.PropertyId == scenario.Flat.Id).OpenBills);
    }

    [Fact]
    public void A_payment_without_contract_counts_for_its_property_and_over_allocation_is_not_a_prepayment()
    {
        var scenario = new OverviewScenario();
        var water = scenario.AddContract("Water");
        var bill = scenario.AddBill(water, 30m, D(10, 20), new YearMonth(2026, 9));
        scenario.AddPayment(35m, D(10, 21), null, scenario.Flat, (bill, 30m));
        scenario.AddPayment(10m, D(10, 22), null, scenario.Flat);

        var money = Assert.Single(OverviewMoney.PerProperty(scenario.Data, Today));

        Assert.Equal(5m + 10m, money.Prepayments);
        Assert.Equal(0m, money.OpenBills);
    }

    [Fact]
    public void Bills_in_two_currencies_give_two_rows()
    {
        var scenario = new OverviewScenario();
        var water = scenario.AddContract("Water");
        scenario.AddBill(water, 10m, D(11, 1), new YearMonth(2026, 10));
        var old = scenario.AddContract("Old", validFrom: new DateOnly(2000, 1, 1));
        old.Currency = Currency.LVL;
        scenario.AddBill(old, 7m, D(11, 1), new YearMonth(2013, 10));

        var rows = OverviewMoney.PerProperty(scenario.Data, Today);

        Assert.Equal([Currency.EUR, Currency.LVL], rows.Select(r => r.Currency));
        Assert.Equal([10m, 7m], rows.Select(r => r.OpenBills));
    }

    [Fact]
    public void Property_without_anything_has_no_rows()
    {
        var scenario = new OverviewScenario();

        Assert.Empty(OverviewMoney.PerProperty(scenario.Data, Today));
    }

    [Fact]
    public void Year_to_date_compares_with_the_same_months_of_last_year()
    {
        var scenario = new OverviewScenario();
        var water = scenario.AddContract("Water");
        // This year up to October: 100.30 + 200.00 + 945.00 = 1245.30; a prepaid November bill is not counted yet.
        scenario.AddBill(water, 100.30m, null, new YearMonth(2026, 1));
        scenario.AddBill(water, 200m, null, new YearMonth(2026, 6));
        scenario.AddBill(water, 945m, null, new YearMonth(2026, 10));
        scenario.AddBill(water, 22.95m, null, new YearMonth(2026, 11));
        // Last year, January to October: 80.10 + 150.00 + 950.00 = 1180.10; November and December of last year are left out.
        scenario.AddBill(water, 80.10m, null, new YearMonth(2025, 1));
        scenario.AddBill(water, 150m, null, new YearMonth(2025, 6));
        scenario.AddBill(water, 950m, null, new YearMonth(2025, 10));
        scenario.AddBill(water, 500m, null, new YearMonth(2025, 12));
        scenario.AddBill(water, 999m, null, new YearMonth(2024, 3));

        var comparison = Assert.Single(OverviewMoney.YearToDate(scenario.Data, Today));

        Assert.Equal((2026, 1245.30m, 1180.10m), (comparison.Year, comparison.ThisYear, comparison.LastYear));
        Assert.Equal(5.5m, comparison.ChangePercent); // (1245.30 − 1180.10) / 1180.10 = 5.525 %
    }

    [Fact]
    public void Year_to_date_without_last_year_has_no_percentage_and_deleted_bills_do_not_count()
    {
        var scenario = new OverviewScenario();
        var water = scenario.AddContract("Water");
        scenario.AddBill(water, 100m, null, new YearMonth(2026, 3));
        scenario.AddBill(water, 50m, null, new YearMonth(2026, 4)).IsDeleted = true;

        var comparison = Assert.Single(OverviewMoney.YearToDate(scenario.Data, Today));

        Assert.Equal((100m, 0m, (decimal?)null), (comparison.ThisYear, comparison.LastYear, comparison.ChangePercent));
    }

    [Fact]
    public void Settlements_list_every_equalized_contract_with_a_result_even_small_ones()
    {
        var scenario = new OverviewScenario();
        var power = scenario.AddContract("Cottage electricity", mode: BillingMode.Equalized);
        var other = scenario.AddContract("Other", mode: BillingMode.Equalized);
        scenario.Settlements[power.Id] = 8.35m;
        scenario.Settlements[other.Id] = -0.40m;

        var rows = OverviewMoney.Settlements(scenario.Data);

        Assert.Equal([("Cottage electricity", 8.35m), ("Other", -0.40m)], rows.Select(r => (r.Title, r.Expected)));
    }
}
