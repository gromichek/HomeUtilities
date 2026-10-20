using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace HomeUtilities.Data.Tests;

public class InterceptorTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 20, 8, 0, 0, TimeSpan.Zero);

    private static (Property Property, Supplier Supplier, Category Category) Basics() =>
        (new Property { Name = "P" }, new Supplier { Name = "S" }, new Category { Name = "C" });

    [Fact]
    public async Task Insert_stamps_both_timestamps()
    {
        using var test = new TestDb();
        var property = new Property { Name = "P" };
        await using var db = test.NewContext();
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        Assert.Equal((T0, T0), (property.CreatedAt, property.UpdatedAt));
        Assert.False(property.IsDeleted);
    }

    [Fact]
    public async Task Insert_stamps_children_too_and_keeps_a_given_created_at()
    {
        using var test = new TestDb();
        var given = T0.AddYears(-1);
        var property = new Property { Name = "P", CreatedAt = given };
        var parameter = new PropertyParameter { PropertyId = property.Id, Kind = PropertyParameterKind.TotalArea, Value = 1m };
        property.Parameters.Add(parameter);
        await using var db = test.NewContext();
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        Assert.Equal((given, T0), (property.CreatedAt, property.UpdatedAt));
        Assert.Equal((T0, T0), (parameter.CreatedAt, parameter.UpdatedAt));
    }

    [Fact]
    public async Task Update_stamps_only_updated_at()
    {
        using var test = new TestDb();
        var id = await AddPropertyAsync(test);
        test.Clock.UtcNow = T0.AddHours(5);

        await using (var db = test.NewContext())
        {
            var property = await db.Properties.SingleAsync();
            property.Name = "Renamed";
            await db.SaveChangesAsync();
        }

        await using var read = test.NewContext();
        var loaded = await read.Properties.SingleAsync(p => p.Id == id);
        Assert.Equal((T0, T0.AddHours(5)), (loaded.CreatedAt, loaded.UpdatedAt));
    }

    [Fact]
    public async Task Unchanged_entities_are_not_stamped()
    {
        using var test = new TestDb();
        await AddPropertyAsync(test);
        test.Clock.UtcNow = T0.AddHours(5);

        await using var db = test.NewContext();
        var property = await db.Properties.SingleAsync();
        await db.SaveChangesAsync();

        Assert.Equal(T0, property.UpdatedAt);
    }

    [Fact]
    public async Task Remove_soft_deletes()
    {
        using var test = new TestDb();
        await AddPropertyAsync(test);
        test.Clock.UtcNow = T0.AddHours(1);

        await using (var db = test.NewContext())
        {
            db.Properties.Remove(await db.Properties.SingleAsync());
            await db.SaveChangesAsync();
        }

        await using var read = test.NewContext();
        var row = await read.Properties.IgnoreQueryFilters().SingleAsync();
        Assert.True(row.IsDeleted);
        Assert.Equal((T0, T0.AddHours(1)), (row.CreatedAt, row.UpdatedAt));
    }

    [Fact]
    public async Task Removing_a_reading_from_the_register_soft_deletes_it()
    {
        using var test = new TestDb();
        var registerId = await AddMeterWithReadingsAsync(test);
        test.Clock.UtcNow = T0.AddHours(2);

        await using (var db = test.NewContext())
        {
            var register = await db.MeterRegisters.Include(r => r.Readings).SingleAsync();
            register.Readings.Remove(register.Readings.First(r => r.Value == 10m));
            await db.SaveChangesAsync();
        }

        await using var read = test.NewContext();
        var all = await read.MeterReadings.IgnoreQueryFilters().OrderBy(r => r.Value).ToListAsync();
        Assert.Equal(2, all.Count);
        Assert.True(all[0].IsDeleted);
        Assert.Equal(T0.AddHours(2), all[0].UpdatedAt);
        Assert.False(all[1].IsDeleted);
        Assert.Equal(T0, all[1].UpdatedAt);
        Assert.Equal(registerId, all[0].RegisterId);
    }

    [Fact]
    public async Task Removing_a_parent_soft_deletes_loaded_children()
    {
        using var test = new TestDb();
        await AddMeterWithReadingsAsync(test);

        await using (var db = test.NewContext())
        {
            var register = await db.MeterRegisters.Include(r => r.Readings).SingleAsync();
            db.MeterRegisters.Remove(register);
            await db.SaveChangesAsync();
        }

        await using var read = test.NewContext();
        Assert.All(await read.MeterReadings.IgnoreQueryFilters().ToListAsync(), r => Assert.True(r.IsDeleted));
        Assert.True((await read.MeterRegisters.IgnoreQueryFilters().SingleAsync()).IsDeleted);
    }

    [Fact]
    public async Task Changing_a_link_inside_the_json_list_stamps_the_owner()
    {
        using var test = new TestDb();
        var (property, supplier, category) = Basics();
        var contract = new Contract { PropertyId = property.Id, SupplierId = supplier.Id, CategoryId = category.Id, Title = "T" };
        contract.Links.Add(new Link { OwnerId = contract.Id, Kind = LinkKind.Website, Url = "https://old.example.test" });
        await using (var db = test.NewContext())
        {
            db.AddRange(property, supplier, category, contract);
            await db.SaveChangesAsync();
        }

        test.Clock.UtcNow = T0.AddDays(1);
        await using (var db = test.NewContext())
        {
            var loaded = await db.Contracts.SingleAsync();
            loaded.Links[0].Url = "https://new.example.test";
            await db.SaveChangesAsync();
        }

        await using var read = test.NewContext();
        var reloaded = await read.Contracts.SingleAsync();
        Assert.Equal("https://new.example.test", reloaded.Links[0].Url);
        Assert.Equal((T0, T0.AddDays(1)), (reloaded.CreatedAt, reloaded.UpdatedAt));
    }

    [Fact]
    public async Task PreserveTimestamps_keeps_given_values()
    {
        using var test = new TestDb();
        var created = T0.AddYears(-2);
        var updated = T0.AddYears(-1);
        await using (var db = test.NewContext())
        {
            db.PreserveTimestamps = true;
            db.Properties.Add(new Property { Name = "Synced", CreatedAt = created, UpdatedAt = updated });
            await db.SaveChangesAsync();
        }

        await using (var db = test.NewContext())
        {
            db.PreserveTimestamps = true;
            var property = await db.Properties.SingleAsync();
            property.Name = "Synced 2";
            await db.SaveChangesAsync();
        }

        await using var read = test.NewContext();
        var row = await read.Properties.SingleAsync();
        Assert.Equal(("Synced 2", created, updated), (row.Name, row.CreatedAt, row.UpdatedAt));
    }

    [Fact]
    public async Task Filters_hide_deleted_rows_unless_ignored()
    {
        using var test = new TestDb();
        await AddPropertyAsync(test);
        await using (var db = test.NewContext())
        {
            db.Properties.Remove(await db.Properties.SingleAsync());
            await db.SaveChangesAsync();
        }

        await using var read = test.NewContext();
        Assert.Empty(await read.Properties.ToListAsync());
        Assert.Single(await read.Properties.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task Deleted_reading_is_not_included_by_default()
    {
        using var test = new TestDb();
        await AddMeterWithReadingsAsync(test);
        await using (var db = test.NewContext())
        {
            var register = await db.MeterRegisters.Include(r => r.Readings).SingleAsync();
            register.Readings.Remove(register.Readings.First(r => r.Value == 10m));
            await db.SaveChangesAsync();
        }

        await using var read = test.NewContext();
        var register2 = await read.MeterRegisters.Include(r => r.Readings).SingleAsync();
        Assert.Equal(20m, Assert.Single(register2.Readings).Value);
        var withDeleted = await read.MeterRegisters.IgnoreQueryFilters().Include(r => r.Readings).SingleAsync();
        Assert.Equal(2, withDeleted.Readings.Count);
    }

    [Fact]
    public async Task Duplicate_vat_code_is_rejected()
    {
        using var test = new TestDb();
        await using var db = test.NewContext();
        db.VatRates.Add(new Core.Tariffs.VatRate { Code = "DUP", Name = "Dup" });
        db.VatRates.Add(new Core.Tariffs.VatRate { Code = "DUP", Name = "Dup" });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Contract_with_unknown_property_is_rejected()
    {
        using var test = new TestDb();
        var (_, supplier, category) = Basics();
        await using var db = test.NewContext();
        db.AddRange(supplier, category, new Contract { PropertyId = Guid.NewGuid(), SupplierId = supplier.Id, CategoryId = category.Id, Title = "T" });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Duplicate_system_key_is_rejected_but_many_user_categories_are_allowed()
    {
        using var test = new TestDb();
        await using (var db = test.NewContext())
        {
            db.Categories.AddRange(new Category { Name = "A" }, new Category { Name = "B" });
            await db.SaveChangesAsync();
        }

        await using var dup = test.NewContext();
        dup.Categories.AddRange(new Category { SystemKey = ServiceKind.Gas }, new Category { SystemKey = ServiceKind.Gas });
        await Assert.ThrowsAsync<DbUpdateException>(() => dup.SaveChangesAsync());
    }

    [Fact]
    public async Task Model_has_no_pending_changes()
    {
        using var test = new TestDb();
        await using var db = test.NewContext();
        Assert.False(db.Database.HasPendingModelChanges());
    }

    private static async Task<Guid> AddPropertyAsync(TestDb test)
    {
        var property = new Property { Name = "P" };
        await using var db = test.NewContext();
        db.Properties.Add(property);
        await db.SaveChangesAsync();
        return property.Id;
    }

    private static async Task<Guid> AddMeterWithReadingsAsync(TestDb test)
    {
        var property = new Property { Name = "P" };
        var meter = new Meter { PropertyId = property.Id, InstalledOn = new DateOnly(2020, 1, 1) };
        var register = MeterRegister.CreateDefault(meter.Id);
        register.Readings.Add(new MeterReading { RegisterId = register.Id, Date = new DateOnly(2026, 1, 1), Value = 10m });
        register.Readings.Add(new MeterReading { RegisterId = register.Id, Date = new DateOnly(2026, 2, 1), Value = 20m });
        meter.Registers.Add(register);
        await using var db = test.NewContext();
        db.AddRange(property, meter);
        await db.SaveChangesAsync();
        return register.Id;
    }
}
