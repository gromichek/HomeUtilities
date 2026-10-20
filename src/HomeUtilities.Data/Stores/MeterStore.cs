using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace HomeUtilities.Data.Stores;

/// <summary>EF Core implementation of <see cref="IMeterStore"/>.</summary>
public sealed class MeterStore(IDbContextFactory<HomeUtilitiesDbContext> factory) : StoreBase<Meter>(factory), IMeterStore
{
    /// <summary>Loads the registers and their readings of a meter query.</summary>
    internal static IQueryable<Meter> IncludeAll(IQueryable<Meter> query) =>
        query.Include(m => m.Registers).ThenInclude(r => r.Readings);

    /// <summary>Sorts registers and readings of a loaded meter.</summary>
    internal static Meter SortChildren(Meter meter)
    {
        meter.Registers = [.. meter.Registers.OrderBy(r => r.Kind).ThenBy(r => r.Direction)];
        foreach (var register in meter.Registers)
        {
            register.Readings = [.. register.Readings.OrderBy(r => r.Date)];
        }

        return meter;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Meter>> ListByPropertyAsync(Guid propertyId, CancellationToken ct = default) =>
        ListAsync(q => q.Where(m => m.PropertyId == propertyId), ct);

    /// <inheritdoc />
    protected override IQueryable<Meter> Include(IQueryable<Meter> query) => IncludeAll(query);

    /// <inheritdoc />
    protected override IQueryable<Meter> Order(IQueryable<Meter> query) =>
        query.OrderBy(m => m.InstalledOn).ThenBy(m => m.Id);

    /// <inheritdoc />
    protected override Meter Normalize(Meter aggregate) => SortChildren(aggregate);
}
