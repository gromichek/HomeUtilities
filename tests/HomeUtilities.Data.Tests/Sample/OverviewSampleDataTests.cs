using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Overview;
using HomeUtilities.Data.Sample;
using HomeUtilities.Data.Stores;

namespace HomeUtilities.Data.Tests.Sample;

/// <summary>The sample data produces the situations the Overview is about, whatever day it is generated on.</summary>
public class OverviewSampleDataTests
{
    private static async Task<(OverviewData Data, IReadOnlyList<TodoItem> Todo, DateOnly Today)> LoadAsync(DateTimeOffset now)
    {
        using var test = new TestDb();
        test.Clock.UtcNow = now;
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

        var today = test.Clock.Today;
        var data = await new OverviewDataLoader(factory, new ChargeInputLoader(factory)).LoadAsync(today);
        return (data, TodoBuilder.Build(data, today), today);
    }

    [Fact]
    public async Task Every_kind_of_item_appears_on_the_test_date()
    {
        var (_, todo, today) = await LoadAsync(new DateTimeOffset(2026, 10, 20, 8, 0, 0, TimeSpan.Zero));

        foreach (var kind in Enum.GetValues<TodoKind>())
        {
            Assert.Contains(todo, i => i.Kind == kind);
        }

        Assert.Contains(todo, i => i is { Kind: TodoKind.BillDue, Severity: TodoSeverity.Overdue });
        Assert.Contains(todo, i => i is { Kind: TodoKind.BillDue, Severity: TodoSeverity.Soon });
        Assert.Contains(todo, i => i is { Kind: TodoKind.VerificationDue, Severity: TodoSeverity.Overdue });
        Assert.Contains(todo, i => i is { Kind: TodoKind.ReadingsWindow, Severity: TodoSeverity.Today });
        Assert.Equal(today, todo.First(i => i.Kind == TodoKind.SettlementExpected).Date);
    }

    [Theory]
    [InlineData(2026, 1, 1)]
    [InlineData(2026, 1, 31)]
    [InlineData(2026, 2, 28)]
    [InlineData(2026, 3, 29)]
    [InlineData(2026, 5, 15)]
    [InlineData(2026, 8, 31)]
    [InlineData(2026, 12, 31)]
    public async Task The_items_that_do_not_depend_on_the_tax_calendar_appear_on_any_day(int year, int month, int day)
    {
        var (_, todo, _) = await LoadAsync(new DateTimeOffset(year, month, day, 8, 0, 0, TimeSpan.Zero));

        foreach (var kind in new[] { TodoKind.ReadingsWindow, TodoKind.BillDue, TodoKind.BillMissing, TodoKind.VerificationDue, TodoKind.SettlementExpected })
        {
            Assert.Contains(todo, i => i.Kind == kind);
        }

        Assert.Contains(todo, i => i is { Kind: TodoKind.BillDue, Severity: TodoSeverity.Overdue });
        Assert.Contains(todo, i => i is { Kind: TodoKind.BillDue, Severity: TodoSeverity.Soon });
    }

    [Theory]
    [InlineData(2026, 10, 20)]
    [InlineData(2026, 3, 5)]
    [InlineData(2027, 1, 31)]
    public async Task Drafts_of_the_templates_are_listed_per_property_and_are_not_in_the_bills(int year, int month, int day)
    {
        var (data, todo, _) = await LoadAsync(new DateTimeOffset(year, month, day, 8, 0, 0, TimeSpan.Zero));

        // the two newest prepaid internet periods (flat) and the chimney cleaning (cottage)
        Assert.All(data.Drafts, d => Assert.True(d.IsDraft));
        Assert.All(data.Bills, b => Assert.False(b.IsDraft));
        var items = todo.Where(i => i.Kind == TodoKind.DraftsToConfirm).ToList();
        Assert.Equal(2, items.Count);
        Assert.Equal(data.Drafts.Count, items.Sum(i => i.Count));
        Assert.Equal(2, items.Single(i => i.PropertyId == data.Properties[0].Id).Count);
    }

    [Fact]
    public async Task The_settlement_of_the_cottage_is_loaded_and_the_data_has_every_table()
    {
        var (data, _, _) = await LoadAsync(new DateTimeOffset(2026, 10, 20, 8, 0, 0, TimeSpan.Zero));

        Assert.Equal(3, data.Properties.Count);
        Assert.Equal(10, data.Contracts.Count);
        Assert.NotEmpty(data.ContractMeters);
        Assert.NotEmpty(data.Bills);
        Assert.NotEmpty(data.Payments);
        var equalized = Assert.Single(data.Contracts, c => c.BillingMode == BillingMode.Equalized);
        Assert.True(Math.Abs(data.ExpectedSettlements[equalized.Id]) >= TodoBuilder.SettlementMinimum);
        Assert.Single(data.ExpectedSettlements);
    }
}
