using System.Reflection;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Charges;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Tariffs;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels.Tests;

/// <summary>Copies of stored graphs, like a real store returns: the caller may change them freely.</summary>
internal static class Copies
{
    private static readonly MethodInfo CloneMethod =
        typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!;

    public static T Shallow<T>(T source)
        where T : class => (T)CloneMethod.Invoke(source, null)!;

    public static Bill Of(Bill bill)
    {
        var copy = Shallow(bill);
        copy.Lines = [.. bill.Lines.Where(l => !l.IsDeleted).Select(l =>
        {
            var line = Shallow(l);
            line.CustomFields = [.. l.CustomFields];
            return line;
        })];
        copy.CustomFields = [.. bill.CustomFields];
        copy.AttachmentIds = [.. bill.AttachmentIds];
        return copy;
    }

    public static Payment Of(Payment payment)
    {
        var copy = Shallow(payment);
        copy.Allocations = [.. payment.Allocations.Where(a => !a.IsDeleted).Select(Shallow)];
        return copy;
    }
}

internal sealed class FakeBillStore : IBillStore
{
    /// <summary>Every bill ever stored, deleted ones included (nothing is hard-deleted).</summary>
    public List<Bill> All { get; } = [];

    public List<Bill> Saved { get; } = [];

    public List<Guid> Deleted { get; } = [];

    public IEnumerable<Bill> Stored => All.Where(b => !b.IsDeleted);

    public Task<IReadOnlyList<Bill>> ListAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Bill>>([.. Stored.Select(Copies.Of).OrderBy(b => b.Period)]);

    public Task<IReadOnlyList<Bill>> ListByContractAsync(Guid contractId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Bill>>([.. Stored.Where(b => b.ContractId == contractId).Select(Copies.Of).OrderBy(b => b.Period)]);

    public Task<IReadOnlyList<Bill>> ListByPropertyAsync(Guid propertyId, YearMonth? from, YearMonth? to, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Bill>>([.. Stored
            .Where(b => b.PropertyId == propertyId && (from is null || b.Period >= from) && (to is null || b.Period <= to))
            .Select(Copies.Of).OrderBy(b => b.Period)]);

    public Task<Bill?> GetAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Stored.FirstOrDefault(b => b.Id == id) is { } bill ? Copies.Of(bill) : null);

    public Task SaveAsync(Bill aggregate, CancellationToken ct = default)
    {
        Saved.Add(Copies.Of(aggregate));
        All.RemoveAll(b => b.Id == aggregate.Id);
        All.Add(Copies.Of(aggregate));
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        Deleted.Add(id);
        foreach (var bill in All.Where(b => b.Id == id))
        {
            bill.IsDeleted = true;
        }

        return Task.CompletedTask;
    }
}

internal sealed class FakePaymentStore : IPaymentStore
{
    /// <summary>Every payment ever stored, deleted ones (and their deleted allocations) included.</summary>
    public List<Payment> All { get; } = [];

    public List<Payment> Saved { get; } = [];

    public List<Guid> Deleted { get; } = [];

    public IEnumerable<Payment> Stored => All.Where(p => !p.IsDeleted);

    /// <summary>Adds a ready payment (test arrangement).</summary>
    public void Add(Payment payment) => All.Add(payment);

    public Task<IReadOnlyList<Payment>> ListAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Payment>>([.. Stored.Select(Copies.Of).OrderBy(p => p.PaidOn)]);

