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

        Assert.Equal((100m, 0m, (decimal?)null, false), (comparison.ThisYear, comparison.LastYear, comparison.ChangePercent, comparison.HasLastYear));
    }

    [Fact]
    public void Year_to_date_stops_at_the_last_month_with_a_bill_and_follows_it_when_a_later_bill_comes()
    {
        var november = new DateOnly(2026, 11, 11);
        var scenario = new OverviewScenario();
        var water = scenario.AddContract("Water");
        // This year January – October: 100 + 200 + 300 = 600. Last year all twelve months, 100 each = 1200.
        scenario.AddBill(water, 100m, null, new YearMonth(2026, 1));
        scenario.AddBill(water, 200m, null, new YearMonth(2026, 6));
        scenario.AddBill(water, 300m, null, new YearMonth(2026, 10));
        for (var month = 1; month <= 12; month++)
        {
            scenario.AddBill(water, 100m, null, new YearMonth(2025, month));
        }

        // Draft and deleted bills of November do not count.
        scenario.AddBill(water, 999m, null, new YearMonth(2026, 11)).IsDraft = true;
        scenario.AddBill(water, 888m, null, new YearMonth(2026, 11)).IsDeleted = true;

        var comparison = Assert.Single(OverviewMoney.YearToDate(scenario.Data, november));

        // Last year January – October = 10 × 100 = 1000; (600 − 1000) / 1000 = −40 %.
        Assert.Equal((600m, 1000m, 10, -40m), (comparison.ThisYear, comparison.LastYear, comparison.ThroughMonth, comparison.ChangePercent));

        // A bill for November is entered: both years go through November (700 vs 1100).
        scenario.AddBill(water, 100m, null, new YearMonth(2026, 11));

        comparison = Assert.Single(OverviewMoney.YearToDate(scenario.Data, november));

        Assert.Equal((700m, 1100m, 11), (comparison.ThisYear, comparison.LastYear, comparison.ThroughMonth));
    }

    [Fact]
    public void Year_to_date_end_month_is_per_property()
    {
        var november = new DateOnly(2026, 11, 11);
        var scenario = new OverviewScenario();
        var cottage = new Property { Name = "Cottage" };
        scenario.Properties.Add(cottage);
        var water = scenario.AddContract("Water");
        var power = scenario.AddContract("Power", cottage);
        scenario.AddBill(water, 10m, null, new YearMonth(2026, 10));
        scenario.AddBill(water, 5m, null, new YearMonth(2025, 10));
        scenario.AddBill(water, 7m, null, new YearMonth(2025, 11));
        scenario.AddBill(power, 20m, null, new YearMonth(2026, 11), property: cottage);
        scenario.AddBill(power, 8m, null, new YearMonth(2025, 11), property: cottage);

        var rows = OverviewMoney.YearToDate(scenario.Data, november);

        Assert.Equal([(10, 5m), (11, 8m)], rows.Select(r => (r.ThroughMonth, r.LastYear)));
    }

    [Fact]
    public void Year_to_date_counts_each_contract_up_to_its_own_last_month()
    {
        var november = new DateOnly(2026, 11, 11);
        var scenario = new OverviewScenario();
        var arrears = scenario.AddContract("Water");          // billed a month late: Jan–Oct this year
        var serviceMonth = scenario.AddContract("House");     // billed in the service month: Jan–Nov this year
        for (var month = 1; month <= 12; month++)
        {
            scenario.AddBill(arrears, 100m, null, new YearMonth(2025, month));
            scenario.AddBill(serviceMonth, 10m, null, new YearMonth(2025, month));
            if (month <= 10)
            {
                scenario.AddBill(arrears, 110m, null, new YearMonth(2026, month));
            }

            if (month <= 11)
            {
                scenario.AddBill(serviceMonth, 12m, null, new YearMonth(2026, month));
            }
        }

        var comparison = Assert.Single(OverviewMoney.YearToDate(scenario.Data, november));

        // This year: 10 × 110 + 11 × 12 = 1232. Last year, the same months: 10 × 100 + 11 × 10 = 1110 (not Jan–Nov of the water: 1200 + ...).
        // Change: (1232 − 1110) / 1110 = 10.99 % -> 11.0.
        Assert.Equal((1232m, 1110m, 11.0m), (comparison.ThisYear, comparison.LastYear, comparison.ChangePercent));
    }

    [Fact]
    public void Year_to_date_leaves_out_a_contract_without_bills_this_year_and_groups_bills_without_a_contract()
    {
        var november = new DateOnly(2026, 11, 11);
        var scenario = new OverviewScenario();
        var water = scenario.AddContract("Water");
        var ended = scenario.AddContract("Old internet");
        scenario.AddBill(water, 50m, null, new YearMonth(2026, 3));
        scenario.AddBill(water, 40m, null, new YearMonth(2025, 3));
        scenario.AddBill(water, 99m, null, new YearMonth(2025, 8));        // after the contract's last month this year
        scenario.AddBill(ended, 700m, null, new YearMonth(2025, 5));       // ended last year: in neither sum
        // One-off bills without a contract: one group, its own last month (September).
        scenario.AddBill(water, 30m, null, new YearMonth(2026, 9)).ContractId = null;
        scenario.AddBill(water, 20m, null, new YearMonth(2025, 9)).ContractId = null;
        scenario.AddBill(water, 8m, null, new YearMonth(2025, 10)).ContractId = null;

        var comparison = Assert.Single(OverviewMoney.YearToDate(scenario.Data, november));

        // This year 50 + 30 = 80; last year 40 (water, up to March) + 20 (one-off, up to September) = 60.
        Assert.Equal((80m, 60m), (comparison.ThisYear, comparison.LastYear));
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

    // ---- Sections of the Money card (T-052) ----------------------------------------------------------------------

    private static OverviewScenario Example()
    {
        // Gas: bill 12.25 due 05.10. (overdue on 27.10.); house: bill 25.15 due 25.11.; electricity: 13.25 paid on top, no bill uses it.
        var scenario = new OverviewScenario();
        var gas = scenario.AddContract("Gas", mode: BillingMode.Equalized);
        var house = scenario.AddContract("House");
        var electricity = scenario.AddContract("Electricity", mode: BillingMode.Equalized);
        scenario.AddBill(gas, 12.25m, D(10, 5), new YearMonth(2026, 9));
        scenario.AddBill(house, 25.15m, D(11, 25), new YearMonth(2026, 10));
        scenario.AddPayment(13.25m, D(10, 20), electricity);
        scenario.Settlements[gas.Id] = -9.32m;
        scenario.Settlements[electricity.Id] = 2.87m;
        return scenario;
    }

    [Fact]
    public void Sections_list_what_to_pay_what_is_overpaid_and_the_expected_settlements_per_contract()
    {
        var scenario = Example();

        var sections = Assert.Single(OverviewMoney.Sections(scenario.Data, Today));

        // To pay 12.25 + 25.15 = 37.40: gas first (overdue since 05.10.), then the house (due 25.11.).
        Assert.Equal(
            [("Gas", 12.25m, (DateOnly?)D(10, 5), (DateOnly?)null), ("House", 25.15m, null, D(11, 25))],
            sections.ToPay.Select(r => (r.Title!, r.Amount, r.OverdueSince, r.NextDue)));
        Assert.Equal(37.40m, sections.ToPay.Sum(r => r.Amount));
        Assert.Equal([("Electricity", 13.25m)], sections.Overpaid.Select(r => (r.Title!, r.Amount)));
        Assert.Equal([("Electricity", 2.87m), ("Gas", -9.32m)], sections.Settlements.Select(s => (s.Title, s.Expected)));
    }

    [Fact]
    public void Only_an_overpayment_leaves_the_to_pay_section_empty()
    {
        var scenario = new OverviewScenario();
        var gas = scenario.AddContract("Gas", mode: BillingMode.Equalized);
        scenario.AddPayment(22.24m, D(10, 20), gas);
        scenario.Settlements[gas.Id] = -9.32m;

        var sections = Assert.Single(OverviewMoney.Sections(scenario.Data, Today));

        Assert.Empty(sections.ToPay);
        Assert.Equal(22.24m, Assert.Single(sections.Overpaid).Amount);
        Assert.Single(sections.Settlements);
    }

    [Fact]
    public void A_debt_of_one_contract_is_not_netted_with_an_overpayment_of_another()
    {
        var scenario = new OverviewScenario();
        var electricity = scenario.AddContract("Electricity");
        var gas = scenario.AddContract("Gas");
        scenario.AddBill(electricity, 10m, D(11, 20), new YearMonth(2026, 10));
        scenario.AddPayment(10m, D(10, 20), gas);

        var sections = Assert.Single(OverviewMoney.Sections(scenario.Data, Today));

        Assert.Equal(("Electricity", 10m), (sections.ToPay[0].Title!, sections.ToPay[0].Amount));
        Assert.Equal(("Gas", 10m), (sections.Overpaid[0].Title!, sections.Overpaid[0].Amount));
    }

    [Fact]
    public void One_contract_with_an_open_bill_and_an_unallocated_payment_is_in_both_sections()
    {
        var scenario = new OverviewScenario();
        var gas = scenario.AddContract("Gas");
        scenario.AddBill(gas, 30m, D(11, 20), new YearMonth(2026, 10));
        scenario.AddPayment(12m, D(10, 20), gas);

        var sections = Assert.Single(OverviewMoney.Sections(scenario.Data, Today));

        Assert.Equal(30m, Assert.Single(sections.ToPay).Amount);
        Assert.Equal(12m, Assert.Single(sections.Overpaid).Amount);
    }

    [Fact]
    public void A_partly_paid_bill_counts_with_its_open_remainder_and_the_oldest_overdue_date()
    {
        var scenario = new OverviewScenario();
        var gas = scenario.AddContract("Gas");
        var sep = scenario.AddBill(gas, 50m, D(10, 20), new YearMonth(2026, 9));
        scenario.AddBill(gas, 40m, D(10, 5), new YearMonth(2026, 8));
        scenario.AddBill(gas, 60m, D(11, 20), new YearMonth(2026, 10));
        scenario.AddPayment(20m, D(10, 18), gas, null, (sep, 20m));

        var row = Assert.Single(Assert.Single(OverviewMoney.Sections(scenario.Data, Today)).ToPay);

        // 40 + 30 + 60 = 130; overdue since the oldest due date (05.10.); the next one not overdue is 20.11.
        Assert.Equal((130m, (DateOnly?)D(10, 5), (DateOnly?)D(11, 20)), (row.Amount, row.OverdueSince, row.NextDue));
    }

    [Fact]
    public void A_bill_without_a_due_date_has_no_second_line_dates()
    {
        var scenario = new OverviewScenario();
        var gas = scenario.AddContract("Gas");
        scenario.AddBill(gas, 30m, null, new YearMonth(2026, 10));

        var row = Assert.Single(Assert.Single(OverviewMoney.Sections(scenario.Data, Today)).ToPay);

        Assert.Equal((null, null), (row.OverdueSince, row.NextDue));
    }

    [Fact]
    public void To_pay_is_ordered_overdue_oldest_first_then_by_due_date_then_by_name()
    {
        var scenario = new OverviewScenario();
        var b = scenario.AddContract("B");
        var a = scenario.AddContract("A");
        var late = scenario.AddContract("Late");
        var older = scenario.AddContract("Older");
        var none = scenario.AddContract("Z-no-date");
        scenario.AddBill(b, 1m, D(11, 10), new YearMonth(2026, 10));
        scenario.AddBill(a, 1m, D(11, 20), new YearMonth(2026, 10));
        scenario.AddBill(late, 1m, D(10, 20), new YearMonth(2026, 9));
        scenario.AddBill(older, 1m, D(10, 1), new YearMonth(2026, 9));
        scenario.AddBill(none, 1m, null, new YearMonth(2026, 9));

        var titles = Assert.Single(OverviewMoney.Sections(scenario.Data, Today)).ToPay.Select(r => r.Title);

        Assert.Equal(["Older", "Late", "B", "A", "Z-no-date"], titles);
    }

    [Fact]
    public void A_payment_without_a_contract_is_a_row_without_a_title_and_overpaid_is_sorted_by_amount()
    {
        var scenario = new OverviewScenario();
        var gas = scenario.AddContract("Gas");
        scenario.AddPayment(5m, D(10, 20), gas);
        scenario.AddPayment(8m, D(10, 21));   // no contract, the property's own

        var overpaid = Assert.Single(OverviewMoney.Sections(scenario.Data, Today)).Overpaid;

        Assert.Equal([(null, 8m), ("Gas", 5m)], overpaid.Select(r => (r.Title, r.Amount)));
        Assert.Null(overpaid[0].ContractId);
    }

    [Fact]
    public void Everything_closed_gives_no_sections()
    {
        var scenario = new OverviewScenario();
        var gas = scenario.AddContract("Gas");
        var bill = scenario.AddBill(gas, 50m, D(10, 20), new YearMonth(2026, 9));
        scenario.AddPayment(50m, D(10, 18), gas, null, (bill, 50m));
        scenario.AddBill(gas, 70m, D(11, 20), new YearMonth(2026, 10)).IsDraft = true;   // a draft counts nowhere

        Assert.Empty(OverviewMoney.Sections(scenario.Data, Today));
    }

    [Fact]
    public void A_settlement_under_one_cent_is_left_out()
    {
        var scenario = new OverviewScenario();
        var gas = scenario.AddContract("Gas", mode: BillingMode.Equalized);
        scenario.Settlements[gas.Id] = 0.004m;

        Assert.Empty(OverviewMoney.Sections(scenario.Data, Today));
    }

    [Fact]
    public void Two_currencies_of_one_contract_are_two_rows()
    {
        var scenario = new OverviewScenario();
        var gas = scenario.AddContract("Gas");
        scenario.AddBill(gas, 10m, null, new YearMonth(2026, 10));
        scenario.AddBill(gas, 20m, null, new YearMonth(2026, 9)).Currency = Currency.LVL;

        var rows = Assert.Single(OverviewMoney.Sections(scenario.Data, Today)).ToPay;

        Assert.Equal([(Currency.EUR, 10m), (Currency.LVL, 20m)], rows.OrderBy(r => r.Currency).Select(r => (r.Currency, r.Amount)));
    }
}
