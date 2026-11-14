using HomeUtilities.Core.PdfBills;

namespace HomeUtilities.Core.Tests.PdfBills;

public class PdfBillAmountsTests
{
    [Fact]
    public void TotalAndVat_GiveNet_AndTheDerivedRate()
    {
        // 59.49 - 10.33 = 49.16; 10.33 / 49.16 = 21.01 %
        var amounts = PdfBillAmounts.Resolve(new PdfBillValues { Gross = 59.49m, Vat = 10.33m });

        Assert.NotNull(amounts);
        Assert.Equal(49.16m, amounts.Net);
        Assert.Equal(10.33m, amounts.Vat);
        Assert.Equal(59.49m, amounts.Gross);
        Assert.Equal(21.01m, amounts.VatPercent);
    }

    [Fact]
    public void ThePrintedRate_IsKept_WhenItMatchesTheAmounts()
    {
        var amounts = PdfBillAmounts.Resolve(new PdfBillValues { Gross = 59.49m, Vat = 10.33m, VatPercent = 21m });

        Assert.Equal(21m, amounts!.VatPercent);
        Assert.Equal(49.16m, amounts.Net);
        Assert.Equal(amounts.Gross, amounts.Net + amounts.Vat);
    }

    [Fact]
    public void APrintedRate_ThatDoesNotMatch_IsReplacedByTheDerivedOne()
    {
        var amounts = PdfBillAmounts.Resolve(new PdfBillValues { Gross = 59.49m, Vat = 10.33m, VatPercent = 12m });

        Assert.Equal(21.01m, amounts!.VatPercent);
    }

    [Fact]
    public void TotalAndRate_GiveNetAndVat()
    {
        // 34.20 / 1.21 = 28.26; VAT 34.20 - 28.26 = 5.94
        var amounts = PdfBillAmounts.Resolve(new PdfBillValues { Gross = 34.20m, VatPercent = 21m });

        Assert.Equal(28.26m, amounts!.Net);
        Assert.Equal(5.94m, amounts.Vat);
        Assert.Equal(34.20m, amounts.Gross);
        Assert.Equal(21m, amounts.VatPercent);
    }

    [Fact]
    public void TotalAlone_LeavesNetAndVatEmpty()
    {
        var amounts = PdfBillAmounts.Resolve(new PdfBillValues { Gross = 34.20m });

        Assert.Equal(34.20m, amounts!.Gross);
        Assert.Null(amounts.Net);
        Assert.Null(amounts.Vat);
        Assert.Null(amounts.VatPercent);
    }

    [Fact]
    public void WithoutATotal_ThereAreNoAmounts() =>
        Assert.Null(PdfBillAmounts.Resolve(new PdfBillValues { Vat = 1m, VatPercent = 21m }));
}
