using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.PdfBills;

/// <summary>Turns the amounts found in a PDF into the amount fields of a bill.</summary>
public static class PdfBillAmounts
{
    private const decimal Tolerance = 0.01m;

    /// <summary>
    /// The total is taken as printed. With a VAT amount, Net = Total − VAT (the printed VAT rate is kept when it matches);
    /// with only a rate, Net and VAT are derived from it; with the total alone Net and VAT stay empty.
    /// The invariant Net + VAT = Total holds. Null when there is no total.
    /// </summary>
    public static AmountSet? Resolve(PdfBillValues values)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (values.Gross is not { } gross)
        {
            return null;
        }

        gross = MoneyMath.Round(gross);
        if (values.Vat is { } vat)
        {
            var net = gross - MoneyMath.Round(vat);
            var percent = values.VatPercent is { } printed && Math.Abs(MoneyMath.FromNet(net, printed).Gross - gross) <= Tolerance
                ? printed
                : Amounts.Complete(net, null, gross).Result?.VatPercent;
            return new AmountSet(net, percent, gross - net, gross);
        }

        if (values.VatPercent is { } rate && Amounts.Complete(null, rate, gross).Result is { } derived)
        {
            return derived;
        }

        return new AmountSet(null, null, null, gross);
    }
}
