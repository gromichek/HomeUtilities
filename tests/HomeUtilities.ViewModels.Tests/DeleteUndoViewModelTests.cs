using CommunityToolkit.Mvvm.Messaging;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels.Tests;

/// <summary>
/// Single delete without a confirmation, with "deleted · Undo" (UI_GUIDELINES §5.1, T-036b). Records with dependants keep the
/// confirmation dialog. The exact recording and restoring of the rows is tested against the real database (Data tests).
/// </summary>
public class DeleteUndoViewModelTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 11, 6);

    private readonly BillTestKit _kit = new(Today);
    private readonly FakeDeleteUndoService _service = new();
    private int _restoredMessages;
    private readonly object _recipient = new();

    public DeleteUndoViewModelTests()
    {
        WeakReferenceMessenger.Default.Register<object, RecordsRestoredMessage>(_recipient, (_, _) => _restoredMessages++);
    }

    public void Dispose() => WeakReferenceMessenger.Default.UnregisterAll(_recipient);

    private async Task<(PaymentEditViewModel Editor, Payment Payment)> OpenPaymentAsync(bool withUndo = true)
    {
        var water = _kit.AddWaterContract("Water", tariff: false);
        var bill = _kit.AddBill(water, new YearMonth(2026, 10), 30m);
        var payment = new Payment { ContractId = water.Id, PropertyId = _kit.Kit.Property.Id, PaidOn = Today.AddDays(-2), Amount = 30m };
        payment.Allocations.Add(new PaymentAllocation { PaymentId = payment.Id, BillId = bill.Id, Amount = 30m });
        _kit.Payments.Add(payment);
        var editor = _kit.CreatePaymentEditor(withUndo ? _kit.Kit.CreateUndoSupport(_service) : null);
        await editor.LoadAsync(payment.Id);
        return (editor, payment);
    }

    [Fact]
    public async Task Deleting_a_payment_asks_nothing_and_offers_Undo()
    {
        var (editor, payment) = await OpenPaymentAsync();
        _service.Touched.AddRange([new DeletedRow("Payments", payment.Id), new DeletedRow("PaymentAllocations", Guid.NewGuid())]);
        EditOutcome? outcome = null;
        editor.Finished = o =>
        {
            outcome = o;
            return Task.CompletedTask;
        };

        await editor.DeleteCommand.ExecuteAsync(null);
        await editor.UndoOffer;

        Assert.Empty(_kit.Kit.Dialogs.Confirmations);
        Assert.Equal([payment.Id], _kit.Payments.Deleted);
        Assert.Equal(EditOutcome.Deleted, outcome);
        Assert.Equal(1, _kit.Kit.Navigation.BackCount);
        var snackbar = Assert.Single(_kit.Kit.Toast.Calls, c => c.Action is not null);
        Assert.Contains("DeletedUndoMessage", snackbar.Message, StringComparison.Ordinal);
        Assert.Equal("Undo [en]", snackbar.Action);
        Assert.Equal(TimeSpan.FromSeconds(6), snackbar.Duration);
        Assert.Empty(_service.Restored);
    }

    [Fact]
    public async Task Tapping_Undo_restores_exactly_the_rows_the_delete_touched_and_refreshes_the_lists()
    {
        var (editor, payment) = await OpenPaymentAsync();
        var touched = new[] { new DeletedRow("Payments", payment.Id), new DeletedRow("PaymentAllocations", Guid.NewGuid()) };
        _service.Touched.AddRange(touched);
        _kit.Kit.Toast.ActionResult = (_, _) => true;

        await editor.DeleteCommand.ExecuteAsync(null);
        await editor.UndoOffer;

        Assert.Equal(1, _service.Recordings);
        Assert.Equal(touched, Assert.Single(_service.Restored));
        Assert.Equal(1, _restoredMessages);
    }

    [Fact]
    public async Task Without_a_tap_on_Undo_nothing_is_restored()
    {
        var (editor, payment) = await OpenPaymentAsync();
        _service.Touched.Add(new DeletedRow("Payments", payment.Id));

        await editor.DeleteCommand.ExecuteAsync(null);
        await editor.UndoOffer;

        Assert.Empty(_service.Restored);
        Assert.Equal(0, _restoredMessages);
    }

    [Fact]
    public async Task A_delete_that_changed_no_rows_offers_no_Undo()
    {
        var (editor, _) = await OpenPaymentAsync();

        await editor.DeleteCommand.ExecuteAsync(null);
        await editor.UndoOffer;

        Assert.DoesNotContain(_kit.Kit.Toast.Calls, c => c.Action is not null);
    }

    [Fact]
    public async Task An_editor_without_Undo_still_asks_before_deleting()
    {
        var (editor, payment) = await OpenPaymentAsync(withUndo: false);
        _kit.Kit.Dialogs.ConfirmResult = false;

        await editor.DeleteCommand.ExecuteAsync(null);

        Assert.Single(_kit.Kit.Dialogs.Confirmations);
        Assert.Empty(_kit.Payments.Deleted);

        _kit.Kit.Dialogs.ConfirmResult = true;
        await editor.DeleteCommand.ExecuteAsync(null);

        Assert.Equal([payment.Id], _kit.Payments.Deleted);
        Assert.DoesNotContain(_kit.Kit.Toast.Calls, c => c.Action is not null);
    }

    [Fact]
    public async Task A_category_editor_with_Undo_deletes_without_asking_and_restores_on_Undo()
    {
        var category = new Category { Name = "Own" };
        var categories = new FakeCategoryStore();
        categories.Stored.Add(category);
        var language = new FakeLanguageService();
        var dialogs = new FakeDialogService();
        var snackbar = new FakeSnackbarService();
        var undo = new DeleteUndoSupport(_service, snackbar, new FakeLocalizer(language));
        var vm = new CategoryEditViewModel(categories, new FakeLocalizer(language), dialogs, new FakeNavigationService(), snackbar, undo);
        await vm.LoadAsync(category.Id);
        _service.Touched.Add(new DeletedRow("Categories", category.Id));
        snackbar.ActionResult = (_, _) => true;

        await vm.DeleteCommand.ExecuteAsync(null);
        await vm.UndoOffer;

        Assert.Empty(dialogs.Confirmations);
        Assert.Equal([category.Id], categories.Deleted);
        Assert.Equal([new DeletedRow("Categories", category.Id)], Assert.Single(_service.Restored));
    }
}
