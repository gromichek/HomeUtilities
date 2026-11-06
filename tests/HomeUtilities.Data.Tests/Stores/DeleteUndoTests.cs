using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Attachments;
using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Tariffs;
using HomeUtilities.Data.Stores;
using Microsoft.EntityFrameworkCore;

namespace HomeUtilities.Data.Tests.Stores;

/// <summary>Undo of a delete restores exactly the rows the delete changed (UI_GUIDELINES §5.1, T-036b).</summary>
public class DeleteUndoTests
{
    private static Bill NewBill(Guid propertyId, Guid categoryId, decimal gross) =>
        new() { PropertyId = propertyId, CategoryId = categoryId, Period = new YearMonth(2026, 9), Gross = gross, Kind = BillKind.OneOff };

    [Fact]
    public async Task Undoing_a_payment_delete_brings_back_the_payment_and_its_live_allocations_only()
    {
        using var test = new TestDb();
        var (propertyId, _, categoryId) = await StoreTests.PrepareContractOwnersAsync(test);
        var bills = new BillStore(test.Factory);
        var first = NewBill(propertyId, categoryId, 30m);
        var second = NewBill(propertyId, categoryId, 40m);
        await bills.SaveAsync(first);
        await bills.SaveAsync(second);

        var payments = new PaymentStore(test.Factory);
        var payment = new Payment { PropertyId = propertyId, PaidOn = new DateOnly(2026, 9, 2), Amount = 70m };
        payment.Allocations.Add(new PaymentAllocation { PaymentId = payment.Id, BillId = first.Id, Amount = 30m });
        payment.Allocations.Add(new PaymentAllocation { PaymentId = payment.Id, BillId = second.Id, Amount = 40m });
        await payments.SaveAsync(payment);

        // An allocation released earlier is not part of this delete and must stay gone after Undo.
        var loaded = (await payments.GetAsync(payment.Id))!;
        loaded.Allocations.RemoveAll(a => a.BillId == second.Id);
        await payments.SaveAsync(loaded);

        var undo = new DeleteUndoService(test.Factory);
        IReadOnlyList<DeletedRow> deleted;
        using (var recording = undo.BeginRecording())
        {
            await payments.DeleteAsync(payment.Id);
            deleted = recording.Rows;
        }

        Assert.Equal(2, deleted.Count);
        Assert.Contains(deleted, r => r.Table == "Payments" && r.Id == payment.Id);
        Assert.Null(await payments.GetAsync(payment.Id));

        await undo.RestoreAsync([.. deleted]);

        var restored = (await payments.GetAsync(payment.Id))!;
        Assert.Equal(70m, restored.Amount);
        var allocation = Assert.Single(restored.Allocations);
        Assert.Equal((first.Id, 30m), (allocation.BillId, allocation.Amount));
        await using var db = test.NewContext();
        Assert.True((await db.PaymentAllocations.IgnoreQueryFilters().SingleAsync(a => a.PaymentId == payment.Id && a.BillId == second.Id)).IsDeleted);
    }

