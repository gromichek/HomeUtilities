namespace HomeUtilities.Core.Common;

/// <summary>Validation error codes. The UI maps them to localized messages.</summary>
public static class ErrorCodes
{
    /// <summary>A comment is longer than 2000 characters.</summary>
    public const string NotesTooLong = "common.notes_too_long";

    /// <summary>The contract has no category.</summary>
    public const string ContractCategoryRequired = "contract.category_required";

    /// <summary>A user category has no name.</summary>
    public const string CategoryNameRequired = "category.name_required";

    /// <summary>The category name is longer than 100 characters.</summary>
    public const string CategoryNameTooLong = "category.name_too_long";

    /// <summary>The category colour is not <c>#RRGGBB</c>.</summary>
    public const string CategoryColorInvalid = "category.color_invalid";

    /// <summary>The IBAN is not valid.</summary>
    public const string PaymentIbanInvalid = "payment.iban_invalid";

    /// <summary>The BIC is not 8 or 11 characters of the BIC format.</summary>
    public const string PaymentBicInvalid = "payment.bic_invalid";

    /// <summary>A payment detail is longer than 140 characters.</summary>
    public const string PaymentFieldTooLong = "payment.field_too_long";

    /// <summary>The purpose template contains a placeholder that does not exist or a stray brace; Args: the offending text.</summary>
    public const string PaymentPurposeInvalidPlaceholder = "payment.purpose_invalid_placeholder";

    /// <summary>A placeholder of the purpose template has an unsupported format; Args: the offending format.</summary>
    public const string PaymentPurposeInvalidFormat = "payment.purpose_invalid_format";

    /// <summary>Two versioned items share the same ValidFrom date.</summary>
    public const string DuplicateValidFrom = "versioned.duplicate_valid_from";

    /// <summary>The property name is blank.</summary>
    public const string PropertyNameRequired = "property.name_required";

    /// <summary>The property name is longer than 100 characters.</summary>
    public const string PropertyNameTooLong = "property.name_too_long";

    /// <summary>A property parameter value is negative.</summary>
    public const string PropertyParameterValueNegative = "property_parameter.value_negative";

    /// <summary>The declared residents value is not a whole number.</summary>
    public const string PropertyParameterResidentsNotWhole = "property_parameter.residents_not_whole";

    /// <summary>A custom property parameter has no name.</summary>
    public const string PropertyParameterNameRequired = "property_parameter.name_required";

    /// <summary>The supplier name is blank.</summary>
    public const string SupplierNameRequired = "supplier.name_required";

    /// <summary>The supplier name is longer than 100 characters.</summary>
    public const string SupplierNameTooLong = "supplier.name_too_long";

    /// <summary>The link URL is not an absolute http/https URL.</summary>
    public const string LinkUrlInvalid = "link.url_invalid";

    /// <summary>The link title is longer than 100 characters.</summary>
    public const string LinkTitleTooLong = "link.title_too_long";

    /// <summary>The contract has no property.</summary>
    public const string ContractPropertyRequired = "contract.property_required";

    /// <summary>The contract has no supplier.</summary>
    public const string ContractSupplierRequired = "contract.supplier_required";

    /// <summary>The contract title is blank.</summary>
    public const string ContractTitleRequired = "contract.title_required";

    /// <summary>The contract title is longer than 100 characters.</summary>
    public const string ContractTitleTooLong = "contract.title_too_long";

    /// <summary>The contract ends before it starts.</summary>
    public const string ContractValidToBeforeValidFrom = "contract.valid_to_before_valid_from";

    /// <summary>A contract day-of-month field is not within 1–31.</summary>
    public const string ContractDayOutOfRange = "contract.day_out_of_range";

    /// <summary>Only one of the two reading window days is set.</summary>
    public const string ContractReadingWindowIncomplete = "contract.reading_window_incomplete";

    /// <summary>The meter has no property.</summary>
    public const string MeterPropertyRequired = "meter.property_required";

    /// <summary>The number of dial digits is not within 1–12.</summary>
    public const string MeterDigitsOutOfRange = "meter.digits_out_of_range";

    /// <summary>The meter is removed before it is installed.</summary>
    public const string MeterRemovedBeforeInstalled = "meter.removed_before_installed";

    /// <summary>The verification interval is not within 1–600 months.</summary>
    public const string MeterVerificationIntervalOutOfRange = "meter.verification_interval_out_of_range";

    /// <summary>The meter has no register.</summary>
    public const string MeterRegisterRequired = "meter.register_required";

    /// <summary>Two registers of the meter share the same kind and direction.</summary>
    public const string MeterDuplicateRegister = "meter.duplicate_register";

    /// <summary>An Export register must have Export direction and vice versa.</summary>
    public const string MeterExportDirectionMismatch = "meter.export_direction_mismatch";

    /// <summary>The unit does not fit the resource.</summary>
    public const string MeterUnitIncompatible = "meter.unit_incompatible";

    /// <summary>The reading value is negative.</summary>
    public const string MeterReadingValueNegative = "meter_reading.value_negative";

