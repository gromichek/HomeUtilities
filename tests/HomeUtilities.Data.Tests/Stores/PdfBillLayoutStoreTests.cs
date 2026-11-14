using HomeUtilities.Core.PdfBills;
using HomeUtilities.Data.Stores;
using Microsoft.EntityFrameworkCore;

namespace HomeUtilities.Data.Tests.Stores;

public class PdfBillLayoutStoreTests
{
    private static PdfBillLayout Layout(string name = "Example Water") => new()
    {
        Name = name,
        Markers = ["SIA \"Piemērs Ūdens\"", "RĒĶINS NR."],
        Fields =
        [
            new() { Field = PdfBillField.Number, Pattern = @"RĒĶINS NR\.:\s+(\S+)" },
            new() { Field = PdfBillField.Gross, Pattern = @"KOPĀ APMAKSAI EUR:\s+([\d.,]+)" },
        ],
        Notes = "invented",
    };

    [Fact]
    public async Task ALayout_RoundTrips_WithMarkersAndFields()
    {
        using var test = new TestDb();
        var store = new PdfBillLayoutStore(test.Factory);
        var layout = Layout();

        await store.SaveAsync(layout);
        var loaded = await store.GetAsync(layout.Id);

        Assert.NotNull(loaded);
        Assert.Equal("Example Water", loaded.Name);
        Assert.Equal(["SIA \"Piemērs Ūdens\"", "RĒĶINS NR."], loaded.Markers);
        Assert.Equal([PdfBillField.Number, PdfBillField.Gross], loaded.Fields.Select(f => f.Field));
        Assert.Equal(@"KOPĀ APMAKSAI EUR:\s+([\d.,]+)", loaded.Fields[1].Pattern);
        Assert.Equal("invented", loaded.Notes);
    }

    [Fact]
    public async Task ReplacingALayout_UpdatesItInPlace()
    {
        using var test = new TestDb();
        var store = new PdfBillLayoutStore(test.Factory);
        var layout = Layout();
        await store.SaveAsync(layout);

        var stored = (await store.GetAsync(layout.Id))!;
        stored.Markers = ["new marker"];
        stored.Fields = [];
        await store.SaveAsync(stored);

        var all = await store.ListAsync();
        var reloaded = Assert.Single(all);
        Assert.Equal(layout.Id, reloaded.Id);
        Assert.Equal(["new marker"], reloaded.Markers);
        Assert.Empty(reloaded.Fields);
    }

    [Fact]
    public async Task Deleting_IsASoftDelete_AndHidesTheLayout()
    {
        using var test = new TestDb();
        var store = new PdfBillLayoutStore(test.Factory);
        var layout = Layout();
        await store.SaveAsync(layout);

        await store.DeleteAsync(layout.Id);

        Assert.Empty(await store.ListAsync());
        using var db = test.NewContext();
        Assert.True(db.PdfBillLayouts.IgnoreQueryFilters().Single(l => l.Id == layout.Id).IsDeleted);
    }

    [Fact]
    public async Task ListIsOrderedByName()
    {
        using var test = new TestDb();
        var store = new PdfBillLayoutStore(test.Factory);
        await store.SaveAsync(Layout("Water"));
        await store.SaveAsync(Layout("Heating"));

        Assert.Equal(["Heating", "Water"], (await store.ListAsync()).Select(l => l.Name));
    }
}
