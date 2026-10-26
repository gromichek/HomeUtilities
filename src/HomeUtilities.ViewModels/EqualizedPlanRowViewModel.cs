using CommunityToolkit.Mvvm.Input;

namespace HomeUtilities.ViewModels;

/// <summary>One version of the equalized plan in the contract editor: "from 01.06.2026 — 40,00 €/month".</summary>
public sealed class EqualizedPlanRowViewModel
{
    public EqualizedPlanRowViewModel(Guid id, string displayText, string noteText, string deleteText, Action edit, Action remove)
    {
        Id = id;
        DisplayText = displayText;
        NoteText = noteText;
        DeleteText = deleteText;
        EditCommand = new RelayCommand(edit);
        RemoveCommand = new RelayCommand(remove);
    }

    public Guid Id { get; }

    public string DisplayText { get; }

    public string NoteText { get; }

    public bool HasNote => NoteText.Length > 0;

    /// <summary>Tooltip / accessibility text of the trash button.</summary>
    public string DeleteText { get; }

    public IRelayCommand EditCommand { get; }

    public IRelayCommand RemoveCommand { get; }
}
