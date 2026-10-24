using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Tests.Common;

public class MoneyMathTests
{
    [Theory]
    [InlineData("2.345", "2.35")]
    [InlineData("2.344", "2.34")]
    [InlineData("-2.345", "-2.35")]
    [InlineData("0.005", "0.01")]
    [InlineData("-0.005", "-0.01")]
    public void Round_RoundsAwayFromZero(string value, string expected)
    {
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture),
            MoneyMath.Round(decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void RoundQuantity_UsesThreeDecimalsByDefault()
    {
        Assert.Equal(1.235m, MoneyMath.RoundQuantity(1.2345m));
        Assert.Equal(-1.235m, MoneyMath.RoundQuantity(-1.2345m));
        Assert.Equal(1.2m, MoneyMath.RoundQuantity(1.15m, 1));
    }

    [Fact]
    public void FromNet_WaterBill()
    {
        // 5 m3 * 1.41 EUR = 7.05; VAT 21% = 1.48 (1.4805); total 8.53
        Assert.Equal((7.05m, 1.48m, 8.53m), MoneyMath.FromNet(5m * 1.41m, 21));
    }

    [Fact]
    public void FromNet_VatIsCalculatedFromTheRoundedNet()
    {
        // 6.500 m3 * 1.41 = 9.165 -> net 9.17; VAT 21% of 9.17 = 1.9257 -> 1.93 (not 1.92 from the unrounded 9.165); total 11.10
        Assert.Equal((9.17m, 1.93m, 11.10m), MoneyMath.FromNet(6.5m * 1.41m, 21));
    }

    [Fact]
    public void FromNet_TwelvePercent()
    {
        // 10.00 + 12% = 1.20 -> 11.20
        Assert.Equal((10.00m, 1.20m, 11.20m), MoneyMath.FromNet(10m, 12));
    }

    [Fact]
    public void FromGross_LegacyWaterTariff()
    {
        // 1.56 / 1.21 = 1.2893 -> 1.29; VAT = 1.56 - 1.29 = 0.27
        Assert.Equal((1.29m, 0.27m, 1.56m), MoneyMath.FromGross(1.56m, 21));
    }

    [Fact]
    public void FromGross_NegativeIsSymmetric()
    {
        // -12.10 / 1.21 = -10.00; VAT = -2.10
        Assert.Equal((-10.00m, -2.10m, -12.10m), MoneyMath.FromGross(-12.10m, 21));
    }

    [Fact]
    public void Invariant_NetPlusVatEqualsGross()
    {
        for (var i = 1; i <= 50; i++)
        {
            var value = i * 1.37m - 20m;
            var (n1, v1, g1) = MoneyMath.FromNet(value, 21);
            var (n2, v2, g2) = MoneyMath.FromGross(value, 21);

            Assert.Equal(g1, n1 + v1);
            Assert.Equal(g2, n2 + v2);
        }
    }
}
