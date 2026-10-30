using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core.Domain;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>One register of the meter being edited.</summary>
public sealed class RegisterRowViewModel
{
    public RegisterRowViewModel(MeterRegister register, ILocalizer localizer)
    {
        Id = register.Id;
        CreatedAt = register.CreatedAt;
        Kind = register.Kind;
        Direction = register.Direction;
        Name = register.Name;
        Original = register;
        HasReadings = register.Readings.Any(r => !r.IsDeleted);
        DisplayText = $"{EnumText.Get(localizer, register.Kind)} · {EnumText.Get(localizer, register.Direction)}";
        DeleteText = localizer["Delete"];
    }

    public Guid Id { get; }

    public DateTimeOffset CreatedAt { get; }

    public RegisterKind Kind { get; }

    public EnergyDirection Direction { get; }

    public string? Name { get; }

    /// <summary>The stored register with its readings; null-equivalent (empty readings) for a register added in this session.</summary>
    public MeterRegister Original { get; }

    public bool HasReadings { get; }

    /// <summary>"Day · Consumption".</summary>
    public string DisplayText { get; }

    public string DeleteText { get; }

    public IRelayCommand? RemoveCommand { get; set; }
}

/// <summary>A contract of the property with the switch "this meter is used by the contract".</summary>
public sealed partial class MeterContractSwitchViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    private bool _isOn;

    public MeterContractSwitchViewModel(MeterContractLink link, string subtitle, bool isOn, IReadOnlyList<ContractMeter>? pairLinks = null)
    {
        Link = link;
        Title = link.Contract.Title;
        Subtitle = subtitle;
        _isOn = isOn;
        PairLinks = pairLinks ?? link.OpenLinks;
    }

    public MeterContractLink Link { get; }

    /// <summary>Every stored link of the meter and the contract, ended ones included (they decide where a new link may start).</summary>
    public IReadOnlyList<ContractMeter> PairLinks { get; }

    public Guid ContractId => Link.Contract.Id;

    public string Title { get; }

    public string Subtitle { get; }

    /// <summary>Called after the user flipped the switch.</summary>
    public Action? Toggled { get; set; }

    /// <summary>Builds the "from … to …" text of the link; set by the editor.</summary>
    public Func<ContractMeter, string>? LinkTextBuilder { get; set; }

    /// <summary>Opens the dialog with the dates of the link; set by the editor.</summary>
    public IRelayCommand? EditLinkCommand { get; set; }

    /// <summary>The link whose dates the row shows and edits: the open link that started last.</summary>
    public ContractMeter? CurrentLink => Link.OpenLinks.OrderByDescending(l => l.ValidFrom).FirstOrDefault();

    /// <summary>True when the meter is linked to the contract and the link can be opened (switched off rows show nothing).</summary>
    public bool ShowLinkDates => IsOn && CurrentLink is not null;

    /// <summary>"from 01.05.2004" or "from 01.05.2004 to 31.12.2030"; empty when the meter is not linked.</summary>
    public string LinkText => ShowLinkDates && LinkTextBuilder is { } builder ? builder(CurrentLink!) : string.Empty;

    public bool IsOn
    {
        get => _isOn;
        set
        {
            if (SetProperty(ref _isOn, value))
            {
                OnPropertyChanged(nameof(ShowLinkDates));
                OnPropertyChanged(nameof(LinkText));
                Toggled?.Invoke();
            }
        }
    }

    /// <summary>Raises the change notifications of the link texts (after the dates or the language changed).</summary>
    public void RefreshLinkText()
    {
        OnPropertyChanged(nameof(ShowLinkDates));
        OnPropertyChanged(nameof(LinkText));
    }
}

/// <summary>A line of the read-only history card: the meter that was replaced or the one that replaced this meter.</summary>
public sealed class MeterHistoryRowViewModel
{
    public MeterHistoryRowViewModel(Guid meterId, string text, Action<Guid> onSelect)
    {
        MeterId = meterId;
        Text = text;
        SelectCommand = new RelayCommand(() => onSelect(MeterId));
    }

    public Guid MeterId { get; }

    public string Text { get; }

    public IRelayCommand SelectCommand { get; }
}
