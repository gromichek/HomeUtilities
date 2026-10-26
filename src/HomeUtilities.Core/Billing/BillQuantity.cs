namespace HomeUtilities.Core.Billing;

/// <summary>A quantity typed from the supplier bill for a <c>QuantityFromBill</c> tariff component.</summary>
/// <param name="ComponentId">The tariff component.</param>
/// <param name="Quantity">Quantity in the unit of the component.</param>
/// <param name="Amount">
/// Optional line amount printed on the bill (net, excl. VAT; the gross of the line when the rate includes VAT). Suppliers
/// print the quantity rounded but bill the unrounded one, so quantity × price can differ from the billed amount by cents.
/// Null = not entered (also for bills saved before this field existed).
/// </param>
public sealed record BillQuantity(Guid ComponentId, decimal Quantity, decimal? Amount = null);
