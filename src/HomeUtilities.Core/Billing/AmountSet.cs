namespace HomeUtilities.Core.Billing;

/// <summary>A money amount with its optional VAT breakdown. When all are set, <c>Net + Vat = Gross</c>.</summary>
/// <param name="Net">Amount excluding VAT; null if unknown.</param>
/// <param name="VatPercent">VAT rate in percent; null if unknown.</param>
/// <param name="Vat">VAT amount; null if unknown.</param>
/// <param name="Gross">Total including VAT.</param>
public sealed record AmountSet(decimal? Net, decimal? VatPercent, decimal? Vat, decimal Gross);
