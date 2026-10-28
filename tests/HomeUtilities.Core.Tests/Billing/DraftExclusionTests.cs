using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Charges;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Equalized;
using HomeUtilities.Core.Overview;
using HomeUtilities.Core.Reports;
using HomeUtilities.Core.Tests.Overview;
using HomeUtilities.Core.Tests.Reports;
using static HomeUtilities.Core.Tests.Billing.BillingFixtures;

namespace HomeUtilities.Core.Tests.Billing;

/// <summary>A draft bill never counts in a status, balance, allocation, report or the Overview until it is confirmed.</summary>
public class DraftExclusionTests
{
    private static readonly DateOnly Date = new(2026, 10, 20);

    private static Bill Draft(Bill bill)
    {
        bill.IsDraft = true;
        return bill;
    }

    [Fact]
    public void A_draft_is_never_overdue()
    {
        var bill = Draft(BillFor(Contract(), 50m, due: new DateOnly(2026, 9, 1)));

        Assert.False(BillStatus.Of(bill, [], Date).IsOverdue);
        bill.IsDraft = false;
        Assert.True(BillStatus.Of(bill, [], Date).IsOverdue);
    }

    [Fact]
    public void A_draft_is_not_in_the_contract_balance()
    {
        var contract = Contract();
        var confirmed = BillFor(contract, 40m, issued: new DateOnly(2026, 9, 1));
        var draft = Draft(BillFor(contract, 25m, issued: new DateOnly(2026, 9, 1)));

        // billed 40 (the draft's 25 is left out); nothing paid
        Assert.Equal(new Balance(40m, 0m, -40m), ContractBalance.Of(contract.Id, [confirmed, draft], [], Date));
    }

    [Fact]
    public void A_payment_is_not_allocated_to_a_draft()
    {
        var contract = Contract();
        var draft = Draft(BillFor(contract, 25m, due: new DateOnly(2026, 9, 1)));
        var confirmed = BillFor(contract, 40m, due: new DateOnly(2026, 10, 1));
        var payment = Pay(100m, Date, contract.Id);

        var allocations = PaymentAllocator.AutoAllocate(payment, [draft, confirmed], id => id == draft.Id ? 25m : 40m);

        // the older draft is skipped; only the confirmed bill gets its 40
        var allocation = Assert.Single(allocations);
        Assert.Equal((confirmed.Id, 40m), (allocation.BillId, allocation.Amount));
    }

    [Fact]
    public void Overview_money_ignores_a_draft()
    {
        var scenario = new OverviewScenario();
        var water = scenario.AddContract();
        scenario.AddBill(water, 60m, new DateOnly(2026, 11, 5), new YearMonth(2026, 10));
        Draft(scenario.AddBill(water, 25m, new DateOnly(2026, 9, 1), new YearMonth(2026, 8)));

        var money = Assert.Single(OverviewMoney.PerProperty(scenario.Data, Date));

        // open 60 only; the draft's 25 (already past its due date) is neither open nor overdue
        Assert.Equal((60m, 0m), (money.OpenBills, money.OverdueBills));
    }

    [Fact]
    public void Overview_year_comparison_ignores_a_draft()
    {
        var scenario = new OverviewScenario();
        var water = scenario.AddContract();
        scenario.AddBill(water, 60m, null, new YearMonth(2026, 3));
        Draft(scenario.AddBill(water, 25m, null, new YearMonth(2026, 4)));

        var comparison = Assert.Single(OverviewMoney.YearToDate(scenario.Data, Date));

        Assert.Equal(60m, comparison.ThisYear);
    }

    [Fact]
    public void A_draft_has_no_due_item_but_drafts_are_counted_per_property()
    {
        var scenario = new OverviewScenario();
        var water = scenario.AddContract();
        var draftA = Draft(scenario.AddBill(water, 25m, new DateOnly(2026, 10, 18), new YearMonth(2026, 9)));
        var draftB = Draft(scenario.AddBill(water, 0m, new DateOnly(2026, 10, 19), new YearMonth(2026, 10)));
        var data = scenario.Data with { Bills = [], Drafts = [draftA, draftB] };

        var item = Assert.Single(TodoBuilder.Build(data, Date));

        Assert.Equal((TodoKind.DraftsToConfirm, TodoSeverity.Info, 2, scenario.Flat.Id), (item.Kind, item.Severity, item.Count, item.PropertyId));
    }

    [Fact]
    public void A_draft_in_the_bills_list_is_still_not_a_due_item()
    {
        var scenario = new OverviewScenario();
        var water = scenario.AddContract();
        Draft(scenario.AddBill(water, 25m, new DateOnly(2026, 10, 18), new YearMonth(2026, 9)));

        // a draft that slipped into Bills is ignored, and without Drafts there is no drafts item either
        Assert.Empty(scenario.Todo(Date));
    }

    [Fact]
    public void A_draft_of_the_period_suppresses_the_missing_bill_item()
    {
        var scenario = new OverviewScenario();
        var water = scenario.AddContract(billDay: 5);
        var data = scenario.Data;
        Assert.Contains(TodoBuilder.Build(data, Date), i => i.Kind == TodoKind.BillMissing);

        var draft = Draft(scenario.AddBill(water, 0m, null, new YearMonth(2026, 9)));
        var withDraft = scenario.Data with { Bills = [], Drafts = [draft] };

        var kinds = TodoBuilder.Build(withDraft, Date).Select(i => i.Kind);
        Assert.Equal([TodoKind.DraftsToConfirm], kinds);
    }

    [Fact]
    public void Costs_report_ignores_a_draft()
    {
        var s = new ReportScenario();
        var water = s.AddContract("Water supply");
        var jan = new YearMonth(2026, 1);
        s.AddBill(water, 20m, jan);
        Draft(s.AddBill(water, 99m, jan));

        var table = ReportBuilder.Costs(s.Data, ReportScenario.Filter(jan, jan));

        Assert.Equal(20m, table.Totals!.Total!.Value);
    }

    [Fact]
    public void Calculated_quantities_from_a_draft_are_ignored()
    {
        var componentId = Guid.NewGuid();
        var confirmed = BillFor(Contract(), 10m);
        confirmed.BillQuantities = [new BillQuantity(componentId, 5m)];
        var draft = Draft(BillFor(Contract(), 10m));
        draft.BillQuantities = [new BillQuantity(componentId, 9m)];

        // oldest first: the draft is newer but does not win
        var (quantities, _) = BillQuantitySelection.From([confirmed, draft]);

        Assert.Equal(5m, quantities[componentId]);
    }

    [Fact]
    public void A_draft_equalized_bill_is_not_billed_in_the_settlement()
    {
        var equalized = Contract(BillingMode.Equalized);
        var june = new YearMonth(2026, 6);
        var confirmed = BillFor(equalized, 40m, june);
        confirmed.Kind = BillKind.Equalized;
        var draft = Draft(BillFor(equalized, 40m, june));
        draft.Kind = BillKind.Equalized;
        var actual = new Dictionary<YearMonth, CalculatedCharge> { [june] = Charge(45m) };

        var result = EqualizedSettlement.Calculate([], [confirmed, draft], actual, june, june);

        // billed 40 (not 80); actual 45; expected +5
        Assert.Equal((40m, 5m), (result.Months[0].Billed, result.Expected));
    }
}
