using HomeUtilities.Core.Domain;
using HomeUtilities.Data.Stores;
using Microsoft.EntityFrameworkCore;

namespace HomeUtilities.Data.Tests.Stores;

public class MeterReadingStoreTests
{
    private static readonly DateOnly Day = new(2026, 10, 23);

    private static async Task<(MeterStore Store, Meter Meter)> NewStoreWithMeterAsync(TestDb test, Guid propertyId)
    {
        var store = new MeterStore(test.Factory, test.Clock);
        var meter = StoreTests.NewMeter(propertyId, new DateOnly(2020, 10, 1));
        meter.Registers.Add(new MeterRegister { MeterId = meter.Id, Kind = RegisterKind.Day, Direction = EnergyDirection.Import });
        await store.SaveAsync(meter);
        return (store, meter);
    }

    [Fact]
    public async Task AddReadings_stores_all_readings_of_all_registers()
    {
        using var test = new TestDb();
        var (store, meter) = await NewStoreWithMeterAsync(test, await StoreTests.SavePropertyAsync(test));
        var total = meter.Registers[0];
        var day = meter.Registers[1];

        await store.AddReadingsAsync(
        [
            (total.Id, new MeterReading { Date = Day, Value = 445.000m, Kind = ReadingKind.Regular, Note = "bathroom" }),
            (day.Id, new MeterReading { Date = Day, Value = 12.5m, Kind = ReadingKind.Regular }),
        ]);

        var stored = (await store.GetAsync(meter.Id))!;
        var totalReading = Assert.Single(stored.Registers.Single(r => r.Id == total.Id).Readings);
        Assert.Equal((total.Id, Day, 445.000m, "bathroom"), (totalReading.RegisterId, totalReading.Date, totalReading.Value, totalReading.Note));
        Assert.Equal(12.5m, Assert.Single(stored.Registers.Single(r => r.Id == day.Id).Readings).Value);
    }

    [Fact]
    public async Task AddReadings_is_atomic_when_a_register_does_not_exist()
    {
        using var test = new TestDb();
        var (store, meter) = await NewStoreWithMeterAsync(test, await StoreTests.SavePropertyAsync(test));

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.AddReadingsAsync(
        [
            (meter.Registers[0].Id, new MeterReading { Date = Day, Value = 445m }),
            (Guid.NewGuid(), new MeterReading { Date = Day, Value = 1m }),
        ]));

        await using var db = test.NewContext();
        Assert.Empty(await db.MeterReadings.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task AddReadings_is_atomic_when_a_later_insert_fails()
    {
        using var test = new TestDb();
        var (store, meter) = await NewStoreWithMeterAsync(test, await StoreTests.SavePropertyAsync(test));
        var shared = Guid.NewGuid();

        // The second reading repeats the id of the first: the primary key makes the single save fail as a whole.
        await Assert.ThrowsAnyAsync<Exception>(() => store.AddReadingsAsync(
        [
            (meter.Registers[0].Id, new MeterReading { Id = shared, Date = Day, Value = 445m }),
            (meter.Registers[1].Id, new MeterReading { Id = shared, Date = Day, Value = 1m }),
        ]));

        await using var db = test.NewContext();
        Assert.Empty(await db.MeterReadings.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task UpdateReadings_changes_the_values_and_keeps_the_register()
    {
        using var test = new TestDb();
        var (store, meter) = await NewStoreWithMeterAsync(test, await StoreTests.SavePropertyAsync(test));
        var reading = new MeterReading { Date = Day, Value = 445m };
        await store.AddReadingsAsync([(meter.Registers[0].Id, reading)]);

        await store.UpdateReadingsAsync(
        [
            new MeterReading
            {
                Id = reading.Id,
                RegisterId = Guid.NewGuid(),
                Date = Day.AddDays(1),
                Value = 446.5m,
                Kind = ReadingKind.Control,
                SubmittedOn = Day.AddDays(2),
                Note = "checked",
            },
        ]);

        var stored = Assert.Single((await store.GetAsync(meter.Id))!.Registers[0].Readings);
        Assert.Equal((meter.Registers[0].Id, Day.AddDays(1), 446.5m), (stored.RegisterId, stored.Date, stored.Value));
        Assert.Equal((ReadingKind.Control, Day.AddDays(2), "checked"), (stored.Kind, stored.SubmittedOn, stored.Note));
    }

    [Fact]
    public async Task UpdateReadings_of_an_unknown_reading_throws_and_changes_nothing()
    {
        using var test = new TestDb();
        var (store, meter) = await NewStoreWithMeterAsync(test, await StoreTests.SavePropertyAsync(test));
        var reading = new MeterReading { Date = Day, Value = 445m };
        await store.AddReadingsAsync([(meter.Registers[0].Id, reading)]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.UpdateReadingsAsync(
        [
            new MeterReading { Id = reading.Id, Date = Day, Value = 999m },
            new MeterReading { Date = Day, Value = 1m },
        ]));

        Assert.Equal(445m, Assert.Single((await store.GetAsync(meter.Id))!.Registers[0].Readings).Value);
    }

    [Fact]
    public async Task DeleteReading_soft_deletes_only_that_reading()
    {
        using var test = new TestDb();
        var (store, meter) = await NewStoreWithMeterAsync(test, await StoreTests.SavePropertyAsync(test));
        var first = new MeterReading { Date = Day, Value = 445m };
        var second = new MeterReading { Date = Day.AddDays(30), Value = 450m };
        await store.AddReadingsAsync([(meter.Registers[0].Id, first), (meter.Registers[0].Id, second)]);

        await store.DeleteReadingAsync(first.Id);
        await store.DeleteReadingAsync(Guid.NewGuid());

        Assert.Equal(450m, Assert.Single((await store.GetAsync(meter.Id))!.Registers[0].Readings).Value);
        await using var db = test.NewContext();
        Assert.True((await db.MeterReadings.IgnoreQueryFilters().SingleAsync(r => r.Id == first.Id)).IsDeleted);
    }

    [Fact]
    public async Task Deleting_a_meter_closes_its_open_links_today()
    {
        using var test = new TestDb();
        var (propertyId, contract) = await MeterReplacementStoreTests.PrepareAsync(test);
        var (store, meter) = await NewStoreWithMeterAsync(test, propertyId);
        var contracts = new ContractStore(test.Factory);
        var today = test.Clock.Today;
        var open = new ContractMeter { ContractId = contract.Id, MeterId = meter.Id, ValidFrom = new DateOnly(2020, 10, 1) };
        var future = new ContractMeter { ContractId = contract.Id, MeterId = meter.Id, ValidFrom = today.AddDays(10) };
        var ended = new ContractMeter { ContractId = contract.Id, MeterId = meter.Id, ValidFrom = new DateOnly(2019, 1, 1), ValidTo = new DateOnly(2020, 9, 30) };
        await contracts.SaveContractMeterAsync(open);
        await contracts.SaveContractMeterAsync(future);
        await contracts.SaveContractMeterAsync(ended);

        await store.DeleteAsync(meter.Id);

        Assert.Null(await store.GetAsync(meter.Id));
        var links = (await contracts.ListContractMetersAsync(contract.Id)).ToDictionary(l => l.Id);
        Assert.Equal(today, links[open.Id].ValidTo);
        Assert.Equal(future.ValidFrom, links[future.Id].ValidTo);
        Assert.Equal(new DateOnly(2020, 9, 30), links[ended.Id].ValidTo);
    }
}
