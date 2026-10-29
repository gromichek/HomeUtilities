using HomeUtilities.Core.Common;
using HomeUtilities.Core.Import;

namespace HomeUtilities.Import.Tests;

public class ImportCellParserTests
{
    [Theory]
    [InlineData("12.5", 12.5)]
    [InlineData("12,5", 12.5)]
    [InlineData("1 234,56", 1234.56)]
    [InlineData("1,234.56", 1234.56)]
    [InlineData("1.234,56", 1234.56)]
    [InlineData("7,05 €", 7.05)]
    [InlineData("Ls 7.05", 7.05)]
    [InlineData("-3,5", -3.5)]
    [InlineData("  42  ", 42)]
    public void Numbers_accept_comma_dot_spaces_and_currency_words(string text, double expected)
    {
        Assert.True(ImportCellParser.TryParseDecimal(SheetCell.String(text), out var value));
        Assert.Equal((decimal)expected, value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("12 m3")]
    [InlineData("1.2.3")]
    [InlineData("12-3")]
    [InlineData("***")]
    public void Text_that_is_not_a_number_is_rejected(string text)
    {
        Assert.False(ImportCellParser.TryParseDecimal(SheetCell.String(text), out _));
    }

    [Fact]
    public void A_date_cell_is_not_a_number()
    {
        Assert.False(ImportCellParser.TryParseDecimal(SheetCell.Date(new DateOnly(2014, 1, 1)), out _));
    }

    [Theory]
    [InlineData("12.2013", 2013, 12)]
    [InlineData("1.2014", 2014, 1)]
    [InlineData("2014-03", 2014, 3)]
    [InlineData("2014.03", 2014, 3)]
    [InlineData("03/2014", 2014, 3)]
    [InlineData("25.10.2012", 2012, 10)]
    [InlineData("2012-10-25", 2012, 10)]
    [InlineData("Январь 2014", 2014, 1)]
    [InlineData("январь 2014", 2014, 1)]
    [InlineData("Февраля 2014", 2014, 2)]
    [InlineData("март 2014", 2014, 3)]
    [InlineData("Мая 2014", 2014, 5)]
    [InlineData("июнь 2014", 2014, 6)]
    [InlineData("Июль-2014", 2014, 7)]
    [InlineData("Сентябрь 2014", 2014, 9)]
    [InlineData("Декабрь 2014", 2014, 12)]
    [InlineData("janvāris 2014", 2014, 1)]
    [InlineData("marts 2014", 2014, 3)]
    [InlineData("maijs 2014", 2014, 5)]
    [InlineData("jūnijs 2014", 2014, 6)]
    [InlineData("augusts 2014", 2014, 8)]
    [InlineData("Oktobris 2014", 2014, 10)]
    [InlineData("Jan 2014", 2014, 1)]
    [InlineData("February 2014", 2014, 2)]
    [InlineData("May 2014", 2014, 5)]
    [InlineData("Oct-14", 2014, 10)]
    [InlineData("2014 November", 2014, 11)]
    [InlineData("янв.98", 1998, 1)]
    public void Periods_accept_numeric_and_month_name_formats(string text, int year, int month)
    {
        Assert.True(ImportCellParser.TryParsePeriod(SheetCell.String(text), out var period));
        Assert.Equal(new YearMonth(year, month), period);
    }

    [Fact]
    public void A_date_cell_gives_its_month()
    {
        Assert.True(ImportCellParser.TryParsePeriod(SheetCell.Date(new DateOnly(2013, 11, 25)), out var period));
        Assert.Equal(new YearMonth(2013, 11), period);
    }

    [Theory]
    [InlineData("")]
    [InlineData("hello")]
    [InlineData("13.2014")]
    [InlineData("00.2014")]
    [InlineData("2014")]
    [InlineData("Jan Feb 2014")]
    [InlineData("Januar")]
    [InlineData("12345678")]
    public void Unreadable_periods_are_rejected(string text)
    {
        Assert.False(ImportCellParser.TryParsePeriod(SheetCell.String(text), out _));
    }

    [Theory]
    [InlineData("25.10.2012", 2012, 10, 25)]
    [InlineData("2012-10-25", 2012, 10, 25)]
    [InlineData("5.1.2013", 2013, 1, 5)]
    public void Dates_accept_common_formats(string text, int year, int month, int day)
    {
        Assert.True(ImportCellParser.TryParseDate(SheetCell.String(text), out var date));
        Assert.Equal(new DateOnly(year, month, day), date);
    }

    [Fact]
    public void Garbage_is_not_a_date()
    {
        Assert.False(ImportCellParser.TryParseDate(SheetCell.String("soon"), out _));
    }
}
