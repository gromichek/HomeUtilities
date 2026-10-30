namespace HomeUtilities.Core.Domain;

/// <summary>A link that starts too late and the start it should get.</summary>
/// <param name="Link">The link (not changed by the search).</param>
/// <param name="Meter">The meter of the link.</param>
/// <param name="Contract">The contract of the link.</param>
/// <param name="NewValidFrom">The proposed start: the later of the meter's installation and the contract's start.</param>
public sealed record MeterLinkRepairItem(ContractMeter Link, Meter Meter, Contract Contract, DateOnly NewValidFrom);

/// <summary>
/// Finds links that were created "today" for a meter installed long ago: they start after the meter was installed, so the
/// earlier readings lie outside the link and charges and reports ignore the meter for those months.
/// </summary>
public static class MeterLinkRepair
{
    /// <summary>
    /// Links to move. A link qualifies when it is the first link of its meter and contract, starts after both the installation of
    /// the meter and the start of the contract, and the meter has a reading between the proposed start and the link's start.
    /// </summary>
    /// <param name="meters">Meters with registers and readings.</param>
    /// <param name="contracts">Contracts.</param>
    /// <param name="links">Meter links of all the contracts.</param>
    public static IReadOnlyList<MeterLinkRepairItem> Find(
        IEnumerable<Meter> meters, IEnumerable<Contract> contracts, IEnumerable<ContractMeter> links)
    {
        ArgumentNullException.ThrowIfNull(meters);
        ArgumentNullException.ThrowIfNull(contracts);
        ArgumentNullException.ThrowIfNull(links);

        var meterById = meters.Where(m => !m.IsDeleted).ToDictionary(m => m.Id);
        var contractById = contracts.Where(c => !c.IsDeleted).ToDictionary(c => c.Id);
        var items = new List<MeterLinkRepairItem>();
        foreach (var pair in links.Where(l => !l.IsDeleted).GroupBy(l => (l.ContractId, l.MeterId)))
        {
            if (!meterById.TryGetValue(pair.Key.MeterId, out var meter) || !contractById.TryGetValue(pair.Key.ContractId, out var contract))
            {
                continue;
            }

            var first = pair.OrderBy(l => l.ValidFrom).First();
            var proposed = MeterLinkRules.DefaultStart(meter.InstalledOn, contract.ValidFrom);
            if (first.ValidFrom <= proposed)
            {
                continue;
            }

            var hasEarlierReading = meter.Registers
                .Where(r => !r.IsDeleted)
                .SelectMany(r => r.Readings)
                .Any(r => !r.IsDeleted && r.Date >= proposed && r.Date < first.ValidFrom);
            if (hasEarlierReading)
            {
                items.Add(new MeterLinkRepairItem(first, meter, contract, proposed));
            }
        }

        return [.. items.OrderBy(i => i.Contract.Title, StringComparer.CurrentCultureIgnoreCase).ThenBy(i => i.Link.ValidFrom)];
    }
}
