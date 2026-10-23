namespace HomeUtilities.Core.Domain;

/// <summary>Where a meter stands in its verification cycle.</summary>
public enum VerificationStatus
{
    /// <summary>No due date is known, or it is far away.</summary>
    None = 0,

    /// <summary>Due within <see cref="MeterVerification.WarningDays"/> days.</summary>
    DueSoon = 1,

    /// <summary>The due date has passed.</summary>
    Overdue = 2,
}
