using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Abstractions;

/// <summary>Store of bills with their lines.</summary>
/// <remarks>
/// <see cref="IStore{T}.ListAsync"/> and <see cref="IStore{T}.GetAsync"/> return drafts too (the Bills screen shows them);
/// the per-contract and per-property lists feed balances, statuses and comparisons and leave them out.
/// </remarks>
public interface IBillStore : IStore<Bill>
{
    /// <summary>Non-deleted, confirmed bills (no drafts) of a contract ordered by period.</summary>
    Task<IReadOnlyList<Bill>> ListByContractAsync(Guid contractId, CancellationToken ct = default);

    /// <summary>Non-deleted, confirmed bills (no drafts) of a property ordered by period; <paramref name="from"/> and <paramref name="to"/> are inclusive.</summary>
    Task<IReadOnlyList<Bill>> ListByPropertyAsync(Guid propertyId, YearMonth? from, YearMonth? to, CancellationToken ct = default);
}
