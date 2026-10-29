using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Import;

/// <summary>Which sheet columns hold the readings of one meter register.</summary>
public sealed class RegisterColumnMapping
{
    /// <summary>The register that receives the readings.</summary>
    public Guid RegisterId { get; set; }

    /// <summary>Column (1-based) with the dial values; 0 = not mapped.</summary>
    public int ReadingColumn { get; set; }

    /// <summary>Column (1-based) with the reading dates; 0 = the last day of the period is used.</summary>
    public int DateColumn { get; set; }
}

/// <summary>
/// How the columns of one sheet map to the records of one contract. Column numbers are 1-based, 0 means "not mapped".
/// A profile holds no amounts or readings, so it can be shared.
/// </summary>
public sealed class ImportProfile
{
    /// <summary>Default text of the "paid" marker.</summary>
    public const string DefaultPaidMarker = "***";

    /// <summary>Name of the sheet.</summary>
    public string SheetName { get; set; } = string.Empty;

    /// <summary>Row with the column headers.</summary>
    public int HeaderRow { get; set; } = 1;

    /// <summary>First row with data.</summary>
    public int FirstDataRow { get; set; } = 2;

    /// <summary>Last row with data; null = up to the first row with an empty period.</summary>
    public int? LastDataRow { get; set; }

    /// <summary>The contract that receives bills and payments; its meters receive the readings.</summary>
    public Guid ContractId { get; set; }

    /// <summary>Column with the period: a date, a month cell, "MM.yyyy", "yyyy-MM" or a month name.</summary>
    public int PeriodColumn { get; set; }

    /// <summary>Reading columns per register.</summary>
    public List<RegisterColumnMapping> Registers { get; set; } = [];

    /// <summary>Column with the bill total (gross).</summary>
    public int TotalColumn { get; set; }

    /// <summary>Column with the amount excluding VAT.</summary>
    public int NetColumn { get; set; }

    /// <summary>Column with the VAT percent.</summary>
    public int VatPercentColumn { get; set; }

    /// <summary>Column with the VAT amount.</summary>
    public int VatColumn { get; set; }

    /// <summary>Column that holds the "paid" marker.</summary>
    public int PaidColumn { get; set; }

    /// <summary>Text in the paid column that means "paid in full" (compared ignoring case and spaces).</summary>
    public string PaidMarker { get; set; } = DefaultPaidMarker;

    /// <summary>Column with the payment date.</summary>
    public int PaidDateColumn { get; set; }

    /// <summary>Column with the quantity the supplier billed.</summary>
    public int QuantityColumn { get; set; }

    /// <summary>Columns with free text; joined with the column header as a prefix.</summary>
    public List<int> NotesColumns { get; set; } = [];

    /// <summary>Currency rule of the sheet.</summary>
    public ImportCurrencyRule CurrencyRule { get; set; } = ImportCurrencyRule.LvlBefore2014;

    /// <summary>Currency of an amount of the given period under <see cref="CurrencyRule"/>.</summary>
    public Currency CurrencyFor(YearMonth period) => CurrencyRule switch
    {
        ImportCurrencyRule.Eur => Currency.EUR,
        ImportCurrencyRule.Lvl => Currency.LVL,
        _ => period < new YearMonth(2014, 1) ? Currency.LVL : Currency.EUR,
    };

    /// <summary>True when the paid marker text matches the cell text.</summary>
    public bool IsPaidMarker(string? cellText)
    {
        var marker = (PaidMarker ?? string.Empty).Trim();
        return marker.Length > 0 && string.Equals((cellText ?? string.Empty).Trim(), marker, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A deep copy.</summary>
    public ImportProfile Clone() => new()
    {
        SheetName = SheetName,
        HeaderRow = HeaderRow,
        FirstDataRow = FirstDataRow,
        LastDataRow = LastDataRow,
        ContractId = ContractId,
        PeriodColumn = PeriodColumn,
        Registers = [.. Registers.Select(r => new RegisterColumnMapping { RegisterId = r.RegisterId, ReadingColumn = r.ReadingColumn, DateColumn = r.DateColumn })],
        TotalColumn = TotalColumn,
        NetColumn = NetColumn,
        VatPercentColumn = VatPercentColumn,
        VatColumn = VatColumn,
        PaidColumn = PaidColumn,
        PaidMarker = PaidMarker,
        PaidDateColumn = PaidDateColumn,
        QuantityColumn = QuantityColumn,
        NotesColumns = [.. NotesColumns],
        CurrencyRule = CurrencyRule,
    };
}
