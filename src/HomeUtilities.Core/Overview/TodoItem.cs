using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Overview;

/// <summary>What a to-do item asks the owner to do.</summary>
public enum TodoKind
{
    /// <summary>Submit the meter readings: the reading window is open, opens soon or was missed.</summary>
    ReadingsWindow = 0,

    /// <summary>Pay a bill that is due soon, today or overdue.</summary>
    BillDue = 1,

    /// <summary>Pay a property tax installment (a bill of kind tax installment that is due soon, today or overdue).</summary>
    TaxInstallmentDue = 2,

    /// <summary>Enter the bill the supplier usually sends by now.</summary>
    BillMissing = 3,

    /// <summary>Have a meter verified (its verification is due or overdue).</summary>
    VerificationDue = 4,

    /// <summary>An equalized contract expects a noticeable settlement (information only).</summary>
    SettlementExpected = 5,
}

/// <summary>How urgent a to-do item is; the order of the values is the order of the list.</summary>
public enum TodoSeverity
{
    /// <summary>The date has passed.</summary>
    Overdue = 0,

    /// <summary>It is today.</summary>
    Today = 1,

    /// <summary>It is coming up within the look-ahead of the kind.</summary>
    Soon = 2,

    /// <summary>Information, nothing is late.</summary>
    Info = 3,
}

/// <summary>One thing the owner has to do now. Only data; the screen turns it into text and an action.</summary>
/// <param name="Kind">What to do.</param>
/// <param name="Severity">How urgent it is.</param>
/// <param name="Date">
/// The date the item is about: the start of the reading window, the due date of a bill, the day the bill usually arrives,
/// the verification due date, today for a settlement. The list is ordered by it within a severity.
/// </param>
/// <param name="PropertyId">The property the item belongs to.</param>
/// <param name="ContractId">The contract, if the item concerns exactly one.</param>
/// <param name="MeterId">The meter of a verification item.</param>
/// <param name="BillId">The bill of a due item.</param>
/// <param name="Amount">The open amount of a bill or the expected settlement (positive = surcharge, negative = refund).</param>
/// <param name="Args">Names for the text: the contract titles of a readings item, otherwise the contract or bill title or the meter name.</param>
/// <param name="EndDate">The end of the reading window.</param>
/// <param name="Period">The billing period a missing bill is for.</param>
/// <param name="Currency">Currency of <paramref name="Amount"/>.</param>
public sealed record TodoItem(
    TodoKind Kind,
    TodoSeverity Severity,
    DateOnly Date,
    Guid PropertyId,
    Guid? ContractId,
    Guid? MeterId,
    Guid? BillId,
    decimal? Amount,
    IReadOnlyList<string> Args,
    DateOnly? EndDate = null,
    YearMonth? Period = null,
    Currency Currency = Currency.EUR);
