using HomeUtilities.Core.Reports;

namespace HomeUtilities.Core.Abstractions;

/// <summary>Loads everything the reports need in one go, so a report does not query per contract or month.</summary>
public interface IReportDataLoader
{
    /// <summary>Properties (archived ones too), categories, contracts, meters with all readings, bills, tariffs and the VAT catalog.</summary>
    Task<ReportData> LoadAsync(CancellationToken ct = default);
}
