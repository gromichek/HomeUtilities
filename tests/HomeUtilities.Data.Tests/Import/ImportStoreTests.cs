using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Import;
using HomeUtilities.Data.Stores;
using HomeUtilities.Import;
using HomeUtilities.Import.Tests;
using Microsoft.EntityFrameworkCore;

namespace HomeUtilities.Data.Tests.Import;

public class ImportStoreTests
{
    private sealed class Rig : IDisposable
    {
        public required TestDb Test { get; init; }
        public required Contract Contract { get; init; }
        public required Meter Cold { get; init; }
        public required Meter Hot { get; init; }
        public required Meter Power { get; init; }
        public required BillStore Bills { get; init; }
        public required PaymentStore Payments { get; init; }
        public required MeterStore Meters { get; init; }
        public required ImportBatchStore Batches { get; init; }
        public required ImportProfileStore Profiles { get; init; }
        public ClosedXmlWorkbookReader Reader { get; } = new();

        public void Dispose() => Test.Dispose();

        public SheetData Read(string sheet)
        {
            using var stream = SyntheticWorkbook.Build();
            return Reader.ReadSheet(stream, sheet);
        }

        public async Task<ImportContext> ContextAsync()
        {
            var meters = await Meters.ListByPropertyAsync(Contract.PropertyId);
            var bills = await Bills.ListByContractAsync(Contract.Id);
            return new ImportContext((await new ContractStore(Test.Factory).GetAsync(Contract.Id))!, meters, bills);
        }

        public ImportProfile WaterProfile() => new()
        {
            SheetName = SyntheticWorkbook.Water,
            ContractId = Contract.Id,
            PeriodColumn = 1,
            DetectReplacements = false,
            Registers =
            [
                new RegisterColumnMapping { RegisterId = Cold.Registers[0].Id, ReadingColumn = 2 },
                new RegisterColumnMapping { RegisterId = Hot.Registers[0].Id, ReadingColumn = 3 },
            ],
            TotalColumn = 4,
            PaidColumn = 5,
            NotesColumns = [6],
        };

        public async Task<(ImportPlan Plan, ImportBatch Batch)> ImportWaterAsync()
        {
            var plan = ImportPlanner.Plan(WaterProfile(), Read(SyntheticWorkbook.Water), await ContextAsync());
            var batch = await Batches.ApplyAsync(new ImportApplyRequest(plan, "book.xlsx", SyntheticWorkbook.Water, Contract.Id, null));
            return (plan, batch);
        }

        public async Task<decimal> BalanceAsync() =>
            (await Payments.ListByContractAsync(Contract.Id)).Sum(p => p.Amount)
            - (await Bills.ListByContractAsync(Contract.Id)).Sum(b => b.Gross);
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
        };
        await new ContractStore(test.Factory).SaveAsync(contract);

        var meters = new MeterStore(test.Factory);
        Meter NewMeter(MeterResource resource, MeterUnit unit)
        {
            var meter = new Meter { PropertyId = flat.Id, Resource = resource, Unit = unit, InstalledOn = new DateOnly(2000, 1, 1) };
            meter.Registers.Add(MeterRegister.CreateDefault(meter.Id));
            return meter;
        }

        var cold = NewMeter(MeterResource.ColdWater, MeterUnit.M3);
        var hot = NewMeter(MeterResource.HotWater, MeterUnit.M3);
        var power = NewMeter(MeterResource.Electricity, MeterUnit.KWh);
        foreach (var meter in new[] { cold, hot, power })
        {
            await meters.SaveAsync(meter);
        }

