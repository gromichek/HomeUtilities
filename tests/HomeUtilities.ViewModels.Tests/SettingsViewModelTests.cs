using HomeUtilities.Core.Backup;

namespace HomeUtilities.ViewModels.Tests;

public class SettingsViewModelTests
{
    private FakeErrorLog ErrorLog { get; } = new();

    private FakeClipboardService Clipboard { get; } = new();

    private FakeSnackbarService Toast { get; } = new();

    private FakeBackupService Backup { get; } = new();

    private FakeBackupFileService BackupFiles { get; } = new();

    private FakeDialogService Dialogs { get; } = new();

    private FakeAppLifecycle App { get; } = new();

    private FakeSettingsStore Settings { get; } = new();

    private FakeSampleDataGenerator Sample { get; } = new();

    private bool IsDebug { get; set; } = true;

    [Fact]
    public async Task CopyLog_PutsTheLogOnTheClipboard()
    {
        var vm = Create(out _, out _, out _);
        ErrorLog.Text = "boom";

        await vm.CopyLogCommand.ExecuteAsync(null);

        Assert.Equal(["boom"], Clipboard.Texts);
        Assert.Equal(["Copied [en]"], Toast.Messages);
    }

    [Fact]
    public async Task CopyLog_WithEmptyLog_SaysSoAndCopiesNothing()
    {
        var vm = Create(out _, out _, out _);

        await vm.CopyLogCommand.ExecuteAsync(null);

        Assert.Empty(Clipboard.Texts);
        Assert.Equal(["SettingsErrorLogEmpty [en]"], Toast.Messages);
    }

    [Fact]
    public async Task ClearLog_AfterConfirmation_ClearsTheLogAndTheCount()
    {
        var vm = Create(out _, out _, out _);
        ErrorLog.Text = LogEntry + LogEntry;
        vm.RefreshSectionValues();
        Assert.Equal("SettingsValueErrorLog 2 [en]", vm.ErrorLogCountText);
        Dialogs.ConfirmResult = true;

        await vm.ClearLogCommand.ExecuteAsync(null);

        Assert.Equal(string.Empty, ErrorLog.Text);
        Assert.Equal("SettingsValueErrorLog 0 [en]", vm.ErrorLogCountText);
        Assert.Equal("SettingsErrorLogClearConfirm [en]", Assert.Single(Dialogs.Confirmations).Message);
    }

    [Fact]
    public async Task ClearLog_WhenCancelled_KeepsTheLog()
    {
        var vm = Create(out _, out _, out _);
        ErrorLog.Text = LogEntry;
        Dialogs.ConfirmResult = false;

        await vm.ClearLogCommand.ExecuteAsync(null);

        Assert.Equal(LogEntry, ErrorLog.Text);
    }

    // The error log starts every entry with "[yyyy-MM-dd HH:mm:ssZ] source" (FileErrorLog).
    private const string LogEntry = "[2026-10-31 08:15:00Z] SettingsViewModel.Test\nSystem.Exception: boom\n\n";

    [Fact]
    public void Sections_AreListedInOrderWithTheirCurrentValues()
    {
        Settings.Values[SettingsViewModel.LastBackupKey] = "2026-10-24T09:30:00.0000000+00:00";
        ErrorLog.Text = LogEntry + LogEntry + LogEntry;

        var vm = Create(out _, out _, out _);

        Assert.Equal(
            [
                SettingsSection.Language, SettingsSection.Theme, SettingsSection.Reminders, SettingsSection.Backup,
                SettingsSection.Categories, SettingsSection.VatRates, SettingsSection.BillTemplates, SettingsSection.Imports,
                SettingsSection.ErrorLog, SettingsSection.About,
            ],
            vm.Sections.Select(r => r.Section));
        Assert.Equal("SettingsLanguage [en]", vm.Sections[0].Title);
        Assert.Equal("LanguageName_en [en]", Row(vm, SettingsSection.Language).Value);
        Assert.Equal("Theme_System [en]", Row(vm, SettingsSection.Theme).Value);
        Assert.Equal("SettingsValueRemindersOff [en]", Row(vm, SettingsSection.Reminders).Value);
        Assert.StartsWith("SettingsValueBackupLast ", Row(vm, SettingsSection.Backup).Value);
        Assert.Equal("SettingsValueErrorLog 3 [en]", Row(vm, SettingsSection.ErrorLog).Value);
        Assert.Equal("SettingsVersion 1.2.3 [en]", Row(vm, SettingsSection.About).Value);
        Assert.Equal("SettingsValueCategories [en]", Row(vm, SettingsSection.Categories).Value);
        Assert.Equal("SettingsValueImports [en]", Row(vm, SettingsSection.Imports).Value);
    }

