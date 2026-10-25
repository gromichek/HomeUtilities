using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Payments;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels.Tests;

public class SupplierEditViewModelTests
{
    private const string ValidIban = "LV80BANK0000435195001";
    private const string ValidIbanFormatted = "LV80 BANK 0000 4351 9500 1";

    private readonly FakeLanguageService _language = new();
    private readonly FakeSupplierStore _store = new();
    private readonly FakeNavigationService _navigation = new();
    private readonly FakeDialogService _dialogs = new();
    private readonly FakeLauncherService _launcher = new();
    private readonly FakeClipboardService _clipboard = new();
    private readonly FakeSnackbarService _toast = new();
    private readonly TestClock _clock = new(new DateOnly(2026, 10, 20));

    [Fact]
    public async Task Save_StoresFieldsLinksAndPaymentDetails()
    {
        var vm = await CreateNewAsync();
        vm.Name = " Water utility ";
        vm.RegistrationNumber = "40000000000";
        vm.Phone = "";
        vm.Email = "info@example.com";
        vm.RecipientName = "Water utility Ltd";
        vm.RecipientRegistrationNumber = "40000000001";
        vm.IbanText = "lv80 bank 0000 4351 9500 1";
        vm.Bic = "bankLV22";
        vm.BankName = "Example bank";
        vm.PurposeTemplate = "Abon. Nr. {CustomerNumber}";
        await AddLink(vm, LinkKind.Website, "", "https://example.com");
        await AddLink(vm, LinkKind.CustomerPortal, "Portal", "https://example.com/portal");

        await vm.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(_store.Saved);
        Assert.Equal("Water utility", saved.Name);
        Assert.Equal("40000000000", saved.RegistrationNumber);
        Assert.Null(saved.Phone);
        Assert.Equal("info@example.com", saved.Email);
        Assert.Equal(
            new PaymentDetails("Water utility Ltd", "40000000001", ValidIban, "BANKLV22", "Example bank", "Abon. Nr. {CustomerNumber}"),
            saved.PaymentDetails);
        Assert.Equal(2, saved.Links.Count);
        Assert.All(saved.Links, l => Assert.Equal(saved.Id, l.OwnerId));
        Assert.Equal([0, 1], saved.Links.Select(l => l.SortOrder));
        Assert.Null(saved.Links[0].Title);
        Assert.Equal("Portal", saved.Links[1].Title);
        Assert.False(vm.IsDirty);
        Assert.False(vm.IsNew);
    }

    [Fact]
    public async Task Save_WithoutPaymentDetails_StoresNull()
    {
        var vm = await CreateNewAsync();
        vm.Name = "Supplier";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Null(Assert.Single(_store.Saved).PaymentDetails);
    }

    [Fact]
    public async Task Iban_IsStoredNormalizedAndDisplayedFormatted()
    {
        var vm = await CreateNewAsync();
        vm.Name = "Supplier";
        vm.IbanText = "lv80 bank 0000 4351 9500 1";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(ValidIban, _store.Saved[0].PaymentDetails!.Iban);
        Assert.Equal(ValidIbanFormatted, vm.IbanText);
        Assert.False(vm.IsDirty);
    }

    [Fact]
    public async Task Load_ShowsStoredIbanFormatted()
    {
        var supplier = new Supplier { Name = "Supplier", PaymentDetails = PaymentDetails.Empty with { Iban = ValidIban } };
        _store.Stored.Add(supplier);
        var vm = Create();

        await vm.LoadAsync(supplier.Id);

        Assert.Equal(ValidIbanFormatted, vm.IbanText);
        Assert.False(vm.IsDirty);
    }

    [Fact]
    public async Task FormatIban_GroupsTheTypedText()
    {
        var vm = await CreateNewAsync();
        vm.IbanText = "lv80bank0000435195001";

        vm.FormatIbanCommand.Execute(null);

        Assert.Equal(ValidIbanFormatted, vm.IbanText);
    }

