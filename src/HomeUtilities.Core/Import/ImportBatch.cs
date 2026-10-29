using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Import;

/// <summary>One run of the import wizard: everything it created carries this batch's id and can be undone together.</summary>
public class ImportBatch : SyncEntity
{
    /// <summary>The profile that was used (may be deleted later).</summary>
    public Guid? ProfileId { get; set; }

    /// <summary>Name of the workbook file.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Name of the sheet.</summary>
    public string SheetName { get; set; } = string.Empty;

    /// <summary>Contract the bills and payments went to.</summary>
    public Guid? ContractId { get; set; }

    /// <summary>
    /// When the import finished (UTC). Records of the batch changed after this moment count as edited by the owner.
    /// </summary>
    public DateTimeOffset ImportedAt { get; set; }

    /// <summary>Number of readings created.</summary>
    public int ReadingCount { get; set; }

    /// <summary>Number of bills created.</summary>
    public int BillCount { get; set; }

    /// <summary>Number of payments created.</summary>
    public int PaymentCount { get; set; }

    /// <summary>Number of meter replacements the import carried out (new meters created).</summary>
    public int ReplacementCount { get; set; }

    /// <summary>Number of rows that created nothing.</summary>
    public int SkippedCount { get; set; }

    /// <summary>Free-text comment.</summary>
    public string? Notes { get; set; }
}
