namespace HomeUtilities.Core.Billing;

/// <summary>A quantity typed from the supplier bill for a <c>QuantityFromBill</c> tariff component.</summary>
/// <param name="ComponentId">The tariff component.</param>
/// <param name="Quantity">Quantity in the unit of the component.</param>
public sealed record BillQuantity(Guid ComponentId, decimal Quantity);
