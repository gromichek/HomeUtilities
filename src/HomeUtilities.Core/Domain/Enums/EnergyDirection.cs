namespace HomeUtilities.Core.Domain;

/// <summary>Direction of the energy flow measured by a register.</summary>
public enum EnergyDirection
{
    /// <summary>Consumption.</summary>
    Import = 0,

    /// <summary>Energy fed into the grid.</summary>
    Export = 1,
}
