using CommunityToolkit.Mvvm.ComponentModel;
using HomeUtilities.Core;
using HomeUtilities.Core.Common;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>
/// Dialog that edits the amount (and note) of one allocation of a payment: how much of the payment settles the bill. The
/// amount has the sign of the payment and may not exceed what is open on the bill. Nothing is stored here; the payment
/// editor reads <see cref="ResultAmount"/> and <see cref="ResultNote"/> after Save.
/// </summary>
public sealed partial class AllocationAmountEditorViewModel : EditorDialogViewModel
{
    private readonly ILocalizer _localizer;
    private readonly int _sign;
    private readonly decimal _openAmount;
    private readonly string _symbol;
    private IReadOnlyDictionary<string, string> _errors = new Dictionary<string, string>();

    [ObservableProperty]
    private string _amountText;

    [ObservableProperty]
    private string _note;

    /// <param name="localizer">Texts.</param>
    /// <param name="billName">"Water 10.2026", shown as the subtitle.</param>
    /// <param name="amount">The current amount of the allocation.</param>
    /// <param name="openAmount">What is open on the bill, not counting this payment (the largest amount that may be allocated).</param>
    /// <param name="note">The allocation's note.</param>
    /// <param name="currencySymbol">Symbol shown next to the amounts.</param>
    public AllocationAmountEditorViewModel(
        ILocalizer localizer, string billName, decimal amount, decimal openAmount, string? note, string currencySymbol)
        : base(localizer)
    {
        _localizer = localizer;
        _sign = PaymentText.SignOf(amount);
        _openAmount = openAmount;
        _symbol = currencySymbol;
        _amountText = PaymentText.Field(amount);
        _note = note ?? string.Empty;
        BillName = billName;
        Title = localizer["PaymentAllocationEdit"];
        AmountLabel = localizer["PaymentAllocationAmountLabel"];
        NoteLabel = localizer["PaymentAllocationNoteLabel"];
        OpenText = localizer.Get("PaymentAllocationOpen", BillText.Money(openAmount, currencySymbol));
    }

    /// <summary>"Water 10.2026".</summary>
    public string BillName { get; }

    public string AmountLabel { get; }

    public string NoteLabel { get; }

    /// <summary>"Open on the bill: 46,02 €".</summary>
    public string OpenText { get; }

    /// <summary>The validated amount; meaningful only after <see cref="EditorDialogViewModel.Saved"/>.</summary>
    public decimal ResultAmount { get; private set; }

    /// <summary>The validated note; meaningful only after <see cref="EditorDialogViewModel.Saved"/>.</summary>
    public string? ResultNote { get; private set; }

    public string AmountError => _errors.GetValueOrDefault("Amount", string.Empty);

    public string NoteError => _errors.GetValueOrDefault("Note", string.Empty);

    protected override bool TryApply()
    {
        var errors = new List<ValidationError>();
        var amount = AmountEditor.ReadOptional(AmountText, "Amount", errors);
        if (errors.Count == 0)
        {
            if (amount is null)
            {
                errors.Add(new ValidationError(CommonErrorCodes.NumberInvalid, "Amount"));
            }
            else if (amount == 0)
            {
                errors.Add(new ValidationError(ErrorCodes.PaymentAllocationAmountZero, "Amount"));
            }
            else if (Math.Sign(amount.Value) != _sign)
            {
                errors.Add(new ValidationError(ErrorCodes.PaymentAllocationSignMismatch, "Amount"));
            }
            else if (Math.Abs(amount.Value) > Math.Abs(_openAmount))
            {
                errors.Add(new ValidationError(
                    CommonErrorCodes.PaymentAllocationExceedsOpen,
                    "Amount",
                    [BillText.Money(amount.Value, _symbol), BillText.Money(_openAmount, _symbol)]));
            }
        }

        var note = string.IsNullOrWhiteSpace(Note) ? null : Note.Trim();
        if (note is { Length: > InputLimits.Notes })
        {
            errors.Add(new ValidationError(ErrorCodes.NotesTooLong, "Note", [InputLimits.Notes]));
        }

        _errors = errors
            .GroupBy(e => e.Field ?? string.Empty)
            .ToDictionary(g => g.Key, g => string.Join("\n", g.Select(e => ValidationText.For(_localizer, e))));
        OnPropertyChanged(nameof(AmountError));
        OnPropertyChanged(nameof(NoteError));
        if (errors.Count > 0)
        {
            return false;
        }

        ResultAmount = amount!.Value;
        ResultNote = note;
        return true;
    }

    protected override void RefreshTexts()
    {
        base.RefreshTexts();
        Title = _localizer["PaymentAllocationEdit"];
    }
}
