using HomeUtilities.Core.Common;
using HomeUtilities.Core.Consumption;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Reports;

/// <summary>
/// Consumption per month from the readings, chosen like the charges choose them (the reading window of the contract that bills
/// the meter). A chain of replaced meters counts as one. A month whose consumption cannot be determined is marked, not zero.
/// </summary>
internal static class ConsumptionReport
{
    internal sealed record Chain(MeterChain Meters, Guid PropertyId, Dictionary<RegisterKind, Dictionary<YearMonth, Acc>> ByKind);

    private static readonly RegisterKind[] KindOrder = [RegisterKind.Total, RegisterKind.Day, RegisterKind.Night, RegisterKind.Peak, RegisterKind.Export];

    public static ReportModel Create(ReportData data, ReportFilter filter, IReadOnlyList<Property> properties, CancellationToken cancellation = default)
    {
        var model = new ReportModel(ReportKind.Consumption, filter)
        {
            ToCell = (_, acc) => new ReportCell(acc.Any ? acc.A : null, acc.Approx || acc.Missing),
        };
        var months = filter.Months();

        foreach (var property in properties)
        {
            cancellation.ThrowIfCancellationRequested();
            var chains = ChainsOf(data, property, months, cancellation);
            switch (filter.ConsumptionGrouping)
            {
                case ConsumptionGrouping.Meter:
                    foreach (var chain in chains)
                    {
                        AddColumns(model, property, [chain], ColumnSubject.Meter, chain.Meters.Meters[^1]);
                    }

                    break;
                case ConsumptionGrouping.Contract:
                    AddContractColumns(model, data, property, chains, filter);
                    break;
                default:
                    foreach (var group in chains.GroupBy(c => (c.Meters.Meters[^1].Resource, c.Meters.Meters[^1].Unit)).OrderBy(g => g.Key.Resource).ThenBy(g => g.Key.Unit))
                    {
                        AddColumns(model, property, [.. group], ColumnSubject.Resource, group.First().Meters.Meters[^1]);
                    }

                    break;
            }
        }

        return model;
    }

    /// <summary>
    /// One column per contract (and unit) with the sum of the meters linked to it. A meter counts in every contract it is linked to
    /// in that month (hot water is in "Water" and in "Heating"); registers of different units are never added together.
    /// </summary>
    private static void AddContractColumns(ReportModel model, ReportData data, Property property, IReadOnlyList<Chain> chains, ReportFilter filter)
    {
        var filterContains = filter.Contains;
        var contracts = data.Contracts
            .Where(c => !c.IsDeleted && c.PropertyId == property.Id)
            .OrderBy(c => c.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Id);
        foreach (var contract in contracts)
        {
            var linked = new List<Chain>();
            var contractLinks = data.LinksOfContract(contract.Id).ToList();
            foreach (var chain in chains)
            {
                var ids = chain.Meters.Meters.Select(m => m.Id).ToHashSet();
                var links = contractLinks.Where(cm => ids.Contains(cm.MeterId)).ToList();
                var kept = new Dictionary<RegisterKind, Dictionary<YearMonth, Acc>>();
                foreach (var (kind, cells) in chain.ByKind)
                {
                    var inContract = cells.Where(c => filterContains(c.Key) && links.Any(cm => cm.IsActiveOn(c.Key.LastDay))).ToDictionary(c => c.Key, c => c.Value);
                    if (inContract.Count > 0)
                    {
                        kept[kind] = inContract;
                    }
                }

                if (kept.Count > 0)
                {
                    linked.Add(chain with { ByKind = kept });
                }
            }

            foreach (var group in linked.GroupBy(c => c.Meters.Meters[^1].Unit).OrderBy(g => g.Key))
            {
                AddColumns(model, property, [.. group], ColumnSubject.Contract, group.First().Meters.Meters[^1], contract);
            }
        }
    }

    private static void AddColumns(ReportModel model, Property property, IReadOnlyList<Chain> chains, ColumnSubject subject, Meter representative, Contract? contract = null)
    {
        var kinds = chains.SelectMany(c => c.ByKind.Keys).Distinct().OrderBy(k => Array.IndexOf(KindOrder, k)).ToList();
        var series = kinds.ToDictionary(k => k, k => Merge(chains.Where(c => c.ByKind.ContainsKey(k)).Select(c => c.ByKind[k])));

        // Day/night meters without a total register: the total is the sum of the tariffs.
        if (!series.ContainsKey(RegisterKind.Total))
        {
            var tariffs = kinds.Where(k => k is RegisterKind.Day or RegisterKind.Night or RegisterKind.Peak).ToList();
            if (tariffs.Count > 0)
            {
                series[RegisterKind.Total] = Merge(tariffs.Select(k => series[k]));
                kinds.Insert(0, RegisterKind.Total);
            }
        }

        var name = subject switch
        {
            ColumnSubject.Meter => representative.Location ?? representative.SerialNumber,
            ColumnSubject.Contract => contract!.Title,
            _ => null,
        };
        foreach (var kind in kinds)
        {
            var key = subject == ColumnSubject.Contract
                ? $"{property.Id}|contract|{contract!.Id}|{representative.Unit}|{kind}"
                : $"{property.Id}|{(subject == ColumnSubject.Meter ? representative.Id : representative.Resource)}|{representative.Unit}|{kind}";
            var column = ReportBuilder.Column(
                key, property, subject, ReportMeasure.Quantity, ReportValueKind.Quantity, name,
                service: contract?.Service, resource: representative.Resource, unit: representative.Unit, register: kind,
                categoryId: contract?.CategoryId);
            model.Add(column, series[kind], (from, to) => new ReportDrill(DrillTarget.Readings, property.Id, from, to));
        }
    }

