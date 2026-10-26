using CommunityToolkit.Mvvm.Input;

namespace HomeUtilities.ViewModels;

/// <summary>One payment allocated to the bill, in the bill editor's payment card.</summary>
public sealed class BillPaymentRowViewModel
{
    public BillPaymentRowViewModel(Guid paymentId, string text, string detail, string removeText, Action open, Action remove)
    {
        PaymentId = paymentId;
        Text = text;
        Detail = detail;
        RemoveText = removeText;
        OpenCommand = new RelayCommand(open);
        RemoveCommand = new RelayCommand(remove);
    }

    public Guid PaymentId { get; }

    /// <summary>"03.11.2026 · 46,02 € · Bank transfer · Paid button".</summary>
    public string Text { get; }

    /// <summary>"10,00 € of 50,00 € goes to this bill" for a payment that covers more; else empty.</summary>
    public string Detail { get; }

    public bool HasDetail => Detail.Length > 0;

    public string RemoveText { get; }

    /// <summary>Tap on the row: open the payment in the payment editor.</summary>
    public IRelayCommand OpenCommand { get; }

    public IRelayCommand RemoveCommand { get; }
}
