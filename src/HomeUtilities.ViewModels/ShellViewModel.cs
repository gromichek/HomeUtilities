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
    [NotifyPropertyChangedFor(nameof(FlyoutWidth))]
    private LayoutMode _layoutMode;

    [ObservableProperty]
    private string _overviewTitle = string.Empty;

    [ObservableProperty]
    private string _propertiesTitle = string.Empty;

    [ObservableProperty]
    private string _suppliersTitle = string.Empty;

    [ObservableProperty]
    private string _readingsTitle = string.Empty;

    [ObservableProperty]
    private string _chargesTitle = string.Empty;

    [ObservableProperty]
    private string _billsTitle = string.Empty;

    [ObservableProperty]
    private string _paymentsTitle = string.Empty;

    [ObservableProperty]
    private string _reportsTitle = string.Empty;

    [ObservableProperty]
    private string _helpTitle = string.Empty;

    [ObservableProperty]
    private string _settingsTitle = string.Empty;

    public ShellViewModel(ILocalizer localizer, ILayoutModeService layoutModeService)
    {
        _localizer = localizer;
        _layoutModeService = layoutModeService;
        _layoutMode = layoutModeService.Current;
        RefreshTexts();
    }

    /// <summary>Width of the narrow menu on medium and expanded layouts (fits the longest EN / RU label plus icon and padding).</summary>
    public const double NarrowFlyoutWidth = 200;

    /// <summary>Menu width: -1 = the platform default (phones), a narrower fixed width on wider layouts.</summary>
    public double FlyoutWidth => LayoutMode == LayoutMode.Compact ? -1 : NarrowFlyoutWidth;

    public bool IsFlyoutLocked => LayoutMode == LayoutMode.Expanded;

    void IRecipient<LayoutModeChangedMessage>.Receive(LayoutModeChangedMessage message) =>
        LayoutMode = _layoutModeService.Current;

    protected override void RefreshTexts()
    {
        Title = _localizer["AppTitle"];
        OverviewTitle = _localizer["SectionOverview"];
        PropertiesTitle = _localizer["SectionProperties"];
        SuppliersTitle = _localizer["SectionSuppliers"];
        ReadingsTitle = _localizer["SectionReadings"];
        ChargesTitle = _localizer["SectionCharges"];
        BillsTitle = _localizer["SectionBills"];
        PaymentsTitle = _localizer["SectionPayments"];
        ReportsTitle = _localizer["SectionReports"];
        HelpTitle = _localizer["SectionHelp"];
        SettingsTitle = _localizer["SectionSettings"];
    }
}
