namespace HomeUtilities.Core.Domain;

/// <summary>Classifies the verification due date of a meter against today.</summary>
public static class MeterVerification
{
    /// <summary>A verification due within this many days is "due soon".</summary>
    public const int WarningDays = 60;

    /// <summary>
    /// <see cref="VerificationStatus.Overdue"/> if the due date is before <paramref name="today"/>,
    /// <see cref="VerificationStatus.DueSoon"/> if it is at most <see cref="WarningDays"/> days ahead, else none.
    /// A removed meter has no status.
    /// </summary>
    public static VerificationStatus GetStatus(Meter meter, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(meter);

        if (meter.RemovedOn is not null || meter.GetVerificationDue() is not { } due)
        {
            return VerificationStatus.None;
        }

        if (due < today)
        {
            return VerificationStatus.Overdue;
        }

        return due <= today.AddDays(WarningDays) ? VerificationStatus.DueSoon : VerificationStatus.None;
    }
}
