using HomeUtilities.Core.Common;
using HomeUtilities.Core.Consumption;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Import;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Data.Stores;
using HomeUtilities.Import;
using HomeUtilities.Import.Tests;
using Microsoft.EntityFrameworkCore;

namespace HomeUtilities.Data.Tests.Import;

/// <summary>T-027: automatic meter replacements are stored with the import and removed again by the undo.</summary>
public class ImportReplacementStoreTests
{
    private sealed class Rig : IDisposable
    {
        public required TestDb Test { get; init; }
        public required Contract Contract { get; init; }
        public required Meter Cold { get; init; }
        public required Meter Hot { get; init; }
        public required MeterStore Meters { get; init; }
        public required ContractStore Contracts { get; init; }
        public required BillStore Bills { get; init; }
        public required ImportBatchStore Batches { get; init; }

        public void Dispose() => Test.Dispose();

        public ImportProfile WaterProfile() => new()
        {
            SheetName = OwnerWorkbook.Water,
            ContractId = Contract.Id,
            PeriodColumn = 1,
            PeriodRule = ImportPeriodRule.ReadingWindow,
            Registers =
            [
                new RegisterColumnMapping { RegisterId = Cold.Registers[0].Id, ReadingColumn = 2 },
                new RegisterColumnMapping { RegisterId = Hot.Registers[0].Id, ReadingColumn = 3 },
            ],
            QuantityColumn = 4,
            TotalColumn = 6,
            TotalFallbackColumn = 5,
            PaidColumn = 7,
            NotesColumns = [8],
            CurrencyRule = ImportCurrencyRule.Eur,
        };

        public async Task<(ImportPlan Plan, ImportBatch Batch)> ImportAsync()
        {
            using var stream = OwnerWorkbook.Build();
            var sheet = new ClosedXmlWorkbookReader().ReadSheet(stream, OwnerWorkbook.Water);
            var meters = await Meters.ListByPropertyAsync(Contract.PropertyId);
            var links = await Contracts.ListContractMetersAsync(Contract.Id);
            var contract = (await Contracts.GetAsync(Contract.Id))!;
            var plan = ImportPlanner.Plan(WaterProfile(), sheet, new ImportContext(contract, meters, await Bills.ListByContractAsync(Contract.Id), links));
            var batch = await Batches.ApplyAsync(new ImportApplyRequest(plan, "owner.xlsx", OwnerWorkbook.Water, Contract.Id, null));
            return (plan, batch);
        }

        public async Task<MeterChain> ChainAsync(Meter first)
        {
            var meters = await Meters.ListByPropertyAsync(Contract.PropertyId);
            return MeterChain.Build(meters, first.Id);
        }
    }

    private static async Task<Rig> ArrangeAsync()
    {
        var test = new TestDb();
        using (var db = test.NewContext())
        {
            await DataSeeder.SeedAsync(db, CancellationToken.None);
        }

        var category = (await new CategoryStore(test.Factory).ListAsync())[0];
        var flat = new Property { Name = "Flat" };
        await new PropertyStore(test.Factory).SaveAsync(flat);
        var supplier = new Supplier { Name = "Water Co" };
        await new SupplierStore(test.Factory).SaveAsync(supplier);
        var contract = new Contract
        {
            PropertyId = flat.Id,
            SupplierId = supplier.Id,
            CategoryId = category.Id,
            Title = "Water",
            ValidFrom = new DateOnly(2000, 1, 1),
            ReadingDayFrom = 25,
            ReadingDayTo = 28,
        };
        var contracts = new ContractStore(test.Factory);
        await contracts.SaveAsync(contract);

        var meters = new MeterStore(test.Factory);
        Meter NewMeter(MeterResource resource)
        {
            var meter = new Meter { PropertyId = flat.Id, Resource = resource, Unit = MeterUnit.M3, InstalledOn = new DateOnly(2004, 1, 1), Digits = 5 };
            meter.Registers.Add(MeterRegister.CreateDefault(meter.Id));
            return meter;
        }

        var cold = NewMeter(MeterResource.ColdWater);
        var hot = NewMeter(MeterResource.HotWater);
        foreach (var meter in new[] { cold, hot })
        {
            await meters.SaveAsync(meter);
            await contracts.SaveContractMeterAsync(new ContractMeter { ContractId = contract.Id, MeterId = meter.Id, ValidFrom = new DateOnly(2004, 1, 1) });
        }

        return new Rig
        {
            Test = test,
            Contract = contract,
            Cold = cold,
            Hot = hot,
            Meters = meters,
            Contracts = contracts,
            Bills = new BillStore(test.Factory),
            Batches = new ImportBatchStore(test.Factory),
        };
    }

