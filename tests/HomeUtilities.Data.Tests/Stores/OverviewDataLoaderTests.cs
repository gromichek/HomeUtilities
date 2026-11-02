using HomeUtilities.Data.Sample;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Charges;
using HomeUtilities.Core.Equalized;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Data.Stores;

namespace HomeUtilities.Data.Tests.Stores;

public class OverviewDataLoaderTests
{
    private static readonly DateOnly Today = new(2026, 10, 20);

    private static async Task<(Property Flat, Contract Water, Meter Meter)> ArrangeAsync(TestDb test)
    {
        using (var db = test.NewContext())
        {
            await DataSeeder.SeedAsync(db, CancellationToken.None);
        }

        var categories = await new CategoryStore(test.Factory).ListAsync();
        var properties = new PropertyStore(test.Factory);
        var flat = new Property { Name = "Flat", SortOrder = 1 };
        await properties.SaveAsync(flat);
        var water = new Contract
        {
            PropertyId = flat.Id,
            SupplierId = await SaveSupplierAsync(test),
            Title = "Water",
            ValidFrom = new DateOnly(2020, 1, 1),
            CategoryId = categories[0].Id,
        };
        await new ContractStore(test.Factory).SaveAsync(water);
        var meter = new Meter { PropertyId = flat.Id, InstalledOn = new DateOnly(2020, 1, 1) };
        var register = new MeterRegister { MeterId = meter.Id };
        register.Readings.Add(new MeterReading { RegisterId = register.Id, Date = Today.AddDays(-150), Value = 10m });
        register.Readings.Add(new MeterReading { RegisterId = register.Id, Date = Today.AddDays(-30), Value = 20m });
        meter.Registers.Add(register);
        await new MeterStore(test.Factory).SaveAsync(meter);
        return (flat, water, meter);
    }

    private static async Task<Guid> SaveSupplierAsync(TestDb test)
    {
        var supplier = new Supplier { Name = "Water utility" };
        await new SupplierStore(test.Factory).SaveAsync(supplier);
        return supplier.Id;
    }

    private static OverviewDataLoader Loader(TestDb test) => new(test.Factory, new ChargeInputLoader(test.Factory));

    [Fact]
    public async Task Loads_properties_contracts_meters_bills_and_payments_without_deleted_rows()
    {
        using var test = new TestDb();
        var (flat, water, meter) = await ArrangeAsync(test);
        var bills = new BillStore(test.Factory);
        var kept = new Bill { PropertyId = flat.Id, ContractId = water.Id, Period = new YearMonth(2026, 9), Gross = 50m, CategoryId = water.CategoryId };
        var removed = new Bill { PropertyId = flat.Id, ContractId = water.Id, Period = new YearMonth(2026, 8), Gross = 40m, CategoryId = water.CategoryId };
        await bills.SaveAsync(kept);
        await bills.SaveAsync(removed);
        await bills.DeleteAsync(removed.Id);
        var payment = new Payment { ContractId = water.Id, PaidOn = Today, Amount = 50m };
        payment.Allocations.Add(new PaymentAllocation { PaymentId = payment.Id, BillId = kept.Id, Amount = 50m });
        await new PaymentStore(test.Factory).SaveAsync(payment);

        var data = await Loader(test).LoadAsync(Today);

        Assert.Equal(flat.Id, Assert.Single(data.Properties).Id);
        Assert.Equal(water.Id, Assert.Single(data.Contracts).Id);
        Assert.Equal(meter.Id, Assert.Single(data.Meters).Id);
        Assert.Equal(kept.Id, Assert.Single(data.Bills).Id);
        var loaded = Assert.Single(data.Payments);
        Assert.Equal(50m, Assert.Single(loaded.Allocations).Amount);
        Assert.Empty(data.ExpectedSettlements);
    }

    [Fact]
    public async Task Archived_properties_are_left_out()
    {
        using var test = new TestDb();
        var (flat, _, _) = await ArrangeAsync(test);
        var properties = new PropertyStore(test.Factory);
        var old = new Property { Name = "Old", IsArchived = true };
        await properties.SaveAsync(old);

        var data = await Loader(test).LoadAsync(Today);

        Assert.Equal([flat.Id], data.Properties.Select(p => p.Id));
    }

    [Fact]
    public async Task Only_recent_readings_are_loaded()
    {
        using var test = new TestDb();
        await ArrangeAsync(test);

        var data = await Loader(test).LoadAsync(Today);

        var reading = Assert.Single(Assert.Single(Assert.Single(data.Meters).Registers).Readings);
        Assert.Equal(Today.AddDays(-30), reading.Date);
    }

