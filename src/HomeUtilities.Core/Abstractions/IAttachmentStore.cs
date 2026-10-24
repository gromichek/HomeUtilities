using HomeUtilities.Core.Attachments;

namespace HomeUtilities.Core.Abstractions;

/// <summary>Stores attachment files in the attachments folder and their rows in the database.</summary>
public interface IAttachmentStore
{
    /// <summary>
    /// Copies <paramref name="content"/> into the attachments folder as <c>{Id}{extension}</c>, fills
    /// <see cref="Attachment.SizeBytes"/> and <see cref="Attachment.Sha256"/> and saves the row. If saving the row
    /// fails the file is removed again.
    /// </summary>
    /// <param name="meta">Name, content type, owner and notes; size and hash are computed here.</param>
    /// <param name="content">The file bytes.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="AttachmentRejectedException">The file is empty, too large or of a type that is not allowed.</exception>
    Task<Attachment> AddAsync(Attachment meta, Stream content, CancellationToken ct = default);

    /// <summary>The attachment row, or null if it does not exist or is deleted.</summary>
    Task<Attachment?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>Opens the stored file for reading; null if the row or the file is missing.</summary>
    Task<Stream?> OpenReadAsync(Guid id, CancellationToken ct = default);

    /// <summary>Full path of the stored file (for the system viewer); null if the row or the file is missing.</summary>
    Task<string?> GetFilePathAsync(Guid id, CancellationToken ct = default);

    /// <summary>The non-deleted attachments of a record, oldest first.</summary>
    Task<IReadOnlyList<Attachment>> ListByOwnerAsync(AttachmentOwnerKind kind, Guid ownerId, CancellationToken ct = default);

    /// <summary>Soft-deletes the row. The file is kept (undo, sync); a cleanup job removes it later.</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
