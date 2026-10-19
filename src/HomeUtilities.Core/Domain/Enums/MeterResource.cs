namespace HomeUtilities.Core.Domain;

/// <summary>Resource measured by a meter.</summary>
public enum MeterResource
{
    /// <summary>Cold water.</summary>
    ColdWater = 0,

    /// <summary>Hot water.</summary>
    HotWater = 1,

    /// <summary>Electricity.</summary>
    Electricity = 2,

    /// <summary>Gas.</summary>
    Gas = 3,

    /// <summary>Heat energy.</summary>
    Heat = 4,

    /// <summary>Anything else.</summary>
    Other = 5,
}