    private static Dictionary<YearMonth, Acc> Merge(IEnumerable<Dictionary<YearMonth, Acc>> parts)
    {
        var sum = new Dictionary<YearMonth, Acc>();
        foreach (var part in parts)
        {
            foreach (var (month, acc) in part)
            {
                sum[month] = sum.TryGetValue(month, out var known) ? known + acc : acc;
            }
        }

        return sum;
    }

    /// <summary>
    /// The total of a chain over the months: the total register, or the sum of the day, night and peak registers when there is none.
    /// Export registers are never part of it. Null if the chain has no import register with figures.
    /// </summary>
    internal static Dictionary<YearMonth, Acc>? ImportTotal(Chain chain)
    {
        if (chain.ByKind.TryGetValue(RegisterKind.Total, out var total))
        {
            return total;
        }

        var tariffs = chain.ByKind.Where(k => k.Key is RegisterKind.Day or RegisterKind.Night or RegisterKind.Peak).Select(k => k.Value).ToList();
        return tariffs.Count == 0 ? null : Merge(tariffs);
    }

    internal static List<Chain> ChainsOf(ReportData data, Property property, IReadOnlyList<YearMonth> months, CancellationToken cancellation = default)
    {
        var meters = data.MetersOf(property.Id).ToList();
        var seen = new HashSet<Guid>();
        var result = new List<Chain>();
        foreach (var meter in meters.OrderBy(m => m.InstalledOn).ThenBy(m => m.Location, StringComparer.OrdinalIgnoreCase).ThenBy(m => m.Id))
        {
            cancellation.ThrowIfCancellationRequested();
            var chain = MeterChain.Build(meters, meter.Id);
            if (!seen.Add(chain.Meters[^1].Id))
            {
                continue;
            }

            var windows = WindowsOf(data, chain);
            var byKind = new Dictionary<RegisterKind, Dictionary<YearMonth, Acc>>();
            var kinds = chain.Meters.SelectMany(m => m.Registers).Where(r => !r.IsDeleted).Select(r => r.Kind).Distinct();
            foreach (var kind in kinds)
            {
                var cells = new Dictionary<YearMonth, Acc>();
                foreach (var month in months)
                {
                    if (Month(chain, kind, month, windows) is { } acc)
                    {
                        cells[month] = acc;
                    }
                }

                if (cells.Count > 0)
                {
                    byKind[kind] = cells;
                }
            }

            if (byKind.Count > 0)
            {
                result.Add(new Chain(chain, property.Id, byKind));
            }
        }

        return result;
    }

    /// <summary>The figures of one month; null when the meter did not exist then.</summary>
    private static Acc? Month(MeterChain chain, RegisterKind kind, YearMonth month, IReadOnlyList<(ContractMeter Link, (int From, int To) Window)> windows)
    {
        var last = chain.Meters[^1];
        if (chain.Meters[0].InstalledOn > month.LastDay || (last.RemovedOn is { } removed && removed < month.FirstDay))
        {
            return null;
        }

        var result = ConsumptionCalculator.CalculateForPeriod(chain, kind, month, WindowOf(windows, month));

        // Without a new reading since the previous period the start and the end are the same reading: nothing is known, not zero.
        if (!result.IsComplete || (result.StartReading is { } start && result.EndReading?.Id == start.Id))
        {
            return new Acc { Missing = true };
        }

        // An estimated reading, or a start reading older than the previous month (the quantity then also covers a month without readings).
        var approximate = result.Warnings.Any(w => w.Code == ConsumptionWarningCodes.EstimatedReadingUsed)
            || (result.StartReading is { } from && from.Date < month.AddMonths(-1).FirstDay);
        return new Acc { A = result.Quantity, Any = true, Approx = approximate };
    }

    /// <summary>The links of the chain's meters to contracts that have a reading window, oldest link first (found once per chain).</summary>
    private static List<(ContractMeter Link, (int From, int To) Window)> WindowsOf(ReportData data, MeterChain chain) =>
        [.. chain.Meters
            .SelectMany(m => data.LinksOfMeter(m.Id))
            .OrderBy(cm => cm.ValidFrom).ThenBy(cm => cm.Id)
            .Select(cm => (Link: cm, Contract: data.ContractById.GetValueOrDefault(cm.ContractId)))
            .Where(x => x.Contract is { ReadingDayFrom: not null, ReadingDayTo: not null })
            .Select(x => (x.Link, (x.Contract!.ReadingDayFrom!.Value, x.Contract.ReadingDayTo!.Value)))];

    /// <summary>The reading window of the first contract that bills the meter in the month.</summary>
    private static (int From, int To)? WindowOf(IReadOnlyList<(ContractMeter Link, (int From, int To) Window)> windows, YearMonth month)
    {
        var last = month.LastDay;
        foreach (var (link, window) in windows)
        {
            if (link.IsActiveOn(last))
            {
                return window;
            }
        }

        return null;
    }
}
