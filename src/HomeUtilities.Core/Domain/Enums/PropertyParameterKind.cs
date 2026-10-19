namespace HomeUtilities.Core.Domain;

/// <summary>Kind of a time-versioned property parameter.</summary>
public enum PropertyParameterKind
{
    /// <summary>TotalArea.</summary>
    TotalArea = 0,

    /// <summary>HeatedArea.</summary>
    HeatedArea = 1,

    /// <summary>DeclaredResidents.</summary>
    DeclaredResidents = 2,

    /// <summary>LandArea.</summary>
    LandArea = 3,

    /// <summary>CadastralValueBuilding.</summary>
    CadastralValueBuilding = 4,

    /// <summary>CadastralValueLand.</summary>
    CadastralValueLand = 5,

    /// <summary>Custom.</summary>
    Custom = 6,
}
