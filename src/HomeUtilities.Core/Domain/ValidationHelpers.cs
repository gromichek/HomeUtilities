using HomeUtilities.Core.Common;
using HomeUtilities.Core.Payments;

namespace HomeUtilities.Core.Domain;

internal static class ValidationHelpers
{
    public const int MaxNameLength = InputLimits.Name;

    public const int MaxNotesLength = InputLimits.Notes;

    /// <summary>Adds <see cref="ErrorCodes.NotesTooLong"/> if the comment exceeds <see cref="MaxNotesLength"/>.</summary>
    public static void CheckNotes(List<ValidationError> errors, string? notes, string field, string? prefix = null)
    {
        if (notes is { Length: > MaxNotesLength })
        {
            errors.Add(new(ErrorCodes.NotesTooLong, prefix is null ? field : prefix + field, [MaxNotesLength]));
        }
    }

    /// <summary>Validates the optional payment details of a supplier or contract under <c>PaymentDetails.</c>.</summary>
    public static IEnumerable<ValidationError> ValidatePaymentDetails(PaymentDetails? details) =>
        details is null
            ? []
            : PaymentDetailsValidator.Validate(details).Errors
                .Select(e => e with { Field = "PaymentDetails" + (e.Field is null ? string.Empty : "." + e.Field) });

    /// <summary>Re-targets the errors of a nested item to <c>Collection[index].Field</c>.</summary>
    public static IEnumerable<ValidationError> Prefix(string collection, int index, ValidationResult result) =>
        result.Errors.Select(e => e with { Field = $"{collection}[{index}]" + (e.Field is null ? string.Empty : "." + e.Field) });

    public static IEnumerable<ValidationError> ValidateLinks(IReadOnlyList<Link> links) =>
        links.SelectMany((link, i) => link.IsDeleted
            ? []
            : Prefix(nameof(Supplier.Links), i, LinkValidator.Validate(link)));

    public static ValidationResult ToResult(List<ValidationError> errors) =>
        errors.Count == 0 ? ValidationResult.Ok : ValidationResult.Fail([.. errors]);
}
