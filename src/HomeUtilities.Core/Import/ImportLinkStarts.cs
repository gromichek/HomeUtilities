using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Import;

/// <summary>A meter link of the contract that should start earlier to cover the imported history.</summary>
/// <param name="Link">The link (not changed by the search).</param>
/// <param name="Meter">The meter of the link.</param>
/// <param name="NewValidFrom">The proposed start.</param>
public sealed record LinkStartMove(ContractMeter Link, Meter Meter, DateOnly NewValidFrom);

/// <summary>
/// Finds the meter links of a contract that start after the earliest period or reading of an import. Charges and reports only count
/// a meter's readings inside its link, so the imported months before the link would show no consumption.
/// </summary>
public static class ImportLinkStarts
{
    /// <summary>The earliest day an import creates data for: the first day of the earliest period or the earliest reading date.</summary>
    public static DateOnly? EarliestImportedDate(ImportPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        DateOnly? earliest = null;
        foreach (var row in plan.Rows.Where(r => !r.HasErrors && !r.IsSkipped))
        {
            if (row.Period is { } period)
            {
                earliest = Min(earliest, period.FirstDay);
            }

            foreach (var reading in row.Readings)
            {
                earliest = Min(earliest, reading.Reading.Date);
            }
        }

        return earliest;
    }

    /// <summary>
    /// The first link of every meter of the contract that starts after the earliest imported date and can start earlier; the new start
    /// is the later of the meter's installation and the contract's start (the plan's suggested start if the contract is moved too).
    /// </summary>
    /// <param name="plan">The planned import.</param>
    /// <param name="contract">The target contract.</param>
    /// <param name="meters">Meters of the property.</param>
    /// <param name="links">Links of the property's contracts (other contracts' links are ignored).</param>
    public static IReadOnlyList<LinkStartMove> Find(ImportPlan plan, Contract contract, IEnumerable<Meter> meters, IEnumerable<ContractMeter> links)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(meters);
        ArgumentNullException.ThrowIfNull(links);

        if (EarliestImportedDate(plan) is not { } earliest)
        {
            return [];
        }

        var contractStart = plan.SuggestedContractStart is { } suggested && suggested < contract.ValidFrom ? suggested : contract.ValidFrom;
        var meterById = meters.Where(m => !m.IsDeleted).ToDictionary(m => m.Id);
        var moves = new List<LinkStartMove>();
        foreach (var pair in links.Where(l => !l.IsDeleted && l.ContractId == contract.Id).GroupBy(l => l.MeterId))
        {
            if (!meterById.TryGetValue(pair.Key, out var meter))
            {
                continue;
            }

            var first = pair.OrderBy(l => l.ValidFrom).First();
            var target = MeterLinkRules.DefaultStart(meter.InstalledOn, contractStart);
            if (first.ValidFrom > earliest && target < first.ValidFrom)
            {
                moves.Add(new LinkStartMove(first, meter, target));
            }
        }

        return moves;
    }

    private static DateOnly Min(DateOnly? current, DateOnly candidate) => current is { } value && value < candidate ? value : candidate;
}
