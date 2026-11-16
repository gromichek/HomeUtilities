using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HomeUtilities.ViewModels.Common;

/// <summary>
/// State of one collapsible card (T-057): title, one-line summary shown in the header, and whether the card is open.
/// The open/closed state is remembered per card on this device through <see cref="ISettingsStore"/> (one key per card, a
/// view preference that is never synced). The owning view model sets <see cref="Title"/> and <see cref="Summary"/>.
/// </summary>
public sealed partial class CollapsibleCardViewModel : ObservableObject
{
    private const string KeyPrefix = "Card.Expanded.";

    private readonly ISettingsStore _store;
    private readonly ILocalizer _localizer;
    private readonly string _storeKey;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSummary))]
    private string _summary = string.Empty;

    [ObservableProperty]
    private bool _summaryIsError;

    [ObservableProperty]
    private string _toggleDescription = string.Empty;

    /// <param name="store">Where the open/closed state is kept.</param>
    /// <param name="localizer">Texts for the screen-reader description.</param>
    /// <param name="key">Unique, stable name of the card (part of the settings key).</param>
    /// <param name="defaultExpanded">State until the owner has toggled the card at least once.</param>
    public CollapsibleCardViewModel(ISettingsStore store, ILocalizer localizer, string key, bool defaultExpanded)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _store = store;
        _localizer = localizer;
        _storeKey = KeyPrefix + key;
        _isExpanded = _store.GetString(_storeKey) switch
        {
            "1" => true,
            "0" => false,
            _ => defaultExpanded,
        };
    }

    /// <summary>True when the header shows a summary line.</summary>
    public bool HasSummary => Summary.Length > 0;

    /// <summary>Sets the title and the header summary in one step; <paramref name="isError"/> shows the summary in the error style.</summary>
    public void SetTexts(string title, string summary, bool isError = false)
    {
        Title = title;
        Summary = summary;
        SummaryIsError = isError;
        ToggleDescription = _localizer.Get("CollapsibleCardToggle", title);
    }

    [RelayCommand]
    private void Toggle() => IsExpanded = !IsExpanded;

    partial void OnIsExpandedChanged(bool value) => _store.SetString(_storeKey, value ? "1" : "0");
}
