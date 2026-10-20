using HomeUtilities.Core.Charges;
using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Billing;

/// <summary>Difference between a bill and the app's calculation.</summary>
/// <param name="Difference">Bill total minus calculated total; null if there is no usable calculation.</param>
/// <param name="DifferencePercent">Difference in percent of the calculated total, 1 decimal.</param>
/// <param name="QuantityDifference">Quantity on the bill minus the metered quantity (e.g. water losses).</param>
public sealed record BillComparisonResult(decimal? Difference, decimal? DifferencePercent, decimal? QuantityDifference);

/// <summary>Compares a bill with the calculated charge and the metered consumption.</summary>
public static class BillComparison
{
    /// <summary>
    /// Compares the bill. The money difference is null when the calculation is missing, incomplete, has no
    /// lines, or only takes its amounts from the bill itself.
    /// </summary>
    public static BillComparisonResult Compare(Bill bill, CalculatedCharge? calculated, decimal? meteredQuantity)
    {
        ArgumentNullException.ThrowIfNull(bill);

        decimal? difference = null;
        decimal? percent = null;
        if (calculated is { IsComplete: true, Lines.Count: > 0 } && !calculated.Lines.All(l => l.IsFromBill))
        {
            difference = bill.Gross - calculated.TotalGross;
            if (calculated.TotalGross != 0)
            {
                percent = Math.Round(difference.Value / calculated.TotalGross * 100, 1, MidpointRounding.AwayFromZero);
            }
        }

        decimal? quantity = bill.ConsumptionOnBill is { } onBill && meteredQuantity is { } metered
            ? onBill - metered
            : null;
        return new(difference, percent, quantity);
    }
}
