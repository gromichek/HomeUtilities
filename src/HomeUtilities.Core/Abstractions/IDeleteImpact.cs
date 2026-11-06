namespace HomeUtilities.Core.Abstractions;

/// <summary>The kinds of records that disappear together with a deleted property, contract, supplier or meter.</summary>
public enum DependantKind
{
    Contracts,
    Meters,
    Bills,
    Payments,
    Tariffs,
    Readings,
    Links,
}

/// <summary>How many records of one kind depend on the record that is about to be deleted.</summary>
/// <param name="Kind">The kind of the dependent records.</param>
/// <param name="Count">How many of them there are (never 0 in a result).</param>
public readonly record struct Dependant(DependantKind Kind, int Count);

/// <summary>
/// Counts the records that are hidden together with a record when it is deleted (UI_GUIDELINES §5.1). Only kinds with at least
/// one record are returned, in the order they should be shown.
/// </summary>
public interface IDeleteImpactService
{
    /// <summary>Contracts, meters, bills and payments of a property.</summary>
    Task<IReadOnlyList<Dependant>> ForPropertyAsync(Guid propertyId, CancellationToken ct = default);

    /// <summary>Bills, payments and tariff components of a contract.</summary>
    Task<IReadOnlyList<Dependant>> ForContractAsync(Guid contractId, CancellationToken ct = default);

    /// <summary>Contracts of a supplier and their bills.</summary>
    Task<IReadOnlyList<Dependant>> ForSupplierAsync(Guid supplierId, CancellationToken ct = default);

    /// <summary>Readings and contract links of a meter.</summary>
    Task<IReadOnlyList<Dependant>> ForMeterAsync(Guid meterId, CancellationToken ct = default);
}
