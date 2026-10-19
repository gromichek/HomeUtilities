namespace HomeUtilities.Core.Domain;

/// <summary>How a meter reading came about.</summary>
public enum ReadingKind
{
    /// <summary>Ordinary reading taken by the owner.</summary>
    Regular = 0,

    /// <summary>First reading of a newly installed meter.</summary>
    Initial = 1,

    /// <summary>Last reading of a removed meter.</summary>
    Final = 2,

    /// <summary>Control reading taken by the supplier.</summary>
    Control = 3,

    /// <summary>Estimated by the supplier.</summary>
    Estimated = 4,
}
