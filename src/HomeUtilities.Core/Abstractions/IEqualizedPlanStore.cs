using HomeUtilities.Core.Equalized;

namespace HomeUtilities.Core.Abstractions;

/// <summary>Store of the equalized plans of contracts.</summary>
public interface IEqualizedPlanStore : IStore<EqualizedPlan>
{
    /// <summary>Non-deleted plans of a contract, oldest first.</summary>
    Task<IReadOnlyList<EqualizedPlan>> ListByContractAsync(Guid contractId, CancellationToken ct = default);
}
