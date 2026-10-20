using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Billing;

/// <summary>Derives the missing amount fields from the ones the user entered.</summary>
public static class Amounts
{
    private const decimal Tolerance = 0.01m;

    /// <summary>
    /// Completes an amount from any two of net / VAT percent / gross, or gross alone.
    /// All three given must be consistent (gross within 0.01 of net plus VAT). Negative values are allowed.
    /// </summary>
    /// <returns>The completed amount, or an error (<see cref="ErrorCodes.AmountInconsistent"/>,
    /// <see cref="ErrorCodes.AmountGrossRequired"/>, <see cref="ErrorCodes.VatPercentOutOfRange"/>).</returns>
    public static (AmountSet? Result, ValidationError? Error) Complete(decimal? net, decimal? vatPercent, decimal? gross)
    {
        if (vatPercent is < 0 or > 100)
        {
            return (null, new(ErrorCodes.VatPercentOutOfRange, nameof(AmountSet.VatPercent)));
        }

        switch (net, vatPercent, gross)
        {
            case (decimal n, decimal p, decimal g):
                var expected = MoneyMath.FromNet(n, p);
                if (Math.Abs(expected.Gross - g) > Tolerance)
                {
                    return (null, new(ErrorCodes.AmountInconsistent, nameof(AmountSet.Gross)));
                }

                return (new(MoneyMath.Round(n), p, MoneyMath.Round(g) - MoneyMath.Round(n), MoneyMath.Round(g)), null);

            case (null, decimal p, decimal g):
                var fromGross = MoneyMath.FromGross(g, p);
                return (new(fromGross.Net, p, fromGross.Vat, fromGross.Gross), null);

            case (decimal n, decimal p, null):
                var fromNet = MoneyMath.FromNet(n, p);
                return (new(fromNet.Net, p, fromNet.Vat, fromNet.Gross), null);

            case (decimal n, null, decimal g):
                var roundedNet = MoneyMath.Round(n);
                var roundedGross = MoneyMath.Round(g);
                var vat = roundedGross - roundedNet;
                decimal? percent = roundedNet == 0 ? null : MoneyMath.Round(vat / roundedNet * 100);
                return (new(roundedNet, percent, vat, roundedGross), null);

            case (null, null, decimal g):
                return (new(null, null, null, MoneyMath.Round(g)), null);

            default:
                return (null, new(ErrorCodes.AmountGrossRequired, nameof(AmountSet.Gross)));
        }
    }
}
