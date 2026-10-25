using CommunityToolkit.Mvvm.Input;

namespace HomeUtilities.ViewModels;

/// <summary>One document of a bill in the bill editor: opens with the system viewer, trash icon removes it.</summary>
public sealed class BillAttachmentRowViewModel
{
    public BillAttachmentRowViewModel(string name, bool isPending, string openText, string removeText, Action? open, Action remove)
    {
        Name = name;
        IsPending = isPending;
        OpenText = openText;
        RemoveText = removeText;
        OpenCommand = new RelayCommand(() => open?.Invoke(), () => open is not null);
        RemoveCommand = new RelayCommand(remove);
    }

    public string Name { get; }

    /// <summary>True while the file is only chosen and will be stored when the bill is saved.</summary>
    public bool IsPending { get; }

    public bool CanOpen => !IsPending;

    public string OpenText { get; }

    public string RemoveText { get; }

    public IRelayCommand OpenCommand { get; }

    public IRelayCommand RemoveCommand { get; }
}
