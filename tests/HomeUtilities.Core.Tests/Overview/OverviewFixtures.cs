using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Overview;

namespace HomeUtilities.Core.Tests.Overview;

/// <summary>Small builders for the Overview tests: one property "Flat" and what hangs on it.</summary>
internal sealed class OverviewScenario
{
    public Property Flat { get; } = new() { Name = "Flat" };

    public List<Property> Properties { get; } = [];

    public List<Contract> Contracts { get; } = [];

    public List<ContractMeter> Links { get; } = [];

    public List<Meter> Meters { get; } = [];

    public List<Bill> Bills { get; } = [];

    public List<Payment> Payments { get; } = [];

    public Dictionary<Guid, decimal> Settlements { get; } = [];

    public OverviewScenario()
    {
        Properties.Add(Flat);
    }

    public OverviewData Data => new(Properties, Contracts, Links, Meters, Bills, Payments, Settlements);

    public IReadOnlyList<TodoItem> Todo(DateOnly today) => TodoBuilder.Build(Data, today);

    public Contract AddContract(
        string title = "Water",
        Property? property = null,
        int? windowFrom = null,
        int? windowTo = null,
        int? billDay = null,
        BillingMode mode = BillingMode.Actual,
        BillingTiming timing = BillingTiming.InArrears,
        DateOnly? validFrom = null)
    {
        var contract = new Contract
        {
            PropertyId = (property ?? Flat).Id,
            Title = title,
            ValidFrom = validFrom ?? new DateOnly(2020, 1, 1),
            ReadingDayFrom = windowFrom,
            ReadingDayTo = windowTo,
            BillDay = billDay,
            BillingMode = mode,
            BillingTiming = timing,
        };
        Contracts.Add(contract);
        return contract;
    }

    /// <summary>A meter of the contract with one register and the readings on the given dates (value rising by one per reading).</summary>
    public Meter AddMeter(Contract contract, string location = "Kitchen", params DateOnly[] readings)
    {
        var meter = new Meter { PropertyId = contract.PropertyId, InstalledOn = new DateOnly(2020, 1, 1), Location = location };
        var register = new MeterRegister { MeterId = meter.Id };
        var value = 100m;
        foreach (var date in readings)
        {
            register.Readings.Add(new MeterReading { RegisterId = register.Id, Date = date, Value = value++ });
        }

        meter.Registers.Add(register);
        Meters.Add(meter);
        Links.Add(new ContractMeter { ContractId = contract.Id, MeterId = meter.Id, ValidFrom = new DateOnly(2020, 1, 1) });
        return meter;
    }

    public Bill AddBill(Contract? contract, decimal gross, DateOnly? due, YearMonth period, BillKind kind = BillKind.Regular, Property? property = null)
    {
        var bill = new Bill
        {
            PropertyId = (property ?? Flat).Id,
            ContractId = contract?.Id,
            Period = period,
            Gross = gross,
            DueOn = due,
            Kind = kind,
            Title = contract is null ? "Repair" : null,
            Currency = contract?.Currency ?? Currency.EUR,
        };
        Bills.Add(bill);
        return bill;
    }

    public Payment AddPayment(decimal amount, DateOnly paidOn, Contract? contract = null, Property? property = null, params (Bill Bill, decimal Amount)[] allocations)
    {
        var payment = new Payment { ContractId = contract?.Id, PropertyId = contract is null ? (property ?? Flat).Id : null, PaidOn = paidOn, Amount = amount };
        foreach (var (bill, allocated) in allocations)
        {
            payment.Allocations.Add(new PaymentAllocation { PaymentId = payment.Id, BillId = bill.Id, Amount = allocated });
        }

        Payments.Add(payment);
        return payment;
    }
}
