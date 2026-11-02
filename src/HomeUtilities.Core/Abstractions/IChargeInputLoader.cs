using HomeUtilities.Core.Charges;
using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Abstractions;

/// <summary>Assembles the <see cref="ChargeInput"/> of a contract from storage.</summary>
public interface IChargeInputLoader
{
    /// <summary>The input for the contract and period, or null if the contract or its property does not exist.</summary>
    Task<ChargeInput?> LoadAsync(Guid contractId, YearMonth period, CancellationToken ct = default);

    /// <summary>
    /// The inputs of many contract/month pairs in one go (T-028h): a storage implementation reads the shared data (contracts,
    /// properties, tariffs, meters with readings, VAT catalog, bills) once instead of once per pair. Pairs whose contract or
    /// property does not exist are missing from the result. The default simply calls <see cref="LoadAsync"/> per pair.
    /// </summary>
    async Task<IReadOnlyDictionary<(Guid ContractId, YearMonth Period), ChargeInput>> LoadManyAsync(
        IReadOnlyCollection<(Guid ContractId, YearMonth Period)> requests, CancellationToken ct = default)
    {
        var result = new Dictionary<(Guid, YearMonth), ChargeInput>();
        foreach (var (contractId, period) in requests.Distinct())
        {
            if (await LoadAsync(contractId, period, ct) is { } input)
            {
                result[(contractId, period)] = input;
            }
        }

        return result;
    }
}
