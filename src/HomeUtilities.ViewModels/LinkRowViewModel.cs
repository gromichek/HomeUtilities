using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.ViewModels;

/// <summary>One web link of a supplier in the editor.</summary>
public sealed partial class LinkRowViewModel : ObservableObject
{
    [ObservableProperty]
    private LinkKind _kind;

    [ObservableProperty]
    private string? _title;

    [ObservableProperty]
    private string _url = string.Empty;

    [ObservableProperty]
    private string _displayText = string.Empty;

    [ObservableProperty]
    private string _deleteText = string.Empty;

    [ObservableProperty]
    private string _openText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorText = string.Empty;

    public Guid Id { get; init; } = Guid.CreateVersion7();

    public DateTimeOffset CreatedAt { get; init; }

    public bool HasError => ErrorText.Length > 0;

    public IRelayCommand EditCommand { get; set; } = new RelayCommand(() => { });

    public IRelayCommand RemoveCommand { get; set; } = new RelayCommand(() => { });

    public IRelayCommand OpenCommand { get; set; } = new RelayCommand(() => { });
}