    /// <summary>The reading date is outside the period the meter was installed.</summary>
    public const string MeterReadingDateOutsideInstallation = "meter_reading.date_outside_installation";

    /// <summary>The register already has an Initial reading.</summary>
    public const string MeterReadingDuplicateInitial = "meter_reading.duplicate_initial";

    /// <summary>The Initial reading is not the earliest of the register.</summary>
    public const string MeterReadingInitialNotEarliest = "meter_reading.initial_not_earliest";

    /// <summary>The register already has a Final reading.</summary>
    public const string MeterReadingDuplicateFinal = "meter_reading.duplicate_final";

    /// <summary>The Final reading is not the latest of the register.</summary>
    public const string MeterReadingFinalNotLatest = "meter_reading.final_not_latest";

    /// <summary>The Final reading is not dated on the day the meter was removed.</summary>
    public const string MeterReadingFinalDateMismatch = "meter_reading.final_date_mismatch";

    /// <summary>Another reading of the same kind exists on the same date.</summary>
    public const string MeterReadingDuplicateOnDate = "meter_reading.duplicate_on_date";

    /// <summary>The link has no contract.</summary>
    public const string ContractMeterContractRequired = "contract_meter.contract_required";

    /// <summary>The link has no meter.</summary>
    public const string ContractMeterMeterRequired = "contract_meter.meter_required";

    /// <summary>The link ends before it starts.</summary>
    public const string ContractMeterValidToBeforeValidFrom = "contract_meter.valid_to_before_valid_from";

    /// <summary>The VAT code is not 1–32 upper-case letters, digits or dashes.</summary>
    public const string VatRateCodeInvalid = "vat_rate.code_invalid";

    /// <summary>The VAT rate name is blank.</summary>
    public const string VatRateNameRequired = "vat_rate.name_required";

    /// <summary>A VAT percent is not within 0–100.</summary>
    public const string VatPercentOutOfRange = "vat.percent_out_of_range";

    /// <summary>The tariff component name is blank.</summary>
    public const string TariffNameRequired = "tariff.name_required";

    /// <summary>The tariff component name is longer than 100 characters.</summary>
    public const string TariffNameTooLong = "tariff.name_too_long";

    /// <summary>The tariff component has no contract.</summary>
    public const string TariffContractRequired = "tariff.contract_required";

    /// <summary>Neither a VAT code nor a VAT percent override is set.</summary>
    public const string TariffVatRequired = "tariff.vat_required";

    /// <summary>The tariff component has no active rate.</summary>
    public const string TariffRateRequired = "tariff.rate_required";

    /// <summary>A price is negative.</summary>
    public const string TariffPriceNegative = "tariff.price_negative";

    /// <summary>The rate has tiers but no tier period.</summary>
    public const string TariffTierPeriodRequired = "tariff.tier_period_required";

    /// <summary>The last tier is not unlimited.</summary>
    public const string TariffTierLastNotUnlimited = "tariff.tier_last_not_unlimited";

    /// <summary>Tier bounds are not ascending, or a tier other than the last is unlimited.</summary>
    public const string TariffTierOrderInvalid = "tariff.tier_order_invalid";

    /// <summary>Register kinds or meters are set for a basis that does not use them.</summary>
    public const string TariffSelectionNotAllowed = "tariff.selection_not_allowed";

    /// <summary>A net export component counts a register other than Export.</summary>
    public const string TariffNetExportRegister = "tariff.net_export_register";

    /// <summary>A quantity parameter is set for a basis other than PerArea or PerResident.</summary>
    public const string TariffQuantityParameterNotAllowed = "tariff.quantity_parameter_not_allowed";

    /// <summary>A custom quantity parameter has no name.</summary>
    public const string TariffQuantityParameterNameRequired = "tariff.quantity_parameter_name_required";

    /// <summary>A QuantityFromBill component has no unit.</summary>
    public const string TariffQuantityUnitRequired = "tariff.quantity_unit_required";

    /// <summary>A quantity unit is set for a basis other than QuantityFromBill.</summary>
    public const string TariffQuantityUnitNotAllowed = "tariff.quantity_unit_not_allowed";

    /// <summary>A QuantityFromBill component has stepped prices.</summary>
    public const string TariffTiersNotAllowed = "tariff.tiers_not_allowed";

    /// <summary>Net, VAT percent and total of an amount contradict each other.</summary>
    public const string AmountInconsistent = "amount.inconsistent";

    /// <summary>An amount needs a total, or net together with VAT percent.</summary>
    public const string AmountGrossRequired = "amount.gross_required";

    /// <summary>A custom field definition has no name.</summary>
    public const string CustomFieldNameRequired = "custom_field.name_required";

    /// <summary>A custom field name is longer than 60 characters.</summary>
    public const string CustomFieldNameTooLong = "custom_field.name_too_long";

    /// <summary>Two definitions of the same owner and target share a name.</summary>
    public const string CustomFieldNameDuplicate = "custom_field.name_duplicate";

    /// <summary>A custom field value does not parse for its kind.</summary>
    public const string CustomFieldInvalidValue = "custom_field.invalid_value";

