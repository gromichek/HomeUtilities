using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Domain;

/// <summary>Validation rules of <see cref="Link"/>.</summary>
public static class LinkValidator
{
    /// <summary>Validates a link.</summary>
    public static ValidationResult Validate(Link link)
    {
        ArgumentNullException.ThrowIfNull(link);

        var errors = new List<ValidationError>();
        if (link.TryGetUri() is null)
        {
            errors.Add(new(ErrorCodes.LinkUrlInvalid, nameof(Link.Url)));
        }

        if (link.Title is { Length: > ValidationHelpers.MaxNameLength })
        {
            errors.Add(new(ErrorCodes.LinkTitleTooLong, nameof(Link.Title), [ValidationHelpers.MaxNameLength]));
        }

        return ValidationHelpers.ToResult(errors);
    }
}
