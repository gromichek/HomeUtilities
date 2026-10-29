using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Domain;

/// <summary>A dial value of a meter register on a given date.</summary>
public class MeterReading : SyncEntity
{
    /// <summary>Register the reading belongs to.</summary>
    public Guid RegisterId { get; set; }

    /// <summary>Date the reading was taken.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Dial value as shown.</summary>
    public decimal Value { get; set; }

    /// <summary>How the reading came about.</summary>
    public ReadingKind Kind { get; set; }

    /// <summary>Date the reading was reported to the supplier.</summary>
    public DateOnly? SubmittedOn { get; set; }

    /// <summary>Photo attachment taken on the phone.</summary>
    public Guid? PhotoAttachmentId { get; set; }

    /// <summary>The Excel import that created the record; null for records entered by hand.</summary>
    public Guid? ImportBatchId { get; set; }

    /// <summary>Free-form note.</summary>
    public string? Note { get; set; }
}
