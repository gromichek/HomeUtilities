namespace HomeUtilities.Core.Import;

/// <summary>Which currency the amounts of a sheet are in.</summary>
public enum ImportCurrencyRule
{
    /// <summary>LVL for periods before 2014-01, EUR from 2014-01 on (the owner's workbook).</summary>
    LvlBefore2014 = 0,

    /// <summary>All amounts are EUR.</summary>
    Eur = 1,

    /// <summary>All amounts are LVL.</summary>
    Lvl = 2,
}
