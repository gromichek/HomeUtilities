using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Overview;

/// <summary>Everything the Overview needs, loaded once: the to-do list and the money figures are calculated from it.</summary>
/// <param name="Properties">The properties that are not archived.</param>
/// <param name="Contracts">All contracts (archived ones included: their balances still count).</param>
/// <param name="ContractMeters">The links between contracts and meters.</param>
/// <param name="Meters">The meters with registers and readings.</param>
/// <param name="Bills">All bills.</param>
/// <param name="Payments">All payments with their allocations.</param>
/// <param name="ExpectedSettlements">The expected settlement of each equalized contract (positive = surcharge, negative = refund).</param>
public sealed record OverviewData(
    IReadOnlyList<Property> Properties,
    IReadOnlyList<Contract> Contracts,
    IReadOnlyList<ContractMeter> ContractMeters,
    IReadOnlyList<Meter> Meters,
    IReadOnlyList<Bill> Bills,
    IReadOnlyList<Payment> Payments,
    IReadOnlyDictionary<Guid, decimal> ExpectedSettlements)
{
    /// <summary>No data at all.</summary>
    public static OverviewData Empty { get; } = new([], [], [], [], [], [], new Dictionary<Guid, decimal>());
}
