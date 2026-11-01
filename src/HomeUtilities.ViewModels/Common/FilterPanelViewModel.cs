using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace HomeUtilities.ViewModels.Common;

/// <summary>
/// The collapsible filter block at the head of a list (Bills, Payments): one summary line when collapsed, the fields in
/// 1 / 2 / 3 columns when expanded. Expanded by default on medium/expanded layouts and collapsed on compact ones, until the
/// user toggles it; that choice is remembered for the session through <paramref name="getRemembered"/> / <paramref name="remember"/>.
/// </summary>
public sealed partial class FilterPanelViewModel : ObservableObject, IRecipient<LayoutModeChangedMessage>
{
    private readonly ILayoutModeService _layout;
    private readonly ILocalizer _localizer;
    private readonly Func<bool?> _getRemembered;
    private readonly Action<bool?> _remember;
    private bool _following;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSummary), nameof(ToggleText))]
    private bool _isExpanded;

    [ObservableProperty]
    private string _summaryText = string.Empty;

    public FilterPanelViewModel(ILayoutModeService layout, ILocalizer localizer, Func<bool?> getRemembered, Action<bool?> remember)
    {
        _layout = layout;
        _localizer = localizer;
        _getRemembered = getRemembered;
        _remember = remember;
        _isExpanded = getRemembered() ?? layout.Current != LayoutMode.Compact;
        WeakReferenceMessenger.Default.Register(this);
    }

    /// <summary>Columns of the filter field grid: 3 on expanded, 2 on medium, 1 on compact layouts.</summary>
    public int Columns => _layout.Current switch
    {
        LayoutMode.Expanded => 3,
        LayoutMode.Medium => 2,
        _ => 1,
    };

    public bool ShowSummary => !IsExpanded;

    public string Title => _localizer["ReportFiltersTitle"];

    public string ToggleText => _localizer[IsExpanded ? "ReportFiltersCollapse" : "ReportFiltersExpand"];

    /// <summary>Call after a language change.</summary>
    public void RefreshTexts()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(ToggleText));
    }

    [RelayCommand]
    private void Toggle() => IsExpanded = !IsExpanded;

    partial void OnIsExpandedChanged(bool value)
    {
        if (!_following)
        {
            _remember(value);
        }
    }

    void IRecipient<LayoutModeChangedMessage>.Receive(LayoutModeChangedMessage message)
    {
        if (_getRemembered() is null)
        {
            // Not chosen by the user yet: follow the layout; the remembered value stays unset.
            _following = true;
            IsExpanded = _layout.Current != LayoutMode.Compact;
            _following = false;
        }

        OnPropertyChanged(nameof(Columns));
    }
}
