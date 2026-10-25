using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Payments;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>
/// Editor of one payment: the general data (contract or property, date, amount, method, reference) and the bills it settles
/// (allocations). The part of the amount that settles no bill is a prepayment. Shown in the side pane of the Payments screen
/// or as a page (<see cref="Routes.PaymentEdit"/>).
/// </summary>
public sealed partial class PaymentEditViewModel : PaneEditorViewModel
{
    protected override HelpTopic? GuideTopic => HelpTopic.Payments;

    /// <summary>The properties that mark the editor dirty when the user changes them.</summary>
    private static readonly HashSet<string> DirtyFields =
    [
        nameof(SelectedProperty), nameof(SelectedContract), nameof(PaidOn), nameof(AmountText), nameof(SelectedMethod),
        nameof(Reference), nameof(Notes),
    ];

    private readonly IPaymentStore _payments;
    private readonly IBillStore _bills;
    private readonly IContractStore _contractStore;
    private readonly ISupplierStore _supplierStore;
    private readonly IPropertyStore _propertyStore;
    private readonly IClock _clock;
    private readonly ILocalizer _localizer;
    private readonly ISnackbarService _snackbar;

    private Payment _payment = new();
    private Payment? _pending;
    private readonly List<PaymentAllocation> _allocations = [];
    private IReadOnlyList<Property> _properties = [];
    private IReadOnlyList<Contract> _contracts = [];
    private Dictionary<Guid, Contract> _contractsById = [];
    private Dictionary<Guid, Bill> _billsById = [];
    private Dictionary<Guid, decimal> _paidByOthers = [];
    private Currency _currency = Currency.EUR;

