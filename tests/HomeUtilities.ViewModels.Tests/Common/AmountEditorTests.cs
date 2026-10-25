using System.Globalization;
using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels.Tests.Common;

public class AmountEditorTests : IDisposable
{
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

    public AmountEditorTests() => CultureInfo.CurrentCulture = new CultureInfo("lv-LV");

    public void Dispose() => CultureInfo.CurrentCulture = _culture;

    [Fact]
    public void NetAndVatPercent_DeriveVatAndTotal()
    {
        var editor = new AmountEditor { NetText = "10,00", VatPercentText = "21" };

        // 10.00 * 21% = 2.10; total 12.10
        Assert.Equal("2,10", editor.VatText);
        Assert.Equal("12,10", editor.GrossText);
        Assert.Empty(editor.TryRead(out var amounts));
        Assert.Equal(new AmountSet(10.00m, 21m, 2.10m, 12.10m), amounts);
    }

    [Fact]
    public void TotalAndVatPercent_DeriveNetAndVat()
    {
        var editor = new AmountEditor { GrossText = "12.10", VatPercentText = "21" };

        // 12.10 / 1.21 = 10.00; VAT 2.10
        Assert.Equal("10,00", editor.NetText);
        Assert.Equal("2,10", editor.VatText);
    }

    [Fact]
    public void NetAndTotal_DeriveVatPercent()
    {
        var editor = new AmountEditor { NetText = "10", GrossText = "12,10" };

        Assert.Equal("21", editor.VatPercentText);
        Assert.Equal("2,10", editor.VatText);
    }

    [Fact]
    public void TotalAlone_IsAllowed_AndLeavesTheRestEmpty()
    {
        var editor = new AmountEditor { GrossText = "46,02" };

        Assert.Empty(editor.TryRead(out var amounts));
        Assert.Equal(new AmountSet(null, null, null, 46.02m), amounts);
        Assert.Equal(string.Empty, editor.NetText);
        Assert.Equal(string.Empty, editor.VatText);
    }

    [Fact]
    public void DefaultVatPercent_CountsAsFilledIn_SoTotalAloneDerivesTheRest()
    {
        var editor = new AmountEditor();
        editor.SetDefaultVatPercent(21m);

        editor.GrossText = "12,10";

        Assert.Equal("10,00", editor.NetText);
        Assert.Equal("2,10", editor.VatText);
        Assert.Equal("21", editor.VatPercentText);
    }

    [Fact]
    public void NetTyped_AfterTheDefaultVatPercent_DerivesTheTotal()
    {
        var editor = new AmountEditor();
        editor.SetDefaultVatPercent(21m);

        editor.NetText = "100";

        Assert.Equal("121,00", editor.GrossText);
        Assert.Equal("21,00", editor.VatText);
    }

    [Fact]
    public void ThirdFieldTyped_ReplacesTheOldestOne()
    {
        // Net was typed first, VAT % second: they are the two that count.
        var editor = new AmountEditor { NetText = "10", VatPercentText = "21" };
        Assert.Equal("12,10", editor.GrossText);

        // Total typed third: Net (the oldest) is derived now: 20 / 1.21 = 16.53; VAT 3.47.
        editor.GrossText = "20";

        Assert.Equal("16,53", editor.NetText);
        Assert.Equal("21", editor.VatPercentText);
        Assert.Equal("3,47", editor.VatText);
    }

    [Fact]
    public void ClearingAField_RecomputesFromTheRemainingOnes()
    {
        var editor = new AmountEditor { NetText = "10", VatPercentText = "21" };

        editor.VatPercentText = string.Empty;

        Assert.Equal(string.Empty, editor.GrossText);
        Assert.Equal(string.Empty, editor.VatText);
        var errors = editor.TryRead(out _);
        Assert.Equal(ErrorCodes.AmountGrossRequired, Assert.Single(errors).Code);
    }

    [Fact]
    public void LoadedAmounts_KeepTheTotalAndVatPercentFixed_WhenTheTotalIsEdited()
    {
        var editor = new AmountEditor();
        editor.Load(new AmountSet(10m, 21m, 2.10m, 12.10m));
        Assert.Equal("12,10", editor.GrossText);

        editor.GrossText = "24,20";

        Assert.Equal("20,00", editor.NetText);
        Assert.Equal("21", editor.VatPercentText);
        Assert.Equal("4,20", editor.VatText);
    }

    [Fact]
    public void Load_DoesNotRaiseEdited_ButTypingDoes()
    {
        var editor = new AmountEditor();
        var raised = 0;
        editor.Edited += () => raised++;

        editor.Load(new AmountSet(null, null, null, 5m));
        Assert.Equal(0, raised);

        editor.GrossText = "6";
        Assert.Equal(1, raised);
    }

    [Fact]
    public void NotANumber_IsReportedUnderTheField_AndNothingIsDerived()
    {
        var editor = new AmountEditor { VatPercentText = "21", NetText = "abc" };

        var errors = editor.TryRead(out var amounts);

        Assert.Null(amounts);
        var error = Assert.Single(errors);
        Assert.Equal((CommonErrorCodes.NumberInvalid, "Net"), (error.Code, error.Field));
        Assert.Equal(string.Empty, editor.GrossText);
    }

    [Theory]
    [InlineData("1e9999")]
    [InlineData("99999999999999999999999999")]
    [InlineData("1000000001")]
    public void HugeNumbers_AreRejected_WithoutThrowing(string text)
    {
        var editor = new AmountEditor { VatPercentText = "21" };

        editor.NetText = text;
        var errors = editor.TryRead(out var amounts);

        Assert.Null(amounts);
        Assert.Contains(errors, e => e.Field == "Net");
    }

    [Fact]
    public void VatPercentAbove100_IsRejected()
    {
        var editor = new AmountEditor { NetText = "10", VatPercentText = "150" };

        var errors = editor.TryRead(out _);

        Assert.Equal(ErrorCodes.VatPercentOutOfRange, Assert.Single(errors).Code);
    }

    [Fact]
    public void NegativeTotal_IsAccepted_ForCreditNotes()
    {
        var editor = new AmountEditor { GrossText = "-12,10", VatPercentText = "21" };

        Assert.Empty(editor.TryRead(out var amounts));
        Assert.Equal(new AmountSet(-10m, 21m, -2.10m, -12.10m), amounts);
    }

    [Fact]
    public void PointAndComma_AreBothAccepted()
    {
        var editor = new AmountEditor { NetText = "10.5", VatPercentText = "21" };

        // 10.50 * 21% = 2.205 -> 2.21; total 12.71
        Assert.Equal("12,71", editor.GrossText);
    }

    [Fact]
    public void GrossPlaceholder_IsOnlyAHint_NotAValue()
    {
        var editor = new AmountEditor { GrossPlaceholder = "46,02" };

        Assert.Equal(string.Empty, editor.GrossText);
        var errors = editor.TryRead(out _);
        Assert.Equal(ErrorCodes.AmountGrossRequired, Assert.Single(errors).Code);
    }
}
