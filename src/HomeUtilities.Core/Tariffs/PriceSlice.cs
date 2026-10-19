namespace HomeUtilities.Core.Tariffs;

/// <summary>A part of a quantity that is charged at one unit price.</summary>
/// <param name="Quantity">Quantity of the slice.</param>
/// <param name="UnitPrice">Unit price of the slice.</param>
public sealed record PriceSlice(decimal Quantity, decimal UnitPrice);
