using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Tariffs;
using HomeUtilities.Data.Stores;
using Microsoft.EntityFrameworkCore;

namespace HomeUtilities.Data.Tests.Stores;

public class StoreTests
{
    [Fact]
    public async Task Property_round_trip_and_order_by_sort_order_then_name()
    {
        using var test = new TestDb();
        var store = new PropertyStore(test.Factory);
        await store.SaveAsync(new Property { Name = "Zeta", SortOrder = 1 });
        await store.SaveAsync(new Property { Name = "Beta", SortOrder = 2 });
        await store.SaveAsync(new Property { Name = "Alpha", SortOrder = 2 });
        var archived = new Property { Name = "Old", SortOrder = 0, IsArchived = true, Kind = PropertyKind.House, Address = "Nowhere 1" };
        await store.SaveAsync(archived);

        Assert.Equal(["Zeta", "Alpha", "Beta"], (await store.ListAsync(false)).Select(p => p.Name));
        Assert.Equal(["Old", "Zeta", "Alpha", "Beta"], (await store.ListAsync(true)).Select(p => p.Name));
        Assert.Equal(4, (await store.ListAsync()).Count);

        var loaded = (await store.GetAsync(archived.Id))!;
        Assert.Equal((archived.Name, archived.Kind, archived.Address, true), (loaded.Name, loaded.Kind, loaded.Address, loaded.IsArchived));
    }

    [Fact]
    public async Task Category_round_trip_and_order()
    {
        using var test = new TestDb();
        var store = new CategoryStore(test.Factory);
        await store.SaveAsync(new Category { Name = "Kindergarten", Color = "#112233", SortOrder = 2 });
        var water = new Category { SystemKey = ServiceKind.ColdWater, SortOrder = 1 };
        await store.SaveAsync(water);

        var list = await store.ListAsync();
        Assert.Equal([ServiceKind.ColdWater, null], list.Select(c => c.SystemKey));
        Assert.Equal("#112233", list[1].Color);
        Assert.True((await store.GetAsync(water.Id))!.IsBuiltIn);
    }

    [Fact]
    public async Task Supplier_round_trip_and_order_by_name()
    {
        using var test = new TestDb();
        var store = new SupplierStore(test.Factory);
        await store.SaveAsync(new Supplier { Name = "Bravo", Email = "b@example.test" });
        await store.SaveAsync(new Supplier { Name = "Alpha" });

        var list = await store.ListAsync();
        Assert.Equal(["Alpha", "Bravo"], list.Select(s => s.Name));
        Assert.Equal("b@example.test", list[1].Email);
    }

    [Fact]
    public async Task VatRate_round_trip_with_sorted_versions()
    {
        using var test = new TestDb();
        var store = new VatRateStore(test.Factory);
        var rate = new VatRate { Code = "X-TEST", Name = "Test" };
        rate.Versions.Add(new VatRateVersion { VatRateId = rate.Id, ValidFrom = new DateOnly(2020, 1, 1), Percent = 21m });
        rate.Versions.Add(new VatRateVersion { VatRateId = rate.Id, ValidFrom = new DateOnly(2010, 1, 1), Percent = 18m });
        await store.SaveAsync(rate);
        await store.SaveAsync(new VatRate { Code = "A-TEST", Name = "First" });

        Assert.Equal(["A-TEST", "X-TEST"], (await store.ListAsync()).Select(v => v.Code));
        var loaded = (await store.GetAsync(rate.Id))!;
        Assert.Equal([18m, 21m], loaded.Versions.Select(v => v.Percent));
        Assert.Equal(21m, loaded.PercentAt(new DateOnly(2024, 1, 1)));
    }

