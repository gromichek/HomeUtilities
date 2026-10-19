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
}
