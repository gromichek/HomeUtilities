using HomeUtilities.Core.Common;
using HomeUtilities.Core.Tariffs;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels.Tests;

public class VatRatesViewModelTests
{
    private static readonly DateOnly Today = new(2026, 10, 24);

    private readonly ContractTestKit _kit = new(Today);

    [Fact]
    public async Task List_ShowsTheSeededRatesWithTheirPercentOfToday()
    {
        var vm = _kit.CreateVatRates();

        await vm.ReloadAsync();

        Assert.Equal(["LV-REDUCED", "LV-STANDARD", "ZERO"], vm.Items.Select(i => i.Code));
        Assert.Equal(
            ["VatPercentValue 12 [en]", "VatPercentValue 21 [en]", "VatPercentValue 0 [en]"],
            vm.Items.Select(i => i.PercentText));
        Assert.Equal("VatRatesBanner [en]", vm.BannerText);
        Assert.False(vm.IsEmpty);
    }

    [Fact]
    public async Task SeededRate_HasAReadOnlyCode_AndCannotBeDeleted()
    {
        var editor = await OpenAsync("LV-STANDARD");

        Assert.True(editor.IsCodeReadOnly);
        Assert.False(editor.IsCodeEditable);
        Assert.False(editor.CanShowDelete);
        Assert.Equal("Standard rate", editor.RateName);
        Assert.Equal("Standard rate", editor.Title);

        editor.Code = "CHANGED";
        editor.RateName = "Standard VAT";
        await editor.SaveCommand.ExecuteAsync(null);

        var saved = _kit.VatRates.Saved[^1];
        Assert.Equal(("LV-STANDARD", "Standard VAT"), (saved.Code, saved.Name));
        Assert.Equal(4, saved.Versions.Count);
    }

    [Fact]
    public async Task VersionsAreListedNewestFirst()
    {
        var editor = await OpenAsync("LV-STANDARD");

        Assert.Equal(
            [new DateOnly(2012, 7, 1), new DateOnly(2011, 1, 1), new DateOnly(2009, 1, 1), new DateOnly(1995, 1, 1)],
            editor.Versions.Select(v => v.Version.ValidFrom));
        Assert.Equal([21m, 22m, 21m, 18m], editor.Versions.Select(v => v.Version.Percent));
        Assert.False(editor.HasNoVersions);
    }

    [Fact]
    public async Task AddingAVersion_AddsAnEarlierOrLaterPercent_AndMarksTheEditorDirty()
    {
        var editor = await OpenAsync("LV-STANDARD");

        await AddVersionAsync(editor, new DateOnly(2027, 1, 1), "22");

        Assert.Equal(new DateOnly(2027, 1, 1), editor.Versions[0].Version.ValidFrom);
        Assert.Equal(22m, editor.Versions[0].Version.Percent);
        Assert.True(editor.IsDirty);

        await editor.SaveCommand.ExecuteAsync(null);

        var saved = _kit.VatRates.Saved[^1];
        Assert.Equal(5, saved.Versions.Count);
        Assert.All(saved.Versions, v => Assert.Equal(saved.Id, v.VatRateId));
        Assert.Equal(22m, saved.PercentAt(new DateOnly(2027, 6, 1)));
        Assert.Equal(21m, saved.PercentAt(new DateOnly(2026, 6, 1)));
    }

    [Fact]
    public async Task EditingAVersion_ChangesItsPercentAndKeepsTheOthers()
    {
        var editor = await OpenAsync("LV-REDUCED");
        _kit.Dialogs.EditorScript = dialog =>
        {
            ((VatVersionEditorViewModel)dialog).PercentText = "13,5";
            dialog.SaveCommand.Execute(null);
        };

        editor.Versions[0].EditCommand.Execute(null);

        Assert.Equal(13.5m, editor.Versions[0].Version.Percent);
        Assert.Equal(new DateOnly(2011, 1, 1), editor.Versions[0].Version.ValidFrom);
        Assert.Equal(3, editor.Versions.Count);
        Assert.False(((VatVersionEditorViewModel)_kit.Dialogs.LastEditor!).IsNew);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("101")]
    [InlineData("-1")]
    public async Task VersionDialog_BadPercent_ShowsAMessage(string text)
    {
        var editor = await OpenAsync("ZERO");

        await AddVersionAsync(editor, new DateOnly(2027, 1, 1), text);

        var dialog = (VatVersionEditorViewModel)_kit.Dialogs.LastEditor!;
        Assert.False(dialog.Saved);
        Assert.NotEmpty(dialog.PercentError);
        Assert.Single(editor.Versions);
    }