    [Fact]
    public async Task Contract_round_trip_list_by_property_and_meter_links()
    {
        using var test = new TestDb();
        var (propertyId, supplierId, categoryId) = await PrepareContractOwnersAsync(test);
        var store = new ContractStore(test.Factory);
        var contract = NewContract(propertyId, supplierId, categoryId, "Water");
        contract.CustomerNumber = "TEST-1";
        contract.ReadingDayFrom = 20;
        contract.ReadingDayTo = 26;
        await store.SaveAsync(contract);
        await store.SaveAsync(NewContract(propertyId, supplierId, categoryId, "Electricity"));
        await store.SaveAsync(NewContract(await SavePropertyAsync(test), supplierId, categoryId, "Elsewhere"));

        var loaded = (await store.GetAsync(contract.Id))!;
        Assert.Equal(("Water", "TEST-1", 20, 26), (loaded.Title, loaded.CustomerNumber, loaded.ReadingDayFrom, loaded.ReadingDayTo));
        Assert.Equal(["Electricity", "Water"], (await store.ListByPropertyAsync(propertyId)).Select(c => c.Title));

        var meterId = await SaveMeterAsync(test, propertyId);
        var later = new ContractMeter { ContractId = contract.Id, MeterId = meterId, ValidFrom = new DateOnly(2024, 1, 1) };
        var earlier = new ContractMeter { ContractId = contract.Id, MeterId = meterId, ValidFrom = new DateOnly(2020, 1, 1), ValidTo = new DateOnly(2023, 12, 31) };
        await store.SaveContractMeterAsync(later);
        await store.SaveContractMeterAsync(earlier);
        earlier.ValidTo = new DateOnly(2023, 6, 30);
        await store.SaveContractMeterAsync(earlier);

        var links = await store.ListContractMetersAsync(contract.Id);
        Assert.Equal([earlier.Id, later.Id], links.Select(l => l.Id));
        Assert.Equal(new DateOnly(2023, 6, 30), links[0].ValidTo);
    }

    [Fact]
    public async Task Meter_round_trip_list_by_property_with_sorted_readings()
    {
        using var test = new TestDb();
        var propertyId = await SavePropertyAsync(test);
        var store = new MeterStore(test.Factory);
        var meter = NewMeter(propertyId, new DateOnly(2020, 1, 1));
        var register = meter.Registers[0];
        register.Readings.Add(new MeterReading { RegisterId = register.Id, Date = new DateOnly(2026, 9, 26), Value = 437.097m });
        register.Readings.Add(new MeterReading { RegisterId = register.Id, Date = new DateOnly(2026, 8, 26), Value = 425.9m });
        await store.SaveAsync(meter);
        await store.SaveAsync(NewMeter(await SavePropertyAsync(test), new DateOnly(2020, 1, 1)));

        var list = await store.ListByPropertyAsync(propertyId);
        var loaded = Assert.Single(list);
        Assert.Equal([new DateOnly(2026, 8, 26), new DateOnly(2026, 9, 26)], loaded.Registers[0].Readings.Select(r => r.Date));
        Assert.Equal(437.097m, loaded.Registers[0].Readings[1].Value);
    }

    [Fact]
    public async Task Tariff_round_trip_list_by_contract_with_rates_and_tiers()
    {
        using var test = new TestDb();
        var (propertyId, supplierId, categoryId) = await PrepareContractOwnersAsync(test);
        var contract = NewContract(propertyId, supplierId, categoryId, "Water");
        await new ContractStore(test.Factory).SaveAsync(contract);
        var store = new TariffStore(test.Factory);
        var second = new TariffComponent { ContractId = contract.Id, Name = "Sewerage", SortOrder = 2, Basis = ChargeBasis.PerUnit, VatCode = "LV-STANDARD" };
        var first = new TariffComponent { ContractId = contract.Id, Name = "Water supply", SortOrder = 1, Basis = ChargeBasis.PerUnit, VatCode = "LV-STANDARD" };
        first.Rates.Add(new TariffRate { ComponentId = first.Id, ValidFrom = new DateOnly(2026, 1, 1), Price = 1.41m, Currency = Currency.EUR });
        first.Rates.Add(new TariffRate { ComponentId = first.Id, ValidFrom = new DateOnly(2025, 1, 1), Price = 1.30m, Currency = Currency.EUR });
        await store.SaveAsync(second);
        await store.SaveAsync(first);

        var list = await store.ListByContractAsync(contract.Id);
        Assert.Equal(["Water supply", "Sewerage"], list.Select(t => t.Name));
        Assert.Equal([1.30m, 1.41m], list[0].Rates.Select(r => r.Price));
        Assert.Empty(await store.ListByContractAsync(Guid.CreateVersion7()));
    }

    [Fact]
    public async Task Bill_round_trip_ordered_by_period()
    {
        using var test = new TestDb();
        var (propertyId, categoryId) = await AggregateMergerTests.PrepareBillOwnersAsync(test);
        var store = new BillStore(test.Factory);
        var bill = NewBill(propertyId, categoryId, new YearMonth(2026, 9), 55.25m);
        bill.Lines.Add(new BillLine { BillId = bill.Id, Description = "Water", Gross = 55.25m, SortOrder = 1 });
        await store.SaveAsync(bill);
        await store.SaveAsync(NewBill(propertyId, categoryId, new YearMonth(2026, 7), 10m));

        var list = await store.ListByPropertyAsync(propertyId, null, null);
        Assert.Equal([new YearMonth(2026, 7), new YearMonth(2026, 9)], list.Select(b => b.Period));
        var loaded = (await store.GetAsync(bill.Id))!;
        Assert.Equal(55.25m, loaded.Gross);
        Assert.Equal("Water", Assert.Single(loaded.Lines).Description);
    }

