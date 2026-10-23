using System.Text.RegularExpressions;
using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Payments;

/// <summary>Validation rules of <see cref="PaymentDetails"/>.</summary>
public static partial class PaymentDetailsValidator
{
    /// <summary>Maximum length of any field (the bank purpose limit).</summary>
    public const int MaxFieldLength = 140;

    [GeneratedRegex("^[A-Z]{6}[A-Z0-9]{2}([A-Z0-9]{3})?$")]
    private static partial Regex BicPattern();

    /// <summary>Validates payment details; field names are relative to the details.</summary>
    public static ValidationResult Validate(PaymentDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);

        var errors = new List<ValidationError>();
        if (!string.IsNullOrWhiteSpace(details.Iban) && !Iban.IsValid(details.Iban))
        {
            errors.Add(new(ErrorCodes.PaymentIbanInvalid, nameof(PaymentDetails.Iban)));
        }

        if (details.Bic is not null && !BicPattern().IsMatch(details.Bic))
        {
            errors.Add(new(ErrorCodes.PaymentBicInvalid, nameof(PaymentDetails.Bic)));
        }

        errors.AddRange(PaymentPurpose.Validate(details.PurposeTemplate).Select(e =>
            new ValidationError(e.Code, nameof(PaymentDetails.PurposeTemplate), e.Args)));
        CheckLength(errors, details.RecipientName, nameof(PaymentDetails.RecipientName));
        CheckLength(errors, details.RecipientRegistrationNumber, nameof(PaymentDetails.RecipientRegistrationNumber));
        CheckLength(errors, details.Iban, nameof(PaymentDetails.Iban));
        CheckLength(errors, details.Bic, nameof(PaymentDetails.Bic));
        CheckLength(errors, details.BankName, nameof(PaymentDetails.BankName));
        CheckLength(errors, details.PurposeTemplate, nameof(PaymentDetails.PurposeTemplate));
        return errors.Count == 0 ? ValidationResult.Ok : ValidationResult.Fail([.. errors]);
    }

    private static void CheckLength(List<ValidationError> errors, string? value, string field)
    {
        if (value is { Length: > MaxFieldLength })
        {
            errors.Add(new(ErrorCodes.PaymentFieldTooLong, field, [MaxFieldLength]));
        }
    }
}
