using System.Globalization;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels.Tests.Common;

public class DecimalInputTests
{
    private static readonly CultureInfo Russian = new("ru-RU");
    private static readonly CultureInfo English = new("en-US");

    [Theory]
    [InlineData("52,3", "52.3")]
    [InlineData("52.3", "52.3")]
    [InlineData("1 234,56", "1234.56")]
    [InlineData("1 234,56", "1234.56")]
    [InlineData("1 234.56", "1234.56")]
    [InlineData("-12.10", "-12.10")]
    [InlineData("−12,5", "-12.5")]
    [InlineData("  7  ", "7")]
    public void TryParse_AcceptsBothSeparatorsAndSpaces(string text, string expected)
    {
        foreach (var culture in new[] { Russian, English })
        {
            Assert.True(DecimalInput.TryParse(text, culture, out var value));
            Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), value);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("abc")]
    [InlineData("12a")]
    [InlineData("1,2,3")]
    [InlineData("1.234,56")]
    [InlineData("-")]
    [InlineData(",")]
    public void TryParse_RejectsInvalidText(string? text)
    {
        Assert.False(DecimalInput.TryParse(text, Russian, out var value));
        Assert.Equal(0m, value);
    }

    [Fact]
    public void Format_UsesCultureSeparatorAndNoTrailingZeros()
    {
        Assert.Equal("52,3", DecimalInput.Format(52.3m, Russian, 2));
        Assert.Equal("52.3", DecimalInput.Format(52.3m, English, 2));
        Assert.Equal("52", DecimalInput.Format(52.00m, English, 2));
        Assert.Equal("52.35", DecimalInput.Format(52.349m, English, 2));
        Assert.Equal("3", DecimalInput.Format(3m, English, 0));
        Assert.Equal("-12.1", DecimalInput.Format(-12.10m, English, 2));
    }
}