    [Fact]
    public void BackupRow_WithoutBackup_SaysSo()
    {
        var vm = Create(out _, out _, out _);

        Assert.Equal("SettingsValueBackupNever [en]", Row(vm, SettingsSection.Backup).Value);
    }

    [Fact]
    public void RemindersRow_ShowsOnWithTheTimeOrOff()
    {
        var vm = Create(out _, out _, out _);
        vm.Reminders.IsEnabled = true;

        Assert.Equal("SettingsValueRemindersOn 09:00 [en]", Row(vm, SettingsSection.Reminders).Value);

        vm.Reminders.Time = new TimeSpan(18, 30, 0);
        Assert.Equal("SettingsValueRemindersOn 18:30 [en]", Row(vm, SettingsSection.Reminders).Value);

        vm.Reminders.IsEnabled = false;
        Assert.Equal("SettingsValueRemindersOff [en]", Row(vm, SettingsSection.Reminders).Value);
    }

    [Fact]
    public void SelectingLanguageOrTheme_UpdatesTheRowValue()
    {
        var vm = Create(out _, out _, out _);

        vm.SelectedLanguage = vm.Languages.Single(l => l.Code == "ru");
        Assert.Equal("LanguageName_ru [ru]", Row(vm, SettingsSection.Language).Value);
        Assert.Equal("SettingsLanguage [ru]", Row(vm, SettingsSection.Language).Title);

        vm.SelectedTheme = vm.Themes.Single(t => t.Theme == AppThemeChoice.Dark);
        Assert.Equal("Theme_Dark [ru]", Row(vm, SettingsSection.Theme).Value);
    }

    [Theory]
    [InlineData(SettingsSection.Categories, Routes.Categories)]
    [InlineData(SettingsSection.VatRates, Routes.VatRates)]
    [InlineData(SettingsSection.BillTemplates, Routes.BillTemplates)]
    [InlineData(SettingsSection.Imports, Routes.Imports)]
    public async Task OpeningAnExistingScreenRow_NavigatesToThatScreen(SettingsSection section, string route)
    {
        var navigation = new FakeNavigationService();
        var vm = Create(out _, out _, out _, navigation);

        await Row(vm, section).OpenCommand.ExecuteAsync(null);

        var visit = Assert.Single(navigation.Visited);
        Assert.Equal(route, visit.Route);
        Assert.Null(visit.Parameters);
    }

    [Theory]
    [InlineData(SettingsSection.Language)]
    [InlineData(SettingsSection.Theme)]
    [InlineData(SettingsSection.Reminders)]
    [InlineData(SettingsSection.Backup)]
    [InlineData(SettingsSection.ErrorLog)]
    [InlineData(SettingsSection.About)]
    public async Task OpeningASettingsRow_PushesTheSectionPageWithItsViewModel(SettingsSection section)
    {
        var navigation = new FakeNavigationService();
        var vm = Create(out _, out _, out _, navigation);

        await Row(vm, section).OpenCommand.ExecuteAsync(null);

        var visit = Assert.Single(navigation.Visited);
        Assert.Equal(Routes.SettingsSection, visit.Route);
        var page = Assert.IsType<SettingsSectionViewModel>(visit.Parameters![SettingsViewModel.SectionParameter]);
        Assert.Equal(section, page.Section);
        Assert.Same(vm, page.Settings);
        Assert.Equal(vm.TitleOf(section), page.Title);
        Assert.Equal(section == SettingsSection.Language, page.IsLanguage);
        Assert.Equal(section == SettingsSection.Reminders, page.IsReminders);
        Assert.Equal(section == SettingsSection.About, page.IsAbout);
    }

