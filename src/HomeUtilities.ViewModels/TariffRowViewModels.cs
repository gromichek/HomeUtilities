using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Tariffs;

namespace HomeUtilities.ViewModels;

/// <summary>One price of a component in the "Prices" card: "from 01.09.2026 — 1,41 €/m³ excl. VAT".</summary>
public sealed partial class TariffRateRowViewModel : ObservableObject
{
    [ObservableProperty]
    private TariffRate _rate;

    [ObservableProperty]
    private string _displayText = string.Empty;

    [ObservableProperty]
    private string _steppedText = string.Empty;

    [ObservableProperty]
    private string _deleteText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorText = string.Empty;

    public TariffRateRowViewModel(TariffRate rate)
    {
        _rate = rate;
    }

    public bool IsStepped => Rate.Tiers.Count > 0;

    public bool HasError => ErrorText.Length > 0;

    public IRelayCommand EditCommand { get; set; } = new RelayCommand(() => { });

    public IRelayCommand RemoveCommand { get; set; } = new RelayCommand(() => { });
}

/// <summary>A meter of the contract with the switch "this component counts it".</summary>
public sealed partial class TariffMeterSwitchViewModel : ObservableObject
{
    private bool _isOn;

    public TariffMeterSwitchViewModel(Guid meterId, string title, bool isOn)
    {
        MeterId = meterId;
        Title = title;
        _isOn = isOn;
    }

    public Guid MeterId { get; }

    public string Title { get; }

    /// <summary>Called after the user flipped the switch.</summary>
    public Action? Toggled { get; set; }

    public bool IsOn
    {
        get => _isOn;
        set
        {
            if (SetProperty(ref _isOn, value))
            {
                Toggled?.Invoke();
            }
        }
    }
}

/// <summary>A register kind with the switch "this component counts it".</summary>
public sealed partial class TariffRegisterSwitchViewModel : ObservableObject
{
    private bool _isOn;

    public TariffRegisterSwitchViewModel(RegisterKind kind, string title, bool isOn)
    {
        Kind = kind;
        Title = title;
        _isOn = isOn;
    }

    public RegisterKind Kind { get; }

    public string Title { get; }

    /// <summary>Called after the user flipped the switch.</summary>
    public Action? Toggled { get; set; }

    public bool IsOn
    {
        get => _isOn;
        set
        {
            if (SetProperty(ref _isOn, value))
            {
                Toggled?.Invoke();
            }
        }
    }
}

/// <summary>An entry of the VAT picker: a catalog code with its current percent, or the custom percent.</summary>
/// <param name="Code">Catalog code; empty for the custom entry.</param>
/// <param name="DisplayName">"Standard rate 21 %" or "Custom %".</param>
/// <param name="IsCustom">True for the entry that lets the user type a percent.</param>
public sealed record VatOption(string Code, string DisplayName, bool IsCustom);

/// <summary>One component in the "Tariff" card of the contract editor.</summary>
public sealed partial class TariffRowViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _canMoveUp;

    [ObservableProperty]
    private bool _canMoveDown;

    public TariffRowViewModel(TariffComponent component, string basisText, string summaryText)
    {
        Id = component.Id;
        Name = component.Name;
        BasisText = basisText;
        SummaryText = summaryText;
    }

    public Guid Id { get; }

    public string Name { get; }

    /// <summary>The basis in words: "Per unit", "Per area"...</summary>
    public string BasisText { get; }

    /// <summary>"1,41 €/m³ excl. VAT · VAT 21 %".</summary>
    public string SummaryText { get; }

    public string MoveUpText { get; set; } = string.Empty;

    public string MoveDownText { get; set; } = string.Empty;

    public IRelayCommand EditCommand { get; set; } = new RelayCommand(() => { });

    public IRelayCommand MoveUpCommand { get; set; } = new RelayCommand(() => { });

    public IRelayCommand MoveDownCommand { get; set; } = new RelayCommand(() => { });
}

/// <summary>A quick-start preset offered while the tariff card is empty.</summary>
public sealed class TariffPresetRowViewModel
{
    public TariffPresetRowViewModel(TariffPreset preset, string name, IRelayCommand command)
    {
        Preset = preset;
        Name = name;
        ApplyCommand = command;
    }

    public TariffPreset Preset { get; }

    public string Name { get; }

    public IRelayCommand ApplyCommand { get; }
}

/// <summary>One percentage of a VAT catalog entry: "from 01.01.2012 — 21 %".</summary>
public sealed partial class VatVersionRowViewModel : ObservableObject
{
    [ObservableProperty]
    private VatRateVersion _version;

    [ObservableProperty]
    private string _displayText = string.Empty;

    [ObservableProperty]
    private string _deleteText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorText = string.Empty;

    public VatVersionRowViewModel(VatRateVersion version)
    {
        _version = version;
    }

    public bool HasError => ErrorText.Length > 0;

    public IRelayCommand EditCommand { get; set; } = new RelayCommand(() => { });

    public IRelayCommand RemoveCommand { get; set; } = new RelayCommand(() => { });
}
