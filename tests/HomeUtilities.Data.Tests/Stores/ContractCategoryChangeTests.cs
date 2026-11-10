using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Data.Stores;
using Microsoft.EntityFrameworkCore;

namespace HomeUtilities.Data.Tests.Stores;

/// <summary>T-044: a category change of a contract can carry its existing bills and templates along, in one transaction.</summary>
public class ContractCategoryChangeTests
{
    private sealed class Rig : IDisposable
    {
        public TestDb Test { get; } = new();
        public Category Old { get; set; } = null!;
        public Category New { get; set; } = null!;
        public Category Third { get; set; } = null!;
        public Contract Contract { get; set; } = null!;
        public Contract Other { get; set; } = null!;
        public ContractStore Contracts => new(Test.Factory);
        public BillStore Bills => new(Test.Factory);
        public Guid[] Matching { get; set; } = [];
        public Bill OtherCategoryBill { get; set; } = null!;
        public Bill DeletedBill { get; set; } = null!;
        public Bill OtherContractBill { get; set; } = null!;
        public BillTemplate Template { get; set; } = null!;
        public BillTemplate OtherContractTemplate { get; set; } = null!;
        public Guid LineCategory { get; set; }

        public void Dispose() => Test.Dispose();
    }

    private static async Task<Rig> ArrangeAsync()
    {
        var rig = new Rig();
        using (var db = rig.Test.NewContext())
        {
            await DataSeeder.SeedAsync(db, CancellationToken.None);
        }

        var categories = new CategoryStore(rig.Test.Factory);
        rig.Old = new Category { Name = "Water" };
        rig.New = new Category { Name = "Heating and hot water" };
        rig.Third = new Category { Name = "Other" };
        foreach (var category in new[] { rig.Old, rig.New, rig.Third })
        {
            await categories.SaveAsync(category);
        }

        var flat = new Property { Name = "Flat" };
        await new PropertyStore(rig.Test.Factory).SaveAsync(flat);
        var supplier = new Supplier { Name = "Heat Ltd" };
        await new SupplierStore(rig.Test.Factory).SaveAsync(supplier);
        Contract Make(string title) => new()
        {
            PropertyId = flat.Id,
            SupplierId = supplier.Id,
            CategoryId = rig.Old.Id,
            Title = title,
            ValidFrom = new DateOnly(2020, 1, 1),
        };
        rig.Contract = Make("Heating");
        rig.Other = Make("Gas");
        await rig.Contracts.SaveAsync(rig.Contract);
        await rig.Contracts.SaveAsync(rig.Other);

        Bill NewBill(Contract c, Guid category, int month) => new()
        {
            PropertyId = flat.Id,
            ContractId = c.Id,
            CategoryId = category,
            Period = new YearMonth(2026, month),
            Gross = 10m,
        };
        var bills = new List<Bill> { NewBill(rig.Contract, rig.Old.Id, 1), NewBill(rig.Contract, rig.Old.Id, 2) };
        var draft = NewBill(rig.Contract, rig.Old.Id, 3);
        draft.IsDraft = true;
        bills.Add(draft);
        bills[0].Lines.Add(new BillLine { Description = "Own category", Gross = 10m, CategoryId = rig.Third.Id });
        rig.OtherCategoryBill = NewBill(rig.Contract, rig.Third.Id, 4);
        rig.DeletedBill = NewBill(rig.Contract, rig.Old.Id, 5);
        rig.OtherContractBill = NewBill(rig.Other, rig.Old.Id, 1);
        foreach (var bill in bills.Append(rig.OtherCategoryBill).Append(rig.DeletedBill).Append(rig.OtherContractBill))
        {
            await rig.Bills.SaveAsync(bill);
        }

        await rig.Bills.DeleteAsync(rig.DeletedBill.Id);
        rig.Matching = [.. bills.Select(b => b.Id)];
        rig.LineCategory = rig.Third.Id;

        BillTemplate NewTemplate(Contract c) => new()
        {
            Name = "Template " + c.Title,
            PropertyId = flat.Id,
            CategoryId = rig.Old.Id,
            ContractId = c.Id,
            Gross = 10m,
            Schedule = new Schedule { Kind = ScheduleKind.Once },
        };
        var templates = new BillTemplateStore(rig.Test.Factory, rig.Test.Clock);
        rig.Template = NewTemplate(rig.Contract);
        rig.OtherContractTemplate = NewTemplate(rig.Other);
        await templates.SaveAsync(rig.Template);
        await templates.SaveAsync(rig.OtherContractTemplate);
        return rig;
    }

