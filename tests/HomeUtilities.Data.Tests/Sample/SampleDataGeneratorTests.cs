using HomeUtilities.Core.Common;
using HomeUtilities.Core.Consumption;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Payments;
using HomeUtilities.Data.Sample;
using HomeUtilities.Data.Stores;
using Microsoft.EntityFrameworkCore;

namespace HomeUtilities.Data.Tests.Sample;

public class SampleDataGeneratorTests
{
    private sealed class Rig : IDisposable
    {
        private readonly TestDb _test = new();

        public Rig()
        {
            using var db = _test.NewContext();
            DataSeeder.SeedAsync(db, CancellationToken.None).GetAwaiter().GetResult();
            Properties = new PropertyStore(_test.Factory);
            Suppliers = new SupplierStore(_test.Factory);
            Contracts = new ContractStore(_test.Factory);
            Meters = new MeterStore(_test.Factory);
            Generator = new SampleDataGenerator(Properties, Suppliers, new CategoryStore(_test.Factory), Contracts, Meters, _test.Clock);
        }

        public PropertyStore Properties { get; }

        public SupplierStore Suppliers { get; }

        public ContractStore Contracts { get; }

        public MeterStore Meters { get; }

        public SampleDataGenerator Generator { get; }

        public FakeClock Clock => _test.Clock;

        public HomeUtilitiesDbContext NewContext() => _test.NewContext();

        public void Dispose() => _test.Dispose();
    }

    [Fact]
    public async Task Fill_on_an_empty_database_creates_the_expected_data()
    {
        using var rig = new Rig();

        Assert.True(await rig.Generator.FillAsync());

        var properties = await rig.Properties.ListAsync(true);
        Assert.Equal(["Flat in town", "Cottage", "Garage"], properties.Select(p => p.Name));
        var flat = properties[0];
        Assert.Equal(52.3m, flat.GetParameter(PropertyParameterKind.TotalArea, rig.Clock.Today));
        Assert.Equal(3m, flat.GetParameter(PropertyParameterKind.DeclaredResidents, rig.Clock.Today));
        Assert.Equal(2m, flat.GetParameter(PropertyParameterKind.DeclaredResidents, rig.Clock.Today.AddMonths(-7)));

        var contracts = await rig.Contracts.ListAsync();
        Assert.Equal(10, contracts.Count);
        Assert.Equal([6, 2, 2], properties.Select(p => contracts.Count(c => c.PropertyId == p.Id)));
        Assert.Contains(contracts, c => c.Service == ServiceKind.InternetTv && c.BillingTiming == HomeUtilities.Core.Billing.BillingTiming.InAdvance);
        Assert.Contains(contracts, c => c is { ReadingDayFrom: 25, ReadingDayTo: 28 });
        Assert.Contains(contracts, c => c is { ReadingDayFrom: 28, ReadingDayTo: 3 });
    }

    [Fact]
    public async Task Everything_passes_the_validators_and_uses_fictitious_payment_data()
    {
        using var rig = new Rig();
        await rig.Generator.FillAsync();

        foreach (var property in await rig.Properties.ListAsync(true))
        {
            Assert.True(PropertyValidator.Validate(property).IsValid, property.Name);
        }

        var suppliers = await rig.Suppliers.ListAsync();
        Assert.Equal(7, suppliers.Count);
        foreach (var supplier in suppliers)
        {
            Assert.True(SupplierValidator.Validate(supplier).IsValid, supplier.Name);
            Assert.True(Iban.IsValid(supplier.PaymentDetails!.Iban!), supplier.Name);
            Assert.All(supplier.Links, l => Assert.EndsWith(".example.com", l.TryGetUri()!.Host.Split('/')[0]));
            Assert.Contains(supplier.Links, l => l.Kind == LinkKind.SubmitReadings);
        }

        foreach (var contract in await rig.Contracts.ListAsync())
        {
            Assert.True(ContractValidator.Validate(contract).IsValid, contract.Title);
        }

        var meters = await AllMetersAsync(rig);
        foreach (var meter in meters)
        {
            Assert.True(MeterValidator.Validate(meter).IsValid, meter.Location);
            foreach (var register in meter.Registers)
            {
                foreach (var reading in register.Readings)
                {
                    Assert.True(MeterReadingValidator.Validate(reading, register, meter).IsValid, $"{meter.Location} {reading.Date}");
                }
            }
        }

        await using var db = rig.NewContext();
        foreach (var link in await db.ContractMeters.ToListAsync())
        {
            Assert.True(ContractMeterValidator.Validate(link).IsValid);
        }
    }