    [Fact]
    public async Task VersionDialog_TwoPercentsOnTheSameDate_AreRefused()
    {
        var editor = await OpenAsync("ZERO");

        await AddVersionAsync(editor, new DateOnly(1995, 1, 1), "5");

        var dialog = (VatVersionEditorViewModel)_kit.Dialogs.LastEditor!;
        Assert.StartsWith("Error_versioned_duplicate_valid_from ", dialog.ValidFromError);
        Assert.Single(editor.Versions);
    }

    [Fact]
    public async Task RemovingAVersion_AsksForConfirmation()
    {
        var editor = await OpenAsync("LV-REDUCED");
        _kit.Dialogs.ConfirmResult = false;
        editor.Versions[0].RemoveCommand.Execute(null);
        Assert.Equal(3, editor.Versions.Count);

        _kit.Dialogs.ConfirmResult = true;
        editor.Versions[0].RemoveCommand.Execute(null);

        Assert.Equal(2, editor.Versions.Count);
        Assert.Contains(_kit.Dialogs.Confirmations, c => c.Title == "Dialog_DeleteRow_Title [en]");
        Assert.True(editor.IsDirty);
    }

    [Fact]
    public async Task NewRate_NeedsNameCodeAndAPercent_AndStaysOpenAfterSave()
    {
        var vm = _kit.CreateVatRates();
        await vm.ReloadAsync();
        await vm.AddCommand.ExecuteAsync(null);
        var editor = vm.SelectedEditor!;
        editor.IsHostedInPage = true;

        await editor.SaveCommand.ExecuteAsync(null);

        Assert.Empty(_kit.VatRates.Saved);
        Assert.Equal("Error_vat_rate_name_required [en]", editor.NameError);
        Assert.Equal("Error_vat_rate_code_invalid [en]", editor.CodeError);
        Assert.Equal("Error_vat_editor_version_required [en]", editor.VersionsError);

        editor.RateName = "Books";
        editor.Code = " books-5 ";
        await AddVersionAsync(editor, new DateOnly(2026, 1, 1), "5");
        await editor.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(_kit.VatRates.Saved);
        Assert.Equal(("BOOKS-5", "Books"), (saved.Code, saved.Name));
        Assert.False(editor.IsNew);
        Assert.Equal(0, _kit.Navigation.BackCount);
        Assert.Contains(vm.Items, i => i.Code == "BOOKS-5");
    }

    [Fact]
    public async Task NewRate_WithACodeOfAnotherRate_ShowsAMessage()
    {
        var vm = _kit.CreateVatRates();
        await vm.ReloadAsync();
        await vm.AddCommand.ExecuteAsync(null);
        var editor = vm.SelectedEditor!;
        editor.RateName = "Another standard";
        editor.Code = "lv-standard";
        await AddVersionAsync(editor, new DateOnly(2026, 1, 1), "21");

        await editor.SaveCommand.ExecuteAsync(null);

        Assert.Empty(_kit.VatRates.Saved);
        Assert.Equal("Error_vat_editor_code_duplicate [en]", editor.CodeError);
    }