    [Fact]
    public async Task Everything_a_flow_deletes_is_recorded_across_stores_and_restored_together()
    {
        // What deleting a bill does: its quick payment goes with it, a shared payment only loses the bill's allocation.
        using var test = new TestDb();
        var (propertyId, _, categoryId) = await StoreTests.PrepareContractOwnersAsync(test);
        var bills = new BillStore(test.Factory);
        var bill = NewBill(propertyId, categoryId, 30m);
        bill.Lines.Add(new BillLine { BillId = bill.Id, Description = "Water", Gross = 30m, SortOrder = 1 });
        var other = NewBill(propertyId, categoryId, 40m);
        await bills.SaveAsync(bill);
        await bills.SaveAsync(other);

        var payments = new PaymentStore(test.Factory);
        var quick = new Payment { PropertyId = propertyId, PaidOn = new DateOnly(2026, 9, 2), Amount = 30m, CreatedByQuickAction = true };
        quick.Allocations.Add(new PaymentAllocation { PaymentId = quick.Id, BillId = bill.Id, Amount = 30m });
        var shared = new Payment { PropertyId = propertyId, PaidOn = new DateOnly(2026, 9, 3), Amount = 50m };
        shared.Allocations.Add(new PaymentAllocation { PaymentId = shared.Id, BillId = bill.Id, Amount = 10m });
        shared.Allocations.Add(new PaymentAllocation { PaymentId = shared.Id, BillId = other.Id, Amount = 40m });
        await payments.SaveAsync(quick);
        await payments.SaveAsync(shared);

        var undo = new DeleteUndoService(test.Factory);
        IReadOnlyList<DeletedRow> deleted;
        using (var recording = undo.BeginRecording())
        {
            await payments.DeleteAsync(quick.Id);
            var sharedLoaded = (await payments.GetAsync(shared.Id))!;
            sharedLoaded.Allocations.RemoveAll(a => a.BillId == bill.Id);
            await payments.SaveAsync(sharedLoaded);
            await bills.DeleteAsync(bill.Id);
            deleted = recording.Rows;
        }

        // quick payment + its allocation, the shared payment's allocation to the bill, the bill + its line.
        Assert.Equal(5, deleted.Count);
        Assert.Null(await bills.GetAsync(bill.Id));
        Assert.Single((await payments.GetAsync(shared.Id))!.Allocations);

        await undo.RestoreAsync([.. deleted]);

        Assert.NotNull(await bills.GetAsync(bill.Id));
        Assert.Single((await bills.GetAsync(bill.Id))!.Lines);
        Assert.Equal(new[] { quick.Id, shared.Id }.Order(), (await payments.ListByBillAsync(bill.Id)).Select(p => p.Id).Order());
        Assert.Equal(2, (await payments.GetAsync(shared.Id))!.Allocations.Count);
        Assert.Equal(30m, Assert.Single((await payments.GetAsync(quick.Id))!.Allocations).Amount);
    }

    [Fact]
    public async Task Deletes_outside_a_recording_and_rows_deleted_before_are_not_recorded()
    {
        using var test = new TestDb();
        var propertyId = await StoreTests.SavePropertyAsync(test);
        var categories = new CategoryStore(test.Factory);
        var kept = new Category { Name = "Kept" };
        var gone = new Category { Name = "Gone" };
        await categories.SaveAsync(kept);
        await categories.SaveAsync(gone);
        Assert.NotEqual(Guid.Empty, propertyId);

        var undo = new DeleteUndoService(test.Factory);
        await categories.DeleteAsync(gone.Id);
        using var recording = undo.BeginRecording();
        await categories.DeleteAsync(gone.Id);
        Assert.Empty(recording.Rows);

        await categories.DeleteAsync(kept.Id);

        var row = Assert.Single(recording.Rows);
        Assert.Equal(("Categories", kept.Id), (row.Table, row.Id));
    }

    [Fact]
    public async Task A_finished_recording_stops_collecting_and_nested_recordings_feed_the_outer_one()
    {
        using var test = new TestDb();
        var categories = new CategoryStore(test.Factory);
        var a = new Category { Name = "A" };
        var b = new Category { Name = "B" };
        var c = new Category { Name = "C" };
        await categories.SaveAsync(a);
        await categories.SaveAsync(b);
        await categories.SaveAsync(c);

        var undo = new DeleteUndoService(test.Factory);
        IReadOnlyList<DeletedRow> outerRows;
        IReadOnlyList<DeletedRow> innerRows;
        using (var outer = undo.BeginRecording())
        {
            using (var inner = undo.BeginRecording())
            {
                await categories.DeleteAsync(a.Id);
                innerRows = inner.Rows;
            }

            await categories.DeleteAsync(b.Id);
            outerRows = outer.Rows;
        }

        await categories.DeleteAsync(c.Id);

        Assert.Equal([a.Id], innerRows.Select(r => r.Id));
        Assert.Equal([a.Id, b.Id], outerRows.Select(r => r.Id));
    }