    [Fact]
    public async Task InvalidIban_ShowsErrorAndDoesNotSave()
    {
        var vm = await CreateNewAsync();
        vm.Name = "Supplier";
        vm.IbanText = "LV81 BANK 0000 4351 9500 1";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Empty(_store.Saved);
        Assert.Equal("Error_payment_iban_invalid [en]", vm.IbanError);
        Assert.Equal(string.Empty, vm.NameError);
    }

    [Fact]
    public async Task BlankName_ShowsError()
    {
        var vm = await CreateNewAsync();

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Empty(_store.Saved);
        Assert.Equal("Error_supplier_name_required [en]", vm.NameError);
    }

    [Fact]
    public async Task InvalidBic_ShowsError()
    {
        var vm = await CreateNewAsync();
        vm.Name = "Supplier";
        vm.Bic = "123";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Error_payment_bic_invalid [en]", vm.BicError);
    }

    [Fact]
    public async Task OpenLink_CallsLauncherWithTheUri()
    {
        var vm = await CreateNewAsync();
        await AddLink(vm, LinkKind.Website, "", "https://example.com/portal");

        await vm.OpenLinkCommand.ExecuteAsync(vm.Links[0]);

        Assert.Equal(new Uri("https://example.com/portal"), Assert.Single(_launcher.Opened));
        Assert.False(vm.Links[0].HasError);
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("ftp://example.com")]
    [InlineData("javascript:alert(1)")]
    public async Task OpenLink_WithInvalidUrl_DoesNotLaunchAndShowsError(string url)
    {
        var supplier = new Supplier { Name = "Supplier", Links = [new Link { Url = url }] };
        _store.Stored.Add(supplier);
        var vm = Create();
        await vm.LoadAsync(supplier.Id);

        await vm.OpenLinkCommand.ExecuteAsync(vm.Links[0]);

        Assert.Empty(_launcher.Opened);
        Assert.Equal("Error_link_url_invalid [en]", vm.Links[0].ErrorText);
    }

    [Fact]
    public async Task Save_WithInvalidLinkUrl_ShowsErrorOnTheRow()
    {
        var supplier = new Supplier { Name = "Supplier", Links = [new Link { Url = "https://example.com" }, new Link { Url = "oops" }] };
        _store.Stored.Add(supplier);
        var vm = Create();
        await vm.LoadAsync(supplier.Id);
        vm.Name = "Supplier 2";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Empty(_store.Saved);
        Assert.Equal(string.Empty, vm.Links[0].ErrorText);
        Assert.Equal("Error_link_url_invalid [en]", vm.Links[1].ErrorText);
    }

    [Fact]
    public async Task LinkDialog_RejectsInvalidUrlAndKeepsOpen()
    {
        var vm = await CreateNewAsync();
        _dialogs.EditorScript = e =>
        {
            var editor = (LinkEditorViewModel)e;
            editor.Url = "nonsense";
            editor.SaveCommand.Execute(null);
        };

        await vm.AddLinkCommand.ExecuteAsync(null);

        Assert.Empty(vm.Links);
        var dialog = (LinkEditorViewModel)_dialogs.LastEditor!;
        Assert.False(dialog.Saved);
        Assert.Equal("Error_link_url_invalid [en]", dialog.UrlError);
    }

    [Fact]
    public async Task EditLink_ChangesTheRowAndMarksDirty()
    {
        var supplier = new Supplier { Name = "Supplier", Links = [new Link { Kind = LinkKind.Website, Url = "https://example.com" }] };
        _store.Stored.Add(supplier);
        var vm = Create();
        await vm.LoadAsync(supplier.Id);
        _dialogs.EditorScript = e =>
        {
            var editor = (LinkEditorViewModel)e;
            Assert.Equal("https://example.com", editor.Url);
            editor.SelectedKind = editor.Kinds.First(k => k.Value.Equals(LinkKind.Tariffs));
            editor.LinkTitle = "Prices";
            editor.SaveCommand.Execute(null);
        };

        await vm.EditLinkCommand.ExecuteAsync(vm.Links[0]);

        Assert.Equal(LinkKind.Tariffs, vm.Links[0].Kind);
        Assert.Equal("Prices", vm.Links[0].DisplayText);
        Assert.True(vm.IsDirty);
    }

