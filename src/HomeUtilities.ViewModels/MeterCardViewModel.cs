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
        Action<Guid> onOpenHistory,
        MeterCardPhoto photoActions,
        IReadOnlyList<SubmitLinkViewModel> submitLinks)
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
        SubmitLinks = submitLinks;
        _photo = photoActions.Current;
        PhotoCommand = new AsyncRelayCommand(photoActions.Pick);
        RemovePhotoCommand = new RelayCommand(photoActions.Remove);
        PhotoTooltip = localizer["PhotoAddTooltip"];
        RemovePhotoTooltip = localizer["PhotoRemove"];
    }

    /// <summary>The photo that will be attached to the reading saved for this meter; null when none was chosen.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPhoto), nameof(PhotoOpen), nameof(PhotoName))]
    private PickedFile? _photo;

    public bool HasPhoto => Photo is not null;

    /// <summary>Opens the chosen photo for the thumbnail; null when there is none.</summary>
    public Func<Task<Stream>>? PhotoOpen => Photo?.OpenAsync;

    public string PhotoName => Photo?.FileName ?? string.Empty;

    public string PhotoTooltip { get; }

    public string RemovePhotoTooltip { get; }

    /// <summary>Takes or picks the photo (camera icon).</summary>
    public IAsyncRelayCommand PhotoCommand { get; }

    /// <summary>Drops the chosen photo (× on the thumbnail).</summary>
    public IRelayCommand RemovePhotoCommand { get; }

    /// <summary>One button per distinct page the meter's contracts accept readings on.</summary>
    public IReadOnlyList<SubmitLinkViewModel> SubmitLinks { get; }

    public bool HasSubmitLinks => SubmitLinks.Count > 0;

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

/// <summary>What a meter card needs to handle its photo: the photo chosen so far and the two actions.</summary>
/// <param name="Current">The photo chosen earlier (kept while the cards are rebuilt).</param>
/// <param name="Pick">Asks where the photo comes from and stores the choice.</param>
/// <param name="Remove">Forgets the chosen photo.</param>
public sealed record MeterCardPhoto(PickedFile? Current, Func<Task> Pick, Action Remove);

/// <summary>The meter cards of one resource.</summary>
public sealed class ReadingGroupViewModel(string header, IReadOnlyList<MeterCardViewModel> cards)
{
    public string Header { get; } = header;

    public IReadOnlyList<MeterCardViewModel> Cards { get; } = cards;
}
