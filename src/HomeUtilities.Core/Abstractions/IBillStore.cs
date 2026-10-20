using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Abstractions;

/// <summary>Store of bills with their lines.</summary>
public interface IBillStore : IStore<Bill>
{
    /// <summary>Non-deleted bills of a contract ordered by period.</summary>
    Task<IReadOnlyList<Bill>> ListByContractAsync(Guid contractId, CancellationToken ct = default);

    /// <summary>Non-deleted bills of a property ordered by period; <paramref name="from"/> and <paramref name="to"/> are inclusive.</summary>
    Task<IReadOnlyList<Bill>> ListByPropertyAsync(Guid propertyId, YearMonth? from, YearMonth? to, CancellationToken ct = default);
}
