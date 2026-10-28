using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Reports;
using HomeUtilities.Data.Stores;

namespace HomeUtilities.Data.Tests.Stores;

public class BillTemplateStoreTests
{
    private static readonly DateOnly Today = new(2026, 10, 15);

    private sealed record Rig(TestDb Test, Property Flat, Category Category, Contract Contract, BillTemplateStore Templates, BillStore Bills) : IDisposable
    {
        public void Dispose() => Test.Dispose();
    }

    private static async Task<Rig> ArrangeAsync()
    {
        var test = new TestDb();
        test.Clock.UtcNow = new DateTimeOffset(2026, 10, 15, 8, 0, 0, TimeSpan.Zero);
        using (var db = test.NewContext())
        {
            await DataSeeder.SeedAsync(db, CancellationToken.None);
        }

        var category = (await new CategoryStore(test.Factory).ListAsync())[0];
        var flat = new Property { Name = "Flat" };
        await new PropertyStore(test.Factory).SaveAsync(flat);
        var supplier = new Supplier { Name = "FiberNet" };
        await new SupplierStore(test.Factory).SaveAsync(supplier);
        var contract = new Contract
        {
            PropertyId = flat.Id,
            SupplierId = supplier.Id,
            CategoryId = category.Id,
            Title = "Internet",
            ValidFrom = new DateOnly(2020, 1, 1),
        };
        await new ContractStore(test.Factory).SaveAsync(contract);
        return new Rig(test, flat, category, contract, new BillTemplateStore(test.Factory, test.Clock), new BillStore(test.Factory));
    }

    private static BillTemplate Monthly(Rig rig, string name = "Internet + TV", YearMonth? start = null, BillingTiming timing = BillingTiming.InAdvance) => new()
    {
        Name = name,
        PropertyId = rig.Flat.Id,
        CategoryId = rig.Category.Id,
        ContractId = rig.Contract.Id,
        Gross = 22.95m,
        Timing = timing,
        DueDayOffset = 14,
        Schedule = new Schedule { Kind = ScheduleKind.Monthly, StartMonth = start ?? new YearMonth(2026, 8), DayOfMonth = 5 },
    };

    [Fact]
    public async Task A_template_round_trips_with_lines_custom_fields_and_schedule()
    {
        using var rig = await ArrangeAsync();
        var template = Monthly(rig);
        template.Lines = [new BillTemplateLine { Description = "TV", Gross = 5m, Quantity = 1m, Unit = "pc" }];
        template.CustomFields = [new CustomFieldDefinition { OwnerId = template.Id, Name = "Ref", Kind = CustomFieldKind.Text, Target = CustomFieldTarget.Bill }];
        template.Schedule = new Schedule { Kind = ScheduleKind.EveryNMonths, EveryMonths = 3, StartMonth = new YearMonth(2026, 2), EndMonth = new YearMonth(2027, 2), DayOfMonth = 31 };
        template.LastGeneratedPeriod = new YearMonth(2026, 5);
        await rig.Templates.SaveAsync(template);

        var loaded = await rig.Templates.GetAsync(template.Id);

        Assert.NotNull(loaded);
        Assert.Equal(("Internet + TV", 22.95m, BillingTiming.InAdvance, 14), (loaded.Name, loaded.Gross, loaded.Timing, loaded.DueDayOffset));
        Assert.Equal(new YearMonth(2026, 5), loaded.LastGeneratedPeriod);
        var line = Assert.Single(loaded.Lines);
        Assert.Equal(("TV", 5m, 1m, "pc"), (line.Description, line.Gross, line.Quantity, line.Unit));
        Assert.Equal("Ref", Assert.Single(loaded.CustomFields).Name);
        Assert.Equal(
            (ScheduleKind.EveryNMonths, 3, new YearMonth(2026, 2), new YearMonth(2027, 2), 31),
            (loaded.Schedule.Kind, loaded.Schedule.EveryMonths, loaded.Schedule.StartMonth, loaded.Schedule.EndMonth, loaded.Schedule.DayOfMonth));
    }

