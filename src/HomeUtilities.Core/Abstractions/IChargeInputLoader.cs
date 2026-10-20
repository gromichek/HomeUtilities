using HomeUtilities.Core.Charges;
using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Abstractions;

/// <summary>Assembles the <see cref="ChargeInput"/> of a contract from storage.</summary>
public interface IChargeInputLoader
{
    /// <summary>The input for the contract and period, or null if the contract or its property does not exist.</summary>
    Task<ChargeInput?> LoadAsync(Guid contractId, YearMonth period, CancellationToken ct = default);
}
