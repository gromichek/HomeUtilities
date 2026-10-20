using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Abstractions;

/// <summary>Store of contracts and their meter links.</summary>
public interface IContractStore : IStore<Contract>
{
    /// <summary>Non-deleted contracts of a property.</summary>
    Task<IReadOnlyList<Contract>> ListByPropertyAsync(Guid propertyId, CancellationToken ct = default);

    /// <summary>Non-deleted meter links of a contract, ordered by valid-from date.</summary>
    Task<IReadOnlyList<ContractMeter>> ListContractMetersAsync(Guid contractId, CancellationToken ct = default);

    /// <summary>Inserts or updates a meter link.</summary>
    Task SaveContractMeterAsync(ContractMeter contractMeter, CancellationToken ct = default);
}
