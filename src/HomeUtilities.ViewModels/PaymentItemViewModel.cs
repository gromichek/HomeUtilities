using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>One row of the payments list.</summary>
public sealed partial class PaymentItemViewModel : ObservableObject, ISelectableListItem
{
    [ObservableProperty]
    private bool _isSelected;

    public PaymentItemViewModel(
        Guid id,
        bool isRefund,
        string dateText,
        string amountText,
        string titleText,
        string methodText,
        string paysText,
        string prepaymentText,
        string quickText,
        bool isHighlighted,
        Action<Guid> select)
    {
        Id = id;
        IsRefund = isRefund;
        DateText = dateText;
        AmountText = amountText;
        TitleText = titleText;
        MethodText = methodText;
        PaysText = paysText;
        PrepaymentText = prepaymentText;
        QuickText = quickText;
        IsHighlighted = isHighlighted;
        SelectCommand = new RelayCommand(() => select(Id));
    }

    public Guid Id { get; }

    /// <summary>A refund received (negative amount), shown in green.</summary>
    public bool IsRefund { get; }

    /// <summary>"03.11.2026".</summary>
    public string DateText { get; }

    /// <summary>"58,32 €" or "−12,00 € refund".</summary>
    public string AmountText { get; }

    /// <summary>The contract's title, the property, or "Several contracts".</summary>
    public string TitleText { get; }

    /// <summary>"Bank transfer".</summary>
    public string MethodText { get; }

    /// <summary>"Water 09.2026, Water 10.2026 +1"; empty when the payment settles no bill.</summary>
    public string PaysText { get; }

    public bool HasPays => PaysText.Length > 0;

    /// <summary>"Prepayment 12,30 €"; empty when everything is allocated.</summary>
    public string PrepaymentText { get; }

    public bool HasPrepayment => PrepaymentText.Length > 0;

    /// <summary>"Paid button" marker of a quick-action payment; empty for others.</summary>
    public string QuickText { get; }

    public bool IsQuick => QuickText.Length > 0;

    /// <summary>The contract the screen was opened for.</summary>
    public bool IsHighlighted { get; }

    public IRelayCommand SelectCommand { get; }
}
