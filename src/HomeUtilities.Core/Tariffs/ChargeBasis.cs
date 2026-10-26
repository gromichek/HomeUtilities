namespace HomeUtilities.Core.Tariffs;

/// <summary>What the quantity of a tariff component is based on.</summary>
public enum ChargeBasis
{
    /// <summary>Consumption × price.</summary>
    PerUnit = 0,

    /// <summary>Area × price.</summary>
    PerArea = 1,

    /// <summary>Declared residents × price.</summary>
    PerResident = 2,

    /// <summary>Fixed amount per month.</summary>
    FixedMonthly = 3,

    /// <summary>No own calculation; the amount comes from the supplier bill.</summary>
    FromBill = 4,

    /// <summary>Export register × price, a negative line.</summary>
    NetExport = 5,

    /// <summary>The supplier distributes the quantity: it is typed from the bill, the price comes from the tariff.</summary>
    QuantityFromBill = 6,
}