    [Fact]
    public async Task Expected_settlement_of_a_long_history_covers_only_the_last_two_years()
    {
        using var test = new TestDb();
        var (_, water, _) = await ArrangeAsync(test);
        water.BillingMode = BillingMode.Equalized;
        await new ContractStore(test.Factory).SaveAsync(water);
        await new EqualizedPlanStore(test.Factory).SaveAsync(new EqualizedPlan { ContractId = water.Id, ValidFrom = new DateOnly(2007, 1, 1), MonthlyAmount = 30m });
        var counter = new CountingChargeInputLoader(new ChargeInputLoader(test.Factory));

        await new OverviewDataLoader(test.Factory, counter).LoadAsync(Today);

        Assert.Equal(OverviewDataLoader.SettlementMaxMonths, counter.Months.Count);
        Assert.Equal(new YearMonth(2024, 11), counter.Months.Min());
        Assert.Equal(new YearMonth(2026, 10), counter.Months.Max());
    }

    private sealed class CountingChargeInputLoader(IChargeInputLoader inner) : IChargeInputLoader
    {
        public List<YearMonth> Months { get; } = [];

        public Task<ChargeInput?> LoadAsync(Guid contractId, YearMonth period, CancellationToken ct = default)
        {
            Months.Add(period);
            return inner.LoadAsync(contractId, period, ct);
        }
    }

    /// <summary>Counts how the Overview loader reads charge inputs.</summary>
    private sealed class CountingChargeLoader(IChargeInputLoader inner) : IChargeInputLoader
    {
        public int SingleCalls { get; private set; }

        public int BatchCalls { get; private set; }

        public int Requested { get; private set; }

        public Task<ChargeInput?> LoadAsync(Guid contractId, YearMonth period, CancellationToken ct = default)
        {
            SingleCalls++;
            return inner.LoadAsync(contractId, period, ct);
        }

        public Task<IReadOnlyDictionary<(Guid ContractId, YearMonth Period), ChargeInput>> LoadManyAsync(
            IReadOnlyCollection<(Guid ContractId, YearMonth Period)> requests, CancellationToken ct = default)
        {
            BatchCalls++;
            Requested += requests.Count;
            return inner.LoadManyAsync(requests, ct);
        }
    }

    [Fact]
    public async Task Settlements_of_all_equalized_contracts_are_read_with_one_batched_charge_query()
    {
        using var test = new TestDb();
        test.Clock.UtcNow = new DateTimeOffset(2026, 10, 20, 8, 0, 0, TimeSpan.Zero);
        using (var db = test.NewContext())
        {
            await DataSeeder.SeedAsync(db, CancellationToken.None);
        }

        var factory = test.Factory;
        var generator = new SampleDataGenerator(
            new PropertyStore(factory), new SupplierStore(factory), new CategoryStore(factory), new ContractStore(factory),
            new MeterStore(factory), new TariffStore(factory), new EqualizedPlanStore(factory), new BillTemplateStore(factory, test.Clock), new BillStore(factory),
            new PaymentStore(factory), new ImportProfileStore(factory), new ChargeInputLoader(factory), test.Clock);
        Assert.True(await generator.FillAsync());

        var counting = new CountingChargeLoader(new ChargeInputLoader(factory));
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var data = await new OverviewDataLoader(factory, counting).LoadAsync(test.Clock.Today);
        watch.Stop();

        Assert.NotEmpty(data.ExpectedSettlements);
        Assert.Equal(0, counting.SingleCalls);
        Assert.Equal(1, counting.BatchCalls);
        Assert.True(counting.Requested <= OverviewDataLoader.SettlementMaxMonths * data.ExpectedSettlements.Count);

        // Only a limit for gross slowdowns: CI machines vary.
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10), $"Overview load took {watch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public async Task Batch_load_gives_the_same_input_as_the_single_load()
    {
        using var test = new TestDb();
        var (_, water, _) = await ArrangeAsync(test);
        var loader = new ChargeInputLoader(test.Factory);
        var month = new YearMonth(2026, 9);
        var other = new YearMonth(2026, 8);

        var single = await loader.LoadAsync(water.Id, month);
        var many = await loader.LoadManyAsync([(water.Id, month), (water.Id, other), (Guid.NewGuid(), month)]);

        Assert.Equal(2, many.Count);
        Assert.NotNull(single);
        Assert.Equal(single.Contract.Id, many[(water.Id, month)].Contract.Id);
        Assert.Equal(single.PropertyMeters.Count, many[(water.Id, month)].PropertyMeters.Count);
        Assert.Equal(other, many[(water.Id, other)].Period);
    }
}
