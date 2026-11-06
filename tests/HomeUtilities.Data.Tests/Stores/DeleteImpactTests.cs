using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Tariffs;
using HomeUtilities.Data.Stores;

namespace HomeUtilities.Data.Tests.Stores;

/// <summary>The dependants listed in the delete confirmation, and the Undo of such a delete (T-036c, UI_GUIDELINES §5.1).</summary>
public class DeleteImpactTests
{
    [Fact]
    public async Task Contract_counts_its_bills_payments_and_tariffs_and_a_delete_is_undone_exactly()
    {
        using var test = new TestDb();
        var (propertyId, supplierId, categoryId) = await StoreTests.PrepareContractOwnersAsync(test);
        var contracts = new ContractStore(test.Factory);
        var gas = StoreTests.NewContract(propertyId, supplierId, categoryId, "Gas");
        var other = StoreTests.NewContract(propertyId, supplierId, categoryId, "Electricity");
        await contracts.SaveAsync(gas);
        await contracts.SaveAsync(other);

        var bills = new BillStore(test.Factory);
        for (var month = 1; month <= 12; month++)
        {
            await bills.SaveAsync(new Bill { PropertyId = propertyId, CategoryId = categoryId, ContractId = gas.Id, Period = new YearMonth(2026, month), Gross = 10m });
        }

        await bills.SaveAsync(new Bill { PropertyId = propertyId, CategoryId = categoryId, ContractId = other.Id, Period = new YearMonth(2026, 1), Gross = 5m });
        var payments = new PaymentStore(test.Factory);
        for (var i = 1; i <= 3; i++)
        {
            await payments.SaveAsync(new Payment { PropertyId = propertyId, ContractId = gas.Id, PaidOn = new DateOnly(2026, i, 5), Amount = 10m });
        }

        await new TariffStore(test.Factory).SaveAsync(
            new TariffComponent { ContractId = gas.Id, Name = "Gas", SortOrder = 1, Basis = ChargeBasis.PerUnit, VatCode = "LV-STANDARD" });

        var impact = new DeleteImpactService(test.Factory);
        Assert.Equal(
            [new Dependant(DependantKind.Bills, 12), new Dependant(DependantKind.Payments, 3), new Dependant(DependantKind.Tariffs, 1)],
            await impact.ForContractAsync(gas.Id));

        // The recorder sees every row of the delete, so Undo brings the contract back whole.
        var undo = new DeleteUndoService(test.Factory);
        IReadOnlyList<DeletedRow> deleted;
        using (var recording = undo.BeginRecording())
        {
            await contracts.DeleteAsync(gas.Id);
            deleted = recording.Rows;
        }

        Assert.Equal([new DeletedRow("Contracts", gas.Id)], deleted);
        Assert.Null(await contracts.GetAsync(gas.Id));
        await undo.RestoreAsync([.. deleted]);
        Assert.NotNull(await contracts.GetAsync(gas.Id));
    }

    [Fact]
    public async Task Property_and_supplier_count_what_belongs_to_them_and_leave_out_empty_kinds()
    {
        using var test = new TestDb();
        var (propertyId, supplierId, categoryId) = await StoreTests.PrepareContractOwnersAsync(test);
        var contract = StoreTests.NewContract(propertyId, supplierId, categoryId, "Water");
        await new ContractStore(test.Factory).SaveAsync(contract);
        await new BillStore(test.Factory).SaveAsync(
            new Bill { PropertyId = propertyId, CategoryId = categoryId, ContractId = contract.Id, Period = new YearMonth(2026, 3), Gross = 10m });
        await new PaymentStore(test.Factory).SaveAsync(new Payment { PropertyId = propertyId, ContractId = contract.Id, PaidOn = new DateOnly(2026, 3, 5), Amount = 10m });
        await StoreTests.SaveMeterAsync(test, propertyId);

        var impact = new DeleteImpactService(test.Factory);

        Assert.Equal(
            [new Dependant(DependantKind.Contracts, 1), new Dependant(DependantKind.Meters, 1), new Dependant(DependantKind.Bills, 1), new Dependant(DependantKind.Payments, 1)],
            await impact.ForPropertyAsync(propertyId));
        Assert.Equal([new Dependant(DependantKind.Contracts, 1), new Dependant(DependantKind.Bills, 1)], await impact.ForSupplierAsync(supplierId));
        Assert.Empty(await impact.ForSupplierAsync(Guid.CreateVersion7()));
    }

    [Fact]
    public async Task Meter_counts_its_contract_links()
    {
        using var test = new TestDb();
        var (propertyId, supplierId, categoryId) = await StoreTests.PrepareContractOwnersAsync(test);
        var contracts = new ContractStore(test.Factory);
        var contract = StoreTests.NewContract(propertyId, supplierId, categoryId, "Water");
        await contracts.SaveAsync(contract);
        var meterId = await StoreTests.SaveMeterAsync(test, propertyId);
        await contracts.SaveContractMeterAsync(new HomeUtilities.Core.Domain.ContractMeter { ContractId = contract.Id, MeterId = meterId, ValidFrom = new DateOnly(2024, 1, 1) });

        Assert.Equal([new Dependant(DependantKind.Links, 1)], await new DeleteImpactService(test.Factory).ForMeterAsync(meterId));
    }
}
