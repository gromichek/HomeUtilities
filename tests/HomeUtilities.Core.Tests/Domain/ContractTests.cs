using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Tests.Domain;

public class ContractTests
{
    private static Link L(LinkKind kind, string title, int order = 0) =>
        new() { Kind = kind, Title = title, Url = "https://example.com", SortOrder = order };

    [Fact]
    public void IsActiveOn_bounds_are_inclusive()
    {
        var c = new Contract { ValidFrom = new DateOnly(2024, 1, 1), ValidTo = new DateOnly(2024, 12, 31) };
        Assert.False(c.IsActiveOn(new DateOnly(2023, 12, 31)));
        Assert.True(c.IsActiveOn(new DateOnly(2024, 1, 1)));
        Assert.True(c.IsActiveOn(new DateOnly(2024, 12, 31)));
        Assert.False(c.IsActiveOn(new DateOnly(2025, 1, 1)));
    }

    [Fact]
    public void IsActiveOn_open_ended()
    {
        var c = new Contract { ValidFrom = new DateOnly(2024, 1, 1) };
        Assert.True(c.IsActiveOn(new DateOnly(2099, 1, 1)));
    }

    [Fact]
    public void IsActiveOn_deleted_is_false()
    {
        var c = new Contract { ValidFrom = new DateOnly(2024, 1, 1), IsDeleted = true };
        Assert.False(c.IsActiveOn(new DateOnly(2024, 6, 1)));
    }

    [Fact]
    public void Effective_links_contract_overrides_per_kind_and_other_is_kept()
    {
        var supplier = new Supplier
        {
            Links = [L(LinkKind.Website, "s.Website"), L(LinkKind.CustomerPortal, "s.Portal", 1), L(LinkKind.Other, "s.Other", 2)],
        };
        var contract = new Contract
        {
            Links = [L(LinkKind.CustomerPortal, "c.Portal"), L(LinkKind.Other, "c.Other", 1)],
        };

        var titles = contract.GetEffectiveLinks(supplier).Select(l => l.Title!).ToArray();

        Assert.Equal(["c.Portal", "c.Other", "s.Website", "s.Other"], titles);
    }

    [Fact]
    public void Effective_links_are_ordered_by_sort_order_and_exclude_deleted()
    {
        var supplier = new Supplier
        {
            Links = [L(LinkKind.Tariffs, "s2", 2), L(LinkKind.Website, "s1", 1), new Link { Kind = LinkKind.PayOnline, Title = "sx", IsDeleted = true }],
        };
        var contract = new Contract
        {
            Links = [L(LinkKind.SubmitReadings, "c2", 5), L(LinkKind.PayOnline, "c1", 3), new Link { Kind = LinkKind.Website, Title = "cx", IsDeleted = true }],
        };

        var titles = contract.GetEffectiveLinks(supplier).Select(l => l.Title!).ToArray();

        // A deleted contract link does not override the supplier's Website link.
        Assert.Equal(["c1", "c2", "s1", "s2"], titles);
    }

    [Theory]
    [InlineData(20, 26, 2026, 10, "2026-10-20", "2026-10-26")]
    [InlineData(25, 31, 2026, 2, "2026-02-25", "2026-02-28")]
    [InlineData(26, 3, 2026, 12, "2026-12-26", "2027-01-03")]
    [InlineData(30, 31, 2028, 2, "2028-02-29", "2028-02-29")]
    public void Reading_window(int from, int to, int year, int month, string expectedFrom, string expectedTo)
    {
        var c = new Contract { ReadingDayFrom = from, ReadingDayTo = to };
        var window = c.GetReadingWindow(new YearMonth(year, month));
        Assert.Equal((DateOnly.Parse(expectedFrom), DateOnly.Parse(expectedTo)), window);
    }

    [Fact]
    public void Wrapping_window_clamps_day_in_next_month()
    {
        // 31 -> 30 wraps; in 2026-01 it ends on 2026-02-28 (30 clamped to month length).
        var c = new Contract { ReadingDayFrom = 31, ReadingDayTo = 30 };
        var window = c.GetReadingWindow(new YearMonth(2026, 1));
        Assert.Equal((new DateOnly(2026, 1, 31), new DateOnly(2026, 2, 28)), window);
    }

    [Fact]
    public void Reading_window_unset_is_null()
    {
        Assert.Null(new Contract().GetReadingWindow(new YearMonth(2026, 10)));
        Assert.Null(new Contract { ReadingDayFrom = 20 }.GetReadingWindow(new YearMonth(2026, 10)));
    }
}