    [Fact]
    public async Task The_import_creates_the_replacement_meters_and_ends_the_old_ones()
    {
        using var rig = await ArrangeAsync();

        var (plan, batch) = await rig.ImportAsync();

        Assert.Equal(5, plan.Counts.Replacements);
        Assert.Equal(5, batch.ReplacementCount);
        var cold = (await rig.ChainAsync(rig.Cold)).Meters;
        var hot = (await rig.ChainAsync(rig.Hot)).Meters;
        Assert.Equal(4, cold.Count);
        Assert.Equal(3, hot.Count);
        Assert.Equal([new DateOnly(2004, 1, 1), new DateOnly(2004, 5, 7), new DateOnly(2004, 7, 27), new DateOnly(2004, 10, 27)], cold.Select(m => m.InstalledOn));
        Assert.Equal([new DateOnly(2004, 5, 7), new DateOnly(2004, 7, 27), new DateOnly(2004, 10, 27), null], cold.Select(m => m.RemovedOn));
        Assert.Equal([null, batch.Id, batch.Id, batch.Id], cold.Select(m => m.ImportBatchId));
        Assert.Equal("1210007", cold[3].SerialNumber);
        Assert.All(cold.Concat(hot), m => Assert.Equal(5, m.Digits));
    }

    [Fact]
    public async Task Consumption_over_the_chains_has_no_dips_or_jumps_at_the_replacements()
    {
        using var rig = await ArrangeAsync();
        await rig.ImportAsync();

        var from = new DateOnly(2004, 2, 1);
        var to = new DateOnly(2005, 2, 26);
        var cold = ConsumptionCalculator.Calculate(await rig.ChainAsync(rig.Cold), RegisterKind.Total, from, to);
        var hot = ConsumptionCalculator.Calculate(await rig.ChainAsync(rig.Hot), RegisterKind.Total, from, to);

        // Cold: 12 (100→112) + 14 (3→17) + 15.3 (0→15.3) + 9.428 (0.072→9.5); hot: 6 + 16 + 19.
        Assert.Equal(50.728m, cold.Quantity);
        Assert.Equal(41m, hot.Quantity);
        Assert.True(cold.IsComplete);
        Assert.All(cold.Segments.Concat(hot.Segments), s => Assert.True(s.Quantity >= 0));
    }

    [Fact]
    public async Task The_contract_links_move_to_the_new_meters()
    {
        using var rig = await ArrangeAsync();

        var (_, batch) = await rig.ImportAsync();

        var links = await rig.Contracts.ListContractMetersAsync(rig.Contract.Id);
        var cold = (await rig.ChainAsync(rig.Cold)).Meters;
        Assert.Equal(new DateOnly(2004, 5, 6), links.Single(l => l.MeterId == cold[0].Id).ValidTo);
        var second = links.Single(l => l.MeterId == cold[1].Id);
        Assert.Equal((new DateOnly(2004, 5, 7), new DateOnly(2004, 7, 26), batch.Id), (second.ValidFrom, second.ValidTo!.Value, second.ImportBatchId));
        var last = links.Single(l => l.MeterId == cold[3].Id);
        Assert.Equal((new DateOnly(2004, 10, 27), null), (last.ValidFrom, last.ValidTo));
        Assert.Equal(7, links.Count);
    }

