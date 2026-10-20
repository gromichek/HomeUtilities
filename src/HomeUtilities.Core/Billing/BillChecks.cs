namespace HomeUtilities.Core.Billing;

/// <summary>Non-blocking checks of a bill.</summary>
public static class BillChecks
{
    private const decimal Tolerance = 0.01m;

    /// <summary>Returns the warnings of a bill; deleted lines are ignored.</summary>
    public static IReadOnlyList<BillWarning> Warnings(Bill bill)
    {
        ArgumentNullException.ThrowIfNull(bill);

        var warnings = new List<BillWarning>();
        var lines = bill.Lines.Where(l => !l.IsDeleted).ToList();
        if (lines.Count > 0)
        {
            var total = lines.Sum(l => l.Gross);
            if (Math.Abs(total - bill.Gross) > Tolerance)
            {
                warnings.Add(new(BillWarningCodes.LinesTotalMismatch, [total, bill.Gross]));
            }
        }

        return warnings;
    }
}
