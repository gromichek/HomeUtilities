using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HomeUtilities.ViewModels;

/// <summary>One step of a stepped price in the rate dialog: "up to N" at a price; the last step is "and above".</summary>
public sealed partial class TariffTierRowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _upToText = string.Empty;

    [ObservableProperty]
    private string _priceText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotLast))]
    private bool _isLast;

    [ObservableProperty]
    private bool _canRemove;

    [ObservableProperty]
    private string _removeText = string.Empty;

    public bool IsNotLast => !IsLast;

    public IRelayCommand RemoveCommand { get; set; } = new RelayCommand(() => { });
}
