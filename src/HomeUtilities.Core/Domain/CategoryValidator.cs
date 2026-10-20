using System.Text.RegularExpressions;
using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Domain;

/// <summary>Validation rules of <see cref="Category"/>.</summary>
public static partial class CategoryValidator
{
    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex ColorPattern();

    /// <summary>Validates a category.</summary>
    public static ValidationResult Validate(Category category)
    {
        ArgumentNullException.ThrowIfNull(category);

        var errors = new List<ValidationError>();
        if (category.Name is null || string.IsNullOrWhiteSpace(category.Name))
        {
            if (!category.IsBuiltIn || category.Name is not null)
            {
                errors.Add(new(ErrorCodes.CategoryNameRequired, nameof(Category.Name)));
            }
        }
        else if (category.Name.Length > ValidationHelpers.MaxNameLength)
        {
            errors.Add(new(ErrorCodes.CategoryNameTooLong, nameof(Category.Name), [ValidationHelpers.MaxNameLength]));
        }

        if (!ColorPattern().IsMatch(category.Color ?? string.Empty))
        {
            errors.Add(new(ErrorCodes.CategoryColorInvalid, nameof(Category.Color)));
        }

        ValidationHelpers.CheckNotes(errors, category.Notes, nameof(Category.Notes));
        return ValidationHelpers.ToResult(errors);
    }
}
