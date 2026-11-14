using HomeUtilities.Core.Common;
using HomeUtilities.Core.PdfBills;

namespace HomeUtilities.Core.Tests.PdfBills;

public class PdfValueParsersTests
{
    [Theory]
    [InlineData("59.49", "59.49")]
    [InlineData("59,49", "59.49")]
    [InlineData("1 234.56", "1234.56")]
    [InlineData("1 234,56", "1234.56")]
    [InlineData("1 234,56 EUR", "1234.56")]
    [InlineData("EUR 59,49", "59.49")]
    [InlineData("€ 12", "12")]
    [InlineData("12 %", "12")]
    [InlineData("21%", "21")]
    [InlineData("1.234,56", "1234.56")]
    [InlineData("1,234.56", "1234.56")]
    [InlineData("-5,10", "-5.10")]
    public void Decimals_AreParsed(string text, string expected)
    {
        Assert.True(PdfValueParsers.TryParseDecimal(text, out var value));
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("12 EUR x")]
    [InlineData("1.2.3")]
    public void Decimals_ThatAreNoNumbers_AreRejected(string text) =>
        Assert.False(PdfValueParsers.TryParseDecimal(text, out _));

    [Theory]
    [InlineData("30.11.2026", 2026, 11, 30)]
    [InlineData("30.11.2026.", 2026, 11, 30)]
    [InlineData("2026-11-30", 2026, 11, 30)]
    [InlineData("2026.gada 30. septembris", 2026, 9, 30)]
    [InlineData("2026. gada 30. septembris", 2026, 9, 30)]
    [InlineData("2026. gada 1. jūlijs", 2026, 7, 1)]
    [InlineData("2026.GADA 15. OKTOBRIS", 2026, 10, 15)]
    [InlineData("2026. gada 1. julijs", 2026, 7, 1)]
    public void Dates_AreParsed(string text, int year, int month, int day)
    {
        Assert.True(PdfValueParsers.TryParseDate(text, out var value));
        Assert.Equal(new DateOnly(year, month, day), value);
    }

    [Theory]
    [InlineData("31.02.2026")]
    [InlineData("2026. gada 31. septembris")]
    [InlineData("2026. gada 5. blabla")]
    [InlineData("hello")]
    public void Dates_ThatDoNotExist_AreRejected(string text) =>
        Assert.False(PdfValueParsers.TryParseDate(text, out _));

    [Theory]
    [InlineData("2026. gada septembris", 2026, 9)]
    [InlineData("2026.GADA septembrī", 2026, 9)]
    [InlineData("2026.gada oktobrī", 2026, 10)]
    [InlineData("09.2026", 2026, 9)]
    [InlineData("2026-09", 2026, 9)]
    [InlineData("2026. gada septembri", 2026, 9)]
    public void Periods_AreParsed(string text, int year, int month)
    {
        Assert.True(PdfValueParsers.TryParsePeriod(text, out var value));
        Assert.Equal(new YearMonth(year, month), value);
    }

    [Theory]
    [InlineData("janvāris", 1)]
    [InlineData("janvārī", 1)]
    [InlineData("februāris", 2)]
    [InlineData("februārī", 2)]
    [InlineData("marts", 3)]
    [InlineData("martā", 3)]
    [InlineData("aprīlis", 4)]
    [InlineData("aprīlī", 4)]
    [InlineData("maijs", 5)]
    [InlineData("maijā", 5)]
    [InlineData("jūnijs", 6)]
    [InlineData("jūnijā", 6)]
    [InlineData("jūlijs", 7)]
    [InlineData("jūlijā", 7)]
    [InlineData("augusts", 8)]
    [InlineData("augustā", 8)]
    [InlineData("septembris", 9)]
    [InlineData("septembrī", 9)]
    [InlineData("oktobris", 10)]
    [InlineData("oktobrī", 10)]
    [InlineData("novembris", 11)]
    [InlineData("novembrī", 11)]
    [InlineData("decembris", 12)]
    [InlineData("decembrī", 12)]
    [InlineData("JŪLIJS", 7)]
    [InlineData("julijs", 7)]
    [InlineData("septembri", 9)]
    public void LatvianMonthNames_AreRecognized_InBothFormsAnyCase(string name, int month) =>
        Assert.Equal(month, PdfValueParsers.MonthOf(name));

    [Fact]
    public void AWordThatIsNoMonth_IsRejected() => Assert.Null(PdfValueParsers.MonthOf("pavasaris"));
}
