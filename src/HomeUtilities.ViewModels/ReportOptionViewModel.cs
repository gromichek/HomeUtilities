using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HomeUtilities.ViewModels;

/// <summary>A button of a button row (report, preset, grouping…) or a chip (property, category) with a selected state.</summary>
public sealed partial class ReportOptionViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private string _text;

    /// <summary>Creates the option; <paramref name="choose"/> is called when it is tapped.</summary>
    public ReportOptionViewModel(string text, bool isSelected, Action<ReportOptionViewModel> choose)
    {
        _text = text;
        _isSelected = isSelected;
        ChooseCommand = new RelayCommand(() => choose(this));
    }

    /// <summary>Tapped.</summary>
    public IRelayCommand ChooseCommand { get; }

    /// <summary>One-line explanation shown under the text in choice dialogs; empty = none.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>True if the option has an explanation line.</summary>
    public bool HasDescription => Description.Length > 0;

    /// <summary>What the option stands for (a report kind, a property id…).</summary>
    public object? Value { get; init; }
}
