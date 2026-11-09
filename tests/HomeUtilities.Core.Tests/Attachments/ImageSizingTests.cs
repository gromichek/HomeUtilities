using HomeUtilities.Core.Attachments;

namespace HomeUtilities.Core.Tests.Attachments;

public class ImageSizingTests
{
    [Theory]
    [InlineData(4000, 3000, 2560, 2560, 1920)] // 12 MP landscape: long side 4000 -> 2560, 3000 * 0.64 = 1920
    [InlineData(3000, 4000, 2560, 1920, 2560)] // portrait
    [InlineData(2560, 1440, 2560, 2560, 1440)] // exactly at the limit: unchanged
    [InlineData(800, 600, 2560, 800, 600)] // smaller: never enlarged
    [InlineData(6000, 1, 2560, 2560, 1)] // a thin panorama keeps at least one pixel
    [InlineData(2561, 10, 2560, 2560, 10)] // 10 * 0.99961 = 9.996 -> 10
    [InlineData(2000, 1500, 2560, 2000, 1500)]
    [InlineData(2561, 1, 2560, 2560, 1)] // never below one pixel
    [InlineData(4000, 3000, 360, 360, 270)] // thumbnail: 3000 * 0.09 = 270
    public void Fit_LimitsTheLongSideAndKeepsTheAspectRatio(int width, int height, int maxSide, int expectedWidth, int expectedHeight) =>
        Assert.Equal((expectedWidth, expectedHeight), ImageSizing.Fit(width, height, maxSide));

    [Fact]
    public void Fit_ZeroSize_IsReturnedAsIs() => Assert.Equal((0, 0), ImageSizing.Fit(0, 0, 100));

    [Fact]
    public void Fit_NonPositiveLimit_Throws() => Assert.Throws<ArgumentOutOfRangeException>(() => ImageSizing.Fit(10, 10, 0));

    [Theory]
    [InlineData(2561, 100, true)]
    [InlineData(2560, 2560, false)]
    public void NeedsShrinking_OnlyAboveTheLimit(int width, int height, bool expected) =>
        Assert.Equal(expected, ImageSizing.NeedsShrinking(width, height, 2560));

    [Theory]
    [InlineData(1, 0)]
    [InlineData(3, 180)]
    [InlineData(6, 90)]
    [InlineData(8, 270)]
    [InlineData(0, 0)]
    [InlineData(99, 0)]
    public void RotationFor_ExifOrientation(int orientation, int degrees) => Assert.Equal(degrees, ImageSizing.RotationFor(orientation));

    [Theory]
    [InlineData(6, true)]
    [InlineData(8, true)]
    [InlineData(3, false)]
    [InlineData(1, false)]
    public void SwapsSides_OnlyForQuarterTurns(int orientation, bool expected) => Assert.Equal(expected, ImageSizing.SwapsSides(orientation));
}
