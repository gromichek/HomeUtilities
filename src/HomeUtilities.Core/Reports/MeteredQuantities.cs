using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Reports;

/// <summary>
/// The quantity a contract's meters measured, read straight from the readings (the way the consumption report "Group by: contract"
/// reads them) instead of from a calculated charge. This is what a contract without per-unit tariff components has: the owner may
/// know only the bills of the past, not the tariffs, yet a unit cost (bill / quantity) is still possible.
/// Meter chains are built once per property, the month figures once per chain: asking for a contract and month is a dictionary lookup.
/// </summary>
internal sealed class MeteredQuantities(ReportData data, ReportFilter filter, CancellationToken cancellation = default)
{
    private sealed record Linked(IReadOnlyList<ContractMeter> Links, Dictionary<YearMonth, Acc> Total, MeterUnit Unit);

    private static readonly IReadOnlyDictionary<MeterUnit, Acc> None = new Dictionary<MeterUnit, Acc>();

    private readonly IReadOnlyList<YearMonth> _months = filter.Months();
    private readonly Dictionary<Guid, List<ConsumptionReport.Chain>> _chains = [];
    private readonly Dictionary<Guid, List<Linked>> _linked = [];

    /// <summary>
    /// The quantity metered for the contract in the month, per unit (registers of different units are never added together):
    /// the import registers of the meter chains linked to the contract that month. A unit whose quantity could not be determined has
    /// <see cref="Acc.Missing"/> set. Empty if the contract has no linked meter with figures for the month.
    /// </summary>
    public IReadOnlyDictionary<MeterUnit, Acc> Of(Contract contract, YearMonth month)
    {
        ArgumentNullException.ThrowIfNull(contract);

        var linked = LinkedChains(contract);
        if (linked.Count == 0)
        {
            return None;
        }

        var last = month.LastDay;
        Dictionary<MeterUnit, Acc>? result = null;
        foreach (var chain in linked)
        {
            if (!chain.Total.TryGetValue(month, out var acc) || !chain.Links.Any(l => l.IsActiveOn(last)))
            {
                continue;
            }

            result ??= [];
            result[chain.Unit] = result.TryGetValue(chain.Unit, out var known) ? known + acc : acc;
        }

        return result ?? None;
    }

    private List<Linked> LinkedChains(Contract contract)
    {
        if (_linked.TryGetValue(contract.Id, out var known))
        {
            return known;
        }

        var result = new List<Linked>();
        var links = data.LinksOfContract(contract.Id).ToList();
        if (links.Count > 0)
        {
            foreach (var chain in ChainsOf(contract.PropertyId))
            {
                var ids = chain.Meters.Meters.Select(m => m.Id).ToHashSet();
                var mine = links.Where(l => ids.Contains(l.MeterId)).ToList();
                if (mine.Count > 0 && ConsumptionReport.ImportTotal(chain) is { } total)
                {
                    result.Add(new Linked(mine, total, chain.Meters.Meters[^1].Unit));
                }
            }
        }

        _linked[contract.Id] = result;
        return result;
    }

    private List<ConsumptionReport.Chain> ChainsOf(Guid propertyId)
    {
        if (!_chains.TryGetValue(propertyId, out var chains))
        {
            var property = data.Properties.FirstOrDefault(p => p.Id == propertyId);
            chains = property is null ? [] : data.ChainsOf(property, _months, cancellation);
            _chains[propertyId] = chains;
        }

        return chains;
    }
}
