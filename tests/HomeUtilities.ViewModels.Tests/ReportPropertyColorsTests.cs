namespace HomeUtilities.ViewModels.Tests;

public sealed class ReportPropertyColorsTests
{
    private static (Guid Id, string Name) P(string name) => (Guid.NewGuid(), name);

    [Fact]
    public void The_slot_is_the_position_in_the_alphabetical_list_of_all_properties()
    {
        var (cottage, flat, barn) = (P("Cottage"), P("flat"), P("Barn"));

        // Given in any order: Barn = 0, Cottage = 1, flat = 2 (case is ignored).
        var colors = ReportPropertyColors.Assign([cottage, flat, barn]);

        Assert.Equal([0, 1, 2], [colors[barn.Id], colors[cottage.Id], colors[flat.Id]]);
    }

    [Fact]
    public void A_property_keeps_its_slot_whatever_the_filter_shows()
    {
        var all = new[] { P("Alpha"), P("Beta"), P("Gamma") };
        var colors = ReportPropertyColors.Assign(all);

        // The map is built from all properties, not from the filtered ones, so Gamma is 2 even when it is shown alone.
        Assert.Equal(2, colors[all[2].Id]);
        Assert.Equal(colors, ReportPropertyColors.Assign(all.Reverse()));
    }

    [Fact]
    public void The_slots_wrap_after_the_palette_size()
    {
        var all = Enumerable.Range(0, ReportPropertyColors.PaletteSize + 2).Select(i => P($"Property {i:00}")).ToList();

        var colors = ReportPropertyColors.Assign(all);

        Assert.Equal(8, ReportPropertyColors.PaletteSize);
        Assert.Equal(7, colors[all[7].Id]);
        Assert.Equal(0, colors[all[8].Id]);
        Assert.Equal(1, colors[all[9].Id]);
    }

    [Fact]
    public void Properties_with_equal_names_get_stable_distinct_slots()
    {
        var (a, b) = (P("Same"), P("Same"));

        var colors = ReportPropertyColors.Assign([a, b]);

        Assert.NotEqual(colors[a.Id], colors[b.Id]);
        Assert.Equal(colors, ReportPropertyColors.Assign([b, a]));
    }
}
