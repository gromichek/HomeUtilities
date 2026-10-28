using HomeUtilities.Core.Billing;

namespace HomeUtilities.Core.Charges;

/// <summary>Picks the quantities and line amounts typed from the bills of one contract and period.</summary>
public static class BillQuantitySelection
{
    /// <summary>
    /// The quantity per component from the bills, oldest first, so the newest bill wins per component. The amount belongs to the
    /// quantity of the same bill, so a newer bill without an amount drops an older amount.
    /// </summary>
    /// <param name="billsOfPeriod">The bills of the contract and period, ordered by creation time (oldest first).</param>
    public static (Dictionary<Guid, decimal> Quantities, Dictionary<Guid, decimal> Amounts) From(IEnumerable<Bill> billsOfPeriod)
    {
        ArgumentNullException.ThrowIfNull(billsOfPeriod);

        var quantities = new Dictionary<Guid, decimal>();
        var amounts = new Dictionary<Guid, decimal>();
        foreach (var quantity in billsOfPeriod.SelectMany(b => b.BillQuantities))
        {
            quantities[quantity.ComponentId] = quantity.Quantity;
            if (quantity.Amount is { } amount)
            {
                amounts[quantity.ComponentId] = amount;
            }
            else
            {
                amounts.Remove(quantity.ComponentId);
            }
        }

        return (quantities, amounts);
    }
}