    [Fact]
    public async Task Restoring_makes_the_sync_carry_the_change_with_a_newer_stamp()
    {
        using var test = new TestDb();
        var categories = new CategoryStore(test.Factory);
        var category = new Category { Name = "A" };
        await categories.SaveAsync(category);
        var undo = new DeleteUndoService(test.Factory);
        IReadOnlyList<DeletedRow> deleted;
        using (var recording = undo.BeginRecording())
        {
            await categories.DeleteAsync(category.Id);
            deleted = recording.Rows;
        }

        test.Clock.UtcNow = test.Clock.UtcNow.AddSeconds(5);
        await undo.RestoreAsync([.. deleted]);

        await using var db = test.NewContext();
        var row = await db.Categories.IgnoreQueryFilters().SingleAsync(x => x.Id == category.Id);
        Assert.False(row.IsDeleted);
        Assert.Equal(test.Clock.UtcNow, row.UpdatedAt);
    }

    private static async Task<IReadOnlyList<DeletedRow>> DeleteRecordedAsync(DeleteUndoService undo, Func<Task> delete)
    {
        using var recording = undo.BeginRecording();
        await delete();
        return recording.Rows;
    }

    [Fact]
    public async Task Undoing_a_tariff_component_delete_brings_back_the_component_with_all_its_rates()
    {
        using var test = new TestDb();
        var (propertyId, supplierId, categoryId) = await StoreTests.PrepareContractOwnersAsync(test);
        var contract = StoreTests.NewContract(propertyId, supplierId, categoryId, "Water");
        await new ContractStore(test.Factory).SaveAsync(contract);
        var tariffs = new TariffStore(test.Factory);
        var component = new TariffComponent { ContractId = contract.Id, Name = "Water supply", Basis = ChargeBasis.PerUnit, VatCode = "LV-STANDARD" };
        component.Rates.Add(new TariffRate { ComponentId = component.Id, ValidFrom = new DateOnly(2026, 1, 1), Price = 1.41m, Currency = Currency.EUR });
        component.Rates.Add(new TariffRate { ComponentId = component.Id, ValidFrom = new DateOnly(2025, 1, 1), Price = 1.30m, Currency = Currency.EUR });
        await tariffs.SaveAsync(component);
        var undo = new DeleteUndoService(test.Factory);

        var deleted = await DeleteRecordedAsync(undo, () => tariffs.DeleteAsync(component.Id));

        Assert.Equal(3, deleted.Count);
        Assert.Empty(await tariffs.ListByContractAsync(contract.Id));
        await undo.RestoreAsync([.. deleted]);
        var restored = Assert.Single(await tariffs.ListByContractAsync(contract.Id));
        Assert.Equal([1.30m, 1.41m], restored.Rates.Select(r => r.Price));
    }

    [Fact]
    public async Task Undoing_a_VAT_rate_delete_brings_back_the_rate_with_its_versions()
    {
        using var test = new TestDb();
        var rates = new VatRateStore(test.Factory);
        var rate = new VatRate { Code = "X-TEST", Name = "Test" };
        rate.Versions.Add(new VatRateVersion { VatRateId = rate.Id, ValidFrom = new DateOnly(2020, 1, 1), Percent = 21m });
        rate.Versions.Add(new VatRateVersion { VatRateId = rate.Id, ValidFrom = new DateOnly(2010, 1, 1), Percent = 18m });
        await rates.SaveAsync(rate);
        var undo = new DeleteUndoService(test.Factory);

        var deleted = await DeleteRecordedAsync(undo, () => rates.DeleteAsync(rate.Id));

        Assert.Equal(3, deleted.Count);
        Assert.Null(await rates.GetAsync(rate.Id));
        await undo.RestoreAsync([.. deleted]);
        Assert.Equal([18m, 21m], (await rates.GetAsync(rate.Id))!.Versions.Select(v => v.Percent).Order());
    }

