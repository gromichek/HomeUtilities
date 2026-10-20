using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Domain;

/// <summary>Validation rules of <see cref="Property"/>.</summary>
public static class PropertyValidator
{
    /// <summary>Validates a property including its parameters (deleted parameters are skipped).</summary>
    public static ValidationResult Validate(Property property)
    {
        ArgumentNullException.ThrowIfNull(property);

        var errors = new List<ValidationError>();
        if (string.IsNullOrWhiteSpace(property.Name))
        {
            errors.Add(new(ErrorCodes.PropertyNameRequired, nameof(Property.Name)));
        }
        else if (property.Name.Length > ValidationHelpers.MaxNameLength)
        {
            errors.Add(new(ErrorCodes.PropertyNameTooLong, nameof(Property.Name), [ValidationHelpers.MaxNameLength]));
        }

        ValidationHelpers.CheckNotes(errors, property.Notes, nameof(Property.Notes));

        for (var i = 0; i < property.Parameters.Count; i++)
        {
            if (!property.Parameters[i].IsDeleted)
            {
                errors.AddRange(ValidationHelpers.Prefix(
                    nameof(Property.Parameters), i, PropertyParameterValidator.Validate(property.Parameters[i])));
            }
        }

        var groups = property.Parameters
            .Where(p => !p.IsDeleted)
            .GroupBy(p => (p.Kind, Name: p.Kind == PropertyParameterKind.Custom ? p.Name : null));
        foreach (var group in groups)
        {
            foreach (var error in Versioned.Validate(group).Errors)
            {
                errors.Add(error with { Field = nameof(Property.Parameters) + "." + error.Field });
            }
        }

        return ValidationHelpers.ToResult(errors);
    }
}
