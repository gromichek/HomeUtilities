using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core.Overview;

namespace HomeUtilities.ViewModels;

/// <summary>How a line of the Money or This year card is coloured.</summary>
public enum OverviewTone
{
    /// <summary>Plain text.</summary>
    Neutral,

    /// <summary>Money owed or overdue (red).</summary>
    Debt,

    /// <summary>Money in advance (green).</summary>
    Good,
}

/// <summary>One row of the "To do" card: an icon by kind, a text coloured by severity, a date or amount and its action.</summary>
public sealed class TodoRowViewModel
{
    public TodoRowViewModel(
        TodoItem item,
        string text,
        string detailText,
        string amountText,
        string payText,
        string enterText,
        Action open,
        Action? pay)
    {
        Item = item;
        Text = text;
        DetailText = detailText;
        AmountText = amountText;
        PayText = payText;
        EnterText = enterText;
        OpenCommand = new RelayCommand(open);
        PayCommand = pay is null ? null : new RelayCommand(pay);
    }

    public TodoItem Item { get; }

    public TodoKind Kind => Item.Kind;

    public TodoSeverity Severity => Item.Severity;

    /// <summary>What to do: "Pay Water and sewerage for 09.2026".</summary>
    public string Text { get; }

    /// <summary>Property and date: "Flat in town · Overdue since 20.09.2026 (37 days)".</summary>
    public string DetailText { get; }

    /// <summary>The open amount of a bill or the expected settlement; empty for other items.</summary>
    public string AmountText { get; }

    public bool HasAmount => AmountText.Length > 0;

    /// <summary>Tooltip and accessibility name of the inline "Paid" button.</summary>
    public string PayText { get; }

    /// <summary>Text of the "Enter bill" button of a missing bill.</summary>
    public string EnterText { get; }

    public bool IsReadings => Kind == TodoKind.ReadingsWindow;

    public bool IsDue => Kind is TodoKind.BillDue or TodoKind.TaxInstallmentDue;

    public bool IsMissing => Kind == TodoKind.BillMissing;

    public bool IsVerification => Kind == TodoKind.VerificationDue;

    public bool IsSettlement => Kind == TodoKind.SettlementExpected;

    public bool IsDrafts => Kind == TodoKind.DraftsToConfirm;

    public bool IsOverdue => Severity == TodoSeverity.Overdue;

    public bool IsToday => Severity == TodoSeverity.Today;

    public bool IsSoon => Severity == TodoSeverity.Soon;

    public bool IsInfo => Severity == TodoSeverity.Info;

    /// <summary>Opens the screen of the item (the row tap).</summary>
    public IRelayCommand OpenCommand { get; }

    /// <summary>The inline "Paid" quick action; null unless the item is a bill that can be paid.</summary>
    public IRelayCommand? PayCommand { get; }

    public bool CanPay => PayCommand is not null;
}

/// <summary>One tappable line of the Money or This year card.</summary>
public sealed class OverviewLineViewModel(string text, OverviewTone tone, Action open)
{
    public string Text { get; } = text;

    public OverviewTone Tone { get; } = tone;

    public bool IsDebt => Tone == OverviewTone.Debt;

    public bool IsGood => Tone == OverviewTone.Good;

    public IRelayCommand OpenCommand { get; } = new RelayCommand(open);
}

/// <summary>The lines of one property in the Money or This year card.</summary>
public sealed class OverviewBlockViewModel(string header, IReadOnlyList<OverviewLineViewModel> lines)
{
    public string Header { get; } = header;

    public IReadOnlyList<OverviewLineViewModel> Lines { get; } = lines;
}
