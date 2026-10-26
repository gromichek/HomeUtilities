using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HomeUtilities.ViewModels;

/// <summary>How a contract card on the charges screen ended up.</summary>
public enum ChargeCardStatus
{
    /// <summary>Every line was calculated.</summary>
    Complete,

    /// <summary>At least one line could not be calculated completely.</summary>
    Incomplete,

    /// <summary>The contract has only "amount from the bill" components.</summary>
    FromBill,

    /// <summary>The contract has no tariff components.</summary>
    NoTariff,

    /// <summary>The calculation failed with an exception (logged).</summary>
    Error,
}

/// <summary>A finding under a contract card with the localized message and, where there is one, the screen that fixes it.</summary>
public sealed class ChargeWarningViewModel
{
    public ChargeWarningViewModel(string message, string actionText, IAsyncRelayCommand? action)
    {
        Message = message;
        ActionText = actionText;
        ActionCommand = action;
    }

    public string Message { get; }

    /// <summary>"Open tariff", "Open readings"…; empty when the message has no action.</summary>
    public string ActionText { get; }

    public IAsyncRelayCommand? ActionCommand { get; }

    public bool HasAction => ActionCommand is not null;
}

/// <summary>The readings behind one quantity: one block per meter register (or replacement chain).</summary>
public sealed class ChargeReadingDetailViewModel
{
    public ChargeReadingDetailViewModel(string header, IReadOnlyList<string> lines, IReadOnlyList<string> notes)
    {
        Header = header;
        Lines = lines;
        Notes = notes;
    }

    /// <summary>"Bathroom cold · Day".</summary>
    public string Header { get; }

    /// <summary>Start reading, end reading, replacement segments, consumption.</summary>
    public IReadOnlyList<string> Lines { get; }

    /// <summary>Flags: roll-over, reading outside the window, estimated, unusually high…</summary>
    public IReadOnlyList<string> Notes { get; }

    public bool HasNotes => Notes.Count > 0;
}

/// <summary>One line of a contract's charge.</summary>
public sealed partial class ChargeLineViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isDetailOpen;

    public ChargeLineViewModel(
        string name,
        string quantityText,
        string unitPriceText,
        string netText,
        string vatPercentText,
        string vatText,
        string totalText,
        bool isFromBill,
        string fromBillText,
        bool isIncomplete,
        IReadOnlyList<ChargeReadingDetailViewModel> details)
    {
        Name = name;
        QuantityText = quantityText;
        UnitPriceText = unitPriceText;
        NetText = netText;
        VatPercentText = vatPercentText;
        VatText = vatText;
        TotalText = totalText;
        IsFromBill = isFromBill;
        FromBillText = fromBillText;
        IsIncomplete = isIncomplete;
        Details = details;
    }

    public string Name { get; }

    /// <summary>"5,500 m³", "52,30 m²", "2 residents", "1 month"; empty when the quantity is unknown.</summary>
    public string QuantityText { get; }

    /// <summary>"× 1,41 € excl. VAT"; empty when there is no price.</summary>
    public string UnitPriceText { get; }

    public string NetText { get; }

    /// <summary>"21 %".</summary>
    public string VatPercentText { get; }

    public string VatText { get; }

    public string TotalText { get; }

    public bool IsFromBill { get; }

    /// <summary>"Amount from the supplier's bill".</summary>
    public string FromBillText { get; }

    /// <summary>True if the line could not be calculated; its amounts are not shown.</summary>
    public bool IsIncomplete { get; }

    public bool HasAmounts => !IsFromBill && !IsIncomplete;

    public bool HasQuantity => QuantityText.Length > 0;

    public bool HasUnitPrice => UnitPriceText.Length > 0;

    public IReadOnlyList<ChargeReadingDetailViewModel> Details { get; }

    public bool HasDetails => Details.Count > 0;

    [RelayCommand]
    private void ToggleDetail() => IsDetailOpen = !IsDetailOpen;
}

/// <summary>The card of one contract: header, lines, totals and warnings.</summary>
public sealed partial class ChargeCardViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isExpanded;

    public ChargeCardViewModel(
        Guid contractId,
        string title,
        string supplierName,
        string color,
        ChargeCardStatus status,
        string statusText,
        string totalText,
        string netText,
        string vatText)
    {
        ContractId = contractId;
        Title = title;
        SupplierName = supplierName;
        Color = color;
        Status = status;
        StatusText = statusText;
        TotalText = totalText;
        NetText = netText;
        VatText = vatText;
    }

    public Guid ContractId { get; }

    public string Title { get; }

    public string SupplierName { get; }

    /// <summary>Colour of the contract's category as <c>#RRGGBB</c>.</summary>
    public string Color { get; }

    public ChargeCardStatus Status { get; }

    /// <summary>"Complete", "Incomplete", "From bill", "No tariff", "Error".</summary>
    public string StatusText { get; }

    /// <summary>Gross total of the month, e.g. "9,39 €"; empty for cards without amounts.</summary>
    public string TotalText { get; }

    public string NetText { get; }

    public string VatText { get; }

    public bool HasTotal => TotalText.Length > 0;

    /// <summary>Warning icon in the header.</summary>
    public bool ShowWarningIcon => Status is ChargeCardStatus.Incomplete or ChargeCardStatus.Error;

    public IReadOnlyList<ChargeLineViewModel> Lines { get; init; } = [];

    /// <summary>"VAT 12 % of 39,18 € = 4,70 €" per VAT percent when VAT is calculated on the total.</summary>
    public IReadOnlyList<string> VatGroupTexts { get; init; } = [];

    public bool HasVatGroups => VatGroupTexts.Count > 0;

    public IReadOnlyList<ChargeWarningViewModel> Warnings { get; init; } = [];

    public bool HasLines => Lines.Count > 0;

    public bool HasWarnings => Warnings.Count > 0;

    public bool ShowNoTariff => Status == ChargeCardStatus.NoTariff;

    public string NoTariffText { get; init; } = string.Empty;

    public string NoTariffLinkText { get; init; } = string.Empty;

    public IAsyncRelayCommand? NoTariffCommand { get; init; }

    public bool ShowError => Status == ChargeCardStatus.Error;

    /// <summary>"Bill: 46,02 € · +0,92 € · Paid ✓" when a bill exists for the month; empty otherwise.</summary>
    public string BillText { get; set; } = string.Empty;

    public bool HasBill => BillText.Length > 0;

    /// <summary>Opens the bill.</summary>
    public IRelayCommand? OpenBillCommand { get; set; }

    /// <summary>"Enter bill": shown while the month has no bill for the contract.</summary>
    public string EnterBillText { get; set; } = string.Empty;

    public bool ShowEnterBill => !HasBill && EnterBillText.Length > 0;

    /// <summary>Opens a new bill with the contract, the month and the calculated total as a suggestion.</summary>
    public IRelayCommand? EnterBillCommand { get; set; }

    public string ErrorText { get; init; } = string.Empty;

    /// <summary>Totals row (Net · VAT · Total) at the end of the lines.</summary>
    public bool ShowTotalsRow => HasLines && Status != ChargeCardStatus.Error;

    public string NetLabel { get; init; } = string.Empty;

    public string VatLabel { get; init; } = string.Empty;

    public string TotalLabel { get; init; } = string.Empty;

    [RelayCommand]
    private void ToggleExpanded() => IsExpanded = !IsExpanded;
}
