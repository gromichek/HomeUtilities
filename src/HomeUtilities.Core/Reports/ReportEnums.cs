namespace HomeUtilities.Core.Reports;

/// <summary>The reports of the Reports screen.</summary>
public enum ReportKind
{
    /// <summary>What each property costs per month or year.</summary>
    Costs = 0,

    /// <summary>How much water, electricity or heat was used.</summary>
    Consumption = 1,

    /// <summary>What one m³ / kWh / MWh really costs.</summary>
    UnitCost = 2,

    /// <summary>How far the bills are from the calculation.</summary>
    CalculatedVsBilled = 3,
}

/// <summary>Whether the rows of a report are months or years.</summary>
public enum ReportRows
{
    /// <summary>One row per month.</summary>
    Months = 0,

    /// <summary>One row per year.</summary>
    Years = 1,
}

/// <summary>What the columns of the costs report are grouped by (inside each property).</summary>
public enum CostGrouping
{
    /// <summary>By the category of the bill.</summary>
    Category = 0,

    /// <summary>By the service of the contract (one-off expenses: the service of their category).</summary>
    Service = 1,

    /// <summary>By contract (one-off expenses form their own column).</summary>
    Contract = 2,

    /// <summary>One column per property.</summary>
    Property = 3,
}

/// <summary>The figure a column shows.</summary>
public enum ReportMeasure
{
    /// <summary>Amount including VAT.</summary>
    Gross = 0,

    /// <summary>Amount excluding VAT.</summary>
    Net = 1,

    /// <summary>VAT amount.</summary>
    Vat = 2,

    /// <summary>Consumed quantity.</summary>
    Quantity = 3,

    /// <summary>Billed minus calculated.</summary>
    Difference = 4,

    /// <summary>Billed minus calculated, in percent of the calculated.</summary>
    DifferencePercent = 5,

    /// <summary>The calculated gross.</summary>
    Calculated = 6,

    /// <summary>The billed gross.</summary>
    Billed = 7,

    /// <summary>Quantity on the bill minus the metered quantity (losses).</summary>
    QuantityDifference = 8,
}

/// <summary>What a column of a report is about.</summary>
public enum ColumnSubject
{
    /// <summary>A whole property.</summary>
    Property = 0,

    /// <summary>A category.</summary>
    Category = 1,

    /// <summary>A service.</summary>
    Service = 2,

    /// <summary>A contract.</summary>
    Contract = 3,

    /// <summary>Expenses without a contract.</summary>
    OneOff = 4,

    /// <summary>A resource (cold water, electricity…) of all meters of a property.</summary>
    Resource = 5,

    /// <summary>One meter (with its replacements).</summary>
    Meter = 6,
}

/// <summary>What kind of number a column holds; decides the decimals.</summary>
public enum ReportValueKind
{
    /// <summary>An amount of money in EUR.</summary>
    Money = 0,

    /// <summary>A quantity (m³, kWh…).</summary>
    Quantity = 1,

    /// <summary>Money per unit (EUR per m³).</summary>
    UnitPrice = 2,

    /// <summary>A percentage.</summary>
    Percent = 3,
}

/// <summary>The kind of a row.</summary>
public enum ReportRowKind
{
    /// <summary>A calendar month.</summary>
    Month = 0,

    /// <summary>A calendar year.</summary>
    Year = 1,

    /// <summary>A month of the year across all years (year over year).</summary>
    MonthOfYear = 2,

    /// <summary>The totals row.</summary>
    Total = 3,
}

/// <summary>Where a tap on a cell leads.</summary>
public enum DrillTarget
{
    /// <summary>The Bills screen.</summary>
    Bills = 0,

    /// <summary>The readings of a property.</summary>
    Readings = 1,
}
