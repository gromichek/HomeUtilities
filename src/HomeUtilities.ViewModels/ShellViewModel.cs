using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

namespace HomeUtilities.ViewModels;

/// <summary>Titles of the shell sections and the flyout mode derived from the layout mode.</summary>
public sealed partial class ShellViewModel : ViewModelBase, IRecipient<LayoutModeChangedMessage>
{
    private readonly ILocalizer _localizer;
    private readonly ILayoutModeService _layoutModeService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFlyoutLocked))]
    private LayoutMode _layoutMode;

    [ObservableProperty]
    private string _overviewTitle = string.Empty;

    [ObservableProperty]
    private string _propertiesTitle = string.Empty;

    [ObservableProperty]
    private string _readingsTitle = string.Empty;

    [ObservableProperty]
    private string _billsTitle = string.Empty;

    [ObservableProperty]
    private string _paymentsTitle = string.Empty;

    [ObservableProperty]
    private string _reportsTitle = string.Empty;

    [ObservableProperty]
    private string _settingsTitle = string.Empty;

    public ShellViewModel(ILocalizer localizer, ILayoutModeService layoutModeService)
    {
        _localizer = localizer;
        _layoutModeService = layoutModeService;
        _layoutMode = layoutModeService.Current;
        RefreshTexts();
    }

    public bool IsFlyoutLocked => LayoutMode == LayoutMode.Expanded;

    void IRecipient<LayoutModeChangedMessage>.Receive(LayoutModeChangedMessage message) =>
        LayoutMode = _layoutModeService.Current;

    protected override void RefreshTexts()
    {
        Title = _localizer["AppTitle"];
        OverviewTitle = _localizer["SectionOverview"];
        PropertiesTitle = _localizer["SectionProperties"];
        ReadingsTitle = _localizer["SectionReadings"];
        BillsTitle = _localizer["SectionBills"];
        PaymentsTitle = _localizer["SectionPayments"];
        ReportsTitle = _localizer["SectionReports"];
        SettingsTitle = _localizer["SectionSettings"];
    }
}
