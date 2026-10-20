namespace HomeUtilities.Core.Common;

/// <summary>Validation error codes. The UI maps them to localized messages.</summary>
public static class ErrorCodes
{
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
}
