using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;

namespace HomeUtilities.ViewModels.Common;

/// <summary>
/// The four amount fields of a bill or bill line: Amount excl. VAT / VAT % / VAT / Total. The user fills in any two of
/// Net / VAT % / Total (the two edited last count) and the rest is derived with <see cref="Amounts.Complete"/>;
/// Total alone is allowed. VAT is always derived.
/// </summary>
public sealed partial class AmountEditor : ObservableObject
{
    /// <summary>Largest absolute amount the editors accept.</summary>
    public const decimal MaxAmount = 1_000_000_000m;

    private readonly List<string> _manual = [];
    private bool _updating;

    [ObservableProperty]
    private string _netText = string.Empty;

    [ObservableProperty]
    private string _vatPercentText = string.Empty;

    [ObservableProperty]
    private string _grossText = string.Empty;

    /// <summary>The derived VAT amount; read-only in the UI.</summary>
    [ObservableProperty]
    private string _vatText = string.Empty;

    /// <summary>Hint shown in the empty Total field (the calculated total), never a value.</summary>
    [ObservableProperty]
    private string _grossPlaceholder = string.Empty;

    /// <summary>Raised when the user changed a field (not when the editor is loaded or derives a value).</summary>
    public event Action? Edited;

    /// <summary>Clears all fields.</summary>
    public void Clear() => Load(null);

    /// <summary>Shows the stored amounts. The two fields that stay fixed when one is edited are Total and VAT %, else Total and Net.</summary>
    public void Load(AmountSet? amounts)
    {
        _manual.Clear();
        _updating = true;
        try
        {
            NetText = amounts?.Net is { } net ? FormatMoney(net) : string.Empty;
            VatPercentText = amounts?.VatPercent is { } percent ? FormatPercent(percent) : string.Empty;
            VatText = amounts?.Vat is { } vat ? FormatMoney(vat) : string.Empty;
            GrossText = amounts is null ? string.Empty : FormatMoney(amounts.Gross);
        }
        finally
        {
            _updating = false;
        }

        if (amounts is null)
        {
            return;
        }

        _manual.Add(nameof(GrossText));
        if (amounts.VatPercent is not null)
        {
            _manual.Add(nameof(VatPercentText));
        }
        else if (amounts.Net is not null)
        {
            _manual.Add(nameof(NetText));
        }
    }

    /// <summary>Sets the VAT % a new bill starts with (the contract's); it counts as filled in, so Net or Total alone completes the amount.</summary>
    public void SetDefaultVatPercent(decimal? percent)
    {
        _updating = true;
        try
        {
            VatPercentText = percent is { } value ? FormatPercent(value) : string.Empty;
        }
        finally
        {
            _updating = false;
        }

        _manual.Remove(nameof(VatPercentText));
        if (percent is not null)
        {
            _manual.Add(nameof(VatPercentText));
        }

        Recompute();
    }

    /// <summary>
    /// Reads the fields. Unparsable or oversized numbers and inconsistent amounts are returned as errors keyed by
    /// <c>Net</c>, <c>VatPercent</c> or <c>Gross</c>. With no errors, <paramref name="amounts"/> is the completed amount.
    /// </summary>
    public IReadOnlyList<ValidationError> TryRead(out AmountSet? amounts)
    {
        amounts = null;
        var errors = new List<ValidationError>();
        var net = ReadOptional(NetText, "Net", errors);
        var percent = ReadOptional(VatPercentText, "VatPercent", errors);
        var gross = ReadOptional(GrossText, "Gross", errors);
        if (errors.Count > 0)
        {
            return errors;
        }

        var (result, error) = Amounts.Complete(net, percent, gross);
        if (error is not null)
        {
            errors.Add(error);
            return errors;
        }

        amounts = result;
        return errors;
    }

    /// <summary>Parses an optional money or quantity text: blank is null; a bad or too large number is an error under <paramref name="field"/>.</summary>
    public static decimal? ReadOptional(string text, string field, List<ValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (!DecimalInput.TryParse(text, CultureInfo.CurrentCulture, out var value))
        {
            errors.Add(new(CommonErrorCodes.NumberInvalid, field));
            return null;
        }

        if (Math.Abs(value) > MaxAmount)
        {
            errors.Add(new(CommonErrorCodes.NumberTooLarge, field, [DecimalInput.Format(MaxAmount, CultureInfo.CurrentCulture, 0)]));
            return null;
        }

        return value;
    }

    /// <summary>Money as typed in the fields: two decimals, current culture.</summary>
    public static string FormatMoney(decimal value) => value.ToString("0.00", CultureInfo.CurrentCulture);

    private static string FormatPercent(decimal value) => DecimalInput.Format(value, CultureInfo.CurrentCulture, 2);

    partial void OnNetTextChanged(string value) => Typed(nameof(NetText), value);

    partial void OnVatPercentTextChanged(string value) => Typed(nameof(VatPercentText), value);

    partial void OnGrossTextChanged(string value) => Typed(nameof(GrossText), value);

    private void Typed(string field, string text)
    {
        if (_updating)
        {
            return;
        }

        _manual.Remove(field);
        if (!string.IsNullOrWhiteSpace(text))
        {
            _manual.Insert(0, field);
            if (_manual.Count > 2)
            {
                _manual.RemoveRange(2, _manual.Count - 2);
            }
        }

        Recompute();
        Edited?.Invoke();
    }

    /// <summary>Derives every field that is not one of the two filled in last; leaves everything alone while a filled field is not a number.</summary>
    private void Recompute()
    {
        var scratch = new List<ValidationError>();
        decimal? net = null, percent = null, gross = null;
        foreach (var field in _manual)
        {
            switch (field)
            {
                case nameof(NetText):
                    net = ReadOptional(NetText, "Net", scratch);
                    break;
                case nameof(VatPercentText):
                    percent = ReadOptional(VatPercentText, "VatPercent", scratch);
                    break;
                default:
                    gross = ReadOptional(GrossText, "Gross", scratch);
                    break;
            }
        }

        if (scratch.Count > 0)
        {
            return;
        }

        var (result, _) = Amounts.Complete(net, percent, gross);
        _updating = true;
        try
        {
            if (!_manual.Contains(nameof(NetText)))
            {
                NetText = result?.Net is { } n ? FormatMoney(n) : string.Empty;
            }

            if (!_manual.Contains(nameof(VatPercentText)))
            {
                VatPercentText = result?.VatPercent is { } p ? FormatPercent(p) : string.Empty;
            }

            if (!_manual.Contains(nameof(GrossText)))
            {
                GrossText = result is not null ? FormatMoney(result.Gross) : string.Empty;
            }

            VatText = result?.Vat is { } v ? FormatMoney(v) : string.Empty;
        }
        finally
        {
            _updating = false;
        }
    }
}
