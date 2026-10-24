using System.Security.Cryptography;
using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Attachments;
using Microsoft.EntityFrameworkCore;

namespace HomeUtilities.Data.Stores;

/// <summary>
/// EF Core and file system implementation of <see cref="IAttachmentStore"/>. Files are stored as <c>{Id}{extension}</c>
/// in one folder; the extension follows the content type, never the user's file name.
/// </summary>
public sealed class AttachmentStore(
    IDbContextFactory<HomeUtilitiesDbContext> factory,
    string folder,
    IImageProcessor? imageProcessor = null) : IAttachmentStore
{
    /// <inheritdoc />
    public async Task<Attachment> AddAsync(Attachment meta, Stream content, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(meta);
        ArgumentNullException.ThrowIfNull(content);

        var bytes = await ReadLimitedAsync(content, ct);
        var contentType = meta.ContentType?.Trim() ?? string.Empty;
        if (imageProcessor is not null && bytes.Length > 0 && AttachmentContentTypes.IsImage(contentType) && AttachmentContentTypes.IsAllowed(contentType))
        {
            var processed = await imageProcessor.ProcessAsync(new MemoryStream(bytes), contentType, ct);
            await using (processed.Content)
            {
                var resized = await ReadLimitedAsync(processed.Content, ct);
                if (processed.ContentType != contentType)
                {
                    meta.FileName = Path.ChangeExtension(meta.FileName, AttachmentContentTypes.ExtensionFor(processed.ContentType));
                }

                bytes = resized;
                contentType = processed.ContentType;
            }
        }

        meta.ContentType = contentType;
        meta.SizeBytes = bytes.Length;
        meta.Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var validation = AttachmentValidator.Validate(meta);
        if (!validation.IsValid)
        {
            throw new AttachmentRejectedException(validation);
        }

        Directory.CreateDirectory(folder);
        var path = PathOf(meta);
        await File.WriteAllBytesAsync(path, bytes, ct);
        try
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            db.Attachments.Add(meta);
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            TryDelete(path);
            throw;
        }

        return meta;
    }

    /// <inheritdoc />
    public async Task<Attachment?> GetAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Attachments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    /// <inheritdoc />
    public async Task<Stream?> OpenReadAsync(Guid id, CancellationToken ct = default) =>
        await GetFilePathAsync(id, ct) is { } path ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true) : null;

    /// <inheritdoc />
    public async Task<string?> GetFilePathAsync(Guid id, CancellationToken ct = default)
    {
        var attachment = await GetAsync(id, ct);
        if (attachment is null)
        {
            return null;
        }

        var path = PathOf(attachment);
        return File.Exists(path) ? path : null;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Attachment>> ListByOwnerAsync(AttachmentOwnerKind kind, Guid ownerId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Attachments.AsNoTracking()
            .Where(a => a.OwnerKind == kind && a.OwnerId == ownerId)
            .OrderBy(a => a.CreatedAt)
            .ThenBy(a => a.Id)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var existing = await db.Attachments.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (existing is null)
        {
            return;
        }

        // The interceptor turns the delete into a soft delete; the file stays in the folder.
        db.Remove(existing);
        await db.SaveChangesAsync(ct);
    }

    private string PathOf(Attachment attachment) =>
        Path.Combine(folder, attachment.Id.ToString() + AttachmentContentTypes.ExtensionFor(attachment.ContentType));

    /// <summary>Reads the stream completely but stops one byte past the size limit, so a huge file is not loaded into memory.</summary>
    private static async Task<byte[]> ReadLimitedAsync(Stream content, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        while (buffer.Length <= AttachmentValidator.MaxSizeBytes)
        {
            var read = await content.ReadAsync(chunk, ct);
            if (read == 0)
            {
                break;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
