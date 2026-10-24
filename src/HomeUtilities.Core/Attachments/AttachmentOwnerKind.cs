namespace HomeUtilities.Core.Attachments;

/// <summary>The kind of record an attachment belongs to.</summary>
public enum AttachmentOwnerKind
{
    /// <summary>A meter reading (photo of the dial).</summary>
    MeterReading = 0,

    /// <summary>A supplier bill (PDF or photo).</summary>
    Bill = 1,

    /// <summary>A meter (e.g. verification certificate).</summary>
    Meter = 2,

    /// <summary>Anything else.</summary>
    Other = 3,
}
