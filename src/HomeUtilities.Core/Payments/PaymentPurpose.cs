using System.Globalization;
using System.Text.RegularExpressions;

namespace HomeUtilities.Core.Payments;

/// <summary>Renders a payment purpose template such as <c>Abon. Nr. {CustomerNumber}, {Period:MM.yyyy}</c>.</summary>
public static partial class PaymentPurpose
{
    private const string DefaultPeriodFormat = "MM.yyyy";

    [GeneratedRegex(@"\{(?<name>[A-Za-z]+)(?::(?<format>[^{}]*))?\}")]
    private static partial Regex Placeholder();

    /// <summary>
    /// Replaces the placeholders <c>{CustomerNumber}</c>, <c>{ContractNumber}</c>, <c>{BillNumber}</c>,
    /// <c>{Period[:format]}</c>, <c>{Amount}</c> and <c>{PropertyName}</c>. Missing values become empty;
    /// unknown placeholders and unmatched braces are kept as typed. Formatting is culture-invariant.
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
                "Period" => FormatPeriod(ctx, format),
                _ => m.Value,
            };
        });
    }

    private static string FormatPeriod(PaymentPurposeContext ctx, string? format) =>
        ctx.Period is { } period
            ? period.FirstDay.ToString(string.IsNullOrEmpty(format) ? DefaultPeriodFormat : format, CultureInfo.InvariantCulture)
            : string.Empty;
}
