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

/// <summary>
/// Row actions are asynchronous so a test can await them; the UI executes the command as before (a tap runs at once, several taps
/// are not blocked while the first one is running).
/// </summary>
internal static class Row
{
    internal static IAsyncRelayCommand Command(Func<Task> action) =>
        new AsyncRelayCommand(action, AsyncRelayCommandOptions.AllowConcurrentExecutions);
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
        Func<Task> open,
        Func<Task>? pay)
    {
        Item = item;
        Text = text;
        DetailText = detailText;
        AmountText = amountText;
        PayText = payText;
        EnterText = enterText;
        OpenCommand = Row.Command(open);
        PayCommand = pay is null ? null : Row.Command(pay);
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
    public IAsyncRelayCommand OpenCommand { get; }

    /// <summary>The inline "Paid" quick action; null unless the item is a bill that can be paid.</summary>
    public IAsyncRelayCommand? PayCommand { get; }

    public bool CanPay => PayCommand is not null;
}

/// <summary>One tappable line of the Money or This year card.</summary>
public sealed class OverviewLineViewModel(string text, OverviewTone tone, Func<Task> open)
{
    public string Text { get; } = text;

    public OverviewTone Tone { get; } = tone;

    public bool IsDebt => Tone == OverviewTone.Debt;

    public bool IsGood => Tone == OverviewTone.Good;

    public IAsyncRelayCommand OpenCommand { get; } = Row.Command(open);
}

/// <summary>The lines of one property in the Money or This year card.</summary>
public sealed class OverviewBlockViewModel(string header, IReadOnlyList<OverviewLineViewModel> lines)
{
    public string Header { get; } = header;

    public IReadOnlyList<OverviewLineViewModel> Lines { get; } = lines;
}

/// <summary>One tappable row of a Money section: name and amount on the first line, an optional detail under the name.</summary>
public sealed class OverviewMoneyRowViewModel(string text, string amount, string detail, OverviewTone detailTone, Func<Task> open)
{
    /// <summary>The contract's name (or "N more").</summary>
    public string Text { get; } = text;

    /// <summary>The amount, formatted with the app culture; empty on the "N more" row.</summary>
    public string Amount { get; } = amount;

    /// <summary>The second line: "overdue since …", "due …", "refund", "surcharge"; empty when there is none.</summary>
    public string Detail { get; } = detail;

    public OverviewTone DetailTone { get; } = detailTone;

    public bool HasDetail => Detail.Length > 0;

    public bool HasAmount => Amount.Length > 0;

    public bool IsDetailDebt => DetailTone == OverviewTone.Debt;

    public bool IsDetailGood => DetailTone == OverviewTone.Good;

    public IAsyncRelayCommand OpenCommand { get; } = Row.Command(open);

    /// <summary>One line for screen readers: "Gas, 12,25 €, overdue since 05.11.2026".</summary>
    public string Description => string.Join(", ", new[] { Text, Amount, Detail }.Where(part => part.Length > 0));
}

/// <summary>A section of the Money card ("To pay", "Overpaid", "Expected settlement") with its rows and, where it makes sense, a total.</summary>
public sealed class OverviewMoneySectionViewModel(string header, string total, IReadOnlyList<OverviewMoneyRowViewModel> rows)
{
    public string Header { get; } = header;

    /// <summary>The sum of the rows; empty when the section has no total (settlements, several currencies).</summary>
    public string Total { get; } = total;

    public bool HasTotal => Total.Length > 0;

    public IReadOnlyList<OverviewMoneyRowViewModel> Rows { get; } = rows;

    /// <summary>One line for screen readers: the header, the total and every row.</summary>
    public string Description => $"{Header} {Total}".Trim() + ": " + string.Join("; ", Rows.Select(r => r.Description));
}

/// <summary>The Money sections of one property.</summary>
public sealed class OverviewMoneyBlockViewModel(string header, IReadOnlyList<OverviewMoneySectionViewModel> sections)
{
    public string Header { get; } = header;

    public IReadOnlyList<OverviewMoneySectionViewModel> Sections { get; } = sections;
}
