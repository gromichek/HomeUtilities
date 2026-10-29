namespace HomeUtilities.Core.Reminders;

/// <summary>What a reminder is about (DATA_MODEL §9).</summary>
public enum ReminderKind
{
    /// <summary>The reading window of one or more contracts of a property opens.</summary>
    SubmitReadings = 0,

    /// <summary>A bill is due soon or today.</summary>
    PayBill = 1,

    /// <summary>A property tax installment is due soon or today.</summary>
    TaxInstallment = 2,

    /// <summary>A meter verification is due soon or today.</summary>
    MeterVerification = 3,

    /// <summary>Draft bills wait for confirmation.</summary>
    DraftsToConfirm = 4,
}
