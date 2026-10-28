using System.Globalization;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Reports;

namespace HomeUtilities.ViewModels;

/// <summary>The texts of a report: column headings, row labels and the numbers as shown (screen and export use the same).</summary>
public sealed class ReportLabels(ILocalizer localizer, CultureInfo? culture = null)
{
    private CultureInfo Culture => culture ?? CultureInfo.CurrentCulture;

    /// <summary>"Costs", "Consumption", …</summary>
    public string KindName(ReportKind kind) => localizer["ReportKind" + kind];

    /// <summary>"Gross (incl. VAT)", "Difference, €", …</summary>
    public string MeasureName(ReportMeasure measure) => localizer["ReportMeasure" + measure];

    /// <summary>"Total".</summary>
    public string Total => localizer["ReportTotal"];

    /// <summary>The heading of the first column.</summary>
    public string PeriodHeader => localizer["ReportPeriodColumn"];

    /// <summary>The unit "m³", "kWh"…</summary>
    public string UnitName(MeterUnit unit) => localizer["Enum_MeterUnit_" + unit];

    /// <summary>The service "Cold water", "Heating"…</summary>
    public string ServiceName(ServiceKind service) => localizer["Enum_ServiceKind_" + service];

    /// <summary>The heading of a column, below the property name.</summary>
    public string Column(ReportColumn column)
    {
        var text = BaseLabel(column);
        if (column.Year is { } year)
        {
            text += "\n" + year.ToString(CultureInfo.InvariantCulture);
        }

        return text;
    }

    /// <summary>The label of a row.</summary>
    public string Row(ReportRow row) => row.Kind switch
    {
        ReportRowKind.Month => BillText.Period(new YearMonth(row.Year, row.Month)),
        ReportRowKind.Year => row.Year.ToString(CultureInfo.InvariantCulture),
        ReportRowKind.MonthOfYear => MonthName(row.Month),
        _ => Total,
    };

    /// <summary>The label of a row on a chart axis: "Jan 26" for a month, "2026" for a year, "Jan" for a month of the year.</summary>
    public string ChartRow(ReportRow row) => row.Kind switch
    {
        ReportRowKind.Month => $"{ShortMonthName(row.Month)} {(row.Year % 100).ToString("00", CultureInfo.InvariantCulture)}",
        ReportRowKind.Year => row.Year.ToString(CultureInfo.InvariantCulture),
        ReportRowKind.MonthOfYear => ShortMonthName(row.Month),
        _ => Total,
    };

    /// <summary>The short month name ("Jan").</summary>
    public string ShortMonthName(int month)
    {
        var name = new DateOnly(2001, month, 1).ToString("MMM", Culture).TrimEnd('.');
        return name.Length == 0 ? name : char.ToUpper(name[0], Culture) + name[1..];
    }

    /// <summary>The name of a chart series: the category, service, contract or property; <paramref name="withProperty"/> puts the property in front.</summary>
    public string SeriesName(ReportColumn column, bool withProperty)
    {
        var name = column.Subject switch
        {
            ColumnSubject.Property => column.PropertyName,
            ColumnSubject.Resource or ColumnSubject.Meter => BaseLabel(column),
            _ => Subject(column),
        };
        return withProperty && column.Subject != ColumnSubject.Property ? $"{column.PropertyName} · {name}" : name;
    }

    /// <summary>The month name of a month of the year ("January").</summary>
    public string MonthName(int month)
    {
        var name = new DateOnly(2001, month, 1).ToString("MMMM", Culture);
        return name.Length == 0 ? name : char.ToUpper(name[0], Culture) + name[1..];
    }

    /// <summary>The number of a cell as shown: "12,150", "≈ 3,79", "≈" for a month that could not be determined, empty for nothing.</summary>
    public string Cell(ReportCell cell, ReportColumn column)
    {
        if (cell.Value is not { } value)
        {
            return cell.IsApproximate ? "≈" : string.Empty;
        }

        var text = Number(value, column.ValueKind);
        return cell.IsApproximate ? "≈ " + text : text;
    }

    /// <summary>A number with the decimals of its kind.</summary>
    public string Number(decimal value, ReportValueKind kind)
    {
        var text = value.ToString("N" + ReportFormat.Decimals(kind, value).ToString(CultureInfo.InvariantCulture), Culture);
        return kind == ReportValueKind.Percent ? text + " %" : text;
    }

    private string BaseLabel(ReportColumn column)
    {
        if (column.IsSubtotal)
        {
            return Total;
        }

        var label = Subject(column);

        var unit = column.Unit is { } u ? UnitName(u) : null;
        return column.ValueKind switch
        {
            ReportValueKind.UnitPrice when unit is not null => $"{label} ({localizer["Currency_Symbol_EUR"]}/{unit})",
            ReportValueKind.Percent => $"{label} (%)",
            ReportValueKind.Quantity when column.Subject == ColumnSubject.Contract && unit is not null => $"{label} ({unit})",
            _ => label,
        };
    }

    private string Subject(ReportColumn column) => column.Subject switch
    {
        ColumnSubject.Category => column.Name ?? (column.Service is { } service ? ServiceName(service) : "—"),
        ColumnSubject.Service => column.Service is { } kind ? ServiceName(kind) : "—",
        ColumnSubject.Contract => column.Name ?? "—",
        ColumnSubject.OneOff => localizer["ReportOneOff"],
        ColumnSubject.Resource => ResourceLabel(column, null),
        ColumnSubject.Meter => ResourceLabel(column, column.Name),
        _ => MeasureName(column.Measure),
    };

    private string ResourceLabel(ReportColumn column, string? meterName)
    {
        var resource = column.Resource is { } r ? localizer["Enum_MeterResource_" + r] : string.Empty;
        var text = meterName is { Length: > 0 } ? $"{resource} ({meterName})" : resource;
        if (column.Unit is { } unit)
        {
            text += ", " + UnitName(unit);
        }

        if (column.Register is { } register and not RegisterKind.Total)
        {
            text += " · " + localizer["Enum_RegisterKind_" + register];
        }

        return text;
    }
}
