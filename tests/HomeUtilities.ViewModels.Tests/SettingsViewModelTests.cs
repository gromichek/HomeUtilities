namespace HomeUtilities.ViewModels.Tests;

public class SettingsViewModelTests
{
    private FakeErrorLog ErrorLog { get; } = new();

    private FakeClipboardService Clipboard { get; } = new();

    private FakeToastService Toast { get; } = new();

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
    public void ClearLog_ClearsTheLog()
    {
        var vm = Create(out _, out _, out _);
        ErrorLog.Text = "boom";

        vm.ClearLogCommand.Execute(null);

        Assert.Equal(string.Empty, ErrorLog.Text);
    }

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
    public async Task OpenCategories_NavigatesToCategoriesPage()
    {
        var navigation = new FakeNavigationService();
        var vm = Create(out _, out _, out _, navigation);

        await vm.OpenCategoriesCommand.ExecuteAsync(null);

        Assert.Equal(Routes.Categories, Assert.Single(navigation.Visited).Route);
        Assert.Equal("SectionCategories [en]", vm.CategoriesText);
    }

    private SettingsViewModel Create(
        out FakeLanguageService language, out FakeThemeService theme, out FakeLayoutModeService layout, FakeNavigationService? navigation = null)
    {
        navigation ??= new FakeNavigationService();
        language = new FakeLanguageService();
        theme = new FakeThemeService();
        layout = new FakeLayoutModeService();
        return new SettingsViewModel(new FakeLocalizer(language), language, theme, layout, new FakeAppInfo(), new FakeAppPaths(), navigation, ErrorLog, Clipboard, Toast);
    }

    private sealed class FakeAppInfo : IAppInfo
    {
        public string Version => "1.2.3";
    }

    private sealed class FakeAppPaths : IAppPaths
    {
        public string DatabasePath => "/data/test.db";

        public string ErrorLogPath => "/data/logs/errors.log";
    }
}