    [Theory]
    [InlineData(SettingsSection.Language, HelpTopic.LanguageAndTheme)]
    [InlineData(SettingsSection.Theme, HelpTopic.LanguageAndTheme)]
    [InlineData(SettingsSection.Reminders, HelpTopic.Reminders)]
    [InlineData(SettingsSection.Backup, HelpTopic.Backup)]
    [InlineData(SettingsSection.ErrorLog, HelpTopic.ErrorLog)]
    [InlineData(SettingsSection.About, HelpTopic.About)]
    public async Task SectionHelp_OpensTheMatchingGuideSection(SettingsSection section, HelpTopic topic)
    {
        var navigation = new FakeNavigationService();
        var vm = Create(out _, out _, out _, navigation);
        var page = new SettingsSectionViewModel(vm, section, new FakeLocalizer(new FakeLanguageService()), navigation);

        await page.OpenHelpCommand.ExecuteAsync(null);

        var visit = Assert.Single(navigation.Visited);
        Assert.Equal(Routes.HelpSection, visit.Route);
        Assert.Equal(topic, visit.Parameters![HelpNavigation.TopicParameter]);
    }

    [Fact]
    public async Task ListHelp_OpensTheSettingsChapter()
    {
        var navigation = new FakeNavigationService();
        var vm = Create(out _, out _, out _, navigation);

        await vm.OpenHelpCommand.ExecuteAsync(null);

        Assert.Equal(HelpTopic.Settings, Assert.Single(navigation.Visited).Parameters![HelpNavigation.TopicParameter]);
    }

    private static SettingsSectionRowViewModel Row(SettingsViewModel vm, SettingsSection section) =>
        vm.Sections.Single(r => r.Section == section);

    [Fact]
    public void Languages_AreListedWithLocalizedNamesAndCurrentSelected()
    {
        var vm = Create(out _, out _, out _);

        Assert.Equal(["en", "ru"], vm.Languages.Select(l => l.Code));
        Assert.Equal("LanguageName_en [en]", vm.Languages[0].DisplayName);
        Assert.Equal("en", vm.SelectedLanguage?.Code);
    }

    [Fact]
    public void SelectingLanguage_CallsSetLanguageAndKeepsSelection()
    {
        var vm = Create(out var language, out _, out _);

        vm.SelectedLanguage = vm.Languages.Single(l => l.Code == "ru");

        Assert.Equal(["ru"], language.SetCalls);
        Assert.Equal("ru", vm.SelectedLanguage?.Code);
        Assert.Equal("LanguageName_ru [ru]", vm.SelectedLanguage?.DisplayName);
        Assert.Equal("SettingsLanguage [ru]", vm.LanguageLabel);
    }

    [Fact]
    public void SelectingTheme_CallsThemeService()
    {
        var vm = Create(out _, out var theme, out _);
        Assert.Equal(AppThemeChoice.System, vm.SelectedTheme?.Theme);

        vm.SelectedTheme = vm.Themes.Single(t => t.Theme == AppThemeChoice.Dark);

        Assert.Equal([AppThemeChoice.Dark], theme.SetCalls);
    }

    [Fact]
    public void Refresh_DoesNotCallServices()
    {
        var vm = Create(out var language, out var theme, out _);

        language.SetLanguage("ru");

        Assert.Equal(["ru"], language.SetCalls);
        Assert.Empty(theme.SetCalls);
        Assert.NotNull(vm.SelectedTheme);
    }

    [Fact]
    public void VersionAndDatabasePath_AreShown()
    {
        var vm = Create(out _, out _, out _);

        Assert.Equal("SettingsVersion 1.2.3 [en]", vm.VersionText);
        Assert.Equal("SettingsDatabaseFile /data/test.db [en]", vm.DatabasePathText);
    }

    [Fact]
    public void LayoutText_FollowsLayoutModeChanges()
    {
        var vm = Create(out _, out _, out var layout);
        Assert.Equal("SettingsLayout Layout_Compact [en] [en]", vm.LayoutText);

        layout.Change(LayoutMode.Expanded);

        Assert.Equal("SettingsLayout Layout_Expanded [en] [en]", vm.LayoutText);
    }

