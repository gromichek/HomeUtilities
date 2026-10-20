namespace HomeUtilities.ViewModels.Tests;

public class ShellViewModelTests
{
    [Fact]
    public void Titles_AreLocalized()
    {
        var vm = new ShellViewModel(new FakeLocalizer(new FakeLanguageService()), new FakeLayoutModeService());

        Assert.Equal("SectionOverview [en]", vm.OverviewTitle);
        Assert.Equal("SectionProperties [en]", vm.PropertiesTitle);
        Assert.Equal("SectionSuppliers [en]", vm.SuppliersTitle);
        Assert.Equal("SectionReadings [en]", vm.ReadingsTitle);
        Assert.Equal("SectionBills [en]", vm.BillsTitle);
        Assert.Equal("SectionPayments [en]", vm.PaymentsTitle);
        Assert.Equal("SectionReports [en]", vm.ReportsTitle);
        Assert.Equal("SectionSettings [en]", vm.SettingsTitle);
    }

    [Fact]
    public void Titles_AreRefreshedAfterLanguageChanged()
    {
        var language = new FakeLanguageService();
        var vm = new ShellViewModel(new FakeLocalizer(language), new FakeLayoutModeService());

        language.SetLanguage("ru");

        Assert.Equal("SectionOverview [ru]", vm.OverviewTitle);
        Assert.Equal("SectionSettings [ru]", vm.SettingsTitle);
    }

    [Fact]
    public void IsFlyoutLocked_FollowsLayoutModeChanges()
    {
        var layout = new FakeLayoutModeService();
        var vm = new ShellViewModel(new FakeLocalizer(new FakeLanguageService()), layout);
        Assert.False(vm.IsFlyoutLocked);

        layout.Change(LayoutMode.Expanded);
        Assert.Equal(LayoutMode.Expanded, vm.LayoutMode);
        Assert.True(vm.IsFlyoutLocked);

        layout.Change(LayoutMode.Medium);
        Assert.False(vm.IsFlyoutLocked);
    }

    [Fact]
    public void IsFlyoutLocked_UsesInitialLayoutMode()
    {
        var layout = new FakeLayoutModeService();
        layout.Change(LayoutMode.Expanded);

        var vm = new ShellViewModel(new FakeLocalizer(new FakeLanguageService()), layout);

        Assert.True(vm.IsFlyoutLocked);
    }
}
