using CommunityToolkit.Mvvm.ComponentModel;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.ViewModels;

/// <summary>One register of the replace-meter wizard: the final reading of the old and the initial reading of the new meter.</summary>
public sealed partial class WizardRegisterRowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _finalText = string.Empty;

    [ObservableProperty]
    private string _initialText = "0";

    [ObservableProperty]
    private string _finalError = string.Empty;

    [ObservableProperty]
    private string _finalWarning = string.Empty;

    [ObservableProperty]
    private string _initialError = string.Empty;

    public WizardRegisterRowViewModel(MeterRegister register, string displayText, string lastReadingHint, decimal? lastValue)
    {
        Kind = register.Kind;
        DisplayText = displayText;
        LastReadingHint = lastReadingHint;
        LastValue = lastValue;
    }

    public RegisterKind Kind { get; }

    /// <summary>"Day · Consumption".</summary>
    public string DisplayText { get; }

    /// <summary>"Last reading: 190 on 26.09.2026", empty if the register has no reading.</summary>
    public string LastReadingHint { get; }

    public bool HasHint => LastReadingHint.Length > 0;

    public decimal? LastValue { get; }

    public bool HasFinalError => FinalError.Length > 0;

    public bool HasFinalWarning => FinalWarning.Length > 0;

    public bool HasInitialError => InitialError.Length > 0;

    partial void OnFinalErrorChanged(string value) => OnPropertyChanged(nameof(HasFinalError));

    partial void OnFinalWarningChanged(string value) => OnPropertyChanged(nameof(HasFinalWarning));

    partial void OnInitialErrorChanged(string value) => OnPropertyChanged(nameof(HasInitialError));
}

/// <summary>A contract of the property with the switch "the new meter is used by it".</summary>
public sealed partial class WizardContractRowViewModel(MeterContractLink link, string subtitle) : ObservableObject
{
    [ObservableProperty]
    private bool _isOn = link.IsLinked;

    public MeterContractLink Link { get; } = link;

    public string Title { get; } = link.Contract.Title;

    public string Subtitle { get; } = subtitle;
}
