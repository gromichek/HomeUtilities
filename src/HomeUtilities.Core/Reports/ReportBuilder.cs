using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Reports;

/// <summary>Builds the report tables from the loaded data. Pure functions: the same data and filter give the same table.</summary>
public static class ReportBuilder
{
    /// <summary>Builds the report of the given kind.</summary>
    public static ReportTable Build(ReportKind kind, ReportData data, ReportFilter filter)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(filter);

        var properties = SelectedProperties(data, filter);
        var model = kind switch
        {
            ReportKind.Costs => CostsReport.Create(data, filter, properties),
            ReportKind.Consumption => ConsumptionReport.Create(data, filter, properties),
            ReportKind.UnitCost => UnitCostReport.Create(data, filter, properties),
            ReportKind.CalculatedVsBilled => CalculatedVsBilledReport.Create(data, filter, properties),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown report."),
        };
        return ReportLayout.Build(model);
    }

    /// <summary>The costs report: what each property costs, per category, service, contract or property.</summary>
    public static ReportTable Costs(ReportData data, ReportFilter filter) => Build(ReportKind.Costs, data, filter);

    /// <summary>The consumption report: how much of each resource was used.</summary>
    public static ReportTable Consumption(ReportData data, ReportFilter filter) => Build(ReportKind.Consumption, data, filter);

    /// <summary>The unit cost report: what one unit of a contract's metered quantity costs.</summary>
    public static ReportTable UnitCost(ReportData data, ReportFilter filter) => Build(ReportKind.UnitCost, data, filter);

    /// <summary>The calculated vs billed report: how far the bills are from the calculation.</summary>
    public static ReportTable CalculatedVsBilled(ReportData data, ReportFilter filter) => Build(ReportKind.CalculatedVsBilled, data, filter);

    /// <summary>The selected properties: active ones first, then archived ones, each in list order.</summary>
    internal static IReadOnlyList<Property> SelectedProperties(ReportData data, ReportFilter filter) =>
    [
        .. data.Properties
            .Where(p => filter.PropertyIds is not { Count: > 0 } ids || ids.Contains(p.Id))
            .OrderBy(p => p.IsArchived).ThenBy(p => p.SortOrder).ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase),
    ];

    internal static ReportColumn Column(
        string key,
        Property property,
        ColumnSubject subject,
        ReportMeasure measure,
        ReportValueKind kind,
        string? name = null,
        ServiceKind? service = null,
        MeterResource? resource = null,
        MeterUnit? unit = null,
        RegisterKind? register = null) =>
        new(key, property.Id, property.Name, subject, name, service, resource, unit, register, measure, kind);
}
