using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Domain;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels.Tests;

/// <summary>T-028i: a list section reloads on appearing only after a change, on a new day or after a language change.</summary>
public sealed class ReloadGateSectionTests : IDisposable
{
    private readonly FakeLanguageService _language = new();
    private readonly FakeCategoryStore _store = new();
    private readonly DataChangeNotifier _changes = new();
    private readonly TestClock _clock = new(new DateOnly(2026, 11, 2));

    public ReloadGateSectionTests()
    {
        ViewModelBase.DataChanges = _changes;
        ViewModelBase.GateClock = _clock;
    }

    public void Dispose()
    {
        ViewModelBase.DataChanges = null;
        ViewModelBase.GateClock = null;
    }

    private CategoriesViewModel Create()
    {
        var localizer = new FakeLocalizer(_language);
        return new CategoriesViewModel(
            _store, localizer, new FakeNavigationService(), new FakeLayoutModeService(), new SectionSelectionMemory(),
            () => new CategoryEditViewModel(_store, localizer, new FakeDialogService(), new FakeNavigationService(), new FakeSnackbarService()));
    }

    [Fact]
    public async Task Second_visit_without_a_change_does_not_reload()
    {
        var vm = Create();

        await vm.OnAppearingAsync();
        await vm.OnAppearingAsync();

        Assert.Equal(1, _store.ListCalls);
    }

    [Fact]
    public async Task A_saved_change_reloads_and_shows_the_new_data()
    {
        var vm = Create();
        await vm.OnAppearingAsync();

        _store.Stored.Add(new Category { Name = "Added elsewhere" });
        _changes.Notify();
        await vm.OnAppearingAsync();

        Assert.Equal(2, _store.ListCalls);
        Assert.Contains(vm.Items, i => i.DisplayName == "Added elsewhere");
    }

    [Fact]
    public async Task A_new_day_reloads()
    {
        var vm = Create();
        await vm.OnAppearingAsync();

        _clock.Today = _clock.Today.AddDays(1);
        await vm.OnAppearingAsync();

        Assert.Equal(2, _store.ListCalls);
    }

    [Fact]
    public async Task A_language_change_reloads()
    {
        var vm = Create();
        await vm.OnAppearingAsync();

        _language.SetLanguage("ru");
        await vm.OnAppearingAsync();

        Assert.Equal(2, _store.ListCalls);
    }

    [Fact]
    public async Task Without_a_notifier_every_visit_reloads()
    {
        ViewModelBase.DataChanges = null;
        var vm = Create();

        await vm.OnAppearingAsync();
        await vm.OnAppearingAsync();

        Assert.Equal(2, _store.ListCalls);
    }
}
