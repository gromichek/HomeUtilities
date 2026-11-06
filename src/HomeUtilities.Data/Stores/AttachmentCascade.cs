using HomeUtilities.Core.Attachments;
using Microsoft.EntityFrameworkCore;

namespace HomeUtilities.Data.Stores;

/// <summary>
/// Attachments follow their owner (T-037a): deleting a bill, meter or reading soft-deletes its attachments in the same save, and
/// restoring the owner brings back exactly those. The one place both directions go through.
/// </summary>
internal static class AttachmentCascade
{
    /// <summary>
    /// Marks the live attachments of the owners as removed in <paramref name="db"/>; the caller's next <c>SaveChanges</c> turns it
    /// into a soft delete (and stamps the same <c>UpdatedAt</c> as the owner gets in that save).
    /// </summary>
    public static async Task DeleteForOwnersAsync(
        HomeUtilitiesDbContext db, AttachmentOwnerKind kind, IReadOnlyCollection<Guid> ownerIds, CancellationToken ct)
    {
        if (ownerIds.Count == 0)
        {
            return;
        }

        var attachments = await db.Attachments.Where(a => a.OwnerKind == kind && ownerIds.Contains(a.OwnerId)).ToListAsync(ct);
        db.Attachments.RemoveRange(attachments);
    }

    /// <summary>
    /// Brings back the attachments of the owner that were deleted together with it: the soft-deleted ones changed at
    /// <paramref name="ownerDeletedAt"/> or later. An attachment the user removed earlier stays removed. Saved by the caller.
    /// </summary>
    public static async Task RestoreForOwnerAsync(
        HomeUtilitiesDbContext db, AttachmentOwnerKind kind, Guid ownerId, DateTimeOffset ownerDeletedAt, CancellationToken ct)
    {
        var attachments = await db.Attachments.IgnoreQueryFilters()
            .Where(a => a.OwnerKind == kind && a.OwnerId == ownerId && a.IsDeleted && a.UpdatedAt >= ownerDeletedAt)
            .ToListAsync(ct);
        foreach (var attachment in attachments)
        {
            attachment.IsDeleted = false;
        }
    }
}