        return new Rig
        {
            Test = test,
            Contract = contract,
            Cold = cold,
            Hot = hot,
            Power = power,
            Bills = new BillStore(test.Factory),
            Payments = new PaymentStore(test.Factory),
            Meters = meters,
            Batches = new ImportBatchStore(test.Factory),
            Profiles = new ImportProfileStore(test.Factory),
        };
    }

    [Fact]
    public async Task Importing_the_water_sheet_stores_the_planned_counts_and_a_batch()
    {
        using var rig = await ArrangeAsync();

        var (plan, batch) = await rig.ImportWaterAsync();

        Assert.Equal(new ImportCounts(8, 3, 2, 1), plan.Counts);
        var stored = await rig.Batches.GetAsync(batch.Id);
        Assert.NotNull(stored);
        Assert.Equal((8, 3, 2, 1, "book.xlsx", "Water"), (stored.ReadingCount, stored.BillCount, stored.PaymentCount, stored.SkippedCount, stored.FileName, stored.SheetName));
        Assert.Equal(3, (await rig.Bills.ListByContractAsync(rig.Contract.Id)).Count);
        Assert.Equal(2, (await rig.Payments.ListByContractAsync(rig.Contract.Id)).Count);
        var meters = await rig.Meters.ListByPropertyAsync(rig.Contract.PropertyId);
        Assert.Equal(8, meters.SelectMany(m => m.Registers).SelectMany(r => r.Readings).Count());
    }

    [Fact]
    public async Task Imported_records_carry_the_batch_id_and_the_documented_values()
    {
        using var rig = await ArrangeAsync();

        var (_, batch) = await rig.ImportWaterAsync();

        var bills = await rig.Bills.ListByContractAsync(rig.Contract.Id);
        Assert.All(bills, b => Assert.Equal(batch.Id, b.ImportBatchId));
        Assert.Equal([Currency.LVL, Currency.LVL, Currency.EUR], bills.Select(b => b.Currency));
        var payments = await rig.Payments.ListByContractAsync(rig.Contract.Id);
        Assert.All(payments, p => Assert.Equal(("imported", batch.Id), (p.Reference, p.ImportBatchId)));
        Assert.All(payments, p => Assert.Single(p.Allocations));
        var cold = (await rig.Meters.ListByPropertyAsync(rig.Contract.PropertyId)).Single(m => m.Id == rig.Cold.Id).Registers[0].Readings.OrderBy(r => r.Date).ToList();
        Assert.Equal([ReadingKind.Initial, ReadingKind.Regular, ReadingKind.Regular, ReadingKind.Regular], cold.Select(r => r.Kind));
        Assert.All(cold, r => Assert.Equal(batch.Id, r.ImportBatchId));
    }

    [Fact]
    public async Task Re_importing_the_same_sheet_adds_nothing()
    {
        using var rig = await ArrangeAsync();
        await rig.ImportWaterAsync();

        var (plan, batch) = await rig.ImportWaterAsync();

        // Rows 2-5: readings exist, bills of rows 2-4 exist; row 6 stays an error.
        Assert.Equal(new ImportCounts(0, 0, 0, 5), plan.Counts);
        Assert.Equal((0, 0, 0), (batch.ReadingCount, batch.BillCount, batch.PaymentCount));
        Assert.Equal(3, (await rig.Bills.ListByContractAsync(rig.Contract.Id)).Count);
        Assert.Equal(2, (await rig.Payments.ListByContractAsync(rig.Contract.Id)).Count);
        var meters = await rig.Meters.ListByPropertyAsync(rig.Contract.PropertyId);
        Assert.Equal(8, meters.SelectMany(m => m.Registers).SelectMany(r => r.Readings).Count());
    }

    [Fact]
    public async Task Undo_soft_deletes_everything_of_the_batch_and_the_balance_returns()
    {
        using var rig = await ArrangeAsync();
        var before = await rig.BalanceAsync();
        var (_, batch) = await rig.ImportWaterAsync();
        Assert.NotEqual(before, await rig.BalanceAsync());

        var result = await rig.Batches.UndoAsync(batch.Id, confirmEdited: false);

        Assert.Equal(new UndoImportResult(UndoImportOutcome.Done, 0), result);
        Assert.Empty(await rig.Bills.ListByContractAsync(rig.Contract.Id));
        Assert.Empty(await rig.Payments.ListByContractAsync(rig.Contract.Id));
        var meters = await rig.Meters.ListByPropertyAsync(rig.Contract.PropertyId);
        Assert.Empty(meters.SelectMany(m => m.Registers).SelectMany(r => r.Readings));
        Assert.Null(await rig.Batches.GetAsync(batch.Id));
        Assert.Empty(await rig.Batches.ListAsync());
        Assert.Equal(before, await rig.BalanceAsync());

        // Soft delete: the rows are still there, including the allocations.
        using var db = rig.Test.NewContext();
        Assert.Equal(3, db.Bills.IgnoreQueryFilters().Count(b => b.ImportBatchId == batch.Id && b.IsDeleted));
        Assert.Equal(2, db.PaymentAllocations.IgnoreQueryFilters().Count(a => a.IsDeleted));
        Assert.Equal(8, db.MeterReadings.IgnoreQueryFilters().Count(r => r.ImportBatchId == batch.Id && r.IsDeleted));
    }

    [Fact]
    public async Task After_an_undo_the_same_sheet_can_be_imported_again()
    {
        using var rig = await ArrangeAsync();
        var (_, first) = await rig.ImportWaterAsync();
        await rig.Batches.UndoAsync(first.Id, false);

        var (plan, second) = await rig.ImportWaterAsync();

        Assert.Equal(new ImportCounts(8, 3, 2, 1), plan.Counts);
        Assert.Equal(3, (await rig.Bills.ListByContractAsync(rig.Contract.Id)).Count);
        Assert.Equal(second.Id, Assert.Single(await rig.Batches.ListAsync()).Id);
    }

    [Fact]
    public async Task Undo_of_edited_records_needs_confirmation_and_changes_nothing_until_confirmed()
    {
        using var rig = await ArrangeAsync();
        var (_, batch) = await rig.ImportWaterAsync();
        rig.Test.Clock.UtcNow = rig.Test.Clock.UtcNow.AddHours(1);
        var bill = (await rig.Bills.ListByContractAsync(rig.Contract.Id))[0];
        bill.Notes = "fixed by hand";
        await rig.Bills.SaveAsync(bill);

        var refused = await rig.Batches.UndoAsync(batch.Id, confirmEdited: false);

        Assert.Equal(new UndoImportResult(UndoImportOutcome.NeedsConfirmation, 1), refused);
        Assert.Equal(3, (await rig.Bills.ListByContractAsync(rig.Contract.Id)).Count);
        Assert.NotNull(await rig.Batches.GetAsync(batch.Id));

        var confirmed = await rig.Batches.UndoAsync(batch.Id, confirmEdited: true);

        Assert.Equal(UndoImportOutcome.Done, confirmed.Outcome);
        Assert.Empty(await rig.Bills.ListByContractAsync(rig.Contract.Id));
    }

    [Fact]
    public async Task A_payment_the_owner_allocated_to_an_imported_bill_counts_as_edited_and_loses_the_allocation()
    {
        using var rig = await ArrangeAsync();
        var (_, batch) = await rig.ImportWaterAsync();
        rig.Test.Clock.UtcNow = rig.Test.Clock.UtcNow.AddHours(1);
        var unpaid = (await rig.Bills.ListByContractAsync(rig.Contract.Id)).Single(b => b.Period == new YearMonth(2014, 1));
        var payment = new Payment { ContractId = rig.Contract.Id, PaidOn = new DateOnly(2014, 2, 5), Amount = 11.50m, Currency = Currency.EUR };
        payment.Allocations.Add(new PaymentAllocation { PaymentId = payment.Id, BillId = unpaid.Id, Amount = 11.50m });
        await rig.Payments.SaveAsync(payment);

        var refused = await rig.Batches.UndoAsync(batch.Id, false);
        var done = await rig.Batches.UndoAsync(batch.Id, true);

        Assert.Equal(UndoImportOutcome.NeedsConfirmation, refused.Outcome);
        Assert.Equal(UndoImportOutcome.Done, done.Outcome);
        var left = Assert.Single(await rig.Payments.ListByContractAsync(rig.Contract.Id));
        Assert.Equal(payment.Id, left.Id);
        Assert.Empty(left.Allocations);
        Assert.Equal(11.50m, await rig.BalanceAsync());
    }

    [Fact]
    public async Task Undo_of_an_unknown_batch_is_reported()
    {
        using var rig = await ArrangeAsync();

        Assert.Equal(UndoImportOutcome.NotFound, (await rig.Batches.UndoAsync(Guid.NewGuid(), true)).Outcome);
    }

    [Fact]
    public async Task An_import_is_all_or_nothing()
    {
        using var rig = await ArrangeAsync();
        var plan = ImportPlanner.Plan(rig.WaterProfile(), rig.Read(SyntheticWorkbook.Water), await rig.ContextAsync());
        // A reading for a register that does not exist breaks the foreign key in the middle of the save.
        plan.Rows.Last(r => r.Readings.Count > 0).Readings[0].Reading.RegisterId = Guid.NewGuid();

        await Assert.ThrowsAnyAsync<Exception>(() =>
            rig.Batches.ApplyAsync(new ImportApplyRequest(plan, "book.xlsx", "Water", rig.Contract.Id, null)));

        Assert.Empty(await rig.Bills.ListByContractAsync(rig.Contract.Id));
        Assert.Empty(await rig.Payments.ListByContractAsync(rig.Contract.Id));
        Assert.Empty(await rig.Batches.ListAsync());
        var meters = await rig.Meters.ListByPropertyAsync(rig.Contract.PropertyId);
        Assert.Empty(meters.SelectMany(m => m.Registers).SelectMany(r => r.Readings));
    }

    [Fact]
    public async Task A_plan_with_problems_cannot_be_applied()
    {
        using var rig = await ArrangeAsync();
        var plan = ImportPlan.Invalid([new ValidationError(ErrorCodes.ImportPeriodColumnRequired)]);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            rig.Batches.ApplyAsync(new ImportApplyRequest(plan, "x.xlsx", "S", rig.Contract.Id, null)));
    }

    [Fact]
    public async Task Imported_history_shows_up_in_the_contract_balance_with_lats_and_euro_kept_apart()
    {
        using var rig = await ArrangeAsync();
        await rig.ImportWaterAsync();

        var bills = await rig.Bills.ListByContractAsync(rig.Contract.Id);
        var payments = await rig.Payments.ListByContractAsync(rig.Contract.Id);

        // 12.34 + 13.00 LVL are paid in full; 11.50 EUR is open.
        Assert.Equal(11.50m, bills.Where(b => b.Currency == Currency.EUR).Sum(b => b.Gross) - payments.Where(p => p.Currency == Currency.EUR).Sum(p => p.Amount));
        Assert.Equal(0m, bills.Where(b => b.Currency == Currency.LVL).Sum(b => b.Gross) - payments.Where(p => p.Currency == Currency.LVL).Sum(p => p.Amount));
    }

    [Fact]
    public async Task Profiles_round_trip_through_the_store_as_json()
    {
        using var rig = await ArrangeAsync();
        var saved = new SavedImportProfile { Name = "Water sheet", Profile = rig.WaterProfile() };
        saved.Profile.PaidMarker = "ok";
        saved.Profile.CurrencyRule = ImportCurrencyRule.Eur;
        saved.Profile.LastDataRow = 40;

        await rig.Profiles.SaveAsync(saved);
        var loaded = await rig.Profiles.GetAsync(saved.Id);

        Assert.NotNull(loaded);
        Assert.Equal("Water sheet", loaded.Name);
        Assert.Equal(("ok", ImportCurrencyRule.Eur, 40, 4, rig.Contract.Id), (loaded.Profile.PaidMarker, loaded.Profile.CurrencyRule, loaded.Profile.LastDataRow, loaded.Profile.TotalColumn, loaded.Profile.ContractId));
        Assert.Equal(2, loaded.Profile.Registers.Count);
        Assert.Equal([6], loaded.Profile.NotesColumns);
    }

    [Fact]
    public async Task Deleting_a_profile_is_a_soft_delete_and_hides_it()
    {
        using var rig = await ArrangeAsync();
        var saved = new SavedImportProfile { Name = "Gone", Profile = rig.WaterProfile() };
        await rig.Profiles.SaveAsync(saved);

        await rig.Profiles.DeleteAsync(saved.Id);

        Assert.Empty(await rig.Profiles.ListAsync());
        using var db = rig.Test.NewContext();
        Assert.True(db.ImportProfiles.IgnoreQueryFilters().Single(p => p.Id == saved.Id).IsDeleted);
    }

    [Fact]
    public async Task A_batch_keeps_working_after_its_profile_is_deleted()
    {
        using var rig = await ArrangeAsync();
        var saved = new SavedImportProfile { Name = "Temp", Profile = rig.WaterProfile() };
        await rig.Profiles.SaveAsync(saved);
        var plan = ImportPlanner.Plan(saved.Profile, rig.Read(SyntheticWorkbook.Water), await rig.ContextAsync());
        var batch = await rig.Batches.ApplyAsync(new ImportApplyRequest(plan, "b.xlsx", "Water", rig.Contract.Id, saved.Id));

        await rig.Profiles.DeleteAsync(saved.Id);
        var undone = await rig.Batches.UndoAsync(batch.Id, false);

        Assert.Equal(UndoImportOutcome.Done, undone.Outcome);
    }
}
