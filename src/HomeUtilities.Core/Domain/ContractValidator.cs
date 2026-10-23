using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Domain;

/// <summary>Validation rules of <see cref="Contract"/>.</summary>
public static class ContractValidator
{
    /// <summary>Validates a contract including its links and custom field definitions (deleted ones are skipped).</summary>
    public static ValidationResult Validate(Contract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);

        var errors = new List<ValidationError>();
        if (contract.PropertyId == Guid.Empty)
        {
            errors.Add(new(ErrorCodes.ContractPropertyRequired, nameof(Contract.PropertyId)));
        }

        if (contract.SupplierId == Guid.Empty)
        {
            errors.Add(new(ErrorCodes.ContractSupplierRequired, nameof(Contract.SupplierId)));
        }

        if (contract.CategoryId == Guid.Empty)
        {
            errors.Add(new(ErrorCodes.ContractCategoryRequired, nameof(Contract.CategoryId)));
        }

        if (string.IsNullOrWhiteSpace(contract.Title))
        {
            errors.Add(new(ErrorCodes.ContractTitleRequired, nameof(Contract.Title)));
        }
        else if (contract.Title.Length > ValidationHelpers.MaxNameLength)
        {
            errors.Add(new(ErrorCodes.ContractTitleTooLong, nameof(Contract.Title), [ValidationHelpers.MaxNameLength]));
        }

        if (contract.ValidTo is { } validTo && validTo < contract.ValidFrom)
        {
            errors.Add(new(ErrorCodes.ContractValidToBeforeValidFrom, nameof(Contract.ValidTo)));
        }

        CheckDay(errors, contract.ReadingDayFrom, nameof(Contract.ReadingDayFrom));
        CheckDay(errors, contract.ReadingDayTo, nameof(Contract.ReadingDayTo));
        CheckDay(errors, contract.BillDay, nameof(Contract.BillDay));
        CheckDay(errors, contract.PaymentDueDay, nameof(Contract.PaymentDueDay));

        if (contract.ReadingDayFrom.HasValue != contract.ReadingDayTo.HasValue)
        {
            var missing = contract.ReadingDayFrom.HasValue ? nameof(Contract.ReadingDayTo) : nameof(Contract.ReadingDayFrom);
            errors.Add(new(ErrorCodes.ContractReadingWindowIncomplete, missing));
        }

        ValidationHelpers.CheckNotes(errors, contract.Notes, nameof(Contract.Notes));
        errors.AddRange(ValidationHelpers.ValidatePaymentDetails(contract.PaymentDetails));
        errors.AddRange(ValidationHelpers.ValidateLinks(contract.Links));
        errors.AddRange(CustomFieldValidator.ValidateAll(contract.CustomFields).Errors);
        return ValidationHelpers.ToResult(errors);
    }

    private static void CheckDay(List<ValidationError> errors, int? day, string field)
    {
        if (day is < 1 or > 31)
        {
            errors.Add(new(ErrorCodes.ContractDayOutOfRange, field, [day]));
        }
    }
}
