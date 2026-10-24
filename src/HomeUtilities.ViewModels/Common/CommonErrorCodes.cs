namespace HomeUtilities.ViewModels.Common;

/// <summary>Error codes raised by the UI layer itself (input that cannot be turned into a domain value).</summary>
public static class CommonErrorCodes
{
    /// <summary>The text is not a number.</summary>
    public const string NumberInvalid = "common.number_invalid";

    /// <summary>A text is longer than its input limit; the argument is the limit.</summary>
    public const string TextTooLong = "common.text_too_long";

    /// <summary>The replacement date of a meter is before its installation date.</summary>
    public const string WizardDateBeforeInstalled = "meter_wizard.date_before_installed";

    /// <summary>The replacement date is before the last reading of the old meter; the argument is that date.</summary>
    public const string WizardDateBeforeLastReading = "meter_wizard.date_before_last_reading";

    /// <summary>The final reading is lower than the last reading and the meter has no digit count for a roll-over.</summary>
    public const string WizardFinalBelowLast = "meter_wizard.final_below_last";

    /// <summary>The "submitted on" date of a reading is before the reading date.</summary>
    public const string ReadingSubmittedBeforeDate = "reading_edit.submitted_before_date";
}
