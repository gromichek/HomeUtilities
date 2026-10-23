using System.Globalization;
using System.Text.RegularExpressions;
using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Payments;

/// <summary>Renders a payment purpose template such as <c>Abon. Nr. {CustomerNumber}, {Period:MM.yyyy}</c>.</summary>
public static partial class PaymentPurpose
{
    private const string DefaultPeriodFormat = "MM.yyyy";
    private const string PeriodFormatChars = "dMy.-/ ";
    private static readonly string[] PlainPlaceholders = ["CustomerNumber", "ContractNumber", "BillNumber", "PropertyName", "Amount"];

    [GeneratedRegex(@"\{(?<name>[A-Za-z]+)(?::(?<format>[^{}]*))?\}")]
    private static partial Regex Placeholder();

    /// <summary>
    /// Replaces the placeholders <c>{CustomerNumber}</c>, <c>{ContractNumber}</c>, <c>{BillNumber}</c>,
    /// <c>{Period[:format]}</c>, <c>{Amount}</c> and <c>{PropertyName}</c>. Missing values become empty;
    /// unknown placeholders, unmatched braces and unsupported period formats are kept as typed; never throws.
    /// Formatting is culture-invariant.
    /// </summary>
    public static string Render(string template, PaymentPurposeContext ctx)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(ctx);

        return Placeholder().Replace(template, m =>
        {
            var format = m.Groups["format"].Success ? m.Groups["format"].Value : null;
            return m.Groups["name"].Value switch
            {
                "CustomerNumber" when format is null => ctx.CustomerNumber ?? string.Empty,
                "ContractNumber" when format is null => ctx.ContractNumber ?? string.Empty,
                "BillNumber" when format is null => ctx.BillNumber ?? string.Empty,
                "PropertyName" when format is null => ctx.PropertyName ?? string.Empty,
                "Amount" when format is null => ctx.Amount?.ToString("0.00", CultureInfo.InvariantCulture) ?? string.Empty,
                "Period" => FormatPeriod(ctx, format, m.Value),
                _ => m.Value,
            };
        });
    }

    /// <summary>
    /// Checks a template typed by the user: unknown placeholders and stray braces
    /// (<see cref="ErrorCodes.PaymentPurposeInvalidPlaceholder"/>) and unsupported formats
    /// (<see cref="ErrorCodes.PaymentPurposeInvalidFormat"/>); <c>Args[0]</c> is the offending text. Null or blank is valid.
    /// </summary>
    public static IReadOnlyList<ValidationError> Validate(string? template)
    {
        var errors = new List<ValidationError>();
        if (string.IsNullOrWhiteSpace(template))
        {
            return errors;
        }

        var rest = template;
        foreach (Match m in Placeholder().Matches(template))
        {
            var name = m.Groups["name"].Value;
            var format = m.Groups["format"].Success ? m.Groups["format"].Value : null;
            if (name == "Period")
            {
                if (format is not null && !IsValidPeriodFormat(format))
                {
                    errors.Add(new ValidationError(ErrorCodes.PaymentPurposeInvalidFormat, null, [format]));
                }
            }
            else if (!PlainPlaceholders.Contains(name))
            {
                errors.Add(new ValidationError(ErrorCodes.PaymentPurposeInvalidPlaceholder, null, [m.Value]));
            }
            else if (format is not null)
            {
                errors.Add(new ValidationError(ErrorCodes.PaymentPurposeInvalidFormat, null, [format]));
            }

            rest = rest.Replace(m.Value, string.Empty, StringComparison.Ordinal);
        }

        if (rest.Contains('{', StringComparison.Ordinal) || rest.Contains('}', StringComparison.Ordinal))
        {
            errors.Add(new ValidationError(ErrorCodes.PaymentPurposeInvalidPlaceholder, null, [Truncate(template)]));
        }

        return errors;
    }

    private static string Truncate(string text) => text.Length <= 40 ? text : text[..40];

    /// <summary>An empty format means the default; otherwise only <c>d M y . - / space</c> are allowed.</summary>
    private static bool IsValidPeriodFormat(string format) =>
        format.Length == 0 || format.All(c => PeriodFormatChars.Contains(c, StringComparison.Ordinal));

    private static string FormatPeriod(PaymentPurposeContext ctx, string? format, string asTyped)
    {
        if (ctx.Period is not { } period)
        {
            return string.Empty;
        }

        if (string.IsNullOrEmpty(format))
        {
            return period.FirstDay.ToString(DefaultPeriodFormat, CultureInfo.InvariantCulture);
        }

        if (!IsValidPeriodFormat(format))
        {
            return asTyped;
        }

        try
        {
            // A single character would be a standard format ("d" = short date); "%" makes it a custom one.
            var custom = format.Length == 1 ? "%" + format : format;
            return period.FirstDay.ToString(custom, CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            return asTyped;
        }
    }
}