    /// <summary>A text value is longer than 500 characters.</summary>
    public const string CustomFieldValueTooLong = "custom_field.value_too_long";

    /// <summary>The bill has no property.</summary>
    public const string BillPropertyRequired = "bill.property_required";

    /// <summary>The bill has no category.</summary>
    public const string BillCategoryRequired = "bill.category_required";

    /// <summary>A bill has a contract exactly when it is not a one-off.</summary>
    public const string BillContractKindMismatch = "bill.contract_kind_mismatch";

    /// <summary>A one-off expense has no title.</summary>
    public const string BillTitleRequired = "bill.title_required";

    /// <summary>The bill belongs to a different contract.</summary>
    public const string BillContractMismatch = "bill.contract_mismatch";

    /// <summary>The bill's property differs from the contract's.</summary>
    public const string BillPropertyMismatch = "bill.property_mismatch";

    /// <summary>The bill's currency differs from the contract's.</summary>
    public const string BillCurrencyMismatch = "bill.currency_mismatch";

    /// <summary>Only one of the service period dates is set.</summary>
    public const string BillServicePeriodIncomplete = "bill.service_period_incomplete";

    /// <summary>The service period ends before it starts.</summary>
    public const string BillServiceToBeforeFrom = "bill.service_to_before_from";

    /// <summary>The bill is due before it was issued.</summary>
    public const string BillDueBeforeIssued = "bill.due_before_issued";

    /// <summary>Only settlements and corrections may have a negative total.</summary>
    public const string BillNegativeNotAllowed = "bill.negative_not_allowed";

    /// <summary>The bill title is longer than 100 characters.</summary>
    public const string BillTitleTooLong = "bill.title_too_long";

    /// <summary>The bill number is longer than 100 characters.</summary>
    public const string BillNumberTooLong = "bill.number_too_long";

    /// <summary>A bill line has no description.</summary>
    public const string BillLineDescriptionRequired = "bill_line.description_required";

    /// <summary>The payment amount is zero.</summary>
    public const string PaymentAmountZero = "payment.amount_zero";

    /// <summary>The allocations exceed the payment amount.</summary>
    public const string PaymentOverAllocated = "payment.over_allocated";

    /// <summary>An allocation amount is zero.</summary>
    public const string PaymentAllocationAmountZero = "payment_allocation.amount_zero";

    /// <summary>An allocation has a different sign than the payment.</summary>
    public const string PaymentAllocationSignMismatch = "payment_allocation.sign_mismatch";

    /// <summary>The allocated bill does not exist or is deleted.</summary>
    public const string PaymentAllocationBillUnknown = "payment_allocation.bill_unknown";

    /// <summary>The allocated bill has a different currency.</summary>
    public const string PaymentAllocationCurrencyMismatch = "payment_allocation.currency_mismatch";

    /// <summary>Two allocations point to the same bill.</summary>
    public const string PaymentAllocationDuplicateBill = "payment_allocation.duplicate_bill";

    /// <summary>An attachment has no file name.</summary>
    public const string AttachmentFileNameRequired = "attachment.file_name_required";

    /// <summary>An attachment file name is longer than 200 characters.</summary>
    public const string AttachmentFileNameTooLong = "attachment.file_name_too_long";

    /// <summary>An attachment has no content type.</summary>
    public const string AttachmentContentTypeRequired = "attachment.content_type_required";

    /// <summary>The file type is not JPEG, PNG, HEIC or PDF.</summary>
    public const string AttachmentContentTypeNotAllowed = "attachment.content_type_not_allowed";

    /// <summary>The file is empty.</summary>
    public const string AttachmentEmpty = "attachment.empty";

    /// <summary>The file is larger than the limit; Args: the limit in MB.</summary>
    public const string AttachmentTooLarge = "attachment.too_large";

    /// <summary>The SHA-256 is not 64 hex characters.</summary>
    public const string AttachmentHashInvalid = "attachment.hash_invalid";

    /// <summary>The monthly amount of an equalized plan is not above zero.</summary>
    public const string EqualizedPlanAmountNotPositive = "equalized_plan.amount_not_positive";

    /// <summary>The end date of an equalized plan is before its start date.</summary>
    public const string EqualizedPlanValidToBeforeValidFrom = "equalized_plan.valid_to_before_valid_from";

    /// <summary>An equalized plan overlaps the previous one; Args: the start date of the later plan.</summary>
    public const string EqualizedPlanOverlap = "equalized_plan.overlap";

    /// <summary>The yearly property tax is not above zero.</summary>
    public const string TaxYearlyAmountNotPositive = "tax.yearly_amount_not_positive";

    /// <summary>The number of installments is not 1, 2 or 4.</summary>
    public const string TaxInstallmentCountInvalid = "tax.installment_count_invalid";

    /// <summary>A due date lies outside the tax year; Args: the year.</summary>
    public const string TaxDueDateOutsideYear = "tax.due_date_outside_year";

    /// <summary>A due date is not after the previous one.</summary>
    public const string TaxDueDatesNotAscending = "tax.due_dates_not_ascending";
}
