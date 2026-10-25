using CommunityToolkit.Mvvm.Input;

namespace HomeUtilities.ViewModels;

/// <summary>One bill a payment settles, in the "Bills paid" card of the payment editor.</summary>
public sealed class PaymentAllocationRowViewModel
{
    public PaymentAllocationRowViewModel(Guid billId, string text, string note, string removeText, Func<Task> edit, Func<Task> remove)
    {
        BillId = billId;
        Text = text;
        Note = note;
        RemoveText = removeText;
        EditCommand = new AsyncRelayCommand(edit);
        RemoveCommand = new AsyncRelayCommand(remove);
    }

    public Guid BillId { get; }

    /// <summary>"Water · 10.2026 · bill 46,02 € — 46,02 €".</summary>
    public string Text { get; }

    /// <summary>The allocation's note; empty when there is none.</summary>
    public string Note { get; }

    public bool HasNote => Note.Length > 0;

    public string RemoveText { get; }

    /// <summary>Tap on the row: edit the allocated amount.</summary>
    public IAsyncRelayCommand EditCommand { get; }

    public IAsyncRelayCommand RemoveCommand { get; }
}
