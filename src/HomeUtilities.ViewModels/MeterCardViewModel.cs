using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core.Consumption;
using HomeUtilities.Core.Domain;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>One installed meter on the readings screen with an entry row per register.</summary>
public sealed partial class MeterCardViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    public MeterCardViewModel(
        Meter meter,
        IReadOnlyList<Meter> propertyMeters,
        (int From, int To)? window,
        Func<DateOnly> readingDate,
        ILocalizer localizer,
        Action changed,
        Action<Guid> onOpenHistory)
    {
        MeterId = meter.Id;
        Resource = meter.Resource;
        Title = MeterNames.Title(localizer, meter);
        SerialText = string.IsNullOrWhiteSpace(meter.SerialNumber) || string.IsNullOrWhiteSpace(meter.Location)
            ? string.Empty
            : meter.SerialNumber;

        var chain = MeterChain.Build(propertyMeters, meter.Id);
        Rows =
        [
            .. meter.Registers
                .Where(r => !r.IsDeleted)
                .OrderBy(r => r.Kind)
                .ThenBy(r => r.Direction)
                .Select(r => new RegisterEntryViewModel(meter, r, chain, window, readingDate, localizer, changed)),
        ];
        OpenHistoryCommand = new RelayCommand(() => onOpenHistory(MeterId));
    }

    public Guid MeterId { get; }

    public MeterResource Resource { get; }

    /// <summary>Location, or "Meter" plus the serial number.</summary>
    public string Title { get; }

    /// <summary>Serial number on its own line; empty when it already is the title.</summary>
    public string SerialText { get; }

    public bool HasSerial => SerialText.Length > 0;

    public IReadOnlyList<RegisterEntryViewModel> Rows { get; }

    /// <summary>Opens the reading history of the meter (tap on the card header).</summary>
    public IRelayCommand OpenHistoryCommand { get; }
}

/// <summary>The meter cards of one resource.</summary>
public sealed class ReadingGroupViewModel(string header, IReadOnlyList<MeterCardViewModel> cards)
{
    public string Header { get; } = header;

    public IReadOnlyList<MeterCardViewModel> Cards { get; } = cards;
}
