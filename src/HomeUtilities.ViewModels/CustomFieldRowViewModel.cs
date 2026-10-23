using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core.Billing;

namespace HomeUtilities.ViewModels;

/// <summary>One custom field definition of a contract in the editor.</summary>
public sealed partial class CustomFieldRowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private CustomFieldKind _kind;

    [ObservableProperty]
    private CustomFieldTarget _target;

    [ObservableProperty]
    private string _displayText = string.Empty;

    [ObservableProperty]
    private string _detailText = string.Empty;

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
}