    [Fact]
    public async Task Bills_by_property_period_range_is_inclusive()
    {
        using var test = new TestDb();
        var (propertyId, categoryId) = await AggregateMergerTests.PrepareBillOwnersAsync(test);
        var store = new BillStore(test.Factory);
        foreach (var month in new[] { 7, 8, 9, 10, 11 })
        {
            await store.SaveAsync(NewBill(propertyId, categoryId, new YearMonth(2026, month), month));
        }

        var from = new YearMonth(2026, 8);
        var to = new YearMonth(2026, 10);
        Assert.Equal([8, 9, 10], (await store.ListByPropertyAsync(propertyId, from, to)).Select(b => b.Period.Month));
        Assert.Equal([8, 9, 10, 11], (await store.ListByPropertyAsync(propertyId, from, null)).Select(b => b.Period.Month));
        Assert.Equal([7, 8], (await store.ListByPropertyAsync(propertyId, null, from)).Select(b => b.Period.Month));
    }

    [Fact]
    public async Task Bills_by_contract()
    {
        using var test = new TestDb();
        var (propertyId, supplierId, categoryId) = await PrepareContractOwnersAsync(test);
        var contract = NewContract(propertyId, supplierId, categoryId, "Water");
        var other = NewContract(propertyId, supplierId, categoryId, "Heat");
        var contracts = new ContractStore(test.Factory);
        await contracts.SaveAsync(contract);
        await contracts.SaveAsync(other);
        var store = new BillStore(test.Factory);
        var bill = NewBill(propertyId, categoryId, new YearMonth(2026, 9), 1m);
        bill.ContractId = contract.Id;
        var otherBill = NewBill(propertyId, categoryId, new YearMonth(2026, 9), 2m);
        otherBill.ContractId = other.Id;
        await store.SaveAsync(bill);
        await store.SaveAsync(otherBill);

        Assert.Equal([bill.Id], (await store.ListByContractAsync(contract.Id)).Select(b => b.Id));
    }

    [Fact]
    public async Task Payments_round_trip_ordered_by_paid_on()
    {
        using var test = new TestDb();
        var store = new PaymentStore(test.Factory);
        var propertyId = await SavePropertyAsync(test);
        var late = new Payment { PropertyId = propertyId, PaidOn = new DateOnly(2026, 10, 1), Amount = 5m };
        var early = new Payment { PropertyId = propertyId, PaidOn = new DateOnly(2026, 9, 1), Amount = 7.5m, Reference = "ref" };
        await store.SaveAsync(late);
        await store.SaveAsync(early);

        var list = await store.ListByPropertyAsync(propertyId);
        Assert.Equal([early.Id, late.Id], list.Select(p => p.Id));
        Assert.Equal(("ref", 7.5m), (list[0].Reference, list[0].Amount));
    }

    [Fact]
    public async Task Payments_by_contract_include_property_level_payments_allocated_to_its_bills()
    {
        using var test = new TestDb();
        var (propertyId, supplierId, categoryId) = await PrepareContractOwnersAsync(test);
        var contracts = new ContractStore(test.Factory);
        var contract = NewContract(propertyId, supplierId, categoryId, "Water");
        var other = NewContract(propertyId, supplierId, categoryId, "Heat");
        await contracts.SaveAsync(contract);
        await contracts.SaveAsync(other);
        var bills = new BillStore(test.Factory);
        var bill = NewBill(propertyId, categoryId, new YearMonth(2026, 9), 30m);
        bill.ContractId = contract.Id;
        var otherBill = NewBill(propertyId, categoryId, new YearMonth(2026, 9), 40m);
        otherBill.ContractId = other.Id;
        await bills.SaveAsync(bill);
        await bills.SaveAsync(otherBill);

        var store = new PaymentStore(test.Factory);
        var direct = new Payment { ContractId = contract.Id, PaidOn = new DateOnly(2026, 9, 1), Amount = 10m };
        var propertyLevel = new Payment { PropertyId = propertyId, PaidOn = new DateOnly(2026, 9, 2), Amount = 70m };
        propertyLevel.Allocations.Add(new PaymentAllocation { PaymentId = propertyLevel.Id, BillId = bill.Id, Amount = 30m });
        propertyLevel.Allocations.Add(new PaymentAllocation { PaymentId = propertyLevel.Id, BillId = otherBill.Id, Amount = 40m });
        var unrelated = new Payment { ContractId = other.Id, PaidOn = new DateOnly(2026, 9, 3), Amount = 1m };
        await store.SaveAsync(direct);
        await store.SaveAsync(propertyLevel);
        await store.SaveAsync(unrelated);

        var forContract = await store.ListByContractAsync(contract.Id);
        Assert.Equal([direct.Id, propertyLevel.Id], forContract.Select(p => p.Id));
        Assert.Equal(2, forContract[1].Allocations.Count);
        Assert.Equal(
            new[] { propertyLevel.Id, direct.Id, unrelated.Id }.Order(),
            (await store.ListByPropertyAsync(propertyId)).Select(p => p.Id).Order());
    }

