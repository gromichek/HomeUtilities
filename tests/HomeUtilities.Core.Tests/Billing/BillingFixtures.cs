using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Charges;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Tariffs;
using HomeUtilities.Core.Tests.Fakes;

namespace HomeUtilities.Core.Tests.Billing;

/// <summary>Synthetic contracts, bills and payments for billing tests.</summary>
internal static class BillingFixtures
{
    public static readonly FakeClock Clock = new(new DateTimeOffset(2026, 10, 20, 8, 0, 0, TimeSpan.Zero));

    public static Contract Contract(BillingMode mode = BillingMode.Actual, BillingTiming timing = BillingTiming.InArrears) => new()
    {
        PropertyId = Guid.CreateVersion7(),
        SupplierId = Guid.CreateVersion7(),
        CategoryId = Guid.CreateVersion7(),
        Title = "Test contract",
        ValidFrom = new(2020, 1, 1),
        BillingMode = mode,
        BillingTiming = timing,
    };

    public static Bill BillFor(Contract contract, decimal gross, YearMonth? period = null, DateOnly? due = null, DateOnly? issued = null)
    {
        var bill = Bill.CreateFor(contract, period ?? new YearMonth(2026, 9), Clock);
        bill.Gross = gross;
        bill.DueOn = due;
        bill.IssuedOn = issued;
        return bill;
    }

    public static Payment Pay(decimal amount, DateOnly paidOn, Guid? contractId = null, Guid? propertyId = null,
        params (Bill Bill, decimal Amount)[] allocations)
    {
        var payment = new Payment { Amount = amount, PaidOn = paidOn, ContractId = contractId, PropertyId = propertyId };
        payment.Allocations = [.. allocations.Select(a => new PaymentAllocation { PaymentId = payment.Id, BillId = a.Bill.Id, Amount = a.Amount })];
        return payment;
    }

    public static CalculatedCharge Charge(decimal totalGross, bool complete = true, bool fromBill = false) => new(
        Guid.CreateVersion7(),
        new YearMonth(2026, 9),
        Currency.EUR,
        [new ChargeLine(Guid.CreateVersion7(), "Water", ServiceKind.ColdWater, Guid.CreateVersion7(), ChargeBasis.PerUnit,
            null, null, totalGross, 0, 0, totalGross, fromBill, complete, [])],
        totalGross, 0, totalGross, complete, []);
}
