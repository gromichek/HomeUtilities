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

    [Theory]
    [InlineData(900, 400, LayoutMode.Compact)] // phone in landscape
    [InlineData(2000, 479.9, LayoutMode.Compact)]
    [InlineData(900, 480, LayoutMode.Medium)]
    [InlineData(1280, 800, LayoutMode.Expanded)] // tablet landscape
    [InlineData(1920, 1000, LayoutMode.Expanded)]
    [InlineData(360, 800, LayoutMode.Compact)] // phone portrait
    [InlineData(700, 0, LayoutMode.Medium)] // height unknown yet: width only
    public void FromSize_LowWindowIsAPhone(double width, double height, LayoutMode expected)
    {
        Assert.Equal(expected, LayoutModes.FromSize(width, height));
    }
}
