using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Tariffs;

namespace HomeUtilities.Core.Tests.Tariffs;

public class TariffPresetsTests
{
    private static readonly Guid ContractId = Guid.CreateVersion7();

    private static IReadOnlyList<TariffComponent> Create(TariffPreset preset, IReadOnlyCollection<Guid>? hot = null, int first = 0) =>
        TariffPresets.Create(preset, ContractId, ServiceKind.Maintenance, key => "name:" + key, hot ?? [], first);

    [Theory]
    [InlineData(TariffPreset.WaterSewerage)]
    [InlineData(TariffPreset.WaterHeatingAndHeating)]
    [InlineData(TariffPreset.Electricity)]
    [InlineData(TariffPreset.AreaFee)]
    [InlineData(TariffPreset.PerResident)]
    [InlineData(TariffPreset.FixedMonthly)]
    [InlineData(TariffPreset.HotWaterAndHeatingDistributed)]
    public void Every_preset_passes_the_validator_once_a_price_is_filled(TariffPreset preset)
    {
        foreach (var component in Create(preset))
        {
            if (component.Basis != ChargeBasis.FromBill)
            {
                Assert.Equal([ErrorCodes.TariffRateRequired], TariffComponentValidator.Validate(component).Errors.Select(e => e.Code));
                component.Rates.Add(new TariffRate { ComponentId = component.Id, ValidFrom = new(2026, 1, 1), Price = 1m });
            }

            Assert.True(TariffComponentValidator.Validate(component).IsValid, component.Name);
        }
    }

    [Fact]
    public void Water_and_sewerage_has_two_per_unit_components_counting_all_meters()
    {
        var components = Create(TariffPreset.WaterSewerage);

        Assert.Equal(["name:WaterSupply", "name:Sewerage"], components.Select(c => c.Name));
        Assert.All(components, c =>
        {
            Assert.Equal(ChargeBasis.PerUnit, c.Basis);
            Assert.Empty(c.MeterIds);
            Assert.Empty(c.RegisterKinds);
            Assert.Equal("LV-STANDARD", c.VatCode);
            Assert.Equal(ContractId, c.ContractId);
            Assert.Empty(c.Rates);
        });
    }

    [Fact]
    public void Water_heating_counts_only_the_hot_meters_and_uses_reduced_vat()
    {
        var hot = new[] { Guid.CreateVersion7(), Guid.CreateVersion7() };

        var components = Create(TariffPreset.WaterHeatingAndHeating, hot);

        Assert.Equal(ChargeBasis.PerUnit, components[0].Basis);
        Assert.Equal(hot, components[0].MeterIds);
        Assert.Equal("LV-REDUCED", components[0].VatCode);
        Assert.Equal(ChargeBasis.FromBill, components[1].Basis);
        Assert.Empty(components[1].MeterIds);
    }

    [Fact]
    public void Hot_water_and_heating_distributed_has_hot_meters_and_two_quantities_from_the_bill()
    {
        var hot = new[] { Guid.CreateVersion7() };

        var components = Create(TariffPreset.HotWaterAndHeatingDistributed, hot);

        Assert.Equal(["name:HotWater", "name:HeatCirculation", "name:HotWaterDifference"], components.Select(c => c.Name));
        Assert.Equal([ChargeBasis.PerUnit, ChargeBasis.QuantityFromBill, ChargeBasis.QuantityFromBill], components.Select(c => c.Basis));
        Assert.Equal(hot, components[0].MeterIds);
        Assert.Empty(components[1].MeterIds);
        Assert.Equal([null, MeterUnit.MWh, MeterUnit.M3], components.Select(c => c.QuantityUnit));
        Assert.All(components, c => Assert.Equal("LV-REDUCED", c.VatCode));
        Assert.Equal([ServiceKind.WaterHeating, ServiceKind.Heating, ServiceKind.WaterHeating], components.Select(c => c.ChargeKind));
    }

    [Fact]
    public void Generic_presets_take_the_service_of_the_contract()
    {
        Assert.Equal(ChargeBasis.PerArea, Assert.Single(Create(TariffPreset.AreaFee)).Basis);
        Assert.Equal(ChargeBasis.PerResident, Assert.Single(Create(TariffPreset.PerResident)).Basis);
        Assert.Equal(ChargeBasis.FixedMonthly, Assert.Single(Create(TariffPreset.FixedMonthly)).Basis);
        Assert.All(
            new[] { TariffPreset.AreaFee, TariffPreset.PerResident, TariffPreset.FixedMonthly },
            p => Assert.Equal(ServiceKind.Maintenance, Create(p)[0].ChargeKind));
        Assert.Null(Create(TariffPreset.AreaFee)[0].QuantityParameter);
    }

    [Fact]
    public void Sort_orders_continue_from_the_first_one()
    {
        Assert.Equal([5, 6], Create(TariffPreset.Electricity, first: 5).Select(c => c.SortOrder));
    }
}
