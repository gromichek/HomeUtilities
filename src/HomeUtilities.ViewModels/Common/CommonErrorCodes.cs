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

    /// <summary>A line amount from the bill was entered without the quantity it belongs to.</summary>
    public const string BillAmountNeedsQuantity = "bill_edit.amount_needs_quantity";

    /// <summary>A number is larger than the editor accepts.</summary>
    public const string NumberTooLarge = "common.number_too_large";

    /// <summary>A price has more than six decimals.</summary>
    public const string PriceTooManyDecimals = "tariff_editor.too_many_decimals";

    /// <summary>A component that counts meters has all of them switched off.</summary>
    public const string TariffNoMeterSelected = "tariff_editor.no_meter_selected";

    /// <summary>A component that counts registers has all of them switched off.</summary>
    public const string TariffNoRegisterSelected = "tariff_editor.no_register_selected";

    /// <summary>The VAT code of a catalog entry is used by another entry.</summary>
    public const string VatCodeDuplicate = "vat_editor.code_duplicate";

    /// <summary>A VAT catalog entry has no percentage.</summary>
    public const string VatVersionRequired = "vat_editor.version_required";

    /// <summary>A payment has neither a contract nor a property.</summary>
    public const string PaymentPropertyRequired = "payment_edit.property_required";

    /// <summary>An allocation is larger than what is open on its bill; the arguments are the allocated amount and the open amount.</summary>
    public const string PaymentAllocationExceedsOpen = "payment_edit.allocation_exceeds_open";

    /// <summary>The allocations take more than the payment; the argument is the excess.</summary>
    public const string PaymentOverAllocatedBy = "payment_edit.over_allocated_by";
}
