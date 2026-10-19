using System.Text.RegularExpressions;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Tariffs;

/// <summary>Validation rules of <see cref="VatRate"/>.</summary>
public static partial class VatRateValidator
{
    [GeneratedRegex("^[A-Z0-9-]{1,32}$")]
    private static partial Regex CodePattern();

    /// <summary>Validates a VAT catalog entry and its versions.</summary>
    public static ValidationResult Validate(VatRate rate)
    {
        ArgumentNullException.ThrowIfNull(rate);

        var errors = new List<ValidationError>();
        if (!CodePattern().IsMatch(rate.Code ?? string.Empty))
        {
            errors.Add(new(ErrorCodes.VatRateCodeInvalid, nameof(VatRate.Code)));
        }

        if (string.IsNullOrWhiteSpace(rate.Name))
        {
            errors.Add(new(ErrorCodes.VatRateNameRequired, nameof(VatRate.Name)));
        }

        for (var i = 0; i < rate.Versions.Count; i++)
        {
            var version = rate.Versions[i];
            if (!version.IsDeleted && (version.Percent < 0 || version.Percent > 100))
            {
                errors.Add(new(ErrorCodes.VatPercentOutOfRange, $"{nameof(VatRate.Versions)}[{i}].{nameof(VatRateVersion.Percent)}"));
            }
        }

        errors.AddRange(Versioned.Validate(rate.Versions).Errors.Select(e => e with { Field = nameof(VatRate.Versions) }));
        return ValidationHelpers.ToResult(errors);
    }
}