    private static async Task<Contract> ChangedAsync(Rig rig)
    {
        var contract = (await rig.Contracts.GetAsync(rig.Contract.Id))!;
        contract.CategoryId = rig.New.Id;
        return contract;
    }

    private static async Task<Guid> CategoryOfBillAsync(Rig rig, Guid id)
    {
        await using var db = await rig.Test.Factory.CreateDbContextAsync();
        return (await db.Bills.IgnoreQueryFilters().SingleAsync(b => b.Id == id)).CategoryId;
    }

    [Fact]
    public async Task Counts_only_live_bills_and_templates_of_the_contract_in_the_old_category()
    {
        using var rig = await ArrangeAsync();

        var records = await rig.Contracts.CountCategoryRecordsAsync(rig.Contract.Id, rig.Old.Id);

        // 3 bills (2 + 1 draft); the deleted bill, the third-category bill and the other contract are not counted.
        Assert.Equal(new ContractCategoryRecords(3, 1), records);
        Assert.False(records.IsEmpty);
        Assert.True((await rig.Contracts.CountCategoryRecordsAsync(rig.Contract.Id, rig.New.Id)).IsEmpty);
    }

    [Fact]
    public async Task Change_records_moves_3_of_3_bills_incl_a_draft_and_the_template_in_one_save()
    {
        using var rig = await ArrangeAsync();
        rig.Test.Clock.UtcNow = new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);

        var changed = await rig.Contracts.SaveWithCategoryChangeAsync(await ChangedAsync(rig), rig.Old.Id);

        Assert.Equal(new ContractCategoryRecords(3, 1), changed);
        Assert.Equal(rig.New.Id, (await rig.Contracts.GetAsync(rig.Contract.Id))!.CategoryId);
        foreach (var id in rig.Matching)
        {
            Assert.Equal(rig.New.Id, await CategoryOfBillAsync(rig, id));
        }

        await using var db = await rig.Test.Factory.CreateDbContextAsync();
        var template = await db.BillTemplates.SingleAsync(t => t.Id == rig.Template.Id);
        Assert.Equal(rig.New.Id, template.CategoryId);

        // Sync carries the change: UpdatedAt of the changed rows is the time of the save.
        var stamp = new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);
        Assert.Equal(stamp, template.UpdatedAt);
        Assert.All(rig.Matching, id => Assert.Equal(stamp, db.Bills.Single(b => b.Id == id).UpdatedAt));
    }

    [Fact]
    public async Task Change_records_leaves_other_categories_deleted_bills_lines_and_other_contracts_alone()
    {
        using var rig = await ArrangeAsync();

        await rig.Contracts.SaveWithCategoryChangeAsync(await ChangedAsync(rig), rig.Old.Id);

        Assert.Equal(rig.Third.Id, await CategoryOfBillAsync(rig, rig.OtherCategoryBill.Id));
        Assert.Equal(rig.Old.Id, await CategoryOfBillAsync(rig, rig.DeletedBill.Id));
        Assert.Equal(rig.Old.Id, await CategoryOfBillAsync(rig, rig.OtherContractBill.Id));
        await using var db = await rig.Test.Factory.CreateDbContextAsync();
        Assert.Equal(rig.Old.Id, (await db.BillTemplates.SingleAsync(t => t.Id == rig.OtherContractTemplate.Id)).CategoryId);
        Assert.Equal(rig.LineCategory, (await db.BillLines.SingleAsync()).CategoryId);
    }

    [Fact]
    public async Task Saving_only_the_contract_leaves_every_record_in_the_old_category()
    {
        using var rig = await ArrangeAsync();

        await rig.Contracts.SaveAsync(await ChangedAsync(rig));

        Assert.Equal(rig.New.Id, (await rig.Contracts.GetAsync(rig.Contract.Id))!.CategoryId);
        foreach (var id in rig.Matching)
        {
            Assert.Equal(rig.Old.Id, await CategoryOfBillAsync(rig, id));
        }

        await using var db = await rig.Test.Factory.CreateDbContextAsync();
        Assert.Equal(rig.Old.Id, (await db.BillTemplates.SingleAsync(t => t.Id == rig.Template.Id)).CategoryId);
    }
}
