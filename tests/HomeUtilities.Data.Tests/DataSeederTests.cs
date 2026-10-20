using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Tariffs;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HomeUtilities.Data.Tests;

public class DataSeederTests
{
    private static async Task SeedAsync(TestDb test)
    {
        await using var db = test.NewContext();
        await DataSeeder.SeedAsync(db, CancellationToken.None);
    }

    [Fact]
    public async Task First_run_inserts_vat_rates_with_versions_and_a_category_per_service_kind()
    {
        using var test = new TestDb();

        await SeedAsync(test);

        await using var db = test.NewContext();
        var rates = await db.VatRates.Include(v => v.Versions).OrderBy(v => v.Code).ToListAsync();
        Assert.Equal(["LV-REDUCED", "LV-STANDARD", "ZERO"], rates.Select(r => r.Code));
        Assert.Equal([3, 4, 1], rates.Select(r => r.Versions.Count));
        Assert.Equal(21m, rates.Single(r => r.Code == "LV-STANDARD").PercentAt(new DateOnly(2026, 9, 1)));
        var categories = await db.Categories.ToListAsync();
        Assert.Equal(Enum.GetValues<ServiceKind>().Length, categories.Count);
        Assert.Equal(Enum.GetValues<ServiceKind>().Order(), categories.Select(c => c.SystemKey!.Value).Order());
    }

    [Fact]
    public async Task Second_run_inserts_nothing()
    {
        using var test = new TestDb();
        await SeedAsync(test);
        await using (var before = test.NewContext())
        {
            Assert.Equal(3, await before.VatRates.CountAsync());
        }

        test.Clock.UtcNow = test.Clock.UtcNow.AddDays(1);
        await SeedAsync(test);

        await using var db = test.NewContext();
        Assert.Equal(3, await db.VatRates.IgnoreQueryFilters().CountAsync());
        Assert.Equal(8, await db.VatRateVersions.IgnoreQueryFilters().CountAsync());
        Assert.Equal(Enum.GetValues<ServiceKind>().Length, await db.Categories.IgnoreQueryFilters().CountAsync());
        Assert.All(await db.Categories.ToListAsync(), c => Assert.Equal(new DateTimeOffset(2026, 10, 20, 8, 0, 0, TimeSpan.Zero), c.UpdatedAt));
    }

    [Fact]
    public async Task User_renamed_built_in_category_keeps_its_name()
    {
        using var test = new TestDb();
        await SeedAsync(test);
        await using (var db = test.NewContext())
        {
            var water = await db.Categories.SingleAsync(c => c.SystemKey == ServiceKind.ColdWater);
            water.Name = "My water";
            water.Color = "#000000";
            await db.SaveChangesAsync();
        }

        await SeedAsync(test);

        await using var read = test.NewContext();
        var loaded = await read.Categories.SingleAsync(c => c.SystemKey == ServiceKind.ColdWater);
        Assert.Equal(("My water", "#000000"), (loaded.Name, loaded.Color));
    }

    [Fact]
    public async Task Soft_deleted_vat_code_is_not_reinserted()
    {
        using var test = new TestDb();
        await SeedAsync(test);
        await using (var db = test.NewContext())
        {
            db.VatRates.Remove(await db.VatRates.Include(v => v.Versions).SingleAsync(v => v.Code == "ZERO"));
            await db.SaveChangesAsync();
        }

        await SeedAsync(test);

        await using var read = test.NewContext();
        Assert.Equal(2, await read.VatRates.CountAsync());
        Assert.Equal(3, await read.VatRates.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Initializer_migrates_and_seeds_a_file_database_idempotently()
    {
        var path = Path.Combine(Path.GetTempPath(), $"homeutilities-test-{Guid.NewGuid():N}.db");
        try
        {
            var services = new ServiceCollection();
            services.AddSingleton<IClock>(new FakeClock(DateTimeOffset.UtcNow));
            services.AddHomeUtilitiesData(path);
            await using var provider = services.BuildServiceProvider();
            var initializer = provider.GetRequiredService<IDatabaseInitializer>();

            await initializer.InitializeAsync();
            await initializer.InitializeAsync();

            Assert.Equal(Enum.GetValues<ServiceKind>().Length, (await provider.GetRequiredService<ICategoryStore>().ListAsync()).Count);
            Assert.Equal(3, (await provider.GetRequiredService<IVatRateStore>().ListAsync()).Count);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            foreach (var file in Directory.GetFiles(Path.GetTempPath(), Path.GetFileName(path) + "*"))
            {
                File.Delete(file);
            }
        }
    }
}
