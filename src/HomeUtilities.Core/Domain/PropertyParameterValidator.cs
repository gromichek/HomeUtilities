using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Domain;

/// <summary>Validation rules of <see cref="PropertyParameter"/>.</summary>
public static class PropertyParameterValidator
{
    /// <summary>Validates a property parameter.</summary>
    public static ValidationResult Validate(PropertyParameter parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);

        var errors = new List<ValidationError>();
        if (parameter.Value < 0)
        {
            errors.Add(new(ErrorCodes.PropertyParameterValueNegative, nameof(PropertyParameter.Value)));
        }
        else if (parameter.Kind == PropertyParameterKind.DeclaredResidents && parameter.Value != decimal.Truncate(parameter.Value))
        {
            errors.Add(new(ErrorCodes.PropertyParameterResidentsNotWhole, nameof(PropertyParameter.Value)));
        }

        if (parameter.Kind == PropertyParameterKind.Custom && string.IsNullOrWhiteSpace(parameter.Name))
        {
            errors.Add(new(ErrorCodes.PropertyParameterNameRequired, nameof(PropertyParameter.Name)));
        }

        return ValidationHelpers.ToResult(errors);
    }
}
