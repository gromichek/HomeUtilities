using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HomeUtilities.ViewModels;

/// <summary>One colour of the palette in the category editor.</summary>
public sealed partial class ColorSwatchViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    public ColorSwatchViewModel(string color, Action<string> onSelect)
    {
        Color = color;
        SelectCommand = new RelayCommand(() => onSelect(Color));
    }

    /// <summary>Colour as <c>#RRGGBB</c>.</summary>
    public string Color { get; }

    public IRelayCommand SelectCommand { get; }
}
