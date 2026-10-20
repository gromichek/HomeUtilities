using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Payments;

/// <summary>Values that fill the placeholders of a purpose template.</summary>
/// <param name="CustomerNumber">Customer number.</param>
/// <param name="ContractNumber">Contract number.</param>
/// <param name="BillNumber">Bill number.</param>
/// <param name="Period">Billing period.</param>
/// <param name="Amount">Amount to pay.</param>
/// <param name="PropertyName">Name of the property.</param>
public sealed record PaymentPurposeContext(
    string? CustomerNumber,
    string? ContractNumber,
    string? BillNumber,
    YearMonth? Period,
    decimal? Amount,
    string? PropertyName);
