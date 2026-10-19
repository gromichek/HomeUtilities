using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Domain;

/// <summary>A physical meter installed in a property; may have several registers.</summary>
public class Meter : SyncEntity
{
    /// <summary>Property the meter is installed in.</summary>
    public Guid PropertyId { get; set; }

    /// <summary>Measured resource.</summary>
    public MeterResource Resource { get; set; }

    /// <summary>Unit of the dial value.</summary>
    public MeterUnit Unit { get; set; }

    /// <summary>Serial number printed on the meter.</summary>
    public string? SerialNumber { get; set; }

    /// <summary>Where the meter is, e.g. "bathroom".</summary>
    public string? Location { get; set; }

    /// <summary>Number of integer digits on the dial; used to detect rollover. Null if unknown.</summary>
    public int? Digits { get; set; }

    /// <summary>Date the meter was installed.</summary>
    public DateOnly InstalledOn { get; set; }

    /// <summary>Date the meter was removed; null while it is in use.</summary>
    public DateOnly? RemovedOn { get; set; }

    /// <summary>The meter this one replaced, if any.</summary>
    public Guid? ReplacedMeterId { get; set; }

    /// <summary>Date of the last metrological verification.</summary>
    public DateOnly? VerifiedOn { get; set; }

    /// <summary>Verification interval in months.</summary>
    public int? VerificationIntervalMonths { get; set; }

    /// <summary>Explicit next verification date; overrides the calculated one.</summary>
    public DateOnly? NextVerificationDue { get; set; }

    /// <summary>Free-form notes.</summary>
    public string? Notes { get; set; }

    /// <summary>Registers of the meter.</summary>
    public List<MeterRegister> Registers { get; set; } = [];

    /// <summary>
    /// <see cref="NextVerificationDue"/> if set; otherwise <c>(VerifiedOn ?? InstalledOn)</c> plus the interval;
    /// null if neither is available.
    /// </summary>
    public DateOnly? GetVerificationDue()
    {
        if (NextVerificationDue is { } explicitDue)
        {
            return explicitDue;
        }

        return VerificationIntervalMonths is { } months ? (VerifiedOn ?? InstalledOn).AddMonths(months) : null;
    }

    /// <summary>True if the meter is not deleted and was installed on the given date (both bounds inclusive).</summary>
    public bool IsInstalledOn(DateOnly date) =>
        !IsDeleted && InstalledOn <= date && (RemovedOn is null || date <= RemovedOn);
}
