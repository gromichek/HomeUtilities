using System.Globalization;
using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels.Tests;

/// <summary>Draft bills in the Bills list and the bill editor, the Confirm action and "From template". Today is 27 Oct 2026.</summary>
public class DraftBillsTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 10, 27);

    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
    private readonly BillTestKit _kit = new(Today);

    public DraftBillsTests() => CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

    public void Dispose() => CultureInfo.CurrentCulture = _culture;

    private BillTemplate AddTemplate(string name = "Internet + TV", Contract? contract = null, decimal? gross = 22.95m, ScheduleKind kind = ScheduleKind.Monthly)
    {
        var template = new BillTemplate
        {
            Name = name,
            PropertyId = _kit.Kit.Property.Id,
            CategoryId = _kit.Kit.CategoryIdFor(ServiceKind.InternetTv),
            ContractId = contract?.Id,
            Gross = gross,
            Timing = BillingTiming.InAdvance,
            DueDayOffset = 14,
            Schedule = new Schedule { Kind = kind, StartMonth = kind == ScheduleKind.Once ? null : new YearMonth(2026, 1), DayOfMonth = 5 },
        };
        _kit.Templates.All.Add(template);
        return template;
    }

    private Bill AddDraft(Contract? contract, YearMonth period, decimal gross, BillTemplate? template = null)
    {
        var bill = contract is null
            ? Bill.CreateOneOff(_kit.Kit.Property.Id, _kit.Kit.CategoryIdFor(ServiceKind.InternetTv), "Internet and TV", period, _kit.Kit.Clock)
            : Bill.CreateFor(contract, period, _kit.Kit.Clock);
        bill.Gross = gross;
        bill.IsDraft = true;
        bill.TemplateId = template?.Id;
        bill.DueOn = new DateOnly(2026, 10, 19);
        _kit.Bills.All.Add(bill);
        return bill;
    }

    private async Task<BillsViewModel> LoadBillsAsync()
    {
        var vm = _kit.CreateBills();
        await vm.ReloadAsync();
        return vm;
    }

    private async Task<BillEditViewModel> OpenAsync(Guid id)
    {
        var vm = _kit.CreateEditor();
        await vm.LoadAsync(id);
        return vm;
    }

    // ---- Bills list ----------------------------------------------------------------------------------------------

    [Fact]
    public async Task Reloading_the_list_first_asks_the_templates_for_the_drafts_that_came_due()
    {
        await LoadBillsAsync();

        Assert.Equal([Today], _kit.Templates.Generated);
    }

    [Fact]
    public async Task A_draft_is_in_the_list_with_a_draft_badge_but_cannot_be_marked_paid()
    {
        var internet = _kit.AddWaterContract("Internet", tariff: false);
        var draft = AddDraft(internet, new YearMonth(2026, 11), 22.95m);
        var bill = _kit.AddBill(internet, new YearMonth(2026, 10), 22.95m, due: new DateOnly(2026, 10, 20));

        var vm = await LoadBillsAsync();

        var item = Assert.Single(vm.Items, i => i.Id == draft.Id);
        Assert.True(item.IsDraft);
        Assert.Equal(BillBadgeKind.Draft, item.Badge);
        Assert.Equal("BillBadgeDraft [en]", item.BadgeText);
        Assert.False(item.CanMarkPaid);
        Assert.Contains(vm.Items, i => i.Id == bill.Id && !i.IsDraft);
    }

    [Fact]
    public async Task A_draft_without_an_amount_says_so()
    {
        var internet = _kit.AddWaterContract("Internet", tariff: false);
        AddDraft(internet, new YearMonth(2026, 11), 0m);

        var vm = await LoadBillsAsync();

        Assert.Equal("BillBadgeDraftAmount [en]", Assert.Single(vm.Items).BadgeText);
    }

    [Fact]
    public async Task Totals_leave_the_drafts_out()
    {
        var internet = _kit.AddWaterContract("Internet", tariff: false);
        _kit.AddBill(internet, new YearMonth(2026, 10), 20m, due: new DateOnly(2026, 11, 20));
        AddDraft(internet, new YearMonth(2026, 11), 99m);

        var vm = await LoadBillsAsync();

        Assert.Equal(2, vm.Items.Count);
        Assert.Equal("BillTotals 20.00 Currency_Symbol_EUR [en]|0.00 Currency_Symbol_EUR [en]|20.00 Currency_Symbol_EUR [en] [en]".Replace(" [en]|", "|"), vm.TotalsText.Replace(" [en]|", "|"));
    }

    [Fact]
    public async Task The_drafts_filter_shows_only_drafts_and_the_other_status_filters_hide_them()
    {
        var internet = _kit.AddWaterContract("Internet", tariff: false);
        var open = _kit.AddBill(internet, new YearMonth(2026, 10), 20m, due: new DateOnly(2026, 11, 20));
        var draft = AddDraft(internet, new YearMonth(2026, 11), 22.95m);
        var vm = await LoadBillsAsync();

        _kit.Filter.Status = BillStatusFilter.Drafts;
        vm = await LoadBillsAsync();
        Assert.Equal([draft.Id], vm.Items.Select(i => i.Id));

        foreach (var status in new[] { BillStatusFilter.Open, BillStatusFilter.Overdue, BillStatusFilter.Paid })
        {
            _kit.Filter.Status = status;
            vm = await LoadBillsAsync();
            Assert.DoesNotContain(vm.Items, i => i.Id == draft.Id);
        }

        _kit.Filter.Status = BillStatusFilter.Open;
        vm = await LoadBillsAsync();
        Assert.Equal([open.Id], vm.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task Confirm_on_a_row_makes_the_draft_a_normal_bill()
    {
        var internet = _kit.AddWaterContract("Internet", tariff: false);
        var draft = AddDraft(internet, new YearMonth(2026, 11), 22.95m);
        var vm = await LoadBillsAsync();

        vm.Items.Single().ConfirmCommand.Execute(null);
        await vm.ConfirmDraftAsync(draft.Id);

        var saved = _kit.Bills.Saved.Last();
        Assert.Equal(draft.Id, saved.Id);
        Assert.False(saved.IsDraft);
        Assert.False(vm.Items.Single().IsDraft);
        Assert.Equal("BillConfirmed [en]", _kit.Kit.Toast.Messages.Last());
    }

    [Fact]
    public async Task Confirm_on_a_draft_without_an_amount_opens_the_editor_instead()
    {
        var internet = _kit.AddWaterContract("Internet", tariff: false);
        var draft = AddDraft(internet, new YearMonth(2026, 11), 0m);
        var vm = await LoadBillsAsync();

        await vm.ConfirmDraftAsync(draft.Id);

        Assert.Empty(_kit.Bills.Saved);
        Assert.True(_kit.Bills.All.Single().IsDraft);
        Assert.Equal(draft.Id, vm.SelectedEditor?.EntityId);
    }

    // ---- DraftService and the Paid quick action --------------------------------------------------------------------

    [Fact]
    public async Task DraftService_confirms_only_a_draft_with_an_amount()
    {
        var internet = _kit.AddWaterContract("Internet", tariff: false);
        var withAmount = AddDraft(internet, new YearMonth(2026, 11), 22.95m);
        var empty = AddDraft(internet, new YearMonth(2026, 12), 0m);
        var normal = _kit.AddBill(internet, new YearMonth(2026, 10), 20m);

        Assert.Equal(DraftConfirmResult.AmountRequired, await _kit.Drafts.ConfirmAsync(empty.Id));
        Assert.Equal(DraftConfirmResult.NotADraft, await _kit.Drafts.ConfirmAsync(normal.Id));
        Assert.Equal(DraftConfirmResult.NotADraft, await _kit.Drafts.ConfirmAsync(Guid.NewGuid()));
        Assert.Equal(DraftConfirmResult.Confirmed, await _kit.Drafts.ConfirmAsync(withAmount.Id));
        Assert.False(_kit.Bills.All.Single(b => b.Id == withAmount.Id).IsDraft);
        Assert.True(_kit.Bills.All.Single(b => b.Id == empty.Id).IsDraft);
    }

    [Fact]
    public async Task Paid_does_nothing_on_a_draft()
    {
        var internet = _kit.AddWaterContract("Internet", tariff: false);
        var draft = AddDraft(internet, new YearMonth(2026, 11), 22.95m);

        var payment = await _kit.QuickPay.MarkPaidAsync(draft.Id);

        Assert.Null(payment);
        Assert.Empty(_kit.Payments.Saved);
    }

    [Fact]
    public async Task The_payment_editor_does_not_offer_a_draft_for_allocation()
    {
        var internet = _kit.AddWaterContract("Internet", tariff: false);
        AddDraft(internet, new YearMonth(2026, 11), 22.95m);
        var normal = _kit.AddBill(internet, new YearMonth(2026, 10), 20m, due: new DateOnly(2026, 10, 20));
        var editor = _kit.CreatePaymentEditor();
        editor.PresetContractId = internet.Id;
        await editor.LoadAsync(null);
        editor.AmountText = "20";
        IReadOnlyList<string>? offered = null;
        _kit.Kit.Dialogs.ChooseScript = options =>
        {
            offered = options;
            return null;
        };

        await editor.AddAllocationCommand.ExecuteAsync(null);

        // only the confirmed bill is open for the payment (the draft is not a bill yet)
        Assert.NotNull(offered);
        Assert.Single(offered);
        Assert.Contains("PaymentBillOption", offered[0], StringComparison.Ordinal);
        Assert.Contains("10.2026", offered[0], StringComparison.Ordinal);
    }

    // ---- Bill editor -----------------------------------------------------------------------------------------------

    [Fact]
    public async Task The_editor_of_a_draft_shows_the_banner_and_no_payment_card()
    {
        var template = AddTemplate();
        var draft = AddDraft(null, new YearMonth(2026, 11), 22.95m, template);

        var vm = await OpenAsync(draft.Id);

        Assert.True(vm.IsDraft);
        Assert.True(vm.ShowDraftBanner);
        Assert.False(vm.ShowPaymentCard);
        Assert.Equal("BillDraftBanner Internet + TV [en]", vm.DraftBannerText);
    }

    [Fact]
    public async Task Save_keeps_a_draft_a_draft()
    {
        var draft = AddDraft(null, new YearMonth(2026, 11), 0m);
        var vm = await OpenAsync(draft.Id);

        vm.Amounts.GrossText = "24.20";
        await vm.SaveCommand.ExecuteAsync(null);

        var saved = _kit.Bills.Saved.Last();
        Assert.True(saved.IsDraft);
        Assert.Equal(24.20m, saved.Gross);
        Assert.True(vm.IsDraft);
    }

    [Fact]
    public async Task Confirm_saves_the_form_as_a_normal_bill()
    {
        var draft = AddDraft(null, new YearMonth(2026, 11), 0m);
        var vm = await OpenAsync(draft.Id);
        vm.Amounts.GrossText = "24.20";

        await vm.ConfirmDraftCommand.ExecuteAsync(null);

        var saved = _kit.Bills.Saved.Last();
        Assert.False(saved.IsDraft);
        Assert.Equal(24.20m, saved.Gross);
        Assert.False(vm.IsDraft);
        Assert.True(vm.ShowPaymentCard);
        Assert.Equal("BillConfirmed [en]", _kit.Kit.Toast.Messages.Last());
    }

    [Fact]
    public async Task Confirm_needs_an_amount()
    {
        var draft = AddDraft(null, new YearMonth(2026, 11), 0m);
        var vm = await OpenAsync(draft.Id);

        await vm.ConfirmDraftCommand.ExecuteAsync(null);

        Assert.Empty(_kit.Bills.Saved);
        Assert.True(vm.IsDraft);
        Assert.True(vm.HasErrors);
        Assert.False(string.IsNullOrEmpty(vm.GrossError));
    }

    [Fact]
    public async Task A_new_bill_can_be_filled_from_a_scheduled_template()
    {
        var template = AddTemplate();
        template.Lines = [new BillTemplateLine { Description = "TV package", Gross = 5m }, new BillTemplateLine { Description = "Internet", Gross = 17.95m }];
        template.CustomFields = [new CustomFieldDefinition { OwnerId = template.Id, Name = "Reference", Kind = CustomFieldKind.Text, Target = CustomFieldTarget.Bill }];
        var vm = _kit.CreateEditor();
        await vm.LoadAsync(null);
        Assert.True(vm.ShowFromTemplate);
        _kit.Kit.Dialogs.ChooseScript = options => options.Contains("Internet + TV") ? "Internet + TV" : null;

        await vm.FromTemplateCommand.ExecuteAsync(null);

        // prepaid: next month; issued 05.10.2026 (day 5 of the month before 2026-11), due 14 days later
        Assert.True(vm.IsOneOff);
        Assert.Equal(new YearMonth(2026, 11), vm.Period);
        Assert.Equal("Internet + TV", vm.BillTitle);
        Assert.Equal("22.95", vm.Amounts.GrossText);
        Assert.Equal((new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 19)), (vm.IssuedOn, vm.DueOn));
        Assert.Equal(["TV package", "Internet"], vm.LineRows.Select(l => l.Description));
        Assert.Equal("Reference", Assert.Single(vm.FieldRows).Name);
        Assert.Equal("Enum_ServiceKind_InternetTv [en]", vm.SelectedCategoryName);

        await vm.SaveCommand.ExecuteAsync(null);
        var saved = _kit.Bills.Saved.Last();
        Assert.False(saved.IsDraft);
        Assert.Equal(template.Id, saved.TemplateId);
        Assert.Equal(22.95m, saved.Gross);
    }

    [Fact]
    public async Task From_template_with_a_once_template_starts_in_the_current_month()
    {
        var once = AddTemplate("Water meter replacement", gross: 34m, kind: ScheduleKind.Once);
        once.Timing = BillingTiming.InArrears;
        var vm = _kit.CreateEditor();
        await vm.LoadAsync(null);
        _kit.Kit.Dialogs.ChooseScript = options => options.Contains(once.Name) ? once.Name : null;

        await vm.FromTemplateCommand.ExecuteAsync(null);

        Assert.Equal(new YearMonth(2026, 10), vm.Period);
        Assert.Equal("34.00", vm.Amounts.GrossText);
    }

    [Fact]
    public async Task Changing_the_period_after_a_template_recomputes_its_dates_until_the_user_edits_them()
    {
        var template = AddTemplate();
        var vm = _kit.CreateEditor();
        await vm.LoadAsync(null);
        _kit.Kit.Dialogs.ChooseScript = options => options.Contains(template.Name) ? template.Name : null;
        await vm.FromTemplateCommand.ExecuteAsync(null);

        vm.NextPeriodCommand.Execute(null);

        // period 2026-12 prepaid: issued 05.11.2026, due 19.11.2026
        Assert.Equal((new DateOnly(2026, 11, 5), new DateOnly(2026, 11, 19)), (vm.IssuedOn, vm.DueOn));
    }

    [Fact]
    public async Task From_template_is_not_offered_without_templates()
    {
        var vm = _kit.CreateEditor();
        await vm.LoadAsync(null);

        Assert.False(vm.ShowFromTemplate);
    }

    [Fact]
    public async Task Inactive_templates_are_not_offered()
    {
        AddTemplate().IsActive = false;
        var vm = _kit.CreateEditor();
        await vm.LoadAsync(null);

        Assert.False(vm.ShowFromTemplate);
    }

    [Fact]
    public async Task Save_as_template_opens_the_template_editor_with_the_bill()
    {
        var internet = _kit.AddWaterContract("Internet", tariff: false);
        var bill = _kit.AddBill(internet, new YearMonth(2026, 10), 22.95m);
        var vm = await OpenAsync(bill.Id);

        await vm.SaveAsTemplateCommand.ExecuteAsync(null);

        var (route, parameters) = Assert.Single(_kit.Kit.Navigation.Visited);
        Assert.Equal(Routes.BillTemplateEdit, route);
        var editor = Assert.IsType<BillTemplateEditViewModel>(parameters![EditorNavigation.EditorParameter]);
        Assert.True(editor.IsNew);
        Assert.Equal("Internet", editor.SelectedContractName);
        Assert.Equal("22.95", editor.Amounts.GrossText);
        Assert.Equal(new YearMonth(2026, 10), editor.StartMonth);
    }

    [Fact]
    public async Task Save_as_template_asks_to_save_the_bill_first()
    {
        var internet = _kit.AddWaterContract("Internet", tariff: false);
        var bill = _kit.AddBill(internet, new YearMonth(2026, 10), 22.95m);
        var vm = await OpenAsync(bill.Id);
        vm.BillTitle = "Changed";

        await vm.SaveAsTemplateCommand.ExecuteAsync(null);

        Assert.Empty(_kit.Kit.Navigation.Visited);
        Assert.Single(_kit.Kit.Dialogs.Alerts);
    }
}
