using System.Globalization;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Payments;

namespace HomeUtilities.Core.Tests.Payments;

public class PaymentPurposeTests
{
    private static readonly PaymentPurposeContext Empty = new(null, null, null, null, null, null);

    [Fact]
    public void Customer_number_and_period_with_format()
    {
        var ctx = Empty with { CustomerNumber = "12345", Period = new YearMonth(2026, 10) };
        Assert.Equal("Abon. Nr. 12345, 10.2026", PaymentPurpose.Render("Abon. Nr. {CustomerNumber}, {Period:MM.yyyy}", ctx));
    }

    [Fact]
    public void Period_default_format_is_month_dot_year()
    {
        Assert.Equal("03.2026", PaymentPurpose.Render("{Period}", Empty with { Period = new YearMonth(2026, 3) }));
    }

    [Fact]
    public void All_placeholders_are_filled()
    {
        var ctx = new PaymentPurposeContext("C1", "K2", "B3", new YearMonth(2026, 10), 59.49m, "Flat");
        Assert.Equal("C1|K2|B3|2026-10|59.49|Flat",
            PaymentPurpose.Render("{CustomerNumber}|{ContractNumber}|{BillNumber}|{Period:yyyy-MM}|{Amount}|{PropertyName}", ctx));
    }

    [Theory]
    [InlineData("ru-RU")]
    [InlineData("lv-LV")]
    [InlineData("en-US")]
    public void Amount_and_period_ignore_current_culture(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);
            var ctx = Empty with { Amount = 59.49m, Period = new YearMonth(2026, 10) };
            Assert.Equal("59.49 / 10/2026", PaymentPurpose.Render("{Amount} / {Period:MM/yyyy}", ctx));
            Assert.Equal("5.00", PaymentPurpose.Render("{Amount}", Empty with { Amount = 5m }));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Missing_values_render_empty()
    {
        Assert.Equal("Nr. , ", PaymentPurpose.Render("Nr. {CustomerNumber}, {Period}{Amount}", Empty));
    }

    [Theory]
    [InlineData("{Unknown} x", "{Unknown} x")]
    [InlineData("{", "{")]
    [InlineData("a } b { c", "a } b { c")]
    [InlineData("{CustomerNumber", "{CustomerNumber")]
    [InlineData("{Amount:0.0}", "{Amount:0.0}")]
    public void Unknown_placeholders_and_stray_braces_are_kept(string template, string expected)
    {
        Assert.Equal(expected, PaymentPurpose.Render(template, Empty with { CustomerNumber = "1", Amount = 1m }));
    }
}
