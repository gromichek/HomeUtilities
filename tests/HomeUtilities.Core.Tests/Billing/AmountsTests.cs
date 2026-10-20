using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Tests.Billing;

public class AmountsTests
{
    [Fact]
    public void Gross_and_vat_percent_derive_net_and_vat()
    {
        // 59.49 / 1.21 = 49.165 -> 49.17; VAT = 59.49 - 49.17 = 10.32
        var (result, error) = Amounts.Complete(null, 21m, 59.49m);
        Assert.Null(error);
        Assert.Equal(new AmountSet(49.17m, 21m, 10.32m, 59.49m), result);
    }

    [Fact]
    public void Net_and_vat_percent_derive_vat_and_gross()
    {
        // 10 * 12% = 1.20; gross 11.20
        var (result, error) = Amounts.Complete(10m, 12m, null);
        Assert.Null(error);
        Assert.Equal(new AmountSet(10m, 12m, 1.20m, 11.20m), result);
    }

    [Fact]
    public void Net_and_gross_derive_vat_and_percent()
    {
        // VAT = 59.49 - 49.17 = 10.32; 10.32 / 49.17 * 100 = 20.988 -> 20.99
        var (result, error) = Amounts.Complete(49.17m, null, 59.49m);
        Assert.Null(error);
        Assert.Equal(new AmountSet(49.17m, 20.99m, 10.32m, 59.49m), result);
    }

    [Fact]
    public void Net_zero_and_gross_leave_percent_null()
    {
        var (result, error) = Amounts.Complete(0m, null, 0m);
        Assert.Null(error);
        Assert.Equal(new AmountSet(0m, null, 0m, 0m), result);
    }

    [Fact]
    public void Gross_only_is_allowed()
    {
        var (result, error) = Amounts.Complete(null, null, 55.25m);
        Assert.Null(error);
        Assert.Equal(new AmountSet(null, null, null, 55.25m), result);
    }

    [Fact]
    public void Consistent_triple_is_accepted()
    {
        // 49.17 * 21% = 10.33 (10.3257); gross 59.50; entered 59.49 is within 0.01
        var (result, error) = Amounts.Complete(49.17m, 21m, 59.49m);
        Assert.Null(error);
        Assert.Equal(new AmountSet(49.17m, 21m, 10.32m, 59.49m), result);
    }

    [Fact]
    public void Inconsistent_triple_is_rejected()
    {
        // 49.17 * 21% -> gross 59.50, entered 60.00
        var (result, error) = Amounts.Complete(49.17m, 21m, 60m);
        Assert.Null(result);
        Assert.Equal(ErrorCodes.AmountInconsistent, error!.Code);
    }

    [Theory]
    [InlineData(10, null, null)]
    [InlineData(null, 21, null)]
    [InlineData(null, null, null)]
    public void Missing_gross_without_a_pair_is_rejected(int? net, int? percent, int? gross)
    {
        var (result, error) = Amounts.Complete(net, percent, gross);
        Assert.Null(result);
        Assert.Equal(ErrorCodes.AmountGrossRequired, error!.Code);
    }

    [Fact]
    public void Negative_amounts_are_allowed()
    {
        // -12.10 / 1.21 = -10.00; VAT = -2.10
        var (result, error) = Amounts.Complete(null, 21m, -12.10m);
        Assert.Null(error);
        Assert.Equal(new AmountSet(-10.00m, 21m, -2.10m, -12.10m), result);
    }

    [Fact]
    public void Vat_percent_out_of_range_is_rejected()
    {
        var (_, error) = Amounts.Complete(null, 101m, 10m);
        Assert.Equal(ErrorCodes.VatPercentOutOfRange, error!.Code);
    }
}