    [Fact]
    public async Task LinkDisplayText_IsTitleOrLocalizedKind()
    {
        var vm = await CreateNewAsync();
        await AddLink(vm, LinkKind.PayOnline, "", "https://example.com/pay");
        await AddLink(vm, LinkKind.Other, "Support", "https://example.com/help");

        Assert.Equal("Enum_LinkKind_PayOnline [en]", vm.Links[0].DisplayText);
        Assert.Equal("Support", vm.Links[1].DisplayText);
    }

    [Fact]
    public async Task RemoveLink_AsksForConfirmation()
    {
        var vm = await CreateNewAsync();
        await AddLink(vm, LinkKind.Website, "", "https://example.com");
        await AddLink(vm, LinkKind.Tariffs, "", "https://example.com/t");
        _dialogs.ConfirmResult = false;

        await vm.RemoveLinkCommand.ExecuteAsync(vm.Links[0]);
        Assert.Equal(2, vm.Links.Count);

        _dialogs.ConfirmResult = true;
        await vm.RemoveLinkCommand.ExecuteAsync(vm.Links[0]);

        Assert.Single(vm.Links);
        Assert.Equal(LinkKind.Tariffs, vm.Links[0].Kind);
        Assert.Equal(2, _dialogs.Confirmations.Count);
    }

    [Fact]
    public async Task PurposePreview_UsesSampleValuesAndFollowsTheTemplate()
    {
        var vm = await CreateNewAsync();
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        Assert.Equal(string.Empty, vm.PurposePreview);
        Assert.False(vm.HasPurposePreview);

        vm.PurposeTemplate = "Abon. Nr. {CustomerNumber}, {Period:MM.yyyy}";

        Assert.Equal("Abon. Nr. 12345, 10.2026", vm.PurposePreview);
        Assert.True(vm.HasPurposePreview);
        Assert.Contains(nameof(SupplierEditViewModel.PurposePreview), changed);

        vm.PurposeTemplate = "{PropertyName}: {Amount} EUR";

        Assert.Equal("Sample: 59.49 EUR", vm.PurposePreview);
    }

    [Fact]
    public async Task CopyButtons_PutTextOnClipboardAndShowToast()
    {
        var vm = await CreateNewAsync();
        vm.IbanText = ValidIbanFormatted;
        vm.RecipientName = " Water utility Ltd ";
        vm.PurposeTemplate = "Abon. Nr. {CustomerNumber}, {Period:MM.yyyy}";

        await vm.CopyIbanCommand.ExecuteAsync(null);
        await vm.CopyRecipientCommand.ExecuteAsync(null);
        await vm.CopyPurposeCommand.ExecuteAsync(null);

        Assert.Equal([ValidIban, "Water utility Ltd", "Abon. Nr. 12345, 10.2026"], _clipboard.Texts);
        Assert.Equal(["Copied [en]", "Copied [en]", "Copied [en]"], _toast.Messages);
    }

    [Fact]
    public async Task Copy_OfEmptyField_DoesNothing()
    {
        var vm = await CreateNewAsync();

        await vm.CopyIbanCommand.ExecuteAsync(null);

        Assert.Empty(_clipboard.Texts);
        Assert.Empty(_toast.Messages);
    }