    [Fact]
    public async Task The_flat_has_four_water_meters_in_use_and_one_replacement_chain()
    {
        using var rig = new Rig();
        await rig.Generator.FillAsync();
        var flat = (await rig.Properties.ListAsync(true))[0];

        var meters = await rig.Meters.ListByPropertyAsync(flat.Id);
        var water = meters.Where(m => m.Resource is MeterResource.ColdWater or MeterResource.HotWater).ToList();
        Assert.Equal(4, water.Count(m => m.RemovedOn is null));
        Assert.Equal(5, water.Count);
        Assert.Equal(2, water.Count(m => m.Resource == MeterResource.HotWater));
        var replaced = Assert.Single(water, m => m.RemovedOn is not null);
        var successor = Assert.Single(water, m => m.ReplacedMeterId == replaced.Id);
        Assert.Equal(replaced.RemovedOn, successor.InstalledOn);
        Assert.Equal(ReadingKind.Final, replaced.Registers[0].Readings.Last().Kind);
        Assert.Equal(ReadingKind.Initial, successor.Registers[0].Readings.First().Kind);
    }

    [Fact]
    public async Task Hot_water_meters_are_linked_to_water_and_heating_contracts()
    {
        using var rig = new Rig();
        await rig.Generator.FillAsync();
        var flat = (await rig.Properties.ListAsync(true))[0];
        var contracts = await rig.Contracts.ListByPropertyAsync(flat.Id);
        var water = contracts.Single(c => c.Service == ServiceKind.ColdWater);
        var heat = contracts.Single(c => c.Service == ServiceKind.Heating);
        var hot = (await rig.Meters.ListByPropertyAsync(flat.Id)).Where(m => m.Resource == MeterResource.HotWater && m.RemovedOn is null).Select(m => m.Id).ToList();

        var waterLinks = (await rig.Contracts.ListContractMetersAsync(water.Id)).Select(l => l.MeterId).ToList();
        var heatLinks = (await rig.Contracts.ListContractMetersAsync(heat.Id)).Select(l => l.MeterId).ToList();

        Assert.Equal(2, hot.Count);
        Assert.All(hot, id => Assert.Contains(id, waterLinks));
        Assert.Equal(hot.Order(), heatLinks.Order());
    }

    [Fact]
    public async Task Consumption_over_the_replaced_meter_chain_adds_up_both_meters()
    {
        using var rig = new Rig();
        await rig.Generator.FillAsync();
        var flat = (await rig.Properties.ListAsync(true))[0];
        var meters = await rig.Meters.ListByPropertyAsync(flat.Id);
        var replaced = meters.Single(m => m.ReplacedMeterId is null && m.RemovedOn is not null);
        var successor = meters.Single(m => m.ReplacedMeterId == replaced.Id);
        var chain = MeterChain.Build(meters, successor.Id);
        var from = replaced.Registers[0].Readings.First().Date;
        var to = successor.Registers[0].Readings.Last().Date;

        var result = ConsumptionCalculator.Calculate(chain, RegisterKind.Total, from, to);

        var oldPart = replaced.Registers[0].Readings.Last().Value - replaced.Registers[0].Readings.First().Value;
        var newPart = successor.Registers[0].Readings.Last().Value - successor.Registers[0].Readings.First().Value;
        Assert.True(result.IsComplete);
        Assert.Equal(oldPart + newPart, result.Quantity);
        Assert.True(result.Quantity > 20m);
    }

