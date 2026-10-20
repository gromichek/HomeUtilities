using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Billing;

/// <summary>Validation rules of custom field definitions and values.</summary>
public static class CustomFieldValidator
{
    /// <summary>Maximum length of a definition name.</summary>
    public const int MaxNameLength = 60;

    /// <summary>Maximum length of a text value.</summary>
    public const int MaxTextLength = 500;

    /// <summary>Validates one definition: the name is required and at most 60 characters.</summary>
    public static ValidationResult Validate(CustomFieldDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var errors = new List<ValidationError>();
        if (string.IsNullOrWhiteSpace(definition.Name))
        {
            errors.Add(new(ErrorCodes.CustomFieldNameRequired, nameof(CustomFieldDefinition.Name)));
        }
        else if (definition.Name.Length > MaxNameLength)
        {
            errors.Add(new(ErrorCodes.CustomFieldNameTooLong, nameof(CustomFieldDefinition.Name), [MaxNameLength]));
        }

        return ValidationHelpers.ToResult(errors);
    }

    /// <summary>
    /// Validates a set of definitions: each one on its own, and names unique per owner and target,
    /// compared case-insensitively. Deleted definitions are skipped.
    /// </summary>
    public static ValidationResult ValidateAll(IEnumerable<CustomFieldDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        var errors = new List<ValidationError>();
        var seen = new HashSet<(Guid, CustomFieldTarget, string)>();
        var index = 0;
        foreach (var definition in definitions)
        {
            if (!definition.IsDeleted)
            {
                errors.AddRange(ValidationHelpers.Prefix("CustomFields", index, Validate(definition)));
                var key = (definition.OwnerId, definition.Target, (definition.Name ?? string.Empty).Trim().ToUpperInvariant());
                if (!string.IsNullOrWhiteSpace(definition.Name) && !seen.Add(key))
                {
                    errors.Add(new(ErrorCodes.CustomFieldNameDuplicate, $"CustomFields[{index}].Name"));
                }
            }

            index++;
        }

        return ValidationHelpers.ToResult(errors);
    }

    /// <summary>Validates a value: it must parse for its kind; text is at most 500 characters.</summary>
    public static ValidationResult Validate(CustomFieldValue value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var text = value.Value ?? string.Empty;
        var valid = value.Kind switch
        {
            CustomFieldKind.Number or CustomFieldKind.Money => CustomFieldValue.TryParseDecimal(text, out _),
            CustomFieldKind.Date => CustomFieldValue.TryParseDate(text, out _),
            _ => true,
        };

        var errors = new List<ValidationError>();
        if (!valid)
        {
            errors.Add(new(ErrorCodes.CustomFieldInvalidValue, nameof(CustomFieldValue.Value)));
        }
        else if (value.Kind == CustomFieldKind.Text && text.Length > MaxTextLength)
        {
            errors.Add(new(ErrorCodes.CustomFieldValueTooLong, nameof(CustomFieldValue.Value), [MaxTextLength]));
        }

        return ValidationHelpers.ToResult(errors);
    }
}
