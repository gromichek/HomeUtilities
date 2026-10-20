namespace HomeUtilities.Core.Billing;

/// <summary>Balance of a contract on a date.</summary>
/// <param name="Billed">Sum of the bills issued up to the date.</param>
/// <param name="Paid">Sum of the payments up to the date.</param>
/// <param name="Value">Paid minus billed: positive = overpayment / prepayment, negative = debt.</param>
public sealed record Balance(decimal Billed, decimal Paid, decimal Value);