    [Fact]
    public async Task CustomRate_CanBeDeleted_WhenNoComponentUsesIt()
    {
        var custom = new VatRate { Code = "BOOKS-5", Name = "Books", Versions = [new VatRateVersion { ValidFrom = new DateOnly(2026, 1, 1), Percent = 5m }] };
        _kit.VatRates.Stored.Add(custom);
        var editor = await OpenAsync("BOOKS-5");
        Assert.True(editor.CanShowDelete);
        Assert.True(editor.IsCodeEditable);

        await editor.DeleteCommand.ExecuteAsync(null);

        Assert.Equal([custom.Id], _kit.VatRates.Deleted);
        Assert.Single(_kit.Dialogs.Confirmations);
        Assert.Empty(_kit.Dialogs.Alerts);
    }

    [Fact]
    public async Task Delete_IsBlockedWithAMessage_WhenAComponentUsesTheRate()
    {
        var custom = new VatRate { Code = "BOOKS-5", Name = "Books", Versions = [new VatRateVersion { ValidFrom = new DateOnly(2026, 1, 1), Percent = 5m }] };
        _kit.VatRates.Stored.Add(custom);
        _kit.Tariffs.Stored.Add(new TariffComponent { ContractId = Guid.CreateVersion7(), Name = "Book club", VatCode = "BOOKS-5" });
        _kit.Tariffs.Stored.Add(new TariffComponent { ContractId = Guid.CreateVersion7(), Name = "Other", VatCode = "ZERO" });
        var editor = await OpenAsync("BOOKS-5");

        await editor.DeleteCommand.ExecuteAsync(null);

        Assert.Empty(_kit.VatRates.Deleted);
        Assert.Empty(_kit.Dialogs.Confirmations);
        var alert = Assert.Single(_kit.Dialogs.Alerts);
        Assert.Equal("VatRateInUse Books|1 [en]", alert.Message);
        Assert.NotNull(await _kit.VatRates.GetAsync(custom.Id));
    }

    [Fact]
    public async Task LeavingWithChanges_AsksToSaveFirst()
    {
        var editor = await OpenAsync("LV-REDUCED");
        editor.Notes = "check against the bills";
        _kit.Dialogs.UnsavedChoice = UnsavedChangesChoice.Cancel;

        Assert.False(await editor.CanLeaveAsync());

        _kit.Dialogs.UnsavedChoice = UnsavedChangesChoice.Save;
        Assert.True(await editor.CanLeaveAsync());
        Assert.Equal("check against the bills", _kit.VatRates.Saved[^1].Notes);
    }

    [Fact]
    public async Task Help_OpensTheVatRatesSection()
    {
        var vm = _kit.CreateVatRates();

        await vm.OpenHelpCommand.ExecuteAsync(null);

        var visit = Assert.Single(_kit.Navigation.Visited);
        Assert.Equal(Routes.HelpSection, visit.Route);
        Assert.Equal(HelpTopic.VatRates, visit.Parameters![HelpNavigation.TopicParameter]);
    }

    [Fact]
    public async Task OnACompactLayout_AnEditorOpensAsAPage()
    {
        var vm = _kit.CreateVatRates();
        await vm.ReloadAsync();

        await vm.SelectCommand.ExecuteAsync(vm.Items[0].Id);

        var visit = Assert.Single(_kit.Navigation.Visited);
        Assert.Equal(Routes.VatRateEdit, visit.Route);
        Assert.Same(vm.SelectedEditor, visit.Parameters![EditorNavigation.EditorParameter]);
    }

    private async Task<VatRateEditViewModel> OpenAsync(string code)
    {
        var editor = _kit.CreateVatRateEditor();
        editor.IsHostedInPage = true;
        await editor.LoadAsync(_kit.VatRates.Stored.Single(v => v.Code == code).Id);
        return editor;
    }

    private async Task AddVersionAsync(VatRateEditViewModel editor, DateOnly validFrom, string percent)
    {
        _kit.Dialogs.EditorScript = dialog =>
        {
            var version = (VatVersionEditorViewModel)dialog;
            version.ValidFrom = validFrom;
            version.PercentText = percent;
            version.SaveCommand.Execute(null);
        };
        await editor.AddVersionCommand.ExecuteAsync(null);
        _kit.Dialogs.EditorScript = null;
    }
}