    [Fact]
    public async Task SaveBackup_writes_the_zip_through_the_file_service_and_remembers_the_time()
    {
        var vm = Create(out _, out _, out _);
        Assert.Equal("SettingsBackupNever [en]", vm.LastBackupText);

        await vm.SaveBackupCommand.ExecuteAsync(null);

        Assert.Equal(["backup.zip"], BackupFiles.SavedNames);
        Assert.Equal(new byte[] { 1, 2, 3 }, BackupFiles.SavedBytes);
        Assert.Equal(1, Backup.Created);
        Assert.Equal("2026-10-24T09:30:00.0000000+00:00", Settings.Values[SettingsViewModel.LastBackupKey]);
        Assert.StartsWith("SettingsBackupLast ", vm.LastBackupText);
        Assert.Equal(["BackupSavedTo C:\\Backups [en]"], Toast.Messages);
    }

    [Fact]
    public async Task SaveBackup_after_sharing_says_saved_without_a_location()
    {
        BackupFiles.SaveResult = new FileSaveResult(true, null);
        var vm = Create(out _, out _, out _);

        await vm.SaveBackupCommand.ExecuteAsync(null);

        Assert.Equal(["BackupSaved [en]"], Toast.Messages);
    }

    [Fact]
    public async Task SaveBackup_cancelled_stores_nothing()
    {
        BackupFiles.SaveResult = new FileSaveResult(false, null);
        var vm = Create(out _, out _, out _);

        await vm.SaveBackupCommand.ExecuteAsync(null);

        Assert.Empty(Settings.Values);
        Assert.Empty(Toast.Messages);
        Assert.Equal("SettingsBackupNever [en]", vm.LastBackupText);
    }

    [Fact]
    public async Task RestoreBackup_confirms_stages_and_offers_to_close_the_app()
    {
        BackupFiles.Picked = new MemoryStream([1]);
        var vm = Create(out _, out _, out _);

        await vm.RestoreBackupCommand.ExecuteAsync(null);

        Assert.Equal(1, Backup.Staged);
        Assert.Equal(2, Dialogs.Confirmations.Count);
        Assert.Equal("BackupRestoreConfirm", Dialogs.Confirmations[0].Message.Split(' ')[0]);
        Assert.Equal("BackupRestartMessage [en]", Dialogs.Confirmations[1].Message);
        Assert.Equal(1, App.QuitCalls);
    }

    [Fact]
    public async Task RestoreBackup_stays_open_when_the_owner_does_not_close_the_app_now()
    {
        BackupFiles.Picked = new MemoryStream([1]);
        Dialogs.ConfirmAnswers.Enqueue(true); // restore
        Dialogs.ConfirmAnswers.Enqueue(false); // close the app now
        var vm = Create(out _, out _, out _);

        await vm.RestoreBackupCommand.ExecuteAsync(null);

        Assert.Equal(1, Backup.Staged);
        Assert.Equal(0, App.QuitCalls);
    }

    [Fact]
    public async Task RestoreBackup_cancelled_in_the_picker_does_nothing()
    {
        var vm = Create(out _, out _, out _);

        await vm.RestoreBackupCommand.ExecuteAsync(null);

        Assert.Equal(0, Backup.Staged);
        Assert.Empty(Dialogs.Confirmations);
        Assert.Empty(Dialogs.Alerts);
    }

    [Fact]
    public async Task RestoreBackup_declined_stages_nothing()
    {
        BackupFiles.Picked = new MemoryStream([1]);
        Dialogs.ConfirmResult = false;
        var vm = Create(out _, out _, out _);

        await vm.RestoreBackupCommand.ExecuteAsync(null);

        Assert.Equal(0, Backup.Staged);
        Assert.Single(Dialogs.Confirmations);
        Assert.Equal(0, App.QuitCalls);
    }

    [Theory]
    [InlineData(BackupError.NotABackup, "BackupNotABackup [en]")]
    [InlineData(BackupError.NewerVersion, "BackupNewerVersion [en]")]
    public async Task RestoreBackup_with_a_bad_file_shows_the_reason_and_changes_nothing(BackupError error, string message)
    {
        BackupFiles.Picked = new MemoryStream([1]);
        Backup.Validation = BackupValidation.Invalid(error);
        var vm = Create(out _, out _, out _);

        await vm.RestoreBackupCommand.ExecuteAsync(null);

        Assert.Equal(message, Assert.Single(Dialogs.Alerts).Message);
        Assert.Empty(Dialogs.Confirmations);
        Assert.Equal(0, Backup.Staged);
        Assert.Equal(0, App.QuitCalls);
    }

