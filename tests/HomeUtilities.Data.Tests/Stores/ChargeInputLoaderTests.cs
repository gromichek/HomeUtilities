using HomeUtilities.Core.Charges;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Tariffs;
using HomeUtilities.Data.Stores;

namespace HomeUtilities.Data.Tests.Stores;

public class ChargeInputLoaderTests
{
    private static readonly YearMonth Sep2026 = new(2026, 9);

    [Fact]
    public async Task Loaded_input_gives_the_same_water_charge_as_the_in_memory_fixture()
    {
        // Cold 425.900 -> 437.097 and hot 371.927 -> 380.844: 11.197 + 8.917 = 20.114 m3
        // Water supply 20.114 * 1.41 = 28.36 net, VAT 5.96, gross 34.32; sewerage 20.114 * 0.86 = 17.30 net, VAT 3.63, gross 20.93
        // total net 45.66, VAT 9.59, gross 55.25
        using var test = new TestDb();
        var (propertyId, supplierId, categoryId) = await StoreTests.PrepareContractOwnersAsync(test);
        var contract = StoreTests.NewContract(propertyId, supplierId, categoryId, "Water");
        contract.ReadingDayFrom = 20;
        contract.ReadingDayTo = 26;
        await new ContractStore(test.Factory).SaveAsync(contract);

        var cold = Meter(propertyId, 425.900m, 437.097m);
        var hot = Meter(propertyId, 371.927m, 380.844m);
        var meters = new MeterStore(test.Factory);
        await meters.SaveAsync(cold);
        await meters.SaveAsync(hot);
        var contracts = new ContractStore(test.Factory);
        await contracts.SaveContractMeterAsync(new ContractMeter { ContractId = contract.Id, MeterId = cold.Id, ValidFrom = new DateOnly(2020, 1, 1) });
        await contracts.SaveContractMeterAsync(new ContractMeter { ContractId = contract.Id, MeterId = hot.Id, ValidFrom = new DateOnly(2020, 1, 1) });

        var tariffs = new TariffStore(test.Factory);
        await tariffs.SaveAsync(Component(contract.Id, "Water supply", 1.41m, 1));
        await tariffs.SaveAsync(Component(contract.Id, "Sewerage", 0.86m, 2));
        await SeedVatAsync(test);

        var input = await new ChargeInputLoader(test.Factory).LoadAsync(contract.Id, Sep2026);

        Assert.NotNull(input);
        Assert.Equal(2, input.PropertyMeters.Count);
        Assert.Equal(2, input.ContractMeters.Count);
        Assert.Equal(["Water supply", "Sewerage"], input.Components.Select(c => c.Name));
        var charge = ChargeCalculator.Calculate(input);
        Assert.True(charge.IsComplete);
        Assert.Empty(charge.Warnings);
        Assert.Equal((45.66m, 9.59m, 55.25m), (charge.TotalNet, charge.TotalVat, charge.TotalGross));
    }

    [Fact]
    public async Task Missing_contract_returns_null()
    {
        using var test = new TestDb();

        Assert.Null(await new ChargeInputLoader(test.Factory).LoadAsync(Guid.CreateVersion7(), Sep2026));
    }

    private static async Task SeedVatAsync(TestDb test)
    {
        await using var db = test.NewContext();
        await DataSeeder.SeedAsync(db, CancellationToken.None);
    }

    private static TariffComponent Component(Guid contractId, string name, decimal price, int sortOrder)
    {
        var component = new TariffComponent
        {
            ContractId = contractId,
            Name = name,
            ChargeKind = ServiceKind.ColdWater,
            Basis = ChargeBasis.PerUnit,
            VatCode = "LV-STANDARD",
            SortOrder = sortOrder,
        };
        component.Rates.Add(new TariffRate { ComponentId = component.Id, ValidFrom = new DateOnly(2026, 1, 1), Price = price, Currency = Currency.EUR });
        return component;
    }

    private static Meter Meter(Guid propertyId, decimal august, decimal september)
    {
        var meter = StoreTests.NewMeter(propertyId, new DateOnly(2020, 1, 1));
        var register = meter.Registers[0];
        register.Readings.Add(new MeterReading { RegisterId = register.Id, Date = new DateOnly(2026, 8, 26), Value = august });
        register.Readings.Add(new MeterReading { RegisterId = register.Id, Date = new DateOnly(2026, 9, 26), Value = september });
        return meter;
    }
}
