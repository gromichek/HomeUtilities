using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HomeUtilities.ViewModels;

/// <summary>
/// A row of the Settings list. Language, Theme, Reminders, Backup, Error log and About open a settings page
/// (<see cref="Routes.SettingsSection"/>); Categories, VAT rates, Bill templates and Imports open the screen that already exists.
/// </summary>
public enum SettingsSection
{
    Language,
    Theme,
    Reminders,
    Backup,
    Categories,
    VatRates,
    BillTemplates,
    Imports,
    ErrorLog,
    About,
}

/// <summary>One row of the Settings list: icon (by section), title and the one-line current value.</summary>
public sealed partial class SettingsSectionRowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private string _value;

    public SettingsSectionRowViewModel(SettingsSection section, string title, string value, Func<SettingsSection, Task> open)
    {
        Section = section;
        _title = title;
        _value = value;
        OpenCommand = new AsyncRelayCommand(() => open(section));
    }

    public SettingsSection Section { get; }

    /// <summary>Name of the section, used by the page to pick the icon.</summary>
    public string IconKey => "IconSettings" + Section;

    public IAsyncRelayCommand OpenCommand { get; }
}

/// <summary>
/// The page of one Settings section (pushed from the list). It shows the settings of <see cref="Settings"/> that belong to
/// <see cref="Section"/>; Back returns to the list.
/// </summary>
public sealed partial class SettingsSectionViewModel : ViewModelBase
{
    private readonly ILocalizer _localizer;
    private readonly INavigationService _navigation;

    [ObservableProperty]
    private string _helpText = string.Empty;

    public SettingsSectionViewModel(SettingsViewModel settings, SettingsSection section, ILocalizer localizer, INavigationService navigation)
    {
        Settings = settings;
        Section = section;
        _localizer = localizer;
        _navigation = navigation;
        RefreshTexts();
    }

    /// <summary>The Settings view model that owns the state; the section views bind to it.</summary>
    public SettingsViewModel Settings { get; }

    public SettingsSection Section { get; }

    public bool IsLanguage => Section == SettingsSection.Language;

    public bool IsTheme => Section == SettingsSection.Theme;

    public bool IsReminders => Section == SettingsSection.Reminders;

    public bool IsBackup => Section == SettingsSection.Backup;

    public bool IsErrorLog => Section == SettingsSection.ErrorLog;

    public bool IsAbout => Section == SettingsSection.About;

    /// <summary>The guide section the "?" button opens.</summary>
    public HelpTopic GuideTopic => Section switch
    {
        SettingsSection.Language or SettingsSection.Theme => HelpTopic.LanguageAndTheme,
        SettingsSection.Reminders => HelpTopic.Reminders,
        SettingsSection.Backup => HelpTopic.Backup,
        SettingsSection.ErrorLog => HelpTopic.ErrorLog,
        SettingsSection.About => HelpTopic.About,
        _ => HelpTopic.Settings,
    };

    [RelayCommand]
    private Task OpenHelpAsync() => RunSafeAsync(() => HelpNavigation.OpenAsync(_navigation, GuideTopic));

    protected override void RefreshTexts()
    {
        Title = Settings.TitleOf(Section);
        HelpText = _localizer["SectionHelp"];
    }
}
