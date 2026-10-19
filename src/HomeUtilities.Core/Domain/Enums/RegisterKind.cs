namespace HomeUtilities.Core.Domain;

/// <summary>Kind of a meter register (tariff zone or export counter).</summary>
public enum RegisterKind
{
    /// <summary>Single total register.</summary>
    Total = 0,

    /// <summary>Day tariff.</summary>
    Day = 1,

    /// <summary>Night tariff.</summary>
    Night = 2,

    /// <summary>Peak tariff.</summary>
    Peak = 3,

    /// <summary>Energy fed into the grid.</summary>
    Export = 4,
}
