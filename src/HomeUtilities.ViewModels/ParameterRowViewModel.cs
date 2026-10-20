using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.ViewModels;

/// <summary>One value of a time-versioned property parameter in the editor.</summary>
public sealed partial class ParameterRowViewModel : ObservableObject
{
    [ObservableProperty]
    private PropertyParameterKind _kind;

    [ObservableProperty]
    private string? _name;

    [ObservableProperty]
    private DateOnly _validFrom;

    [ObservableProperty]
    private decimal _value;

    [ObservableProperty]
    private string? _note;

    [ObservableProperty]
    private string _displayText = string.Empty;

    [ObservableProperty]
    private string _deleteText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorText = string.Empty;

    public Guid Id { get; init; } = Guid.CreateVersion7();

    public DateTimeOffset CreatedAt { get; init; }

    public bool HasError => ErrorText.Length > 0;

    public IRelayCommand EditCommand { get; set; } = new RelayCommand(() => { });

    public IRelayCommand RemoveCommand { get; set; } = new RelayCommand(() => { });

    public bool HasNote => !string.IsNullOrEmpty(Note);

    partial void OnNoteChanged(string? value) => OnPropertyChanged(nameof(HasNote));
}
