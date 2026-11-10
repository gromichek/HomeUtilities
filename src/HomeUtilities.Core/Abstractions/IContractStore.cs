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

    /// <summary>
    /// Counts the records of a contract that a category change would carry along: non-deleted bills (drafts included) and
    /// bill templates of the contract whose category is <paramref name="oldCategoryId"/>.
    /// </summary>
    Task<ContractCategoryRecords> CountCategoryRecordsAsync(Guid contractId, Guid oldCategoryId, CancellationToken ct = default);

    /// <summary>
    /// Saves the contract and, in the same transaction, moves its bills and bill templates from
    /// <paramref name="oldCategoryId"/> to the contract's new category. Bill lines and tariff components are not touched.
    /// </summary>
    /// <returns>How many bills and templates were changed.</returns>
    Task<ContractCategoryRecords> SaveWithCategoryChangeAsync(Contract contract, Guid oldCategoryId, CancellationToken ct = default);
}

/// <summary>Number of bills and bill templates of one contract in one category.</summary>
public readonly record struct ContractCategoryRecords(int Bills, int Templates)
{
    /// <summary>True when there is nothing to change.</summary>
    public bool IsEmpty => Bills == 0 && Templates == 0;
}