    [Fact]
    public async Task One_meter_rolls_over_and_consumption_stays_positive()
    {
        using var rig = new Rig();
        await rig.Generator.FillAsync();
        var flat = (await rig.Properties.ListAsync(true))[0];
        var meters = await rig.Meters.ListByPropertyAsync(flat.Id);
        var power = meters.Single(m => m.Resource == MeterResource.Electricity);
        var values = power.Registers[0].Readings.OrderBy(r => r.Date).Select(r => r.Value).ToList();
        Assert.Equal(5, power.Digits);
        Assert.Contains(values.Zip(values.Skip(1)), p => p.Second < p.First);

        var chain = MeterChain.Build(meters, power.Id);
        var readings = power.Registers[0].Readings.OrderBy(r => r.Date).ToList();
        var result = ConsumptionCalculator.Calculate(chain, RegisterKind.Total, readings[0].Date, readings[^1].Date);

        Assert.InRange(result.Quantity, 1_000m, 2_000m);
        Assert.True(result.IsComplete);
    }

    [Fact]
    public async Task One_meter_has_an_overdue_verification_and_the_cottage_meter_has_an_export_register()
    {
        using var rig = new Rig();
        await rig.Generator.FillAsync();
        var properties = await rig.Properties.ListAsync(true);
        var all = await AllMetersAsync(rig);

        Assert.Equal(1, all.Count(m => MeterVerification.GetStatus(m, rig.Clock.Today) == VerificationStatus.Overdue));
        var cottage = Assert.Single(all, m => m.PropertyId == properties[1].Id);
        Assert.Equal([RegisterKind.Day, RegisterKind.Night, RegisterKind.Export], cottage.Registers.Select(r => r.Kind));
        Assert.Equal(EnergyDirection.Export, cottage.Registers[2].Direction);
        Assert.DoesNotContain(all, m => m.PropertyId == properties[2].Id);
    }

    [Fact]
    public async Task Readings_cover_thirteen_months_with_submitted_and_estimated_ones()
    {
        using var rig = new Rig();
        await rig.Generator.FillAsync();
        var flat = (await rig.Properties.ListAsync(true))[0];
        var meters = await rig.Meters.ListByPropertyAsync(flat.Id);
        var kitchen = meters.Single(m => m.Location == "Kitchen cold");
        var readings = kitchen.Registers[0].Readings.OrderBy(r => r.Date).ToList();

        Assert.Equal(HomeUtilities.Data.Sample.SampleDataGenerator.MonthsOfReadings, readings.Count);
        Assert.All(readings, r => Assert.True(r.Date <= rig.Clock.Today));
        Assert.Single(readings, r => r.Kind == ReadingKind.Estimated);
        Assert.Contains(readings, r => r.SubmittedOn is not null);
        Assert.Contains(readings, r => r.SubmittedOn is null);
        Assert.Equal(readings.Select(r => r.Value).Order(), readings.Select(r => r.Value));
    }

    [Fact]
    public async Task Fill_is_refused_when_a_property_exists_and_changes_nothing()
    {
        using var rig = new Rig();
        await rig.Properties.SaveAsync(new Property { Name = "Mine", Kind = PropertyKind.House });

        Assert.False(await rig.Generator.FillAsync());

        Assert.Equal(["Mine"], (await rig.Properties.ListAsync(true)).Select(p => p.Name));
        Assert.Empty(await rig.Suppliers.ListAsync());
    }

    [Fact]
    public async Task Output_is_deterministic_for_a_fixed_today()
    {
        string[] Snapshot(IEnumerable<Meter> meters) =>
            [.. meters.OrderBy(m => m.Location).SelectMany(m => m.Registers.OrderBy(r => r.Kind)
                .SelectMany(r => r.Readings.OrderBy(x => x.Date).Select(x => $"{m.Location}|{r.Kind}|{x.Date:O}|{x.Value}|{x.Kind}")))];

        using var first = new Rig();
        using var second = new Rig();
        await first.Generator.FillAsync();
        await second.Generator.FillAsync();

        var a = Snapshot(await AllMetersAsync(first));
        var b = Snapshot(await AllMetersAsync(second));
        Assert.NotEmpty(a);
        Assert.Equal(a, b);
    }

    private static async Task<List<Meter>> AllMetersAsync(Rig rig)
    {
        var result = new List<Meter>();
        foreach (var property in await rig.Properties.ListAsync(true))
        {
            result.AddRange(await rig.Meters.ListByPropertyAsync(property.Id));
        }

        return result;
    }
}
