using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Tariffs;
using Microsoft.EntityFrameworkCore;

namespace HomeUtilities.Data.Stores;

/// <summary>EF Core implementation of <see cref="ITariffStore"/>.</summary>
public sealed class TariffStore(IDbContextFactory<HomeUtilitiesDbContext> factory) : StoreBase<TariffComponent>(factory), ITariffStore
{
    /// <summary>Loads the rates of a component query.</summary>
    internal static IQueryable<TariffComponent> IncludeAll(IQueryable<TariffComponent> query) => query.Include(t => t.Rates);

    /// <summary>Sorts the rates of a loaded component by valid-from date.</summary>
    internal static TariffComponent SortChildren(TariffComponent component)
    {
        component.Rates = [.. component.Rates.OrderBy(r => r.ValidFrom)];
        return component;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<TariffComponent>> ListByContractAsync(Guid contractId, CancellationToken ct = default) =>
        ListAsync(q => q.Where(t => t.ContractId == contractId), ct);

    /// <inheritdoc />
    protected override IQueryable<TariffComponent> Include(IQueryable<TariffComponent> query) => IncludeAll(query);

    /// <inheritdoc />
    protected override IQueryable<TariffComponent> Order(IQueryable<TariffComponent> query) =>
        query.OrderBy(t => t.SortOrder).ThenBy(t => t.Name);

    /// <inheritdoc />
    protected override TariffComponent Normalize(TariffComponent aggregate) => SortChildren(aggregate);
}
