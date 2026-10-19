using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Tests.Common;

public class CurrencyConverterTests
{
    [Fact]
    public void ToEur_LvlRateIsOneEur() =>
        Assert.Equal(1m, CurrencyConverter.ToEur(0.702804m, Currency.LVL));

    [Fact]
    public void ToEur_Lvl_RoundedByCaller()
    {
        // 9.32 / 0.702804 = 13.2615... -> 13.26
        Assert.Equal(13.26m, MoneyMath.Round(CurrencyConverter.ToEur(9.32m, Currency.LVL)));
    }

    [Fact]
    public void ToEur_EurUnchanged() =>
        Assert.Equal(12.3456m, CurrencyConverter.ToEur(12.3456m, Currency.EUR));
}
