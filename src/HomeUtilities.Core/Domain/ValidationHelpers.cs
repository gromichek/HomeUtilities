using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Domain;

internal static class ValidationHelpers
{
    public const int MaxNameLength = 100;

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