    public Task<IReadOnlyList<Payment>> ListByContractAsync(Guid contractId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Payment>>([.. Stored
            .Where(p => p.ContractId == contractId || p.Allocations.Any(a => !a.IsDeleted && BillContract(a.BillId) == contractId))
            .Select(Copies.Of).OrderBy(p => p.PaidOn)]);

    public Task<IReadOnlyList<Payment>> ListByBillAsync(Guid billId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Payment>>([.. Stored
            .Where(p => p.Allocations.Any(a => !a.IsDeleted && a.BillId == billId))
            .Select(Copies.Of).OrderBy(p => p.PaidOn)]);

    public Task<IReadOnlyList<Payment>> ListByPropertyAsync(Guid propertyId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Payment>>([.. Stored.Where(p => p.PropertyId == propertyId).Select(Copies.Of).OrderBy(p => p.PaidOn)]);

    public Task<Payment?> GetAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Stored.FirstOrDefault(p => p.Id == id) is { } payment ? Copies.Of(payment) : null);

    public Task SaveAsync(Payment aggregate, CancellationToken ct = default)
    {
        Saved.Add(Copies.Of(aggregate));
        var stored = All.FirstOrDefault(p => p.Id == aggregate.Id);
        var copy = Copies.Of(aggregate);
        if (stored is not null)
        {
            // Allocations missing from the incoming graph are soft-deleted, like the real store does.
            var incoming = copy.Allocations.Select(a => a.Id).ToHashSet();
            foreach (var removed in stored.Allocations.Where(a => !incoming.Contains(a.Id)))
            {
                removed.IsDeleted = true;
                copy.Allocations.Add(removed);
            }
        }

        All.RemoveAll(p => p.Id == aggregate.Id);
        All.Add(copy);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        Deleted.Add(id);
        foreach (var payment in All.Where(p => p.Id == id))
        {
            payment.IsDeleted = true;
            foreach (var allocation in payment.Allocations)
            {
                allocation.IsDeleted = true;
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>Resolves the contract of a bill for the contract listing; set by the test kit.</summary>
    public Func<Guid, Guid?> BillContractResolver { get; set; } = _ => null;

    private Guid? BillContract(Guid billId) => BillContractResolver(billId);
}

/// <summary>Everything a bill editor or the bills list needs, on top of the contract test kit (one property, built-in categories).</summary>
internal sealed class BillTestKit
{
    public BillTestKit(DateOnly today)
    {
        Kit = new ContractTestKit(today);
        QuickPay = new QuickPayService(Kit.Bills, Kit.Payments, Kit.Contracts, Kit.Suppliers, Kit.Properties, Kit.Clock);
    }

    public ContractTestKit Kit { get; }

    public FakeBillStore Bills => Kit.Bills;

    public FakePaymentStore Payments => Kit.Payments;

    public QuickPayService QuickPay { get; }

    public BillFilterState Filter { get; } = new();

    public PaymentFilterState PaymentFilter { get; } = new();

    public PaymentEditViewModel CreatePaymentEditor() => Kit.CreatePaymentEditor();

    public PaymentsViewModel CreatePayments(ISectionSelectionMemory? memory = null) =>
        new(Payments, Bills, Kit.Contracts, Kit.Properties, Kit.Dialogs, Kit.Clock, Kit.Localizer, Kit.Navigation, Kit.Layout,
            memory ?? new SectionSelectionMemory(), PaymentFilter, CreatePaymentEditor);

    public Supplier Supplier { get; } = new()
    {
        Name = "Water utility Ltd",
        PaymentDetails = new HomeUtilities.Core.Payments.PaymentDetails(null, null, null, null, null, "Abon. Nr. {CustomerNumber}, {Period:MM.yyyy}"),
    };

    public BillEditViewModel CreateEditor() =>
        new(Bills, Payments, Kit.Contracts, Kit.Suppliers, Kit.Categories, Kit.Properties, Kit.ChargeLoader, Kit.Attachments,
            Kit.Media, Kit.Viewer, QuickPay, Kit.Prepayments, Kit.Clock, Kit.Localizer, Kit.Dialogs, Kit.Navigation, Kit.Toast, Kit.ErrorLog);

    public BillsViewModel CreateBills(ISectionSelectionMemory? memory = null) =>
        new(Bills, Payments, Kit.Contracts, Kit.Suppliers, Kit.Categories, Kit.Properties, QuickPay, Kit.Toast, Kit.Dialogs,
            Kit.Clock, Kit.Localizer, Kit.Navigation, Kit.Layout, memory ?? new SectionSelectionMemory(), Filter, CreateEditor);

    /// <summary>
    /// A water contract (due day 20, readings 25–28) of the kit's property. With a tariff and a cold meter read 100.000 on
    /// 26 Sep and 112.150 on 26 Oct 2026 at 3.0675 per m³: 12.150 m³ → net 37.27, VAT 21 % = 7.83, total 45.10.
    /// </summary>
    public Contract AddWaterContract(
        string title = "Water", bool tariff = true, BillingTiming timing = BillingTiming.InArrears, int? dueDay = 20, decimal price = 3.0675m)
    {
        if (Kit.Suppliers.Stored.All(s => s.Id != Supplier.Id))
        {
            Kit.Suppliers.Stored.Add(Supplier);
        }

        var contract = new Contract
        {
            PropertyId = Kit.Property.Id,
            SupplierId = Supplier.Id,
            Title = title,
            Service = ServiceKind.ColdWater,
            CategoryId = Kit.CategoryIdFor(ServiceKind.ColdWater),
            CustomerNumber = "C-100",
            ValidFrom = new DateOnly(2020, 1, 1),
            ReadingDayFrom = 25,
            ReadingDayTo = 28,
            PaymentDueDay = dueDay,
            BillingTiming = timing,
        };
        Kit.Contracts.Stored.Add(contract);
        if (!tariff)
        {
            return contract;
        }

        var meter = new Meter
        {
            PropertyId = Kit.Property.Id,
            Resource = MeterResource.ColdWater,
            Unit = MeterUnit.M3,
            Location = "Bathroom cold",
            InstalledOn = new DateOnly(2020, 1, 1),
        };
        meter.Registers.Add(new MeterRegister { MeterId = meter.Id, Kind = RegisterKind.Total, Direction = EnergyDirection.Import });
        meter.Registers[0].Readings.Add(new MeterReading { RegisterId = meter.Registers[0].Id, Date = new DateOnly(2026, 9, 26), Value = 100m });
        meter.Registers[0].Readings.Add(new MeterReading { RegisterId = meter.Registers[0].Id, Date = new DateOnly(2026, 10, 26), Value = 112.150m });
        Kit.Meters.Stored.Add(meter);
        Kit.Contracts.Links.Add(new ContractMeter { ContractId = contract.Id, MeterId = meter.Id, ValidFrom = new DateOnly(2020, 1, 1) });

        var component = new TariffComponent
        {
            ContractId = contract.Id,
            Name = "Water supply",
            ChargeKind = ServiceKind.ColdWater,
            Basis = ChargeBasis.PerUnit,
            VatCode = "LV-STANDARD",
        };
        component.Rates.Add(new TariffRate { ComponentId = component.Id, ValidFrom = new DateOnly(2020, 1, 1), Price = price, Currency = Currency.EUR });
        Kit.Tariffs.Stored.Add(component);
        return contract;
    }

    /// <summary>Stores a bill of the contract and returns it.</summary>
    public Bill AddBill(Contract contract, YearMonth period, decimal gross, DateOnly? due = null, CalculatedCharge? snapshot = null)
    {
        var bill = Bill.CreateFor(contract, period, Kit.Clock);
        bill.Gross = gross;
        bill.DueOn = due ?? BillDefaults.DueDate(contract, period);
        bill.CalculatedSnapshot = snapshot;
        Bills.All.Add(bill);
        return bill;
    }

    /// <summary>Stores a manual payment allocated to the bills.</summary>
    public Payment AddPayment(decimal amount, params (Bill Bill, decimal Amount)[] allocations)
    {
        var payment = new Payment { ContractId = allocations.FirstOrDefault().Bill?.ContractId, PaidOn = Kit.Clock.Today.AddDays(-3), Amount = amount };
        foreach (var (bill, allocated) in allocations)
        {
            payment.Allocations.Add(new PaymentAllocation { PaymentId = payment.Id, BillId = bill.Id, Amount = allocated });
        }

        Payments.Add(payment);
        return payment;
    }

    /// <summary>A snapshot-like calculation with one water line of the given total.</summary>
    public CalculatedCharge Calculation(Contract contract, YearMonth period, decimal gross, decimal quantity = 12.150m) =>
        new(contract.Id, period, Currency.EUR,
            [new ChargeLine(Guid.NewGuid(), "Water supply", ServiceKind.ColdWater, contract.CategoryId, ChargeBasis.PerUnit, quantity, 3.0675m, gross, 21m, 0m, gross, false, true, [])],
            gross, 0m, gross, true, []);
}