    [Fact]
    public async Task Undoing_a_bill_template_delete_brings_back_the_template_with_its_lines()
    {
        using var test = new TestDb();
        var (propertyId, _, categoryId) = await StoreTests.PrepareContractOwnersAsync(test);
        var templates = new BillTemplateStore(test.Factory, test.Clock);
        var template = new BillTemplate
        {
            Name = "Internet + TV",
            PropertyId = propertyId,
            CategoryId = categoryId,
            Gross = 22.95m,
            Lines = [new BillTemplateLine { Description = "TV", Gross = 5m, Quantity = 1m, Unit = "pc" }],
        };
        await templates.SaveAsync(template);
        var undo = new DeleteUndoService(test.Factory);

        var deleted = await DeleteRecordedAsync(undo, () => templates.DeleteAsync(template.Id));

        Assert.Equal([template.Id], deleted.Select(r => r.Id));
        Assert.Null(await templates.GetAsync(template.Id));
        await undo.RestoreAsync([.. deleted]);
        var restored = (await templates.GetAsync(template.Id))!;
        Assert.Equal(("Internet + TV", 22.95m), (restored.Name, restored.Gross));
        Assert.Equal("TV", Assert.Single(restored.Lines).Description);
    }

    [Fact]
    public async Task Undoing_a_category_delete_brings_the_category_back()
    {
        using var test = new TestDb();
        var categories = new CategoryStore(test.Factory);
        var category = new Category { Name = "Kindergarten", Color = "#112233" };
        await categories.SaveAsync(category);
        var undo = new DeleteUndoService(test.Factory);

        var deleted = await DeleteRecordedAsync(undo, () => categories.DeleteAsync(category.Id));

        Assert.Empty(await categories.ListAsync());
        await undo.RestoreAsync([.. deleted]);
        Assert.Equal(("Kindergarten", "#112233"), (Assert.Single(await categories.ListAsync()).Name, (await categories.GetAsync(category.Id))!.Color));
    }

    [Fact]
    public async Task Attachments_soft_deleted_in_the_same_flow_come_back_with_the_record()
    {
        // T-037a soft-deletes the attachments of a deleted owner; Undo must bring them back with it (the file is never removed).
        using var test = new TestDb();
        var (propertyId, _, categoryId) = await StoreTests.PrepareContractOwnersAsync(test);
        var folder = Path.Combine(Path.GetTempPath(), "hu-undo-" + Guid.NewGuid().ToString("N"));
        try
        {
            var attachments = new AttachmentStore(test.Factory, folder);
            var bills = new BillStore(test.Factory);
            var bill = NewBill(propertyId, categoryId, 30m);
            await bills.SaveAsync(bill);
            var photo = await attachments.AddAsync(
                new Attachment { OwnerKind = AttachmentOwnerKind.Bill, OwnerId = bill.Id, FileName = "bill.png", ContentType = "image/png" },
                new MemoryStream([0x89, 0x50, 0x4E, 0x47, 1, 2, 3]));
            var undo = new DeleteUndoService(test.Factory);

            var deleted = await DeleteRecordedAsync(undo, async () =>
            {
                await attachments.DeleteAsync(photo.Id);
                await bills.DeleteAsync(bill.Id);
            });

            Assert.Equal(2, deleted.Count);
            Assert.Null(await attachments.GetAsync(photo.Id));
            await undo.RestoreAsync([.. deleted]);
            Assert.NotNull(await attachments.GetAsync(photo.Id));
            Assert.NotNull(await attachments.GetFilePathAsync(photo.Id));
            Assert.NotNull(await bills.GetAsync(bill.Id));
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }
}
