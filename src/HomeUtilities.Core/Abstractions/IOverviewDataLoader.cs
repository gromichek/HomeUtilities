using HomeUtilities.Core.Overview;

namespace HomeUtilities.Core.Abstractions;

/// <summary>Loads everything the Overview shows in one go, so the screen does not query per contract.</summary>
public interface IOverviewDataLoader
{
    /// <summary>
    /// The properties, contracts, meters (with the recent readings), bills and payments, and the expected settlement of every
    /// equalized contract that is active on <paramref name="today"/>.
    /// </summary>
    Task<OverviewData> LoadAsync(DateOnly today, CancellationToken ct = default);
}
