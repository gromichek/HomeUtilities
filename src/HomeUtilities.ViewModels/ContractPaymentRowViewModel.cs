using CommunityToolkit.Mvvm.Input;

namespace HomeUtilities.ViewModels;

/// <summary>One payment in the "Balance" card of the contract editor: date, amount and prepayment; tap to open the payment.</summary>
public sealed class ContractPaymentRowViewModel
{
    public ContractPaymentRowViewModel(Guid id, string text, string prepaymentText, Action open)
    {
        Id = id;
        Text = text;
        PrepaymentText = prepaymentText;
        OpenCommand = new RelayCommand(open);
    }

    public Guid Id { get; }

    /// <summary>"03.11.2026 · 46,02 € · Bank transfer".</summary>
    public string Text { get; }

    /// <summary>"Prepayment 12,30 €"; empty when everything is allocated.</summary>
    public string PrepaymentText { get; }

    public bool HasPrepayment => PrepaymentText.Length > 0;

    public IRelayCommand OpenCommand { get; }
}