    [Fact]
    public async Task A_deleted_template_is_hidden()
    {
        using var rig = await ArrangeAsync();
        var template = Monthly(rig);
        await rig.Templates.SaveAsync(template);

        await rig.Templates.DeleteAsync(template.Id);

        Assert.Empty(await rig.Templates.ListAsync());
        Assert.Null(await rig.Templates.GetAsync(template.Id));
    }

    [Fact]
    public async Task Generation_creates_the_due_drafts_and_remembers_the_last_period()
    {
        using var rig = await ArrangeAsync();
        var template = Monthly(rig);
        await rig.Templates.SaveAsync(template);

        var created = await rig.Templates.GenerateDraftsAsync(Today);

        // prepaid, today 15.10.2026: August … November
        Assert.Equal(4, created);
        var drafts = await rig.Bills.ListAsync();
        Assert.Equal(["2026-08", "2026-09", "2026-10", "2026-11"], drafts.Select(b => b.Period.ToString()));
        Assert.All(drafts, b => Assert.True(b.IsDraft));
        Assert.All(drafts, b => Assert.Equal(template.Id, b.TemplateId));
        Assert.All(drafts, b => Assert.Equal(22.95m, b.Gross));
        // period 2026-11 is issued 05.10.2026 and due 19.10.2026
        var november = drafts.Single(b => b.Period == new YearMonth(2026, 11));
        Assert.Equal((new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 19)), (november.IssuedOn, november.DueOn));
        Assert.Equal(new YearMonth(2026, 11), (await rig.Templates.GetAsync(template.Id))!.LastGeneratedPeriod);
    }

    [Fact]
    public async Task Generation_twice_creates_no_duplicates()
    {
        using var rig = await ArrangeAsync();
        await rig.Templates.SaveAsync(Monthly(rig));

        await rig.Templates.GenerateDraftsAsync(Today);
        var second = await rig.Templates.GenerateDraftsAsync(Today);

        Assert.Equal(0, second);
        Assert.Equal(4, (await rig.Bills.ListAsync()).Count);
    }

    [Fact]
    public async Task A_deleted_draft_does_not_come_back_and_a_new_month_adds_one_draft()
    {
        using var rig = await ArrangeAsync();
        var template = Monthly(rig);
        await rig.Templates.SaveAsync(template);
        await rig.Templates.GenerateDraftsAsync(Today);
        var august = (await rig.Bills.ListAsync()).Single(b => b.Period == new YearMonth(2026, 8));
        await rig.Bills.DeleteAsync(august.Id);

        // even if the template's last period is reset (e.g. edited), the deleted draft is still recognised
        var stored = (await rig.Templates.GetAsync(template.Id))!;
        stored.LastGeneratedPeriod = null;
        await rig.Templates.SaveAsync(stored);
        var again = await rig.Templates.GenerateDraftsAsync(Today);
        Assert.Equal(0, again);
        Assert.DoesNotContain(await rig.Bills.ListAsync(), b => b.Period == new YearMonth(2026, 8));

        // a month later the next period is added
        var next = await rig.Templates.GenerateDraftsAsync(new DateOnly(2026, 11, 2));
        Assert.Equal(1, next);
        Assert.Contains(await rig.Bills.ListAsync(), b => b.Period == new YearMonth(2026, 12));
    }

    [Fact]
    public async Task Inactive_and_once_templates_create_nothing()
    {
        using var rig = await ArrangeAsync();
        var inactive = Monthly(rig, "Old");
        inactive.IsActive = false;
        var once = Monthly(rig, "Meter");
        once.Schedule = new Schedule { Kind = ScheduleKind.Once };
        await rig.Templates.SaveAsync(inactive);
        await rig.Templates.SaveAsync(once);

        Assert.Equal(0, await rig.Templates.GenerateDraftsAsync(Today));
        Assert.Empty(await rig.Bills.ListAsync());
    }

    [Fact]
    public async Task A_draft_of_a_contract_template_is_valid_and_has_the_contract_currency()
    {
        using var rig = await ArrangeAsync();
        await rig.Templates.SaveAsync(Monthly(rig, timing: BillingTiming.InArrears));

        await rig.Templates.GenerateDraftsAsync(Today);

        var draft = (await rig.Bills.ListAsync()).First();
        Assert.Equal(rig.Contract.Currency, draft.Currency);
        Assert.True(BillValidator.Validate(draft, rig.Contract).IsValid);
    }

    [Fact]
    public async Task Drafts_are_left_out_of_the_per_contract_and_per_property_lists_but_not_of_the_full_list()
    {
        using var rig = await ArrangeAsync();
        await rig.Templates.SaveAsync(Monthly(rig));
        await rig.Templates.GenerateDraftsAsync(Today);
        var confirmed = new Bill { PropertyId = rig.Flat.Id, CategoryId = rig.Category.Id, ContractId = rig.Contract.Id, Period = new YearMonth(2026, 7), Gross = 20m };
        await rig.Bills.SaveAsync(confirmed);

        Assert.Equal(5, (await rig.Bills.ListAsync()).Count);
        Assert.Equal(confirmed.Id, Assert.Single(await rig.Bills.ListByContractAsync(rig.Contract.Id)).Id);
        Assert.Equal(confirmed.Id, Assert.Single(await rig.Bills.ListByPropertyAsync(rig.Flat.Id, null, null)).Id);
    }

    [Fact]
    public async Task Confirming_a_draft_makes_it_count()
    {
        using var rig = await ArrangeAsync();
        await rig.Templates.SaveAsync(Monthly(rig));
        await rig.Templates.GenerateDraftsAsync(Today);
        var draft = (await rig.Bills.ListAsync()).First();

        draft.IsDraft = false;
        await rig.Bills.SaveAsync(draft);

        Assert.Equal(draft.Id, Assert.Single(await rig.Bills.ListByContractAsync(rig.Contract.Id)).Id);
    }

    [Fact]
    public async Task Overview_and_report_loaders_keep_drafts_apart()
    {
        using var rig = await ArrangeAsync();
        await rig.Templates.SaveAsync(Monthly(rig));
        await rig.Templates.GenerateDraftsAsync(Today);
        var confirmed = new Bill { PropertyId = rig.Flat.Id, CategoryId = rig.Category.Id, ContractId = rig.Contract.Id, Period = new YearMonth(2026, 7), Gross = 20m };
        await rig.Bills.SaveAsync(confirmed);

        var overview = await new OverviewDataLoader(rig.Test.Factory, new ChargeInputLoader(rig.Test.Factory)).LoadAsync(Today);
        var reports = await new ReportDataLoader(rig.Test.Factory).LoadAsync();

        Assert.Equal(confirmed.Id, Assert.Single(overview.Bills).Id);
        Assert.Equal(4, overview.Drafts.Count);
        Assert.Equal(confirmed.Id, Assert.Single(reports.Bills).Id);
    }

    [Fact]
    public async Task Quantities_typed_on_a_draft_do_not_reach_the_charge_input()
    {
        using var rig = await ArrangeAsync();
        var draft = new Bill
        {
            PropertyId = rig.Flat.Id, CategoryId = rig.Category.Id, ContractId = rig.Contract.Id,
            Period = new YearMonth(2026, 9), Gross = 0m, IsDraft = true,
            BillQuantities = [new BillQuantity(Guid.NewGuid(), 3m)],
        };
        await rig.Bills.SaveAsync(draft);

        var input = await new ChargeInputLoader(rig.Test.Factory).LoadAsync(rig.Contract.Id, new YearMonth(2026, 9));

        Assert.NotNull(input);
        Assert.True(input.QuantitiesFromBill is null or { Count: 0 });
    }
}
