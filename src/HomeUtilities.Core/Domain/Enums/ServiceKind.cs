namespace HomeUtilities.Core.Domain;

/// <summary>Kind of service delivered under a contract.</summary>
public enum ServiceKind
{
    /// <summary>ColdWater.</summary>
    ColdWater = 0,

    /// <summary>HotWater.</summary>
    HotWater = 1,

    /// <summary>Sewerage.</summary>
    Sewerage = 2,

    /// <summary>WaterHeating.</summary>
    WaterHeating = 3,

    /// <summary>Heating.</summary>
    Heating = 4,

    /// <summary>Electricity.</summary>
    Electricity = 5,

    /// <summary>Gas.</summary>
    Gas = 6,

    /// <summary>Waste.</summary>
    Waste = 7,

    /// <summary>Maintenance.</summary>
    Maintenance = 8,

    /// <summary>Internet.</summary>
    Internet = 9,

    /// <summary>Tv.</summary>
    Tv = 10,

    /// <summary>InternetTv.</summary>
    InternetTv = 11,

    /// <summary>PropertyTaxBuilding.</summary>
    PropertyTaxBuilding = 12,

    /// <summary>PropertyTaxLand.</summary>
    PropertyTaxLand = 13,

    /// <summary>Insurance.</summary>
    Insurance = 14,

    /// <summary>Other.</summary>
    Other = 15,
}
