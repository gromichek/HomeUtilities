using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Consumption;

/// <summary>Meters replacing one another, ordered old to new.</summary>
/// <param name="Meters">The meters of the chain, ordered by installation date.</param>
public sealed record MeterChain(IReadOnlyList<Meter> Meters)
{
    /// <summary>
    /// Builds the chain containing <paramref name="anyMeterId"/> by following
    /// <see cref="Meter.ReplacedMeterId"/> in both directions. Deleted meters and meters of other chains are ignored.
    /// </summary>
    /// <exception cref="ArgumentException">The meter is not among <paramref name="meters"/>.</exception>
    public static MeterChain Build(IEnumerable<Meter> meters, Guid anyMeterId)
    {
        ArgumentNullException.ThrowIfNull(meters);

        var all = meters.Where(m => !m.IsDeleted).ToList();
        var byId = all.ToDictionary(m => m.Id);
        if (!byId.TryGetValue(anyMeterId, out var start))
        {
            throw new ArgumentException("The meter is not in the list.", nameof(anyMeterId));
        }

        var chain = new List<Meter> { start };
        var seen = new HashSet<Guid> { start.Id };

        var current = start;
        while (current.ReplacedMeterId is { } previousId && byId.TryGetValue(previousId, out var previous) && seen.Add(previous.Id))
        {
            chain.Insert(0, previous);
            current = previous;
        }

        current = start;
        while (all.FirstOrDefault(m => m.ReplacedMeterId == current.Id && !seen.Contains(m.Id)) is { } next)
        {
            seen.Add(next.Id);
            chain.Add(next);
            current = next;
        }

        return new MeterChain([.. chain.OrderBy(m => m.InstalledOn)]);
    }
}
