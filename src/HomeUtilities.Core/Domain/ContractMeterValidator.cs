using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Domain;

/// <summary>Validation rules of <see cref="ContractMeter"/>.</summary>
public static class ContractMeterValidator
{
    /// <summary>Validates a contract–meter link.</summary>
    public static ValidationResult Validate(ContractMeter link)
    {
        ArgumentNullException.ThrowIfNull(link);

        var errors = new List<ValidationError>();
        if (link.ContractId == Guid.Empty)
        {
            errors.Add(new(ErrorCodes.ContractMeterContractRequired, nameof(ContractMeter.ContractId)));
        }

        if (link.MeterId == Guid.Empty)
        {
            errors.Add(new(ErrorCodes.ContractMeterMeterRequired, nameof(ContractMeter.MeterId)));
        }

        if (link.ValidTo is { } validTo && validTo < link.ValidFrom)
        {
            errors.Add(new(ErrorCodes.ContractMeterValidToBeforeValidFrom, nameof(ContractMeter.ValidTo)));
        }

        return ValidationHelpers.ToResult(errors);
    }
}
