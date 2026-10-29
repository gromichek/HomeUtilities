using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Equalized;

namespace HomeUtilities.Core.Import;

/// <summary>How serious an import issue is.</summary>
public enum ImportIssueSeverity
{
    /// <summary>The row (or the part named) is imported; the owner should know.</summary>
    Warning = 0,

    /// <summary>The row is skipped.</summary>
    Error = 1,

    /// <summary>Information only (e.g. which fallback column was used); not counted as a problem.</summary>
    Info = 2,
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

    /// <summary>The date in the period column, or null when the cell names the period itself (text such as "12.2013").</summary>
    public DateOnly? RowDate { get; set; }

    /// <summary>The period the rule gave, before the owner's correction (see <see cref="Override"/>).</summary>
    public YearMonth? RuleBasedPeriod { get; set; }

    /// <summary>The owner's correction that was applied to this row, if any.</summary>
    public ImportRowOverride? Override { get; set; }

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

/// <summary>One register of a replaced meter: the final reading of the old register and the initial reading of the new one.</summary>
public sealed class PlannedReplacementRegister
{
    /// <summary>The position (register of the first meter of the chain) this register belongs to.</summary>
    public Guid PositionRegisterId { get; init; }

    /// <summary>Register of the replaced meter.</summary>
    public Guid OldRegisterId { get; init; }

    /// <summary>Register of the new meter.</summary>
    public Guid NewRegisterId { get; init; }

    /// <summary>The Final reading of the old register (dated with the replacement).</summary>
    public required MeterReading FinalReading { get; init; }

    /// <summary>The Initial reading of the new register (dated with the replacement).</summary>
    public required MeterReading InitialReading { get; init; }

    /// <summary>True when the initial reading is the sheet row's own reading (it is then listed in that row, not counted twice).</summary>
    public bool InitialIsRowReading { get; set; }

    /// <summary>True when the final value was estimated; the reading is stored as <see cref="ReadingKind.Estimated"/>.</summary>
    public bool FinalEstimated { get; set; }

    /// <summary>True when the initial value is a default (0) and not read from the sheet.</summary>
    public bool InitialEstimated { get; set; }

    /// <summary>True when the register is not mapped to a column (its values are the last known reading and 0).</summary>
    public bool Unmapped { get; init; }
}

/// <summary>A meter replacement the import will carry out (detected in the readings or taken from a note).</summary>
public sealed class PlannedReplacement
{
    /// <summary>Identifier of the proposal.</summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>The replaced meter.</summary>
    public Guid OldMeterId { get; init; }

    /// <summary>True when the replaced meter is itself created by this import (the chain grows by several replacements).</summary>
    public bool OldMeterIsPlanned { get; init; }

    /// <summary>The new meter with its registers; stored by the import.</summary>
    public required Meter NewMeter { get; init; }

    /// <summary>Replacement date: removal of the old meter, installation of the new one.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Sheet row the replacement was found in.</summary>
    public int RowNumber { get; init; }

    /// <summary>The registers with their final and initial values.</summary>
    public List<PlannedReplacementRegister> Registers { get; } = [];

    /// <summary>Existing links of the old meter that end the day before the replacement (copies with the new end date).</summary>
    public List<ContractMeter> LinksToClose { get; } = [];

    /// <summary>Links of the new meter, one for each open link of the old meter.</summary>
    public List<ContractMeter> LinksToCreate { get; } = [];

    /// <summary>All readings the replacement creates besides the ones listed in sheet rows.</summary>
    public IEnumerable<MeterReading> OwnReadings
    {
        get
        {
            foreach (var register in Registers)
            {
                yield return register.FinalReading;
                if (!register.InitialIsRowReading)
                {
                    yield return register.InitialReading;
                }
            }
        }
    }
}

/// <summary>The owner's correction of a proposed replacement (found again by row and position when the plan is made again).</summary>
/// <param name="RowNumber">Sheet row of the replacement.</param>
/// <param name="PositionRegisterId">The position (register of the first meter of the chain).</param>
/// <param name="Date">New replacement date, or null to keep the proposal.</param>
/// <param name="Final">New final value of the old meter, or null.</param>
/// <param name="Initial">New initial value of the new meter, or null.</param>
/// <param name="SerialNumber">Serial number of the new meter, or null.</param>
public sealed record ReplacementEdit(int RowNumber, Guid PositionRegisterId, DateOnly? Date, decimal? Final, decimal? Initial, string? SerialNumber);

/// <summary>Totals of a plan.</summary>
/// <param name="Readings">Readings to create (those of replacements included).</param>
/// <param name="Bills">Bills to create.</param>
/// <param name="Payments">Payments to create.</param>
/// <param name="SkippedRows">Rows that create nothing.</param>
/// <param name="Replacements">Meter replacements to carry out.</param>
public sealed record ImportCounts(int Readings, int Bills, int Payments, int SkippedRows, int Replacements = 0);

/// <summary>The dry run of an import: what every row of the sheet would create.</summary>
public sealed class ImportPlan
{
    /// <summary>A plan that cannot run because of a problem with the profile.</summary>
    public static ImportPlan Invalid(IReadOnlyList<ValidationError> problems) => new([], problems);

    /// <summary>Creates a plan.</summary>
    public ImportPlan(IReadOnlyList<PlannedRow> rows, IReadOnlyList<ValidationError> problems, IReadOnlyList<PlannedReplacement>? replacements = null)
    {
        Rows = rows;
        Problems = problems;
        Replacements = replacements ?? [];
        Counts = new(
            rows.Sum(r => r.Readings.Count) + Replacements.Sum(r => r.OwnReadings.Count()),
            rows.Count(r => r.Bill is not null),
            rows.Count(r => r.Payment is not null),
            rows.Count(r => r.IsSkipped),
            Replacements.Count);
    }

    /// <summary>Meter replacements proposed by the readings or taken from notes, oldest first.</summary>
    public IReadOnlyList<PlannedReplacement> Replacements { get; }

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
/// <param name="ContractLinks">Meter links of every contract of the property (to move them to a replacement meter); may be null.</param>
/// <param name="ReplacementEdits">Corrections of proposed replacements made by the owner; may be null.</param>
/// <param name="Plans">Equalized plans of the contract; a row whose period a plan covers becomes an equalized bill. May be null.</param>
public sealed record ImportContext(
    Contract Contract,
    IReadOnlyList<Meter> Meters,
    IReadOnlyList<Bill> ExistingBills,
    IReadOnlyList<ContractMeter>? ContractLinks = null,
    IReadOnlyList<ReplacementEdit>? ReplacementEdits = null,
    IReadOnlyList<EqualizedPlan>? Plans = null);

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

    /// <summary>The preferred amount column is empty; the fallback column was used; Args: column letter.</summary>
    public const string FallbackUsed = "import_issue.fallback_used";

    /// <summary>The row's bill is replaced by a later row of the same period; Args: period, row number of the later row.</summary>
    public const string PeriodSuperseded = "import_issue.period_superseded";

    /// <summary>This row's bill replaces the one of an earlier row of the same period; Args: period, row number of the earlier row.</summary>
    public const string PeriodSupersedes = "import_issue.period_supersedes";

    /// <summary>A meter replacement is proposed for the row; Args: none.</summary>
    public const string ReplacementProposed = "import_issue.replacement_proposed";

    /// <summary>The row was corrected by the owner (period, months covered or remark); Args: none.</summary>
    public const string RowOverridden = "import_issue.row_overridden";
}
