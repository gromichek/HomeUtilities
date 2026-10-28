using HomeUtilities.Core.Charges;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Tariffs;

namespace HomeUtilities.Core.Reports;

/// <summary>The metered quantity behind a calculated charge, for unit costs and water losses.</summary>
internal static class ContractQuantity
{
    /// <summary>
    /// The quantity metered for the contract: the per-unit lines of the calculation, counting each metered source once (water and
    /// sewerage both charge the same cubic metres; day and night electricity are different registers and add up).
    /// <c>HasPerUnit</c> is false when the contract has no per-unit line at all; <c>Quantity</c> is null when a line is incomplete.
    /// </summary>
    public static (decimal? Quantity, bool HasPerUnit) Of(CalculatedCharge? charge)
    {
        if (charge is null)
        {
            return (null, false);
        }

        var lines = charge.Lines.Where(l => l is { Basis: ChargeBasis.PerUnit, IsFromBill: false }).ToList();
        if (lines.Count == 0)
        {
            return (null, false);
        }

        if (lines.Any(l => l is not { IsComplete: true, Quantity: not null }) || HasNoNewReading(charge))
        {
            return (null, true);
        }

        var seen = new HashSet<string>();
        var total = 0m;
        foreach (var component in lines.GroupBy(l => l.ComponentId))
        {
            var registers = component.SelectMany(l => l.Consumption)
                .Select(c => c.EndReading?.RegisterId)
                .OfType<Guid>()
                .Distinct()
                .Order()
                .ToList();
            var key = registers.Count == 0 ? component.Key.ToString() : string.Join(',', registers);
            if (seen.Add(key))
            {
                total += component.Sum(l => l.Quantity!.Value);
            }
        }

        return (MoneyMath.RoundQuantity(total), true);
    }

    /// <summary>
    /// True if a per-unit line takes its quantity from the same reading at both ends: no reading was taken since the previous period,
    /// so the zero consumption is not a measurement.
    /// </summary>
    public static bool HasNoNewReading(CalculatedCharge charge) =>
        charge.Lines
            .Where(l => l.Basis == ChargeBasis.PerUnit)
            .SelectMany(l => l.Consumption)
            .Any(c => c.StartReading is { } start && c.EndReading?.Id == start.Id);
}
