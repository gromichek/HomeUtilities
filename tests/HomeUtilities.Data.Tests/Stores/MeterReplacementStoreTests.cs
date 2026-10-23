using HomeUtilities.Core.Domain;
using HomeUtilities.Data.Stores;
using Microsoft.EntityFrameworkCore;

namespace HomeUtilities.Data.Tests.Stores;

public class MeterReplacementStoreTests
{
    private static readonly DateOnly Day = new(2026, 10, 23);

    [Fact]
    public async Task SaveReplacement_stores_both_meters_readings_and_moves_the_links()
    {
        using var test = new TestDb();
        var (propertyId, contract) = await PrepareAsync(test);
        var meters = new MeterStore(test.Factory);
        var contracts = new ContractStore(test.Factory);
        var old = StoreTests.NewMeter(propertyId, new DateOnly(2020, 10, 1));
        old.Registers[0].Readings.Add(new MeterReading { RegisterId = old.Registers[0].Id, Date = new DateOnly(2026, 9, 26), Value = 190m });
        await meters.SaveAsync(old);
        var oldLink = new ContractMeter { ContractId = contract.Id, MeterId = old.Id, ValidFrom = new DateOnly(2020, 10, 1) };
        await contracts.SaveContractMeterAsync(oldLink);

        var loaded = (await meters.GetAsync(old.Id))!;
        var result = MeterReplacement.Replace(loaded, Day, 202m, 0m, test.Clock);
        oldLink.ValidTo = Day.AddDays(-1);
        var newLink = new ContractMeter { ContractId = contract.Id, MeterId = result.NewMeter.Id, ValidFrom = Day };
        await meters.SaveReplacementAsync(loaded, result.NewMeter, [oldLink], [newLink]);

        var oldStored = (await meters.GetAsync(old.Id))!;
        Assert.Equal(Day, oldStored.RemovedOn);
        Assert.Equal([190m, 202m], oldStored.Registers[0].Readings.Select(r => r.Value));
        Assert.Equal(ReadingKind.Final, oldStored.Registers[0].Readings[1].Kind);

        var newStored = (await meters.GetAsync(result.NewMeter.Id))!;
        Assert.Equal((old.Id, Day), (newStored.ReplacedMeterId, newStored.InstalledOn));
        var initial = Assert.Single(newStored.Registers[0].Readings);
        Assert.Equal((ReadingKind.Initial, 0m), (initial.Kind, initial.Value));

        var links = await contracts.ListContractMetersAsync(contract.Id);
        Assert.Equal(2, links.Count);
        Assert.Equal(Day.AddDays(-1), links.Single(l => l.MeterId == old.Id).ValidTo);
        Assert.Null(links.Single(l => l.MeterId == result.NewMeter.Id).ValidTo);
        Assert.Equal(2, (await meters.ListByPropertyAsync(propertyId)).Count);
    }

    [Fact]
    public async Task SaveReplacement_is_atomic_when_a_later_save_fails()
    {
        using var test = new TestDb();
        var (propertyId, contract) = await PrepareAsync(test);
        var meters = new MeterStore(test.Factory);
        var old = StoreTests.NewMeter(propertyId, new DateOnly(2020, 10, 1));
        await meters.SaveAsync(old);

        var loaded = (await meters.GetAsync(old.Id))!;
        var result = MeterReplacement.Replace(loaded, Day, 202m, 0m, test.Clock);
        // The second link points at a contract that does not exist: the foreign key makes the last save fail.
        var good = new ContractMeter { ContractId = contract.Id, MeterId = result.NewMeter.Id, ValidFrom = Day };
        var bad = new ContractMeter { ContractId = Guid.NewGuid(), MeterId = result.NewMeter.Id, ValidFrom = Day };

        await Assert.ThrowsAnyAsync<Exception>(() => meters.SaveReplacementAsync(loaded, result.NewMeter, [], [good, bad]));

        var stored = Assert.Single(await meters.ListByPropertyAsync(propertyId));
        Assert.Equal(old.Id, stored.Id);
        Assert.Null(stored.RemovedOn);
        Assert.Empty(stored.Registers[0].Readings);
        await using var db = test.NewContext();
        Assert.Empty(await db.ContractMeters.IgnoreQueryFilters().ToListAsync());
        Assert.Single(await db.Meters.IgnoreQueryFilters().ToListAsync());
    }

    private static async Task<(Guid PropertyId, Contract Contract)> PrepareAsync(TestDb test)
    {
        var (propertyId, supplierId, categoryId) = await StoreTests.PrepareContractOwnersAsync(test);
        var contract = new Contract
        {
            PropertyId = propertyId,
            SupplierId = supplierId,
            CategoryId = categoryId,
            Title = "Water",
            ValidFrom = new DateOnly(2020, 1, 1),
        };
        await new ContractStore(test.Factory).SaveAsync(contract);
        return (propertyId, contract);
    }
}
