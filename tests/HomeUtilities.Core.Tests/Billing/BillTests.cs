using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using static HomeUtilities.Core.Tests.Billing.BillingFixtures;

namespace HomeUtilities.Core.Tests.Billing;

public class BillTests
{
    [Theory]
    [InlineData(BillingMode.Actual, BillKind.Regular)]
    [InlineData(BillingMode.Fixed, BillKind.Regular)]
    [InlineData(BillingMode.Equalized, BillKind.Equalized)]
    [InlineData(BillingMode.Assessed, BillKind.TaxInstallment)]
    public void CreateFor_copies_contract_data_and_picks_kind(BillingMode mode, BillKind kind)
    {
        var contract = Contract(mode);
        contract.Currency = Currency.LVL;

        var bill = Bill.CreateFor(contract, new YearMonth(2026, 10), Clock);

        Assert.Equal((contract.PropertyId, contract.CategoryId, contract.Id, contract.SupplierId), (bill.PropertyId, bill.CategoryId, bill.ContractId, bill.SupplierId));
        Assert.Equal((new YearMonth(2026, 10), Currency.LVL, kind), (bill.Period, bill.Currency, bill.Kind));
        Assert.Equal(Clock.UtcNow, bill.CreatedAt);
    }

    [Fact]
    public void DefaultPeriod_follows_billing_timing()
    {
        var today = new DateOnly(2026, 10, 20);
        Assert.Equal(new YearMonth(2026, 9), Bill.DefaultPeriod(Contract(timing: BillingTiming.InArrears), today));
        Assert.Equal(new YearMonth(2026, 11), Bill.DefaultPeriod(Contract(timing: BillingTiming.InAdvance), today));
    }

    [Fact]
    public void DefaultPeriod_crosses_year_boundary()
    {
        Assert.Equal(new YearMonth(2025, 12), Bill.DefaultPeriod(Contract(), new DateOnly(2026, 1, 5)));
        Assert.Equal(new YearMonth(2027, 1), Bill.DefaultPeriod(Contract(timing: BillingTiming.InAdvance), new DateOnly(2026, 12, 5)));
    }

    [Fact]
    public void Contract_defaults()
    {
        var contract = new Contract();
        Assert.Equal(BillingTiming.InArrears, contract.BillingTiming);
        Assert.Empty(contract.CustomFields);
    }

    [Fact]
    public void CreateOneOff_has_no_contract()
    {
        var property = Guid.CreateVersion7();
        var category = Guid.CreateVersion7();

        var bill = Bill.CreateOneOff(property, category, "Boiler repair", new YearMonth(2026, 10), Clock);

        Assert.Null(bill.ContractId);
        Assert.Equal((BillKind.OneOff, "Boiler repair", property, category), (bill.Kind, bill.Title, bill.PropertyId, bill.CategoryId));
        Assert.Equal(Currency.EUR, bill.Currency);
    }
}
