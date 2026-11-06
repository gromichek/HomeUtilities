using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels.Tests;

/// <summary>Columns of catalog lists and the centred single column of date lists (UI_GUIDELINES §3.1, R-21).</summary>
public class ListLayoutRulesTests
{
    [Theory]
    [InlineData(0, 1)] // size not known yet
    [InlineData(360, 1)] // a phone
    [InlineData(727, 1)] // 2 * 360 + 8 = 728 would be needed for two columns
    [InlineData(728, 2)]
    [InlineData(900, 2)] // a half-width Windows window
    [InlineData(1096, 3)] // 3 * 360 + 2 * 8 = 1096
    [InlineData(1240, 3)] // a maximised window: three columns
    [InlineData(2560, 3)] // never more than three
    public void A_catalog_list_gets_as_many_columns_of_360_as_fit_but_at_most_three(double width, int expected) =>
        Assert.Equal(expected, ListLayoutRules.SpanFor(width, 360));

    [Fact]
    public void Without_a_minimum_width_there_is_one_column() => Assert.Equal(1, ListLayoutRules.SpanFor(1240, 0));

    [Theory]
    [InlineData(1240, 760, 240)] // maximised window: 240 dp on each side of a 760 dp column
    [InlineData(1241, 760, 240)] // an odd width rounds down, never negative
    [InlineData(760, 760, 0)]
    [InlineData(360, 760, 0)] // a phone uses the whole width
    [InlineData(1240, 0, 0)] // no limit
    public void A_date_list_is_centred_with_a_side_margin_that_leaves_at_most_760(double width, double max, double expected) =>
        Assert.Equal(expected, ListLayoutRules.SideMarginFor(width, max));
}
