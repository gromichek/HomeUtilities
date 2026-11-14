using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.PdfBills;

/// <summary>Turns the amounts found in a PDF into the amount fields of a bill.</summary>
public static class PdfBillAmounts
{
    private const decimal Tolerance = 0.01m;

    /// <summary>
    /// The total is taken as printed. With a VAT amount, Net = Total − VAT (the printed VAT rate is kept when it matches);
    /// with a net amount (no VAT amount), VAT = Total − Net; with only a rate, Net and VAT are derived from it; with the
    /// total alone Net and VAT stay empty. When all three amounts are given the total wins (the VAT amount is kept).
    /// Without a total it is derived from net + VAT or from net and the rate. The invariant Net + VAT = Total holds.
    /// Null when there is no total and none can be derived.
    /// </summary>
    public static AmountSet? Resolve(PdfBillValues values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var total = values.Gross;
        if (total is null && values.Net is { } onlyNet)
        {
            total = values.Vat is { } onlyVat
                ? onlyNet + onlyVat
                : values.VatPercent is { } onlyRate ? Amounts.Complete(onlyNet, onlyRate, null).Result?.Gross : null;
        }

        if (total is not { } gross)
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

        if (values.Net is { } printedNet)
        {
            var net = MoneyMath.Round(printedNet);
            var percent = values.VatPercent is { } rated && Math.Abs(MoneyMath.FromNet(net, rated).Gross - gross) <= Tolerance
                ? rated
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
