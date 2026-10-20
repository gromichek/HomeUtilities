using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Payments;
using HomeUtilities.Data.Stores;
using Microsoft.EntityFrameworkCore;

namespace HomeUtilities.Data.Tests.Stores;

public class AggregateMergerTests
{
    private static PropertyParameter Param(Property p, decimal value, int year = 2024) =>
        new() { PropertyId = p.Id, Kind = PropertyParameterKind.TotalArea, ValidFrom = new DateOnly(year, 1, 1), Value = value };

    private static async Task<Property> SaveNewAsync(TestDb test, params decimal[] areas)
    {
        var p = new Property { Name = "Flat", Kind = PropertyKind.Apartment };
        for (var i = 0; i < areas.Length; i++)
        {
            p.Parameters.Add(Param(p, areas[i], 2020 + i));
        }

        await new PropertyStore(test.Factory).SaveAsync(p);
        return p;
    }

    [Fact]
    public async Task Update_scalar_of_detached_root()
    {
        using var test = new TestDb();
        var saved = await SaveNewAsync(test, 50m);
        var store = new PropertyStore(test.Factory);

        var detached = (await store.GetAsync(saved.Id))!;
        detached.Name = "Renamed";
        detached.Address = "1 Test Street";
        test.Clock.UtcNow = test.Clock.UtcNow.AddHours(1);
        await store.SaveAsync(detached);

        var loaded = (await store.GetAsync(saved.Id))!;
        Assert.Equal("Renamed", loaded.Name);
        Assert.Equal("1 Test Street", loaded.Address);
        Assert.Equal(saved.CreatedAt, loaded.CreatedAt);
        Assert.Equal(test.Clock.UtcNow, loaded.UpdatedAt);
        Assert.Single(loaded.Parameters);
    }

    [Fact]
    public async Task Added_parameter_is_inserted()
    {
        using var test = new TestDb();
        var saved = await SaveNewAsync(test, 50m);
        var store = new PropertyStore(test.Factory);

        var detached = (await store.GetAsync(saved.Id))!;
        detached.Parameters.Add(Param(detached, 55m, 2025));
        await store.SaveAsync(detached);

        var loaded = (await store.GetAsync(saved.Id))!;
        Assert.Equal([50m, 55m], loaded.Parameters.Select(p => p.Value));
    }

    [Fact]
    public async Task Removed_parameter_is_soft_deleted_and_row_remains()
    {
        using var test = new TestDb();
        var saved = await SaveNewAsync(test, 50m, 55m);
        var store = new PropertyStore(test.Factory);

        var detached = (await store.GetAsync(saved.Id))!;
        var removed = detached.Parameters[0];
        detached.Parameters.Remove(removed);
        await store.SaveAsync(detached);

        Assert.Equal([55m], (await store.GetAsync(saved.Id))!.Parameters.Select(p => p.Value));
        await using var db = test.NewContext();
        var row = await db.PropertyParameters.IgnoreQueryFilters().SingleAsync(p => p.Id == removed.Id);
        Assert.True(row.IsDeleted);
    }

    [Fact]
    public async Task Reading_value_changed_deep_in_meter_graph()
    {
        using var test = new TestDb();
        var propertyId = await StoreTests.SavePropertyAsync(test);
        var meter = new Meter { PropertyId = propertyId, Resource = MeterResource.ColdWater, Unit = MeterUnit.M3, InstalledOn = new DateOnly(2020, 1, 1) };
        var register = MeterRegister.CreateDefault(meter.Id);
        register.Readings.Add(new MeterReading { RegisterId = register.Id, Date = new DateOnly(2026, 8, 26), Value = 425.9m });
        register.Readings.Add(new MeterReading { RegisterId = register.Id, Date = new DateOnly(2026, 9, 26), Value = 437.097m });
        meter.Registers.Add(register);
        var store = new MeterStore(test.Factory);
        await store.SaveAsync(meter);

        var detached = (await store.GetAsync(meter.Id))!;
        detached.Registers[0].Readings[1].Value = 437.5m;
        detached.Registers[0].Readings.Add(new MeterReading { RegisterId = register.Id, Date = new DateOnly(2026, 10, 26), Value = 450m });
        await store.SaveAsync(detached);

        var loaded = (await store.GetAsync(meter.Id))!;
        Assert.Equal([425.9m, 437.5m, 450m], loaded.Registers[0].Readings.Select(r => r.Value));
    }

