using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Overview;

/// <summary>Everything the Overview needs, loaded once: the to-do list and the money figures are calculated from it.</summary>
/// <param name="Properties">The properties that are not archived.</param>
/// <param name="Contracts">All contracts (archived ones included: their balances still count).</param>
/// <param name="ContractMeters">The links between contracts and meters.</param>
/// <param name="Meters">The meters with registers and readings.</param>
/// <param name="Bills">All confirmed bills (drafts are in <paramref name="Drafts"/>).</param>
/// <param name="Payments">All payments with their allocations.</param>
/// <param name="ExpectedSettlements">The expected settlement of each equalized contract (positive = surcharge, negative = refund).</param>
/// <param name="Drafts">The draft bills created from templates that wait for confirmation; they do not count in any figure.</param>
public sealed record OverviewData(
    IReadOnlyList<Property> Properties,
    IReadOnlyList<Contract> Contracts,
    IReadOnlyList<ContractMeter> ContractMeters,
    IReadOnlyList<Meter> Meters,
    IReadOnlyList<Bill> Bills,
    IReadOnlyList<Payment> Payments,
    IReadOnlyDictionary<Guid, decimal> ExpectedSettlements,
    IReadOnlyList<Bill>? Drafts = null)
{
    /// <summary>The draft bills; empty when none were loaded.</summary>
    public IReadOnlyList<Bill> Drafts { get; init; } = Drafts ?? [];

    /// <summary>No data at all.</summary>
    public static OverviewData Empty { get; } = new([], [], [], [], [], [], new Dictionary<Guid, decimal>());
}
