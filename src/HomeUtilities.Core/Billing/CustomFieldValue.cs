using System.Globalization;

namespace HomeUtilities.Core.Billing;

/// <summary>
/// A value entered for a custom field. The name and kind are copied so the value survives removal of the
/// definition. <see cref="Value"/> is stored in an invariant format: numbers as <c>12.5</c>, dates as <c>yyyy-MM-dd</c>.
/// </summary>
/// <param name="DefinitionId">The definition, or null if it was removed or never existed.</param>
/// <param name="Name">Field name at the time of entry.</param>
/// <param name="Kind">Type of the value.</param>
/// <param name="Value">Value in invariant text format.</param>
public sealed record CustomFieldValue(Guid? DefinitionId, string Name, CustomFieldKind Kind, string Value)
{
    private const string DateFormat = "yyyy-MM-dd";

    /// <summary>Creates a value for a definition from a typed value (string, decimal/int/long, or DateOnly).</summary>
    /// <exception cref="ArgumentException">The value type does not fit the definition's kind.</exception>
    public static CustomFieldValue From(CustomFieldDefinition definition, object value)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(value);

        var text = (definition.Kind, value) switch
        {
            (CustomFieldKind.Text, string s) => s,
            (CustomFieldKind.Number or CustomFieldKind.Money, decimal d) => d.ToString(CultureInfo.InvariantCulture),
            (CustomFieldKind.Number or CustomFieldKind.Money, int i) => ((decimal)i).ToString(CultureInfo.InvariantCulture),
            (CustomFieldKind.Number or CustomFieldKind.Money, long l) => ((decimal)l).ToString(CultureInfo.InvariantCulture),
            (CustomFieldKind.Date, DateOnly date) => date.ToString(DateFormat, CultureInfo.InvariantCulture),
            _ => throw new ArgumentException($"A {value.GetType().Name} cannot be stored in a {definition.Kind} field.", nameof(value)),
        };
        return new(definition.Id, definition.Name, definition.Kind, text);
    }

    /// <summary>The value as a decimal for Number / Money fields; null for other kinds or an unparsable value.</summary>
    public decimal? AsDecimal() => TryParseDecimal(Value, out var result) && Kind is CustomFieldKind.Number or CustomFieldKind.Money
        ? result
        : null;

    /// <summary>The value as a date for Date fields; null for other kinds or an unparsable value.</summary>
    public DateOnly? AsDate() => TryParseDate(Value, out var result) && Kind == CustomFieldKind.Date ? result : null;

    internal static bool TryParseDecimal(string text, out decimal result) =>
        decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out result);

    internal static bool TryParseDate(string text, out DateOnly result) =>
        DateOnly.TryParseExact(text, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
}