    [Fact]
    public async Task Graph_from_a_different_context_instance_works()
    {
        using var test = new TestDb();
        Property detached;
        await using (var first = test.NewContext())
        {
            var p = new Property { Name = "Flat" };
            p.Parameters.Add(Param(p, 40m));
            first.Properties.Add(p);
            await first.SaveChangesAsync();
            detached = p;
        }

        // 'first' is gone; the graph is attached to nothing and is saved through new contexts.
        detached.Parameters[0].Value = 41m;
        await new PropertyStore(test.Factory).SaveAsync(detached);

        var loaded = (await new PropertyStore(test.Factory).GetAsync(detached.Id))!;
        Assert.Equal(41m, loaded.Parameters.Single().Value);
    }

    [Fact]
    public async Task Complex_payment_details_are_merged()
    {
        using var test = new TestDb();
        var store = new SupplierStore(test.Factory);
        var supplier = new Supplier { Name = "Invented Water Ltd" };
        await store.SaveAsync(supplier);

        var details = new PaymentDetails("Invented Water Ltd", "40000000000", "LV00TEST0000000000000", "TESTLV22", "Test Bank", "Water {period}");
        var withDetails = (await store.GetAsync(supplier.Id))!;
        withDetails.PaymentDetails = details;
        await store.SaveAsync(withDetails);
        Assert.Equal(details, (await store.GetAsync(supplier.Id))!.PaymentDetails);

        var cleared = (await store.GetAsync(supplier.Id))!;
        cleared.PaymentDetails = null;
        await store.SaveAsync(cleared);
        Assert.Null((await store.GetAsync(supplier.Id))!.PaymentDetails);
    }

    [Fact]
    public async Task Json_column_changes_are_saved()
    {
        using var test = new TestDb();
        var store = new SupplierStore(test.Factory);
        var supplier = new Supplier { Name = "S" };
        supplier.Links.Add(new Link { OwnerId = supplier.Id, Kind = LinkKind.Website, Url = "https://example.test" });
        await store.SaveAsync(supplier);

        var detached = (await store.GetAsync(supplier.Id))!;
        detached.Links[0].Url = "https://example.test/new";
        detached.Links.Add(new Link { OwnerId = supplier.Id, Kind = LinkKind.CustomerPortal, Url = "https://example.test/portal" });
        await store.SaveAsync(detached);

        var loaded = (await store.GetAsync(supplier.Id))!;
        Assert.Equal(["https://example.test/new", "https://example.test/portal"], loaded.Links.Select(l => l.Url));
    }

    [Fact]
    public async Task Bill_lines_added_and_removed()
    {
        using var test = new TestDb();
        var (propertyId, categoryId) = await PrepareBillOwnersAsync(test);
        var store = new BillStore(test.Factory);
        var bill = new Bill { PropertyId = propertyId, CategoryId = categoryId, Period = new(2026, 9), Gross = 10m, Kind = BillKind.OneOff };
        bill.Lines.Add(new BillLine { BillId = bill.Id, Description = "A", Gross = 4m, SortOrder = 1 });
        bill.Lines.Add(new BillLine { BillId = bill.Id, Description = "B", Gross = 6m, SortOrder = 2 });
        await store.SaveAsync(bill);

        var detached = (await store.GetAsync(bill.Id))!;
        detached.Lines.RemoveAt(0);
        detached.Lines.Add(new BillLine { BillId = bill.Id, Description = "C", Gross = 1m, SortOrder = 3 });
        await store.SaveAsync(detached);

        Assert.Equal(["B", "C"], (await store.GetAsync(bill.Id))!.Lines.Select(l => l.Description));
    }

    internal static async Task<(Guid PropertyId, Guid CategoryId)> PrepareBillOwnersAsync(TestDb test)
    {
        var property = new Property { Name = "P" };
        var category = new Category { Name = "C" };
        await using var db = test.NewContext();
        db.Add(property);
        db.Add(category);
        await db.SaveChangesAsync();
        return (property.Id, category.Id);
    }
}