    [Fact]
    public async Task Re_importing_the_same_sheet_adds_nothing()
    {
        using var rig = await ArrangeAsync();
        await rig.ImportAsync();

        var (plan, batch) = await rig.ImportAsync();

        Assert.Equal(new ImportCounts(0, 0, 0, plan.Counts.SkippedRows), plan.Counts);
        Assert.Empty(plan.Replacements);
        Assert.Equal(0, batch.ReplacementCount);
        Assert.Equal(4, (await rig.ChainAsync(rig.Cold)).Meters.Count);
        Assert.Equal(3, (await rig.ChainAsync(rig.Hot)).Meters.Count);
    }

    [Fact]
    public async Task Undo_removes_the_new_meters_and_puts_the_old_ones_back_in_use()
    {
        using var rig = await ArrangeAsync();
        var (_, batch) = await rig.ImportAsync();

        var result = await rig.Batches.UndoAsync(batch.Id, confirmEdited: false);

        Assert.Equal(new UndoImportResult(UndoImportOutcome.Done, 0), result);
        var meters = await rig.Meters.ListByPropertyAsync(rig.Contract.PropertyId);
        Assert.Equal(new[] { rig.Cold.Id, rig.Hot.Id }.Order(), meters.Select(m => m.Id).Order());
        Assert.All(meters, m => Assert.Null(m.RemovedOn));
        Assert.All(meters, m => Assert.Empty(m.Registers.SelectMany(r => r.Readings)));
        var links = await rig.Contracts.ListContractMetersAsync(rig.Contract.Id);
        Assert.Equal(2, links.Count);
        Assert.All(links, l => Assert.Null(l.ValidTo));

        // Soft delete: the new meters are still in the table.
        using var db = rig.Test.NewContext();
        Assert.Equal(5, db.Meters.IgnoreQueryFilters().Count(m => m.ImportBatchId == batch.Id && m.IsDeleted));
    }

    [Fact]
    public async Task After_an_undo_the_sheet_imports_again_with_the_same_result()
    {
        using var rig = await ArrangeAsync();
        var (_, first) = await rig.ImportAsync();
        await rig.Batches.UndoAsync(first.Id, false);

        var (plan, second) = await rig.ImportAsync();

        Assert.Equal(5, plan.Counts.Replacements);
        Assert.Equal(4, (await rig.ChainAsync(rig.Cold)).Meters.Count);
        Assert.Equal(second.Id, Assert.Single(await rig.Batches.ListAsync()).Id);
    }

    [Fact]
    public async Task A_reading_the_owner_added_to_a_new_meter_makes_the_undo_ask_first()
    {
        using var rig = await ArrangeAsync();
        var (_, batch) = await rig.ImportAsync();
        var newest = (await rig.ChainAsync(rig.Cold)).Meters[^1];
        await rig.Meters.AddReadingsAsync([(newest.Registers[0].Id, new MeterReading { Date = new DateOnly(2005, 3, 26), Value = 12.5m, Kind = ReadingKind.Regular })]);

        var refused = await rig.Batches.UndoAsync(batch.Id, confirmEdited: false);

        Assert.Equal(UndoImportOutcome.NeedsConfirmation, refused.Outcome);
        Assert.Equal(4, (await rig.ChainAsync(rig.Cold)).Meters.Count);

        var done = await rig.Batches.UndoAsync(batch.Id, confirmEdited: true);
        Assert.Equal(UndoImportOutcome.Done, done.Outcome);
        Assert.DoesNotContain(await rig.Meters.ListByPropertyAsync(rig.Contract.PropertyId), m => m.ImportBatchId is not null);
    }

    [Fact]
    public async Task Readings_bills_and_payments_of_the_sheet_are_stored_with_the_replacements()
    {
        using var rig = await ArrangeAsync();

        var (plan, _) = await rig.ImportAsync();

        var meters = await rig.Meters.ListByPropertyAsync(rig.Contract.PropertyId);
        Assert.Equal(plan.Counts.Readings, meters.SelectMany(m => m.Registers).SelectMany(r => r.Readings).Count());
        Assert.Equal(plan.Counts.Bills, (await rig.Bills.ListByContractAsync(rig.Contract.Id)).Count);
    }
}
