using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core.Billing;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>Whether the contract owes money, has been paid in advance or is settled.</summary>
public enum BalanceKind
{
    /// <summary>Paid exactly what was billed.</summary>
    Settled,

    /// <summary>Billed more than paid (red).</summary>
    Debt,

    /// <summary>Paid more than billed (green).</summary>
    PaidInAdvance,
}

// The "Balance" card of the contract editor: billed, paid, debt or paid in advance, the unallocated money with "Use for open
// bills", and the latest payments.
public sealed partial class ContractEditViewModel
{
    /// <summary>How many payments the card lists.</summary>
    public const int PaymentsShown = 6;

    private readonly PrepaymentService _prepayments;
    private Balance _balance = new(0m, 0m, 0m);
    private decimal _unallocated;
    private IReadOnlyList<Payment> _recentPayments = [];

    [ObservableProperty]
    private IReadOnlyList<ContractPaymentRowViewModel> _paymentRows = [];

    [ObservableProperty]
    private string _balanceHeader = string.Empty;

    [ObservableProperty]
    private string _balanceBilledLabel = string.Empty;

    [ObservableProperty]
    private string _balancePaidLabel = string.Empty;

    [ObservableProperty]
    private string _balanceBilledText = string.Empty;

    [ObservableProperty]
    private string _balancePaidText = string.Empty;

    [ObservableProperty]
    private string _balanceResultText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBalanceDebt), nameof(IsBalanceAdvance))]
    private BalanceKind _balanceKind;

    [ObservableProperty]
    private string _unallocatedText = string.Empty;

    [ObservableProperty]
    private string _useForOpenBillsText = string.Empty;

    [ObservableProperty]
    private string _paymentsEmptyText = string.Empty;

    [ObservableProperty]
    private string _allPaymentsText = string.Empty;

    [ObservableProperty]
    private string _addPaymentText = string.Empty;

    /// <summary>The card is shown for a saved contract.</summary>
    public bool ShowBalanceCard => !IsNew;

    public bool IsBalanceDebt => BalanceKind == BalanceKind.Debt;

    public bool IsBalanceAdvance => BalanceKind == BalanceKind.PaidInAdvance;

    /// <summary>Some payment has money no bill uses: the line and "Use for open bills" are shown.</summary>
    public bool HasUnallocated => _unallocated > 0;

    public bool HasPayments => PaymentRows.Count > 0;

    public bool HasNoPayments => PaymentRows.Count == 0;

    /// <summary>Re-reads the balance and the latest payments of the contract.</summary>
    private async Task LoadBalanceAsync()
    {
        if (IsNew)
        {
            _balance = new Balance(0m, 0m, 0m);
            _unallocated = 0m;
            _recentPayments = [];
        }
        else
        {
            var bills = await _billStore.ListByContractAsync(_contract.Id);
            var payments = await _paymentStore.ListByContractAsync(_contract.Id);
            _balance = ContractBalance.Of(_contract.Id, bills, payments, _clock.Today);
            _unallocated = payments
                .Where(p => p.ContractId == _contract.Id)
                .Sum(p => Math.Max(0m, PaymentRules.Unallocated(p)));
            _recentPayments = [.. payments.OrderByDescending(p => p.PaidOn).ThenByDescending(p => p.CreatedAt).Take(PaymentsShown)];
        }

        RebuildBalance();
    }

    private void RebuildBalance()
    {
        var symbol = _localizer["Currency_Symbol_" + _contract.Currency];
        BalanceBilledText = BillText.Money(_balance.Billed, symbol);
        BalancePaidText = BillText.Money(_balance.Paid, symbol);
        BalanceKind = _balance.Value < 0 ? BalanceKind.Debt : _balance.Value > 0 ? BalanceKind.PaidInAdvance : BalanceKind.Settled;
        BalanceResultText = BalanceKind switch
        {
            BalanceKind.Debt => _localizer.Get("ContractBalanceDebt", BillText.Money(-_balance.Value, symbol)),
            BalanceKind.PaidInAdvance => _localizer.Get("ContractBalanceAdvance", BillText.Money(_balance.Value, symbol)),
            _ => _localizer["ContractBalanceSettled"],
        };
        UnallocatedText = _localizer.Get("ContractBalanceUnallocated", BillText.Money(_unallocated, symbol));
        PaymentRows =
        [
            .. _recentPayments.Select(p => new ContractPaymentRowViewModel(
                p.Id,
                _localizer.Get(
                    "BillPaymentRow",
                    BillText.Date(p.PaidOn),
                    PaymentText.Amount(_localizer, p.Amount, _localizer["Currency_Symbol_" + p.Currency]),
                    EnumText.Get(_localizer, p.Method)),
                PaymentRules.Unallocated(p) is var open && open > 0 && p.ContractId == _contract.Id
                    ? _localizer.Get("PaymentPrepaymentBadge", BillText.Money(open, _localizer["Currency_Symbol_" + p.Currency]))
                    : string.Empty,
                () => _ = RunSafeAsync(() => _navigation.GoToAsync(
                    Routes.PaymentEdit,
                    new Dictionary<string, object> { [EditorNavigation.IdParameter] = p.Id })))),
        ];
        OnPropertyChanged(nameof(ShowBalanceCard));
        OnPropertyChanged(nameof(HasUnallocated));
        OnPropertyChanged(nameof(HasPayments));
        OnPropertyChanged(nameof(HasNoPayments));
    }

    /// <summary>"Use for open bills": spreads the unallocated money over the contract's open bills, oldest first; Undo takes it back.</summary>
    [RelayCommand]
    private Task UseForOpenBillsAsync() => RunSafeAsync(async () =>
    {
        var result = await _prepayments.UseForOpenBillsAsync(_contract.Id);
        await LoadBillsAsync();
        if (result is null)
        {
            _snackbar.Notify(_localizer["ContractBalanceNothingToUse"]);
            return;
        }

        if (await _snackbar.ShowAsync(_localizer["PrepaymentUsed"], _localizer["BillUndo"]))
        {
            await _prepayments.UndoAsync(result);
            await LoadBillsAsync();
        }
    });

    /// <summary>"All payments": the Payments screen for the property, filtered to this contract.</summary>
    [RelayCommand]
    private Task OpenAllPaymentsAsync() => RunSafeAsync(() => _navigation.GoToAsync(
        Routes.PaymentsFor,
        new Dictionary<string, object>
        {
            [EditorNavigation.PropertyIdParameter] = _contract.PropertyId,
            [EditorNavigation.ContractIdParameter] = _contract.Id,
        }));

    /// <summary>"＋": a new payment for this contract.</summary>
    [RelayCommand]
    private Task AddPaymentAsync() => RunSafeAsync(() => _navigation.GoToAsync(
        Routes.PaymentEdit,
        new Dictionary<string, object>
        {
            [EditorNavigation.ContractIdParameter] = _contract.Id,
            [EditorNavigation.PropertyIdParameter] = _contract.PropertyId,
        }));
}
