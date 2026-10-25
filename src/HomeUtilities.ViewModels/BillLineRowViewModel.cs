using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core.Billing;

namespace HomeUtilities.ViewModels;

/// <summary>One detail line of a bill in the bill editor; tap to edit, trash icon to delete.</summary>
public sealed class BillLineRowViewModel
{
    public BillLineRowViewModel(BillLine line, string totalText, string detailText, string deleteText, Action<BillLine> edit, Action<BillLine> remove)
    {
        Line = line;
        Description = line.Description;
        TotalText = totalText;
        DetailText = detailText;
        DeleteText = deleteText;
        EditCommand = new RelayCommand(() => edit(line));
        RemoveCommand = new RelayCommand(() => remove(line));
    }

    public BillLine Line { get; }

    public string Description { get; }

    /// <summary>"12,10 €".</summary>
    public string TotalText { get; }

    /// <summary>"2 m³ × 1,41" or empty.</summary>
    public string DetailText { get; }

    public bool HasDetail => DetailText.Length > 0;

    public string DeleteText { get; }

    public IRelayCommand EditCommand { get; }

    public IRelayCommand RemoveCommand { get; }
}