    [Fact]
    public async Task Delete_SoftDeletesAfterConfirmation()
    {
        var supplier = new Supplier { Name = "Old supplier" };
        _store.Stored.Add(supplier);
        var vm = Create();
        await vm.LoadAsync(supplier.Id);

        await vm.DeleteCommand.ExecuteAsync(null);

        Assert.Equal([supplier.Id], _store.Deleted);
        Assert.Equal("Dialog_Delete_Title Old supplier [en]", _dialogs.Confirmations[0].Title);
    }

    [Fact]
    public async Task Cancel_WithUnsavedChanges_AsksAndStaysWhenCancelled()
    {
        var vm = await CreateNewAsync();
        vm.Name = "Draft";
        _dialogs.UnsavedChoice = UnsavedChangesChoice.Cancel;
        var outcome = (EditOutcome?)null;
        vm.Finished = o =>
        {
            outcome = o;
            return Task.CompletedTask;
        };

        await vm.CancelCommand.ExecuteAsync(null);

        Assert.Equal(1, _dialogs.UnsavedAsked);
        Assert.Null(outcome);
    }

    [Fact]
    public async Task Save_InPage_StaysOpen_AndCancelGoesBack()
    {
        var vm = await CreateNewAsync();
        vm.IsHostedInPage = true;
        vm.Name = "Supplier";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(0, _navigation.BackCount);
        Assert.False(vm.IsDirty);
        Assert.Equal("Supplier", vm.Title);

        await vm.CancelCommand.ExecuteAsync(null);

        Assert.Equal(1, _navigation.BackCount);
    }

    [Fact]
    public async Task DeleteVisibility_FollowsHostingAndNewState()
    {
        var vm = await CreateNewAsync();
        vm.IsHostedInPage = true;
        Assert.False(vm.ShowFormDelete);

        vm.Name = "Supplier";
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.True(vm.ShowFormDelete);
        Assert.False(vm.ShowFooter);
    }

    [Fact]
    public async Task Texts_AreRefreshedAfterLanguageChange()
    {
        var vm = await CreateNewAsync();
        await AddLink(vm, LinkKind.Website, "", "https://example.com");

        _language.SetLanguage("ru");

        Assert.Equal("SupplierName [ru]", vm.NameLabel);
        Assert.Equal("Enum_LinkKind_Website [ru]", vm.Links[0].DisplayText);
    }

    private SupplierEditViewModel Create() =>
        new(_store, _clock, new FakeLocalizer(_language), _dialogs, _navigation, _launcher, _clipboard, _toast);

    private async Task<SupplierEditViewModel> CreateNewAsync()
    {
        var vm = Create();
        await vm.LoadAsync(null);
        return vm;
    }

    private async Task AddLink(SupplierEditViewModel vm, LinkKind kind, string title, string url)
    {
        _dialogs.EditorScript = e =>
        {
            var editor = (LinkEditorViewModel)e;
            editor.SelectedKind = editor.Kinds.First(k => k.Value.Equals(kind));
            editor.LinkTitle = title;
            editor.Url = url;
            editor.SaveCommand.Execute(null);
        };
        await vm.AddLinkCommand.ExecuteAsync(null);
    }

    [Fact]
    public async Task InvalidPurposeFormat_ShowsErrorUnderTheField_PreviewShowsTheTemplateAsTyped_AndNothingIsSaved()
    {
        var vm = await CreateNewAsync();
        vm.Name = "Supplier";
        vm.PurposeTemplate = "Abon. Nr. {CustomerNumber}, {Period:mm.yyyy}";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Empty(_store.Saved);
        Assert.StartsWith("Error_payment_purpose_invalid_format", vm.PurposeTemplateError);
        Assert.Equal("Abon. Nr. 12345, {Period:mm.yyyy}", vm.PurposePreview);

        vm.PurposeTemplate = "Abon. Nr. {CustomerNumber}, {Period:MM.yyyy}";
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(string.Empty, vm.PurposeTemplateError);
        Assert.Single(_store.Saved);
        Assert.Equal("Abon. Nr. 12345, 10.2026", vm.PurposePreview);
    }
}
