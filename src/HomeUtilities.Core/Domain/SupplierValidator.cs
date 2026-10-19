using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Domain;

/// <summary>Validation rules of <see cref="Supplier"/>.</summary>
public static class SupplierValidator
{
    /// <summary>Validates a supplier including its links (deleted links are skipped).</summary>
    public static ValidationResult Validate(Supplier supplier)
    {
        ArgumentNullException.ThrowIfNull(supplier);

        var errors = new List<ValidationError>();
        if (string.IsNullOrWhiteSpace(supplier.Name))
        {
            errors.Add(new(ErrorCodes.SupplierNameRequired, nameof(Supplier.Name)));
        }
        else if (supplier.Name.Length > ValidationHelpers.MaxNameLength)
        {
            errors.Add(new(ErrorCodes.SupplierNameTooLong, nameof(Supplier.Name), [ValidationHelpers.MaxNameLength]));
        }

        errors.AddRange(ValidationHelpers.ValidateLinks(supplier.Links));
        return ValidationHelpers.ToResult(errors);
    }
}
