using HomeUtilities.Core.Charges;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Tariffs;

namespace HomeUtilities.Core.Billing;

/// <summary>Difference between a bill and the app's calculation.</summary>
/// <param name="Difference">Bill total minus calculated total; null if there is no usable calculation.</param>
/// <param name="DifferencePercent">Difference in percent of the calculated total, 1 decimal.</param>
/// <param name="QuantityDifference">Quantity on the bill minus the metered quantity (e.g. water losses).</param>
public sealed record BillComparisonResult(decimal? Difference, decimal? DifferencePercent, decimal? QuantityDifference);

/// <summary>How far a bill is from the calculation, for colouring: red above +5 %, green below −5 %.</summary>
public enum DifferenceTone
{
    /// <summary>Within ±5 % or unknown.</summary>
    Neutral = 0,

    /// <summary>The bill is more than 5 % above the calculation.</summary>
    Higher = 1,

    /// <summary>The bill is more than 5 % below the calculation.</summary>
    Lower = 2,
}

/// <summary>Compares a bill with the calculated charge and the metered consumption.</summary>
public static class BillComparison
{
    /// <summary>The difference (in percent) above which a bill counts as noticeably higher (and, negated, lower).</summary>
    public const decimal ToneThresholdPercent = 5m;

    /// <summary>Strictly above +5 % is <see cref="DifferenceTone.Higher"/>, strictly below −5 % is <see cref="DifferenceTone.Lower"/>.</summary>
    public static DifferenceTone ToneOf(decimal? differencePercent) => differencePercent switch
    {
        > ToneThresholdPercent => DifferenceTone.Higher,
        < -ToneThresholdPercent => DifferenceTone.Lower,
        _ => DifferenceTone.Neutral,
    };

    /// <summary>
    /// The metered quantity the bill's quantity is compared with: the quantity of the first per-unit component
    /// (a stepped price yields several lines of one component, which are added up). Null if the calculation has no such line.
    /// </summary>
    public static decimal? MeteredQuantity(CalculatedCharge? calculated)
    {
        if (calculated is null)
        {
            return null;
        }

        var first = calculated.Lines
            .Where(l => l is { Basis: ChargeBasis.PerUnit, IsFromBill: false, IsComplete: true, Quantity: not null })
            .GroupBy(l => l.ComponentId)
            .FirstOrDefault();
        return first?.Sum(l => l.Quantity!.Value);
    }

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
