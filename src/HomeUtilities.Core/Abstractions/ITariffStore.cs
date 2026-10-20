using HomeUtilities.Core.Tariffs;

namespace HomeUtilities.Core.Abstractions;

/// <summary>Store of tariff components with their rates.</summary>
public interface ITariffStore : IStore<TariffComponent>
{
    /// <summary>Non-deleted components of a contract including rates.</summary>
    Task<IReadOnlyList<TariffComponent>> ListByContractAsync(Guid contractId, CancellationToken ct = default);
}
