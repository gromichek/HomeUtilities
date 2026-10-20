namespace HomeUtilities.Core.Billing;

/// <summary>Codes of <see cref="BillWarning"/>. The UI maps them to localized messages.</summary>
public static class BillWarningCodes
{
    /// <summary>The lines do not add up to the bill total (more than 0.01 apart).</summary>
    public const string LinesTotalMismatch = "bill.lines_total_mismatch";
}
