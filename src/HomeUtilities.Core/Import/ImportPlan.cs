using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Import;

/// <summary>How serious an import issue is.</summary>
public enum ImportIssueSeverity
{
    /// <summary>The row (or the part named) is imported; the owner should know.</summary>
    Warning = 0,

    /// <summary>The row is skipped.</summary>
    Error = 1,
}

/// <summary>A problem found in a row. The UI maps <see cref="Code"/> to a localized message.</summary>
/// <param name="Severity">Warning or error.</param>
/// <param name="Code">One of <see cref="ImportIssueCodes"/>.</param>
/// <param name="Args">Values for the message (column letters, texts).</param>
public sealed record ImportIssue(ImportIssueSeverity Severity, string Code, IReadOnlyList<string> Args);

/// <summary>A reading the import will create.</summary>
/// <param name="RegisterId">The register that receives it.</param>
/// <param name="Reading">The reading, ready to be stored.</param>
public sealed record PlannedReading(Guid RegisterId, MeterReading Reading);

/// <summary>What the import will do with one sheet row.</summary>
public sealed class PlannedRow
{
    /// <summary>Row number in the sheet (1-based).</summary>
    public int RowNumber { get; init; }

    /// <summary>The parsed period, or null if the cell could not be read.</summary>
    public YearMonth? Period { get; set; }

    /// <summary>Issues of the row.</summary>
    public List<ImportIssue> Issues { get; } = [];

    /// <summary>Readings that will be created.</summary>
    public List<PlannedReading> Readings { get; } = [];

    /// <summary>The bill that will be created.</summary>
    public Bill? Bill { get; set; }

    /// <summary>The payment (allocated to <see cref="Bill"/>) that will be created.</summary>
    public Payment? Payment { get; set; }

    /// <summary>True when an error makes the row skipped.</summary>
    public bool HasErrors => Issues.Any(i => i.Severity == ImportIssueSeverity.Error);

    /// <summary>True when the row has warnings and no errors.</summary>
    public bool HasWarnings => !HasErrors && Issues.Any(i => i.Severity == ImportIssueSeverity.Warning);

    /// <summary>True when the row creates nothing.</summary>
    public bool IsSkipped => Readings.Count == 0 && Bill is null;
}

/// <summary>Totals of a plan.</summary>
/// <param name="Readings">Readings to create.</param>
/// <param name="Bills">Bills to create.</param>
/// <param name="Payments">Payments to create.</param>
/// <param name="SkippedRows">Rows that create nothing.</param>
public sealed record ImportCounts(int Readings, int Bills, int Payments, int SkippedRows);

/// <summary>The dry run of an import: what every row of the sheet would create.</summary>
public sealed class ImportPlan
{
    /// <summary>A plan that cannot run because of a problem with the profile.</summary>
    public static ImportPlan Invalid(IReadOnlyList<ValidationError> problems) => new([], problems);

    /// <summary>Creates a plan.</summary>
    public ImportPlan(IReadOnlyList<PlannedRow> rows, IReadOnlyList<ValidationError> problems)
    {
        Rows = rows;
        Problems = problems;
        Counts = new(
            rows.Sum(r => r.Readings.Count),
            rows.Count(r => r.Bill is not null),
            rows.Count(r => r.Payment is not null),
            rows.Count(r => r.IsSkipped));
    }

    /// <summary>One entry per sheet row that is not blank.</summary>
    public IReadOnlyList<PlannedRow> Rows { get; }

    /// <summary>Problems with the profile that stop the plan (no rows are planned then).</summary>
    public IReadOnlyList<ValidationError> Problems { get; }

    /// <summary>Totals.</summary>
    public ImportCounts Counts { get; }

    /// <summary>Number of rows with errors.</summary>
    public int ErrorRows => Rows.Count(r => r.HasErrors);

    /// <summary>Number of rows with warnings only.</summary>
    public int WarningRows => Rows.Count(r => r.HasWarnings);
}

/// <summary>Everything the planner needs to know about the existing data.</summary>
/// <param name="Contract">The target contract.</param>
/// <param name="Meters">Meters (with registers and readings) that may receive readings.</param>
/// <param name="ExistingBills">Non-deleted bills of the contract.</param>
public sealed record ImportContext(Contract Contract, IReadOnlyList<Meter> Meters, IReadOnlyList<Bill> ExistingBills);

/// <summary>Codes of the issues the planner reports.</summary>
public static class ImportIssueCodes
{
    /// <summary>The period cell is empty although the row has data; Args: none.</summary>
    public const string PeriodMissing = "import_issue.period_missing";

    /// <summary>The period cell cannot be read; Args: the cell text.</summary>
    public const string PeriodInvalid = "import_issue.period_invalid";

    /// <summary>A number cell cannot be read; Args: column letter, cell text.</summary>
    public const string NumberInvalid = "import_issue.number_invalid";

    /// <summary>A reading is negative; Args: column letter, value.</summary>
    public const string ReadingNegative = "import_issue.reading_negative";

    /// <summary>A date cell cannot be read; Args: column letter, cell text.</summary>
    public const string DateInvalid = "import_issue.date_invalid";

    /// <summary>A reading is lower than the previous one and the meter has no dial digits; Args: column letter, value, previous value.</summary>
    public const string ReadingLowerThanPrevious = "import_issue.reading_lower_than_previous";

    /// <summary>A reading is higher than a later one that already exists; Args: column letter, value, later value.</summary>
    public const string ReadingHigherThanNext = "import_issue.reading_higher_than_next";

    /// <summary>A reading is lower than the previous one; the dial rolled over (the meter has digits); Args: column letter, value, previous value.</summary>
    public const string ReadingRollover = "import_issue.reading_rollover";

    /// <summary>A reading already exists on that date and is not imported; Args: column letter, date.</summary>
    public const string ReadingExists = "import_issue.reading_exists";

    /// <summary>The reading date is outside the installation period of the meter; Args: column letter, date.</summary>
    public const string ReadingOutsideInstallation = "import_issue.reading_outside_installation";

    /// <summary>A bill of this contract and period exists (or an earlier row has it); bill and payment are skipped; Args: period.</summary>
    public const string DuplicatePeriod = "import_issue.duplicate_period";

    /// <summary>Net, VAT and total do not match; the total is imported alone; Args: none.</summary>
    public const string AmountsInconsistent = "import_issue.amounts_inconsistent";

    /// <summary>The VAT percent is not between 0 and 100; the total is imported alone; Args: none.</summary>
    public const string VatPercentInvalid = "import_issue.vat_percent_invalid";

    /// <summary>The total is empty but readings are present; only the readings are imported; Args: none.</summary>
    public const string TotalMissing = "import_issue.total_missing";

    /// <summary>The row is marked paid but there is no bill to pay; Args: none.</summary>
    public const string PaidWithoutBill = "import_issue.paid_without_bill";

    /// <summary>The payment date cannot be read; the 1st of the month is used; Args: column letter, cell text.</summary>
    public const string PaidDateInvalid = "import_issue.paid_date_invalid";

    /// <summary>The row has no reading, amount or note to import; Args: none.</summary>
    public const string NothingToImport = "import_issue.nothing_to_import";

    /// <summary>The mapped register does not exist; Args: none.</summary>
    public const string RegisterMissing = "import_issue.register_missing";
}