    [ObservableProperty]
    private PaymentEditTexts _texts;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedPropertyName), nameof(ShowPropertyChooser))]
    private ChoiceOption? _selectedProperty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedContractName), nameof(ShowFillReference))]
    private ChoiceOption? _selectedContract;

    [ObservableProperty]
    private DateOnly _paidOn;

    [ObservableProperty]
    private string _amountText = string.Empty;

    [ObservableProperty]
    private string _currencySymbol = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedMethodIndex))]
    private IReadOnlyList<EnumOption> _methods = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedMethodIndex))]
    private EnumOption? _selectedMethod;

    [ObservableProperty]
    private string _reference = string.Empty;

    [ObservableProperty]
    private string _notes = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoAllocations), nameof(HasAllocations))]
    private IReadOnlyList<PaymentAllocationRowViewModel> _allocationRows = [];

    [ObservableProperty]
    private string _allocationSummary = string.Empty;

    [ObservableProperty]
    private bool _isOverAllocated;

    public PaymentEditViewModel(
        IPaymentStore payments,
        IBillStore bills,
        IContractStore contractStore,
        ISupplierStore supplierStore,
        IPropertyStore propertyStore,
        IClock clock,
        ILocalizer localizer,
        IDialogService dialogs,
        INavigationService navigation,
        ISnackbarService snackbar)
        : base(localizer, dialogs, navigation, snackbar)
    {
        _payments = payments;
        _bills = bills;
        _contractStore = contractStore;
        _supplierStore = supplierStore;
        _propertyStore = propertyStore;
        _clock = clock;
        _localizer = localizer;
        _snackbar = snackbar;
        _texts = new PaymentEditTexts(localizer);
        _paidOn = clock.Today;
        PropertyChanged += OnOwnPropertyChanged;
        RefreshTexts();
    }

    public override Guid EntityId => _payment.Id;

    // Presets of a new payment (from a bill, a contract or a property); set before LoadAsync(null).

    /// <summary>The bill a new payment starts with: its contract, the open amount, allocated in full.</summary>
    public Guid? PresetBillId { get; set; }

    /// <summary>The contract a new payment starts with.</summary>
    public Guid? PresetContractId { get; set; }

    /// <summary>The property a new payment starts with.</summary>
    public Guid? PresetPropertyId { get; set; }

    // Visibility.

    public bool ShowPropertyChooser => IsNew && _properties.Count > 1;

    public bool IsPropertyReadOnly => !ShowPropertyChooser;

    public bool IsContractEditable => IsNew;

    public bool IsContractReadOnly => !IsNew;

    public bool ShowFillReference => SelectedContract is not null;

    public bool ShowQuickNote => _payment.CreatedByQuickAction;

    public bool HasAllocations => AllocationRows.Count > 0;

    public bool HasNoAllocations => AllocationRows.Count == 0;

    // Derived texts.

    public string SelectedPropertyName => SelectedProperty?.Name ?? string.Empty;

    public string SelectedContractName => SelectedContract?.Name ?? _localizer["PaymentNoContract"];

    public int SelectedMethodIndex
    {
        get => SelectedMethod is null ? -1 : Methods.ToList().IndexOf(SelectedMethod);
        set
        {
            if (value >= 0 && value < Methods.Count)
            {
                SelectedMethod = Methods[value];
            }
        }
    }

    // Error texts of the fields.

    public string PropertyError => ErrorOf("PropertyId");

    public string AmountError => ErrorOf("Amount");

    public string ReferenceError => ErrorOf(nameof(Payment.Reference));

    public string NotesError => ErrorOf(nameof(Payment.Notes));

    public string AllocationsError => ErrorOf(nameof(Payment.Allocations));

    public bool HasAllocationsError => AllocationsError.Length > 0;

    public override async Task LoadAsync(Guid? id)
    {
        var previous = SuppressDirty;
        SuppressDirty = true;
        try
        {
            var existing = id is { } value ? await _payments.GetAsync(value) : null;
            _payment = existing ?? new Payment { PaidOn = _clock.Today, Method = PaymentMethod.BankTransfer };
            _pending = null;
            IsNew = existing is null;

            _contracts = await _contractStore.ListAsync();
            _contractsById = _contracts.ToDictionary(c => c.Id);
            _billsById = (await _bills.ListAsync()).ToDictionary(b => b.Id);
            _properties = await _propertyStore.ListAsync(false);
            if (existing is not null)
            {
                foreach (var propertyId in PropertyIdsOf(existing).Where(p => _properties.All(known => known.Id != p)))
                {
                    if (await _propertyStore.GetAsync(propertyId) is { } own)
                    {
                        _properties = [.. _properties, own];
                    }
                }
            }

            _paidByOthers = (await _payments.ListAsync())
                .Where(p => p.Id != _payment.Id)
                .SelectMany(p => p.Allocations)
                .Where(a => !a.IsDeleted)
                .GroupBy(a => a.BillId)
                .ToDictionary(g => g.Key, g => g.Sum(a => a.Amount));

            _allocations.Clear();
            Methods = EnumText.Options<PaymentMethod>(_localizer);
            if (existing is null)
            {
                await PrepareNewAsync();
            }
            else
            {
                ShowExisting(existing);
            }

            ClearErrors();
            Title = TitleText();
            IsDirty = false;
        }
        finally
        {
            SuppressDirty = previous;
        }

        RebuildAllocations();

        // A page binds before the data arrives: have every binding read the loaded state.
        OnPropertyChanged(string.Empty);
    }

    protected override IReadOnlyList<ValidationError> Validate()
    {
        var errors = new List<ValidationError>();
        var payment = BuildPayment(errors);
        errors.AddRange(CheckPayment(payment, errors.Any(e => e.Field == "Amount")));
        _pending = payment;
        return errors;
    }

    protected override async Task SaveCoreAsync()
    {
        var payment = _pending ?? throw new InvalidOperationException("Validate must run before saving.");
        PaymentRules.ApplyQuickFlagRule(payment);
        await _payments.SaveAsync(payment);
        _payment = payment;
        IsNew = false;
        Title = TitleText();
        OnPropertyChanged(nameof(ShowQuickNote));
        OnPropertyChanged(nameof(IsContractEditable));
        OnPropertyChanged(nameof(IsContractReadOnly));
        OnPropertyChanged(nameof(ShowPropertyChooser));
        OnPropertyChanged(nameof(IsPropertyReadOnly));
    }

    protected override async Task DeleteCoreAsync() => await _payments.DeleteAsync(_payment.Id);

    protected override string DeleteName() =>
        $"{PaymentText.Amount(_localizer, _payment.Amount, CurrencySymbol)} · {BillText.Date(_payment.PaidOn)}";

    protected override string DeleteMessage() =>
        _allocations.Count > 0 ? _localizer["PaymentDeleteWithBills"] : base.DeleteMessage();

    protected override void OnErrorsChanged()
    {
        foreach (var name in new[]
        {
            nameof(PropertyError), nameof(AmountError), nameof(ReferenceError), nameof(NotesError), nameof(AllocationsError),
            nameof(HasAllocationsError),
        })
        {
            OnPropertyChanged(name);
        }
    }

    protected override void RefreshTexts()
    {
        base.RefreshTexts();
        Texts = new PaymentEditTexts(_localizer);
        DeleteWholeText = Texts.DeletePaymentText;
        var current = SelectedMethod?.Value;
        Methods = EnumText.Options<PaymentMethod>(_localizer);
        SelectedMethod = current is null ? Methods[0] : Methods.FirstOrDefault(m => m.Value.Equals(current)) ?? Methods[0];
        OnPropertyChanged(nameof(SelectedContractName));
        if (_payment.Id != Guid.Empty)
        {
            Title = TitleText();
        }

        RebuildAllocations();
    }

    private string ErrorOf(string field) => Errors.GetValueOrDefault(field, string.Empty);

    private void OnOwnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not { } name)
        {
            return;
        }

        if (name == nameof(IsNew))
        {
            foreach (var derived in new[] { nameof(ShowPropertyChooser), nameof(IsPropertyReadOnly), nameof(IsContractEditable), nameof(IsContractReadOnly) })
            {
                OnPropertyChanged(derived);
            }
        }

        if (DirtyFields.Contains(name))
        {
            MarkDirty();
        }
    }

    partial void OnAmountTextChanged(string value) => UpdateSummary();

    // ---- Loading --------------------------------------------------------------------------------------------------

    private async Task PrepareNewAsync()
    {
        _currency = Currency.EUR;
        PaidOn = _clock.Today;
        AmountText = string.Empty;
        Reference = string.Empty;
        Notes = string.Empty;
        SelectedProperty = null;
        SelectedContract = null;
        SelectedMethod = Methods.First(m => m.Value.Equals(PaymentMethod.BankTransfer));

        var bill = PresetBillId is { } billId && _billsById.TryGetValue(billId, out var found) ? found : null;
        var contract = bill?.ContractId is { } billContract
            ? _contractsById.GetValueOrDefault(billContract)
            : PresetContractId is { } contractId ? _contractsById.GetValueOrDefault(contractId) : null;
        var propertyId = bill?.PropertyId ?? contract?.PropertyId ?? PresetPropertyId ?? (_properties.Count == 1 ? _properties[0].Id : (Guid?)null);

        SelectedProperty = _properties.Where(p => p.Id == propertyId).Select(PropertyOption).FirstOrDefault();
        if (contract is not null)
        {
            SelectedContract = ContractOption(contract);
            _currency = contract.Currency;
        }

        if (bill is not null)
        {
            _currency = bill.Currency;
            var open = OpenOf(bill);
            if (open != 0)
            {
                AmountText = PaymentText.Field(open);
                _allocations.Add(new PaymentAllocation { PaymentId = _payment.Id, BillId = bill.Id, Amount = open });
            }

            if (contract is not null && await PurposeAsync(contract) is { } purpose)
            {
                Reference = purpose;
            }
        }

        CurrencySymbol = _localizer["Currency_Symbol_" + _currency];
    }

    private void ShowExisting(Payment existing)
    {
        _currency = existing.Currency;
        CurrencySymbol = _localizer["Currency_Symbol_" + _currency];
        PaidOn = existing.PaidOn;
        AmountText = PaymentText.Field(existing.Amount);
        Reference = existing.Reference ?? string.Empty;
        Notes = existing.Notes ?? string.Empty;
        SelectedMethod = Methods.FirstOrDefault(m => m.Value.Equals(existing.Method)) ?? Methods[0];
        _allocations.AddRange(existing.Allocations.Where(a => !a.IsDeleted));

        var contract = existing.ContractId is { } contractId ? _contractsById.GetValueOrDefault(contractId) : null;
        SelectedContract = contract is null ? null : ContractOption(contract);
        var propertyId = PropertyIdsOf(existing).FirstOrDefault();
        SelectedProperty = _properties.Where(p => p.Id == propertyId).Select(PropertyOption).FirstOrDefault();
    }

    /// <summary>The properties a payment belongs to: its own, its contract's, those of the bills it settles.</summary>
    private IEnumerable<Guid> PropertyIdsOf(Payment payment)
    {
        if (payment.PropertyId is { } own)
        {
            yield return own;
        }

        if (payment.ContractId is { } contractId && _contractsById.TryGetValue(contractId, out var contract))
        {
            yield return contract.PropertyId;
        }

        foreach (var allocation in payment.Allocations.Where(a => !a.IsDeleted))
        {
            if (_billsById.TryGetValue(allocation.BillId, out var bill))
            {
                yield return bill.PropertyId;
            }
        }
    }

    private static ChoiceOption PropertyOption(Property property) => new(property.Id, property.Name, string.Empty);

    private static ChoiceOption ContractOption(Contract contract) => new(contract.Id, contract.Title, string.Empty);

    private string TitleText() => IsNew
        ? _localizer["PaymentNewTitle"]
        : _localizer.Get("PaymentEditTitle", PaymentText.Amount(_localizer, _payment.Amount, CurrencySymbol), BillText.Date(_payment.PaidOn));

    // ---- Bills ----------------------------------------------------------------------------------------------------

    /// <summary>What is open on the bill without this payment (the most this payment may allocate to it).</summary>
    private decimal OpenOf(Bill bill) => bill.Gross - _paidByOthers.GetValueOrDefault(bill.Id);

    private string TitleOf(Bill bill) =>
        bill.ContractId is { } contractId && _contractsById.TryGetValue(contractId, out var contract)
            ? contract.Title
            : bill.Title ?? string.Empty;

    private string BillName(Bill bill) => PaymentText.BillName(TitleOf(bill), bill.Period);

    private string Money(decimal amount) => BillText.Money(amount, CurrencySymbol);

    /// <summary>The amount as typed, or 0 while it is not a valid number.</summary>
    private decimal TypedAmount() =>
        DecimalInput.TryParse(AmountText, CultureInfo.CurrentCulture, out var value) && Math.Abs(value) <= AmountEditor.MaxAmount ? value : 0m;

    // ---- Choosers -------------------------------------------------------------------------------------------------

    [RelayCommand]
    private Task ChoosePropertyAsync() => RunSafeAsync(async () =>
    {
        if (!IsNew)
        {
            return;
        }

        var options = _properties.Select(PropertyOption).ToList();
        var picked = await Dialogs.ChooseAsync(_localizer["PaymentChooseProperty"], [.. options.Select(o => o.Name)]);
        if (picked is null || options.FirstOrDefault(o => o.Name == picked) is not { } option || option.Id == SelectedProperty?.Id)
        {
            return;
        }

        SelectedProperty = option;
        SelectedContract = null;
        _currency = Currency.EUR;
        CurrencySymbol = _localizer["Currency_Symbol_" + _currency];
        ClearAllocations();
    });

    [RelayCommand]
    private Task ChooseContractAsync() => RunSafeAsync(async () =>
    {
        if (!IsNew)
        {
            return;
        }

        if (SelectedProperty is not { } property)
        {
            await Dialogs.AlertAsync(_localizer["Dialog_Error_Title"], _localizer["PaymentChoosePropertyFirst"]);
            return;
        }

        var none = _localizer["PaymentNoContract"];
        var contracts = _contracts.Where(c => c.PropertyId == property.Id).OrderBy(c => c.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
        var picked = await Dialogs.ChooseAsync(_localizer["PaymentChooseContract"], [none, .. contracts.Select(c => c.Title)]);
        if (picked is null)
        {
            return;
        }

        var contract = picked == none ? null : contracts.FirstOrDefault(c => c.Title == picked);
        if (contract?.Id == SelectedContract?.Id)
        {
            return;
        }

        SelectedContract = contract is null ? null : ContractOption(contract);
        if (contract is not null)
        {
            _currency = contract.Currency;
            CurrencySymbol = _localizer["Currency_Symbol_" + _currency];
        }

        ClearAllocations();
    });

    private void ClearAllocations()
    {
        if (_allocations.Count == 0)
        {
            return;
        }

        _allocations.Clear();
        AllocationsChanged();
    }

    // ---- Reference ------------------------------------------------------------------------------------------------

    /// <summary>"Fill from contract": the contract's payment purpose with the allocated bill's data when there is exactly one.</summary>
    [RelayCommand]
    private Task FillReferenceAsync() => RunSafeAsync(async () =>
    {
        if (SelectedContract is not { } selected || !_contractsById.TryGetValue(selected.Id, out var contract))
        {
            return;
        }

        if (await PurposeAsync(contract) is not { } purpose)
        {
            await Dialogs.AlertAsync(_localizer["Dialog_Error_Title"], _localizer["PaymentNoPurposeTemplate"]);
            return;
        }

        Reference = purpose;
    });

    private async Task<string?> PurposeAsync(Contract contract)
    {
        var supplier = await _supplierStore.GetAsync(contract.SupplierId);
        var template = (supplier is null ? contract.PaymentDetails : PaymentDetails.Effective(contract, supplier))?.PurposeTemplate;
        if (string.IsNullOrWhiteSpace(template))
        {
            return null;
        }

        Bill? bill = null;
        if (_allocations.Count == 1)
        {
            _billsById.TryGetValue(_allocations[0].BillId, out bill);
        }

        var typed = TypedAmount();
        decimal? amount = typed != 0 ? typed : _allocations.Count == 1 ? _allocations[0].Amount : null;
        var property = await _propertyStore.GetAsync(contract.PropertyId);
        var text = PaymentPurpose.Render(
            template,
            new PaymentPurposeContext(contract.CustomerNumber, contract.ContractNumber, bill?.Number, bill?.Period, amount, property?.Name));
        return text.Length > InputLimits.Notes ? text[..InputLimits.Notes] : text;
    }

    // ---- Allocations ----------------------------------------------------------------------------------------------

    /// <summary>"＋": picks one of the open bills of the contract (or property) and allocates as much of the payment as is left.</summary>
    [RelayCommand]
    private Task AddAllocationAsync() => RunSafeAsync(async () =>
    {
        if (SelectedContract is null && SelectedProperty is null)
        {
            await Dialogs.AlertAsync(_localizer["Dialog_Error_Title"], _localizer["PaymentChoosePropertyFirst"]);
            return;
        }

        var amount = TypedAmount();
        var sign = PaymentText.SignOf(amount);
        var candidates = OpenBills(sign);
        if (candidates.Count == 0)
        {
            await Dialogs.AlertAsync(_localizer["Dialog_Error_Title"], _localizer["PaymentNoOpenBills"]);
            return;
        }

        var labels = LabelsOf(candidates);
        var picked = await Dialogs.ChooseAsync(_localizer["PaymentChooseBill"], labels);
        if (picked is null || labels.ToList().IndexOf(picked) is not (>= 0 and var index))
        {
            return;
        }

        var bill = candidates[index];
        var open = OpenOf(bill);
        var left = Math.Abs(amount) - Math.Abs(_allocations.Sum(a => a.Amount));

        // Nothing left of the payment (or no amount typed yet): take the whole open amount; the footer shows what is missing.
        var take = left > 0 ? Math.Min(left, Math.Abs(open)) : Math.Abs(open);
        _allocations.Add(new PaymentAllocation { PaymentId = _payment.Id, BillId = bill.Id, Amount = take * Math.Sign(open) });
        AllocationsChanged();
    });

    /// <summary>The open bills the payment may settle: of its contract (or property), its currency and sign, not yet listed.</summary>
    private List<Bill> OpenBills(int sign) =>
    [
        .. _billsById.Values
            .Where(b => b.Currency == _currency
                && (SelectedContract is { } contract ? b.ContractId == contract.Id : b.PropertyId == SelectedProperty?.Id)
                && _allocations.All(a => a.BillId != b.Id)
                && OpenOf(b) != 0
                && Math.Sign(OpenOf(b)) == sign)
            .OrderBy(b => b.Period)
            .ThenBy(b => TitleOf(b), StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(b => b.Number, StringComparer.CurrentCultureIgnoreCase),
    ];

    /// <summary>"Water · 10.2026 · open 46,02 €"; a number is added when two bills would read the same.</summary>
    private string[] LabelsOf(List<Bill> bills)
    {
        var labels = bills
            .Select(b => _localizer.Get("PaymentBillOption", BillName(b), Money(OpenOf(b))))
            .ToArray();
        for (var i = 0; i < labels.Length; i++)
        {
            if (labels.Count(l => l == labels[i]) > 1)
            {
                labels[i] += $" ({bills[i].Number ?? (i + 1).ToString(CultureInfo.CurrentCulture)})";
            }
        }

        return labels;
    }

    /// <summary>"Allocate automatically": spreads the unallocated part over the oldest open bills.</summary>
    [RelayCommand]
    private Task AutoAllocateAsync() => RunSafeAsync(async () =>
    {
        var amount = TypedAmount();
        if (amount == 0)
        {
            await Dialogs.AlertAsync(_localizer["Dialog_Error_Title"], _localizer["PaymentEnterAmountFirst"]);
            return;
        }

        if (SelectedContract is null && SelectedProperty is null)
        {
            await Dialogs.AlertAsync(_localizer["Dialog_Error_Title"], _localizer["PaymentChoosePropertyFirst"]);
            return;
        }

        var draft = new Payment
        {
            Id = _payment.Id,
            Amount = amount,
            Currency = _currency,
            ContractId = SelectedContract?.Id,
            PropertyId = SelectedContract is null ? SelectedProperty?.Id : null,
            Allocations = [.. _allocations],
        };
        var added = PaymentAllocator.AutoAllocate(draft, _billsById.Values, id => OpenOf(_billsById[id]));
        if (added.Count == 0)
        {
            _snackbar.Notify(_localizer["PaymentNothingToAllocate"]);
            return;
        }

        _allocations.AddRange(added);
        AllocationsChanged();
    });

    private Task EditAllocationAsync(PaymentAllocation allocation) => RunSafeAsync(async () =>
    {
        if (!_billsById.TryGetValue(allocation.BillId, out var bill))
        {
            return;
        }

        var dialog = new AllocationAmountEditorViewModel(_localizer, BillName(bill), allocation.Amount, OpenOf(bill), allocation.Note, CurrencySymbol);
        if (!await Dialogs.ShowEditorAsync(dialog))
        {
            return;
        }

        allocation.Amount = dialog.ResultAmount;
        allocation.Note = dialog.ResultNote;
        AllocationsChanged();
    });

    private Task RemoveAllocationAsync(PaymentAllocation allocation) => RunSafeAsync(async () =>
    {
        var confirmed = await Dialogs.ConfirmAsync(
            _localizer["Dialog_DeleteRow_Title"],
            _localizer.Get("Dialog_DeleteRow_Message", AllocationText(allocation)),
            _localizer["Delete"],
            _localizer["Cancel"]);
        if (!confirmed)
        {
            return;
        }

        _allocations.Remove(allocation);
        AllocationsChanged();
    });

    private void AllocationsChanged()
    {
        MarkDirty();
        RebuildAllocations();
    }

    private string AllocationText(PaymentAllocation allocation) =>
        _billsById.TryGetValue(allocation.BillId, out var bill)
            ? _localizer.Get("PaymentAllocationRow", TitleOf(bill), BillText.Period(bill.Period), Money(bill.Gross), Money(allocation.Amount))
            : _localizer.Get("PaymentAllocationBillMissing", Money(allocation.Amount));

    private void RebuildAllocations()
    {
        AllocationRows =
        [
            .. _allocations.Select(a => new PaymentAllocationRowViewModel(
                a.BillId,
                AllocationText(a),
                a.Note ?? string.Empty,
                _localizer["Delete"],
                () => EditAllocationAsync(a),
                () => RemoveAllocationAsync(a))),
        ];
        UpdateSummary();
    }

    /// <summary>"Allocated 58,32 € · Prepayment 12,30 €" / "Allocated … · Over-allocated by …".</summary>
    private void UpdateSummary()
    {
        var amount = TypedAmount();
        var allocated = _allocations.Sum(a => a.Amount);
        var rest = amount - allocated;
        IsOverAllocated = Math.Abs(allocated) > Math.Abs(amount);
        AllocationSummary = _allocations.Count == 0 && amount == 0
            ? string.Empty
            : IsOverAllocated
                ? _localizer.Get("PaymentSummaryOver", Money(allocated), Money(Math.Abs(allocated) - Math.Abs(amount)))
                : rest == 0
                    ? _localizer.Get("PaymentSummaryFull", Money(allocated))
                    : amount > 0
                        ? _localizer.Get("PaymentSummaryPrepayment", Money(allocated), Money(rest))
                        : _localizer.Get("PaymentSummaryRest", Money(allocated), Money(-rest));
    }

    // ---- Validation -----------------------------------------------------------------------------------------------

    /// <summary>Reads the form into a payment; unreadable numbers and a missing property are added to <paramref name="errors"/>.</summary>
    private Payment BuildPayment(List<ValidationError> errors)
    {
        var amount = AmountEditor.ReadOptional(AmountText, "Amount", errors);
        if (amount is null && !errors.Any(e => e.Field == "Amount"))
        {
            errors.Add(new ValidationError(CommonErrorCodes.NumberInvalid, "Amount"));
        }

        var reference = string.IsNullOrWhiteSpace(Reference) ? null : Reference.Trim();
        if (reference is { Length: > InputLimits.Notes })
        {
            errors.Add(new ValidationError(CommonErrorCodes.TextTooLong, nameof(Payment.Reference), [InputLimits.Notes]));
        }

        var contract = SelectedContract is { } selected ? _contractsById.GetValueOrDefault(selected.Id) : null;
        var propertyId = contract?.PropertyId ?? SelectedProperty?.Id;
        if (propertyId is null)
        {
            errors.Add(new ValidationError(CommonErrorCodes.PaymentPropertyRequired, "PropertyId"));
        }

        var payment = new Payment
        {
            Id = _payment.Id,
            CreatedAt = _payment.CreatedAt,
            UpdatedAt = _payment.UpdatedAt,
            ContractId = contract?.Id,
            PropertyId = propertyId,
            PaidOn = PaidOn,
            Amount = amount ?? 0m,
            Currency = _currency,
            Method = SelectedMethod?.As<PaymentMethod>() ?? PaymentMethod.BankTransfer,
            CreatedByQuickAction = _payment.CreatedByQuickAction,
            Reference = reference,
            Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
        };
        payment.Allocations =
        [
            .. _allocations.Select(a => new PaymentAllocation
            {
                Id = a.Id,
                CreatedAt = a.CreatedAt,
                UpdatedAt = a.UpdatedAt,
                PaymentId = payment.Id,
                BillId = a.BillId,
                Amount = a.Amount,
                Note = a.Note,
            }),
        ];
        return payment;
    }

    /// <summary>The domain rules, with the allocation errors shown on the "Bills paid" card, plus the open amount of each bill.</summary>
    private IEnumerable<ValidationError> CheckPayment(Payment payment, bool amountUnreadable)
    {
        foreach (var error in PaymentValidator.Validate(payment, _billsById).Errors)
        {
            if (error.Code == ErrorCodes.PaymentAmountZero && amountUnreadable)
            {
                continue;
            }

            if (error.Code == ErrorCodes.PaymentOverAllocated)
            {
                var excess = Math.Abs(payment.Allocations.Sum(a => a.Amount)) - Math.Abs(payment.Amount);
                yield return new ValidationError(CommonErrorCodes.PaymentOverAllocatedBy, nameof(Payment.Allocations), [Money(excess)]);
            }
            else if (error.Field?.StartsWith(nameof(Payment.Allocations), StringComparison.Ordinal) == true)
            {
                yield return error with { Field = nameof(Payment.Allocations) };
            }
            else
            {
                yield return error;
            }
        }

        foreach (var allocation in payment.Allocations)
        {
            if (allocation.Amount == 0 || !_billsById.TryGetValue(allocation.BillId, out var bill))
            {
                continue;
            }

            var open = OpenOf(bill);
            if (Math.Sign(allocation.Amount) != Math.Sign(open) || Math.Abs(allocation.Amount) > Math.Abs(open))
            {
                yield return new ValidationError(
                    CommonErrorCodes.PaymentAllocationExceedsOpen,
                    nameof(Payment.Allocations),
                    [Money(allocation.Amount), Money(open)]);
            }
        }
    }
}
