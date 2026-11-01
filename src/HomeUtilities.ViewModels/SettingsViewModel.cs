using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.Messaging;
using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Backup;

namespace HomeUtilities.ViewModels;

public sealed record LanguageOption(string Code, string DisplayName);

public sealed record ThemeOption(AppThemeChoice Theme, string DisplayName);

public sealed partial class SettingsViewModel : ViewModelBase, IRecipient<LayoutModeChangedMessage>
{
    private readonly ILocalizer _localizer;
    private readonly ILanguageService _languageService;
    private readonly IThemeService _themeService;
    private readonly ILayoutModeService _layoutModeService;
    private readonly INavigationService _navigation;
    private readonly IErrorLog _errorLog;
    private readonly IClipboardService _clipboard;
    private readonly ISnackbarService _snackbar;
    private readonly IBackupService _backup;
    private readonly IBackupFileService _backupFiles;
    private readonly IDialogService _dialogs;
    private readonly IAppLifecycle _app;
    private readonly ISettingsStore _settings;
    private readonly ISampleDataGenerator _sampleData;
    private readonly IClock _clock;

    private bool _refreshing;

    [ObservableProperty]
    private IReadOnlyList<LanguageOption> _languages = [];

    [ObservableProperty]
    private LanguageOption? _selectedLanguage;

    [ObservableProperty]
    private IReadOnlyList<ThemeOption> _themes = [];

    [ObservableProperty]
    private ThemeOption? _selectedTheme;

    [ObservableProperty]
    private string _languageLabel = string.Empty;

    [ObservableProperty]
    private string _themeLabel = string.Empty;

    [ObservableProperty]
    private string _versionText = string.Empty;

    [ObservableProperty]
    private string _databasePathText = string.Empty;

    [ObservableProperty]
    private string _layoutText = string.Empty;

    [ObservableProperty]
    private string _errorLogHeader = string.Empty;

    [ObservableProperty]
    private string _errorLogHint = string.Empty;

    [ObservableProperty]
    private string _errorLogCountText = string.Empty;

    [ObservableProperty]
    private string _aboutHeader = string.Empty;

    [ObservableProperty]
    private string _helpText = string.Empty;

    [ObservableProperty]
    private string _copyLogText = string.Empty;

    [ObservableProperty]
    private string _clearLogText = string.Empty;

    [ObservableProperty]
    private string _backupHeader = string.Empty;

    [ObservableProperty]
    private string _backupHint = string.Empty;

    [ObservableProperty]
    private string _saveBackupText = string.Empty;

    [ObservableProperty]
    private string _restoreBackupText = string.Empty;

    [ObservableProperty]
    private string _lastBackupText = string.Empty;

    [ObservableProperty]
    private string _testDataHeader = string.Empty;

    [ObservableProperty]
    private string _fillSampleDataText = string.Empty;

    [ObservableProperty]
    private string _deleteAllDataText = string.Empty;

