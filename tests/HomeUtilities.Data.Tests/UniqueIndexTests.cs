using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Tariffs;
using Microsoft.EntityFrameworkCore;

namespace HomeUtilities.Data.Tests;

public class UniqueIndexTests
{
    [Fact]
    public async Task VatCode_CanBeReusedAfterSoftDelete()
    {
        using var test = new TestDb();
        await using var db = test.NewContext();
        var first = new VatRate { Code = "LV-TEST", Name = "First" };
        db.Add(first);
        await db.SaveChangesAsync();

        first.IsDeleted = true;
        await db.SaveChangesAsync();
        db.Add(new VatRate { Code = "LV-TEST", Name = "Second" });
        await db.SaveChangesAsync();

        var active = await db.Set<VatRate>().Where(v => v.Code == "LV-TEST").ToListAsync();
        Assert.Equal(["Second"], active.Select(v => v.Name));
    }

    [Fact]
    public async Task VatCode_TwoActiveRatesWithSameCode_AreRefused()
    {
        using var test = new TestDb();
        await using var db = test.NewContext();
        db.Add(new VatRate { Code = "LV-TEST", Name = "First" });
        await db.SaveChangesAsync();

        db.Add(new VatRate { Code = "LV-TEST", Name = "Second" });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task SystemKey_CanBeReusedAfterSoftDelete()
    {
        using var test = new TestDb();
        await using var db = test.NewContext();
        var first = new Category { SystemKey = ServiceKind.Gas };
        db.Add(first);
        await db.SaveChangesAsync();

        first.IsDeleted = true;
        await db.SaveChangesAsync();
        db.Add(new Category { SystemKey = ServiceKind.Gas });
        await db.SaveChangesAsync();

        Assert.Equal(1, await db.Set<Category>().CountAsync(c => c.SystemKey == ServiceKind.Gas));
    }

    [Fact]
    public async Task SystemKey_TwoActiveCategoriesWithSameKey_AreRefused()
    {
        using var test = new TestDb();
        await using var db = test.NewContext();
        db.Add(new Category { SystemKey = ServiceKind.Gas });
        await db.SaveChangesAsync();

        db.Add(new Category { SystemKey = ServiceKind.Gas });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
