namespace HomeUtilities.ViewModels.Tests;

public class LayoutModesTests
{
    [Theory]
    [InlineData(0, LayoutMode.Compact)]
    [InlineData(599.9, LayoutMode.Compact)]
    [InlineData(600, LayoutMode.Medium)]
    [InlineData(1000, LayoutMode.Medium)]
    [InlineData(1000.1, LayoutMode.Expanded)]
    [InlineData(1920, LayoutMode.Expanded)]
    public void FromWidth_UsesThresholds(double width, LayoutMode expected)
    {
        Assert.Equal(expected, LayoutModes.FromWidth(width));
    }
}
