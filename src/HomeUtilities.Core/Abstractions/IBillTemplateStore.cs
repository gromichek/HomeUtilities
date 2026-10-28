using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Abstractions;

/// <summary>Store of bill templates.</summary>
public interface IBillTemplateStore : IStore<BillTemplate>
{
    /// <summary>
    /// Creates the drafts that are due today for every active scheduled template and moves each template's
    /// <see cref="BillTemplate.LastGeneratedPeriod"/>. Safe to call as often as wanted: nothing is created twice, and a
    /// deleted draft is not created again.
    /// </summary>
    /// <returns>The number of drafts created.</returns>
    Task<int> GenerateDraftsAsync(DateOnly today, CancellationToken ct = default);
}
