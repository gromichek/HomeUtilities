using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Reports;

/// <summary>One month's share of a bill, in EUR.</summary>
/// <param name="Bill">The bill.</param>
/// <param name="Month">The month the share belongs to.</param>
/// <param name="Gross">Gross share.</param>
/// <param name="Net">Net share.</param>
/// <param name="Vat">VAT share (gross minus net).</param>
/// <param name="IsSpread">True if the bill is spread over several months.</param>
internal readonly record struct CostShare(Bill Bill, YearMonth Month, decimal Gross, decimal Net, decimal Vat, bool IsSpread)
{
    /// <summary>The share for the measure (Gross, Net or Vat; anything else counts as Gross).</summary>
    public decimal Of(ReportMeasure measure) => measure switch
    {
        ReportMeasure.Net => Net,
        ReportMeasure.Vat => Vat,
        _ => Gross,
    };
}

/// <summary>Amounts of bills in EUR, spread over the months of their service period.</summary>
internal static class ReportMoney
{
    /// <summary>Converts to EUR and rounds to 2 decimals.</summary>
    public static decimal Eur(decimal amount, Currency currency) => MoneyMath.Round(CurrencyConverter.ToEur(amount, currency));

    /// <summary>The net amount of a bill: the stated one, else gross minus VAT, else the gross itself.</summary>
    public static decimal NetOf(Bill bill) => bill.Net ?? (bill.Vat is { } vat ? bill.Gross - vat : bill.Gross);

    /// <summary>
    /// The shares of the bill per month (<see cref="BillSpread"/>), gross and net spread separately, VAT as their difference,
    /// so the shares add up to the bill exactly.
    /// </summary>
    public static IReadOnlyList<CostShare> Shares(Bill bill)
    {
        var gross = Eur(bill.Gross, bill.Currency);
        var net = Eur(NetOf(bill), bill.Currency);
        var grossShares = BillSpread.ByMonth(Clone(bill, gross));
        var netShares = BillSpread.ByMonth(Clone(bill, net));
        var spread = grossShares.Count > 1;
        return
        [
            .. grossShares.Select((g, i) => new CostShare(bill, g.Month, g.Gross, netShares[i].Gross, g.Gross - netShares[i].Gross, spread)),
        ];
    }

    private static Bill Clone(Bill bill, decimal gross) => new()
    {
        Period = bill.Period,
        ServiceFrom = bill.ServiceFrom,
        ServiceTo = bill.ServiceTo,
        Gross = gross,
    };
}
