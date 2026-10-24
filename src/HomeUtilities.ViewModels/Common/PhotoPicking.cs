using HomeUtilities.Core.Attachments;
using HomeUtilities.Core.Common;

namespace HomeUtilities.ViewModels.Common;

/// <summary>Shared helpers of the screens that attach a photo to a record.</summary>
internal static class PhotoPicking
{
    /// <summary>
    /// Runs a media action. A declined permission is reported as the returned message (the file is then null) instead
    /// of an exception, so that the screen shows it and carries on.
    /// </summary>
    public static async Task<(PickedFile? File, string Error)> TryPickAsync(Func<Task<PickedFile?>> pick, ILocalizer localizer)
    {
        try
        {
            var file = await pick();
            if (file is null)
            {
                return (null, string.Empty);
            }

            var error = CheckType(file, localizer);
            if (error.Length == 0)
            {
                error = await CheckSizeAsync(file, localizer);
            }

            return error.Length == 0 ? (file, string.Empty) : (null, error);
        }
        catch (MediaPermissionDeniedException)
        {
            return (null, localizer["PhotoPermissionDenied"]);
        }
    }

    /// <summary>
    /// Asks where the photo comes from where a camera exists ("Take photo" / "Choose from gallery") and runs the choice;
    /// without a camera the image picker opens directly. A cancelled choice returns no file and no error.
    /// </summary>
    public static async Task<(PickedFile? File, string Error)> ChooseAndPickAsync(IMediaService media, IDialogService dialogs, ILocalizer localizer)
    {
        if (!media.IsCaptureSupported)
        {
            return await TryPickAsync(media.PickPhotoAsync, localizer);
        }

        var take = localizer["PhotoTake"];
        var gallery = localizer["PhotoGallery"];
        var choice = await dialogs.ChooseAsync(localizer["PhotoTitle"], [take, gallery]);
        if (choice is null)
        {
            return (null, string.Empty);
        }

        return await TryPickAsync(choice == take ? media.CapturePhotoAsync : media.PickPhotoAsync, localizer);
    }

    /// <summary>Builds the attachment row for a picked file; the store fills size and hash.</summary>
    public static Attachment ToAttachment(PickedFile file, AttachmentOwnerKind kind, Guid ownerId) => new()
    {
        FileName = ShortName(file.FileName),
        ContentType = NormalizedContentType(file),
        OwnerKind = kind,
        OwnerId = ownerId,
    };

    /// <summary>Rejects a file that can be seen to be wrong before anything is stored: a type that is not allowed or a file over the size limit.</summary>
    private static string CheckType(PickedFile file, ILocalizer localizer)
    {
        // Size and hash are only known to the store, so the probe gets valid placeholders.
        var probe = ToAttachment(file, AttachmentOwnerKind.Other, Guid.Empty);
        probe.SizeBytes = 1;
        probe.Sha256 = new string('0', 64);
        return Describe(AttachmentValidator.Validate(probe).Errors, localizer);
    }

    /// <summary>Checks the size of the opened stream when the stream can tell it; the store checks again when it reads the bytes.</summary>
    private static async Task<string> CheckSizeAsync(PickedFile file, ILocalizer localizer)
    {
        try
        {
            await using var stream = await file.OpenAsync();
            if (stream.CanSeek && stream.Length > AttachmentValidator.MaxSizeBytes)
            {
                return localizer.Get(
                    ValidationText.KeyFor(ErrorCodes.AttachmentTooLarge),
                    AttachmentValidator.MaxSizeBytes / (1024 * 1024));
            }
        }
        catch (NotSupportedException)
        {
        }

        return string.Empty;
    }

    public static string Describe(IEnumerable<ValidationError> errors, ILocalizer localizer) =>
        string.Join("\n", errors.Select(e => ValidationText.For(localizer, e)));

    private static string NormalizedContentType(PickedFile file)
    {
        var type = file.ContentType?.Trim() ?? string.Empty;
        if (string.Equals(type, "image/jpg", StringComparison.OrdinalIgnoreCase))
        {
            return AttachmentContentTypes.Jpeg;
        }

        return type.Length > 0 && !string.Equals(type, "application/octet-stream", StringComparison.OrdinalIgnoreCase)
            ? type
            : AttachmentContentTypes.FromFileName(file.FileName) ?? type;
    }

    private static string ShortName(string? fileName)
    {
        var name = string.IsNullOrWhiteSpace(fileName) ? "photo" : Path.GetFileName(fileName.Trim());
        if (name.Length <= AttachmentValidator.MaxFileNameLength)
        {
            return name;
        }

        var extension = Path.GetExtension(name);
        return name[..(AttachmentValidator.MaxFileNameLength - extension.Length)] + extension;
    }
}