    [Fact]
    public void Test_data_card_is_hidden_in_release_builds()
    {
        IsDebug = false;

        var vm = Create(out _, out _, out _);

        Assert.False(vm.IsDebugBuild);
    }

    [Fact]
    public async Task Test_data_commands_do_nothing_in_release_builds()
    {
        IsDebug = false;
        var vm = Create(out _, out _, out _);

        await vm.FillSampleDataCommand.ExecuteAsync(null);
        await vm.DeleteAllDataCommand.ExecuteAsync(null);

        Assert.Equal(0, Sample.Calls);
        Assert.Equal(0, Backup.Wipes);
        Assert.Empty(Dialogs.Confirmations);
    }

    [Fact]
    public async Task FillSampleData_asks_first_then_fills()
    {
        var vm = Create(out _, out _, out _);

        await vm.FillSampleDataCommand.ExecuteAsync(null);

        Assert.True(vm.IsDebugBuild);
        Assert.Single(Dialogs.Confirmations);
        Assert.Equal(1, Sample.Calls);
        Assert.Equal(["TestDataFilled [en]"], Toast.Messages);
    }

    [Fact]
    public async Task FillSampleData_declined_does_nothing()
    {
        Dialogs.ConfirmResult = false;
        var vm = Create(out _, out _, out _);

        await vm.FillSampleDataCommand.ExecuteAsync(null);

        Assert.Equal(0, Sample.Calls);
    }

    [Fact]
    public async Task FillSampleData_on_a_non_empty_app_explains_why_nothing_happened()
    {
        Sample.Result = false;
        var vm = Create(out _, out _, out _);

        await vm.FillSampleDataCommand.ExecuteAsync(null);

        Assert.Equal("TestDataNotEmpty [en]", Assert.Single(Dialogs.Alerts).Message);
        Assert.Empty(Toast.Messages);
    }

    [Fact]
    public async Task DeleteAllData_confirms_stages_the_wipe_and_offers_to_close()
    {
        var vm = Create(out _, out _, out _);

        await vm.DeleteAllDataCommand.ExecuteAsync(null);

        Assert.Equal(1, Backup.Wipes);
        Assert.Equal(2, Dialogs.Confirmations.Count);
        Assert.Equal(1, App.QuitCalls);
    }

    [Fact]
    public async Task DeleteAllData_declined_deletes_nothing()
    {
        Dialogs.ConfirmResult = false;
        var vm = Create(out _, out _, out _);

        await vm.DeleteAllDataCommand.ExecuteAsync(null);

        Assert.Equal(0, Backup.Wipes);
        Assert.Equal(0, App.QuitCalls);
    }

    private static ReminderSettingsViewModel Reminders()
    {
        var language = new FakeLanguageService();
        var notifications = new ReminderTests.FakeNotifications();
        var store = new ReminderSettingsStore(new FakeSettingsStore());
        var scheduler = new ReminderScheduler(
            new ReminderTests.FakeDatabase(), new ReminderTests.FakeLoader(), store, notifications, new FakeLocalizer(language),
            new FixedClock(), new FakeSnackbarService(), new HomeUtilities.Core.Abstractions.DataChangeNotifier(), new FakeErrorLog());
        return new ReminderSettingsViewModel(store, scheduler, notifications, new FakeDialogService(), new FakeLocalizer(language));
    }

    private SettingsViewModel Create(
        out FakeLanguageService language, out FakeThemeService theme, out FakeLayoutModeService layout, FakeNavigationService? navigation = null)
    {
        navigation ??= new FakeNavigationService();
        language = new FakeLanguageService();
        theme = new FakeThemeService();
        layout = new FakeLayoutModeService();
        return new SettingsViewModel(new FakeLocalizer(language), language, theme, layout, new FakeAppInfo(IsDebug), new FakeAppPaths(), navigation, ErrorLog, Clipboard, Toast, Backup, BackupFiles, Dialogs, App, Settings, Sample, new FixedClock(), Reminders());
    }

    private sealed class FakeAppInfo(bool isDebug) : IAppInfo
    {
        public string Version => "1.2.3";

        public bool IsDebugBuild => isDebug;
    }

    private sealed class FakeAppPaths : IAppPaths
    {
        public string DatabasePath => "/data/test.db";

        public string ErrorLogPath => "/data/logs/errors.log";

        public string AttachmentsFolder => "/data/attachments";
    }
}
