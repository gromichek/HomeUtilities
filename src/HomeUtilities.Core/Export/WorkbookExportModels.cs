using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Import;
using HomeUtilities.Core.Reports;

namespace HomeUtilities.Core.Export;

/// <summary>What to export.</summary>
/// <param name="ContractIds">The contracts that get a sheet; null = all.</param>
/// <param name="From">First month; null = from the beginning.</param>
/// <param name="To">Last month; null = to the end.</param>
/// <param name="ConvertToEur">True = amounts before 2014-01 are converted to EUR; false = every amount in the currency it is stored in.</param>
public sealed record WorkbookExportOptions(IReadOnlyCollection<Guid>? ContractIds = null, YearMonth? From = null, YearMonth? To = null, bool ConvertToEur = false);

/// <summary>The stored data an export reads.</summary>
/// <param name="Data">Properties, contracts, meters with readings, bills, tariffs (as the reports load them).</param>
/// <param name="Payments">The payments with their allocations.</param>
/// <param name="Suppliers">The suppliers (for the sheet names).</param>
/// <param name="Profiles">The saved import profiles; a contract with a profile is laid out like the sheet it was imported from.</param>
public sealed record WorkbookExportInput(
    ReportData Data,
    IReadOnlyList<Payment> Payments,
    IReadOnlyList<Supplier> Suppliers,
    IReadOnlyList<SavedImportProfile> Profiles);

/// <summary>The localized texts of the standard layout and the Total sheet.</summary>
/// <param name="Date">Header of the date column.</param>
/// <param name="Consumption">Header of the consumption column.</param>
/// <param name="Net">Header of the net amount.</param>
/// <param name="VatPercent">Header of the VAT percent.</param>
/// <param name="Vat">Header of the VAT amount.</param>
/// <param name="Calculated">Header of the calculated total.</param>
/// <param name="BillTotal">Header of the bill total.</param>
/// <param name="Difference">Header of the difference between bill and calculation.</param>
/// <param name="Paid">Header of the paid mark.</param>
/// <param name="PaidDate">Header of the payment date.</param>
/// <param name="Notes">Header of the notes.</param>
/// <param name="Currency">Header of the currency.</param>
/// <param name="TotalSheet">Name of the sheet with the totals per month and contract.</param>
/// <param name="TotalMonth">Header of the month column of the total sheet.</param>
/// <param name="TotalColumn">Header of the row totals.</param>
/// <param name="OneOff">Header of the column of bills without a contract.</param>
/// <param name="PositionHeader">Header of the readings column of a meter position (the register of the first meter of its chain).</param>
public sealed record ExportLabels(
    string Date,
    string Consumption,
    string Net,
    string VatPercent,
    string Vat,
    string Calculated,
    string BillTotal,
    string Difference,
    string Paid,
    string PaidDate,
    string Notes,
    string Currency,
    string TotalSheet,
    string TotalMonth,
    string TotalColumn,
    string OneOff,
    Func<Meter, MeterRegister, string> PositionHeader);