    [Fact]
    public async Task Delete_soft_deletes_root_and_children()
    {
        using var test = new TestDb();
        var store = new PropertyStore(test.Factory);
        var property = new Property { Name = "Flat" };
        property.Parameters.Add(new PropertyParameter { PropertyId = property.Id, Kind = PropertyParameterKind.TotalArea, ValidFrom = new DateOnly(2020, 1, 1), Value = 50m });
        await store.SaveAsync(property);

        await store.DeleteAsync(property.Id);

        Assert.Null(await store.GetAsync(property.Id));
        Assert.Empty(await store.ListAsync());
        await using var db = test.NewContext();
        Assert.True((await db.Properties.IgnoreQueryFilters().SingleAsync()).IsDeleted);
        Assert.True((await db.PropertyParameters.IgnoreQueryFilters().SingleAsync()).IsDeleted);
    }

    [Fact]
    public async Task Delete_of_deep_graph_and_of_missing_id()
    {
        using var test = new TestDb();
        var propertyId = await SavePropertyAsync(test);
        var store = new MeterStore(test.Factory);
        var meter = NewMeter(propertyId, new DateOnly(2020, 1, 1));
        meter.Registers[0].Readings.Add(new MeterReading { RegisterId = meter.Registers[0].Id, Date = new DateOnly(2026, 9, 26), Value = 1m });
        await store.SaveAsync(meter);

        await store.DeleteAsync(meter.Id);
        await store.DeleteAsync(Guid.CreateVersion7());

        Assert.Null(await store.GetAsync(meter.Id));
        await using var db = test.NewContext();
        Assert.True((await db.MeterReadings.IgnoreQueryFilters().SingleAsync()).IsDeleted);
        Assert.True((await db.MeterRegisters.IgnoreQueryFilters().SingleAsync()).IsDeleted);
    }

    internal static Contract NewContract(Guid propertyId, Guid supplierId, Guid categoryId, string title) =>
        new()
        {
            PropertyId = propertyId,
            SupplierId = supplierId,
            CategoryId = categoryId,
            Title = title,
            ValidFrom = new DateOnly(2020, 1, 1),
        };

    internal static Meter NewMeter(Guid propertyId, DateOnly installedOn)
    {
        var meter = new Meter { PropertyId = propertyId, Resource = MeterResource.ColdWater, Unit = MeterUnit.M3, InstalledOn = installedOn, Digits = 6 };
        meter.Registers.Add(MeterRegister.CreateDefault(meter.Id));
        return meter;
    }

    private static Bill NewBill(Guid propertyId, Guid categoryId, YearMonth period, decimal gross) =>
        new() { PropertyId = propertyId, CategoryId = categoryId, Period = period, Gross = gross, Kind = BillKind.OneOff };

    internal static async Task<Guid> SavePropertyAsync(TestDb test)
    {
        var property = new Property { Name = "P" };
        await new PropertyStore(test.Factory).SaveAsync(property);
        return property.Id;
    }

    internal static async Task<Guid> SaveMeterAsync(TestDb test, Guid propertyId)
    {
        var meter = NewMeter(propertyId, new DateOnly(2020, 1, 1));
        await new MeterStore(test.Factory).SaveAsync(meter);
        return meter.Id;
    }

    internal static async Task<(Guid PropertyId, Guid SupplierId, Guid CategoryId)> PrepareContractOwnersAsync(TestDb test)
    {
        var propertyId = await SavePropertyAsync(test);
        var supplier = new Supplier { Name = "Invented Water Ltd" };
        var category = new Category { Name = "Water" };
        await new SupplierStore(test.Factory).SaveAsync(supplier);
        await new CategoryStore(test.Factory).SaveAsync(category);
        return (propertyId, supplier.Id, category.Id);
    }
}
