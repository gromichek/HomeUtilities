using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Billing;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

// The "Bills" card of the contract editor: the last bills of the contract, a link to all of them and a button for a new one.
public sealed partial class ContractEditViewModel
{
    /// <summary>How many bills the card lists.</summary>
    public const int BillsShown = 6;

    private readonly IBillStore _billStore;
    private readonly IPaymentStore _paymentStore;
    private IReadOnlyList<(Bill Bill, BillState State)> _recentBills = [];

    [ObservableProperty]
    private IReadOnlyList<ContractBillRowViewModel> _billRows = [];

    [ObservableProperty]
    private string _billsHeader = string.Empty;

    [ObservableProperty]
    private string _billsEmptyText = string.Empty;

    [ObservableProperty]
    private string _allBillsText = string.Empty;

    [ObservableProperty]
    private string _addBillText = string.Empty;

    /// <summary>The card is shown for a saved contract.</summary>
    public bool ShowBillsCard => !IsNew;

    public bool HasBills => BillRows.Count > 0;

    public bool HasNoBills => BillRows.Count == 0;

    /// <summary>Loads the newest bills of the contract with their payment state.</summary>
    private async Task LoadBillsAsync()
    {
        if (IsNew)
        {
            _recentBills = [];
        }
        else
        {
            var recent = (await _billStore.ListByContractAsync(_contract.Id))
                .OrderByDescending(b => b.Period)
                .ThenByDescending(b => b.CreatedAt)
                .Take(BillsShown)
                .ToList();
            var rows = new List<(Bill, BillState)>();
            foreach (var bill in recent)
            {
                rows.Add((bill, BillStatus.Of(bill, await _paymentStore.ListByBillAsync(bill.Id), _clock.Today)));
            }

            _recentBills = rows;
        }

        RebuildBills();
        await LoadBalanceAsync();
    }

    private void RebuildBills()
    {
        BillRows =
        [
            .. _recentBills.Select(r => new ContractBillRowViewModel(
                r.Bill.Id,
                BillText.Period(r.Bill.Period),
                BillText.Money(r.Bill.Gross, _localizer["Currency_Symbol_" + r.Bill.Currency]),
                BillBadge.KindOf(r.State),
                BillBadge.TextOf(_localizer, r.State, _localizer["Currency_Symbol_" + r.Bill.Currency]),
                () => _ = RunSafeAsync(() => _navigation.GoToAsync(
                    Routes.BillEdit,
                    new Dictionary<string, object> { [EditorNavigation.IdParameter] = r.Bill.Id })))),
        ];
        OnPropertyChanged(nameof(ShowBillsCard));
        OnPropertyChanged(nameof(HasBills));
        OnPropertyChanged(nameof(HasNoBills));
    }

    /// <summary>"All bills": the Bills screen for the property with this contract highlighted.</summary>
    [RelayCommand]
    private Task OpenAllBillsAsync() => RunSafeAsync(() => _navigation.GoToAsync(
        Routes.BillsFor,
        new Dictionary<string, object>
        {
            [EditorNavigation.PropertyIdParameter] = _contract.PropertyId,
            [EditorNavigation.ContractIdParameter] = _contract.Id,
        }));

    /// <summary>"＋": a new bill for this contract.</summary>
    [RelayCommand]
    private Task AddBillAsync() => RunSafeAsync(() => _navigation.GoToAsync(
        Routes.BillEdit,
        new Dictionary<string, object>
        {
            [EditorNavigation.ContractIdParameter] = _contract.Id,
            [EditorNavigation.PropertyIdParameter] = _contract.PropertyId,
        }));
}
