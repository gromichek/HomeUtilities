using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Attachments;

/// <summary>
/// A file that belongs to another record (a meter photo, a bill PDF). The file itself lives in the attachments folder
/// as <c>{Id}{extension}</c>; this row describes it.
/// </summary>
public class Attachment : SyncEntity
{
    /// <summary>Original file name as picked by the user.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>MIME type, one of <see cref="AttachmentContentTypes.Allowed"/>.</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>Size of the stored file in bytes.</summary>
    public long SizeBytes { get; set; }

    /// <summary>SHA-256 of the stored file as 64 lower-case hex characters.</summary>
    public string Sha256 { get; set; } = string.Empty;

    /// <summary>Kind of the owning record.</summary>
    public AttachmentOwnerKind OwnerKind { get; set; }

    /// <summary>Id of the owning record.</summary>
    public Guid OwnerId { get; set; }

    /// <summary>Free-form comment.</summary>
    public string? Notes { get; set; }
}