    public SettingsViewModel(
        ILocalizer localizer,
        ILanguageService languageService,
        IThemeService themeService,
        ILayoutModeService layoutModeService,
        IAppInfo appInfo,
        IAppPaths appPaths,
        INavigationService navigation,
        IErrorLog errorLog,
        IClipboardService clipboard,
        ISnackbarService snackbar,
        IBackupService backup,
        IBackupFileService backupFiles,
        IDialogService dialogs,
        IAppLifecycle app,
        ISettingsStore settings,
        ISampleDataGenerator sampleData,
        IClock clock,
        ReminderSettingsViewModel reminders)
    {
        Reminders = reminders;
        _backup = backup;
        _backupFiles = backupFiles;
        _dialogs = dialogs;
        _app = app;
        _settings = settings;
        _sampleData = sampleData;
        _clock = clock;
        IsDebugBuild = appInfo.IsDebugBuild;
        _errorLog = errorLog;
        _clipboard = clipboard;
        _snackbar = snackbar;
        _localizer = localizer;
        _languageService = languageService;
        _themeService = themeService;
        _layoutModeService = layoutModeService;
        _navigation = navigation;
        Version = appInfo.Version;
        DatabasePath = appPaths.DatabasePath;
        Reminders.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(ReminderSettingsViewModel.IsEnabled) or nameof(ReminderSettingsViewModel.Time))
            {
                RefreshSectionValues();
            }
        };
        RefreshTexts();
    }

    /// <summary>The rows of the list: icon, title and the current value of each section.</summary>
    public ObservableCollection<SettingsSectionRowViewModel> Sections { get; } = [];

    /// <summary>The Reminders card.</summary>
    public ReminderSettingsViewModel Reminders { get; }

    public string Version { get; }

    public string DatabasePath { get; }

    /// <summary>Whether the test-data card is shown (debug builds only).</summary>
    public bool IsDebugBuild { get; }

    /// <summary>Settings key of the time the last backup was created on this device (ISO 8601).</summary>
    public const string LastBackupKey = "LastBackupAt";

    /// <summary>
    /// Opens a row: the settings pages of Language, Theme, Reminders, Backup, Error log and About are pushed with this view
    /// model as their owner; Categories, VAT rates, Bill templates and Imports open their existing screens.
    /// </summary>
    [RelayCommand]
    private Task OpenSectionAsync(SettingsSection section) => RunSafeAsync(() => OpenAsync(section));

    private Task OpenAsync(SettingsSection section) => section switch
    {
        SettingsSection.Categories => _navigation.GoToAsync(Routes.Categories),
        SettingsSection.VatRates => _navigation.GoToAsync(Routes.VatRates),
        SettingsSection.BillTemplates => _navigation.GoToAsync(Routes.BillTemplates),
        SettingsSection.Imports => _navigation.GoToAsync(Routes.Imports),
        _ => _navigation.GoToAsync(
            Routes.SettingsSection,
            new Dictionary<string, object> { [SectionParameter] = new SettingsSectionViewModel(this, section, _localizer, _navigation) }),
    };

    /// <summary>The "?" button of the list: the Settings chapter of the user guide.</summary>
    [RelayCommand]
    private Task OpenHelpAsync() => RunSafeAsync(() => HelpNavigation.OpenAsync(_navigation, HelpTopic.Settings));

    /// <summary>Navigation parameter carrying the <see cref="SettingsSectionViewModel"/> of a section page.</summary>
    public const string SectionParameter = "section";

    /// <summary>The page title of a section (also the title of its row in the list).</summary>
    public string TitleOf(SettingsSection section) => _localizer[section switch
    {
        SettingsSection.Language => "SettingsLanguage",
        SettingsSection.Theme => "SettingsTheme",
        SettingsSection.Reminders => "SettingsReminders",
        SettingsSection.Backup => "SettingsBackup",
        SettingsSection.Categories => "SectionCategories",
        SettingsSection.VatRates => "SectionVatRates",
        SettingsSection.BillTemplates => "SectionBillTemplates",
        SettingsSection.Imports => "SectionImports",
        SettingsSection.ErrorLog => "SettingsErrorLog",
        _ => "SettingsAbout",
    }];

    /// <summary>The one-line value shown under the title of a row.</summary>
    public string ValueOf(SettingsSection section) => section switch
    {
        SettingsSection.Language => SelectedLanguage?.DisplayName ?? string.Empty,
        SettingsSection.Theme => SelectedTheme?.DisplayName ?? string.Empty,
        SettingsSection.Reminders => Reminders.IsEnabled
            ? _localizer.Get("SettingsValueRemindersOn", Reminders.Time.ToString(@"hh\:mm", CultureInfo.InvariantCulture))
            : _localizer["SettingsValueRemindersOff"],
        SettingsSection.Backup => DateTimeOffset.TryParse(_settings.GetString(LastBackupKey), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var last)
            ? _localizer.Get("SettingsValueBackupLast", last.ToLocalTime())
            : _localizer["SettingsValueBackupNever"],
        SettingsSection.Categories => _localizer["SettingsValueCategories"],
        SettingsSection.VatRates => _localizer["SettingsValueVatRates"],
        SettingsSection.BillTemplates => _localizer["SettingsValueBillTemplates"],
        SettingsSection.Imports => _localizer["SettingsValueImports"],
        SettingsSection.ErrorLog => _localizer.Get("SettingsValueErrorLog", CountLogEntries()),
        _ => _localizer.Get("SettingsVersion", Version),
    };

    /// <summary>Number of entries in the error log: every entry starts with a <c>[timestamp]</c> line.</summary>
    private int CountLogEntries() => LogEntryStart().Count(_errorLog.ReadAll());

    [GeneratedRegex(@"^\[\d{4}-\d{2}-\d{2} ", RegexOptions.Multiline)]
    private static partial Regex LogEntryStart();

    /// <summary>Re-reads the values of all rows (language, theme, reminders, backup, log); cheap, called when the list appears.</summary>
    public void RefreshSectionValues()
    {
        foreach (var row in Sections)
        {
            row.Title = TitleOf(row.Section);
            row.Value = ValueOf(row.Section);
        }

        ErrorLogCountText = _localizer.Get("SettingsValueErrorLog", CountLogEntries());
    }

    public override Task OnAppearingAsync()
    {
        RefreshSectionValues();
        return Task.CompletedTask;
    }

    /// <summary>Puts the error log on the clipboard so the owner can send it; an empty log says so instead.</summary>
    [RelayCommand]
    private Task CopyLogAsync() => RunSafeAsync(async () =>
    {
        var log = _errorLog.ReadAll();
        if (log.Length == 0)
        {
            _snackbar.Notify(_localizer["SettingsErrorLogEmpty"]);
            return;
        }

        await _clipboard.SetTextAsync(log);
        _snackbar.Notify(_localizer["Copied"]);
    });

    /// <summary>Creates a backup zip and lets the owner put it somewhere outside the app.</summary>
    [RelayCommand]
    private Task SaveBackupAsync() => RunSafeAsync(async () =>
    {
        var result = await _backupFiles.SaveAsync(_backup.SuggestFileName(), stream => _backup.CreateAsync(stream));
        if (!result.Saved)
        {
            return;
        }

        _settings.SetString(LastBackupKey, _clock.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        RefreshLastBackupText();
        _snackbar.Notify(result.Location is { } location ? _localizer.Get("BackupSavedTo", location) : _localizer["BackupSaved"]);
    });

    /// <summary>Picks a backup, checks it, asks for confirmation, stages it and asks to close the app to finish.</summary>
    [RelayCommand]
    private Task RestoreBackupAsync() => RunSafeAsync(async () =>
    {
        var stream = await _backupFiles.PickAsync();
        if (stream is null)
        {
            return;
        }

        await using (stream)
        {
            var validation = await _backup.ValidateAsync(stream);
            if (validation.Manifest is not { } manifest)
            {
                await ShowBackupErrorAsync(validation.Error);
                return;
            }

            var title = _localizer["BackupRestoreTitle"];
            var confirmed = await _dialogs.ConfirmAsync(
                title,
                _localizer.Get("BackupRestoreConfirm", manifest.CreatedAt.ToLocalTime()),
                _localizer["BackupRestoreAction"],
                _localizer["Cancel"]);
            if (!confirmed)
            {
                return;
            }

            var staged = await _backup.StageRestoreAsync(stream);
            if (!staged.IsValid)
            {
                await ShowBackupErrorAsync(staged.Error);
                return;
            }
        }

        await AskToCloseAppAsync("BackupRestartMessage");
    });

    /// <summary>Debug builds only: fills an empty app with invented data.</summary>
    [RelayCommand]
    private Task FillSampleDataAsync() => RunSafeAsync(async () =>
    {
        if (!IsDebugBuild)
        {
            return;
        }

        var confirmed = await _dialogs.ConfirmAsync(
            _localizer["SettingsTestData"], _localizer["TestDataFillConfirm"], _localizer["TestDataFillAction"], _localizer["Cancel"]);
        if (!confirmed)
        {
            return;
        }

        if (await _sampleData.FillAsync())
        {
            _snackbar.Notify(_localizer["TestDataFilled"]);
        }
        else
        {
            await _dialogs.AlertAsync(_localizer["SettingsTestData"], _localizer["TestDataNotEmpty"]);
        }
    });

    /// <summary>Debug builds only: deletes the database and the attachments at the next start.</summary>
    [RelayCommand]
    private Task DeleteAllDataAsync() => RunSafeAsync(async () =>
    {
        if (!IsDebugBuild)
        {
            return;
        }

        var confirmed = await _dialogs.ConfirmAsync(
            _localizer["SettingsTestData"], _localizer["TestDataDeleteConfirm"], _localizer["TestDataDeleteAction"], _localizer["Cancel"]);
        if (!confirmed)
        {
            return;
        }

        _backup.StageWipe();
        await AskToCloseAppAsync("TestDataDeleteRestart");
    });

    private Task ShowBackupErrorAsync(BackupError? error) =>
        _dialogs.AlertAsync(
            _localizer["BackupRestoreTitle"],
            _localizer[error == BackupError.NewerVersion ? "BackupNewerVersion" : "BackupNotABackup"]);

    private async Task AskToCloseAppAsync(string messageKey)
    {
        var close = await _dialogs.ConfirmAsync(_localizer["BackupRestoreTitle"], _localizer[messageKey], _localizer["BackupCloseApp"], _localizer["BackupLater"]);
        if (close)
        {
            _app.Quit();
        }
    }

    /// <summary>Clears the error log after a confirmation.</summary>
    [RelayCommand]
    private Task ClearLogAsync() => RunSafeAsync(async () =>
    {
        var confirmed = await _dialogs.ConfirmAsync(
            _localizer["SettingsErrorLog"], _localizer["SettingsErrorLogClearConfirm"], _localizer["SettingsErrorLogClear"], _localizer["Cancel"]);
        if (!confirmed)
        {
            return;
        }

        _errorLog.Clear();
        RefreshSectionValues();
    });

    void IRecipient<LayoutModeChangedMessage>.Receive(LayoutModeChangedMessage message) => RefreshLayoutText();

    partial void OnSelectedLanguageChanged(LanguageOption? value)
    {
        if (!_refreshing && value is not null && value.Code != _languageService.CurrentLanguage)
        {
            _languageService.SetLanguage(value.Code);
        }
    }

    partial void OnSelectedThemeChanged(ThemeOption? value)
    {
        if (!_refreshing && value is not null && value.Theme != _themeService.Current)
        {
            _themeService.Set(value.Theme);
            RefreshSectionValues();
        }
    }

    protected override void RefreshTexts()
    {
        _refreshing = true;
        try
        {
            Title = _localizer["SectionSettings"];
            ErrorLogHeader = _localizer["SettingsErrorLog"];
            ErrorLogHint = _localizer["SettingsErrorLogHint"];
            AboutHeader = _localizer["SettingsAbout"];
            HelpText = _localizer["SectionHelp"];
            CopyLogText = _localizer["SettingsErrorLogCopy"];
            ClearLogText = _localizer["SettingsErrorLogClear"];
            BackupHeader = _localizer["SettingsBackup"];
            BackupHint = _localizer["SettingsBackupHint"];
            SaveBackupText = _localizer["SettingsBackupSave"];
            RestoreBackupText = _localizer["SettingsBackupRestore"];
            TestDataHeader = _localizer["SettingsTestData"];
            FillSampleDataText = _localizer["SettingsTestDataFill"];
            DeleteAllDataText = _localizer["SettingsTestDataDelete"];
            RefreshLastBackupText();
            LanguageLabel = _localizer["SettingsLanguage"];
            ThemeLabel = _localizer["SettingsTheme"];
            VersionText = _localizer.Get("SettingsVersion", Version);
            DatabasePathText = _localizer.Get("SettingsDatabaseFile", DatabasePath);

            Languages = [.. _languageService.SupportedLanguages.Select(c => new LanguageOption(c, _localizer[$"LanguageName_{c}"]))];
            SelectedLanguage = Languages.FirstOrDefault(l => l.Code == _languageService.CurrentLanguage);

            Themes = [.. Enum.GetValues<AppThemeChoice>().Select(t => new ThemeOption(t, _localizer[$"Theme_{t}"]))];
            SelectedTheme = Themes.FirstOrDefault(t => t.Theme == _themeService.Current);

            RefreshLayoutText();
            RebuildSections();
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void RebuildSections()
    {
        Sections.Clear();
        foreach (var section in Enum.GetValues<SettingsSection>())
        {
            Sections.Add(new SettingsSectionRowViewModel(section, TitleOf(section), ValueOf(section), open => RunSafeAsync(() => OpenAsync(open))));
        }

        ErrorLogCountText = _localizer.Get("SettingsValueErrorLog", CountLogEntries());
    }

    private void RefreshLastBackupText() =>
        LastBackupText = DateTimeOffset.TryParse(_settings.GetString(LastBackupKey), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var last)
            ? _localizer.Get("SettingsBackupLast", last.ToLocalTime())
            : _localizer["SettingsBackupNever"];

    private void RefreshLayoutText() =>
        LayoutText = _localizer.Get("SettingsLayout", _localizer[$"Layout_{_layoutModeService.Current}"]);
}
