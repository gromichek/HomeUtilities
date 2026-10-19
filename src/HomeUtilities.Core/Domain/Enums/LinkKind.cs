namespace HomeUtilities.Core.Domain;

/// <summary>Purpose of a web link.</summary>
public enum LinkKind
{
    /// <summary>Website.</summary>
    Website = 0,

    /// <summary>CustomerPortal.</summary>
    CustomerPortal = 1,

    /// <summary>SubmitReadings.</summary>
    SubmitReadings = 2,

    /// <summary>Tariffs.</summary>
    Tariffs = 3,

    /// <summary>PayOnline.</summary>
    PayOnline = 4,

    /// <summary>Other.</summary>
    Other = 5,
}
