namespace HomeUtilities.Core.Charges;

/// <summary>Totals of all lines of a calculated charge that share one VAT percent.</summary>
/// <param name="VatPercent">The VAT percent.</param>
/// <param name="Net">Net amount of the group.</param>
/// <param name="Vat">VAT of the group (on the group total, or the sum of the line VATs).</param>
/// <param name="Gross">Gross amount of the group.</param>
public sealed record VatGroup(decimal VatPercent, decimal Net, decimal Vat, decimal Gross);
